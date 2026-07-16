using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bindito.Core;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.Coordinates;
using Timberborn.LevelVisibilitySystem;
using Timberborn.Localization;
using Timberborn.Modding;
using Timberborn.NaturalResources;
using Timberborn.Persistence;
using Timberborn.PlatformUtilities;
using Timberborn.QuickNotificationSystem;
using Timberborn.Ruins;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace Calloatti.Grid
{
  public class MarkerService : ILoadableSingleton, IPostLoadableSingleton, ISaveableSingleton, IDisposable
  {
    public static MarkerService Instance { get; private set; }

    private const string ModId = "Calloatti.Grid";

    // --- DEPENDENCIES ---
    private readonly ITerrainService _terrainService;
    private readonly IBlockService _blockService;
    private readonly ILevelVisibilityService _levelVisibilityService;
    private readonly EventBus _eventBus;
    private readonly ISingletonLoader _singletonLoader;
    private readonly ModRepository _modRepository;
    private readonly QuickNotificationService _notificationService;
    private readonly ILoc _loc;

    // --- STATE ---
    public MarkerSettings Settings { get; private set; } = new MarkerSettings();
    private readonly Dictionary<Vector2Int, MarkerData> _activeMarkers = new Dictionary<Vector2Int, MarkerData>();

    // --- OPTIMIZATION BUFFERS ---
    private readonly HashSet<Vector2Int> _tempColumns = new HashSet<Vector2Int>();
    private readonly List<float> _targetHeightsBuffer = new List<float>();

    // --- VISUAL CONSTANTS & STATE ---
    private Material[] _paletteMaterials;
    private Mesh _sharedCrossMesh;
    private bool _markersVisible = true;

    private const float Thickness = 0.12f;
    private const float DiagonalLength = 0.65f;
    private const float HeightOffset = 0.05f;
    private const float SliceBaseHeight = 0.85f;
    private const float SurfaceBaseHeight = 1.00f;

    // --- PERSISTENCE KEYS ---
    private static readonly SingletonKey MarkersKey = new SingletonKey("Calloatti.Grid.Markers");
    private static readonly ListKey<int> XsKey = new ListKey<int>("Xs");
    private static readonly ListKey<int> YsKey = new ListKey<int>("Ys");
    private static readonly ListKey<int> ColorsKey = new ListKey<int>("Colors");

    private List<int> _loadedXs;
    private List<int> _loadedYs;
    private List<int> _loadedColors;

    [Inject]
    public MarkerService(
        ITerrainService terrainService,
        IBlockService blockService,
        ILevelVisibilityService levelVisibilityService,
        EventBus eventBus,
        ISingletonLoader singletonLoader,
        ModRepository modRepository,
        QuickNotificationService notificationService,
        ILoc loc)
    {
      _terrainService = terrainService;
      _blockService = blockService;
      _levelVisibilityService = levelVisibilityService;
      _eventBus = eventBus;
      _singletonLoader = singletonLoader;
      _modRepository = modRepository;
      _notificationService = notificationService;
      _loc = loc;
      Instance = this;
    }

    // ====================================================================
    // LIFECYCLE & PERSISTENCE
    // ====================================================================

    public void Load()
    {
      EnsureSettingsLoaded();
      LoadState();
    }

    public void PostLoad()
    {
      _eventBus.Register(this);
      _terrainService.TerrainHeightChanged += OnTerrainHeightChanged;

      if (_loadedXs != null)
      {
        for (int i = 0; i < _loadedXs.Count; i++)
        {
          AddMarker(new Vector2Int(_loadedXs[i], _loadedYs[i]), _loadedColors[i]);
        }

        _loadedXs = null;
        _loadedYs = null;
        _loadedColors = null;
      }
    }

    private void LoadState()
    {
      if (_singletonLoader.TryGetSingleton(MarkersKey, out IObjectLoader objectLoader) && objectLoader.Has(XsKey))
      {
        _loadedXs = objectLoader.Get(XsKey);
        _loadedYs = objectLoader.Get(YsKey);
        _loadedColors = objectLoader.Get(ColorsKey);
      }
    }

    public void Save(ISingletonSaver saver)
    {
      IObjectSaver objectSaver = saver.GetSingleton(MarkersKey);

      List<int> xs = new List<int>(_activeMarkers.Count);
      List<int> ys = new List<int>(_activeMarkers.Count);
      List<int> colors = new List<int>(_activeMarkers.Count);

      foreach (var kvp in _activeMarkers)
      {
        xs.Add(kvp.Key.x);
        ys.Add(kvp.Key.y);
        colors.Add(kvp.Value.ColorIndex);
      }

      objectSaver.Set(XsKey, xs);
      objectSaver.Set(YsKey, ys);
      objectSaver.Set(ColorsKey, colors);
    }

    public void Dispose()
    {
      _terrainService.TerrainHeightChanged -= OnTerrainHeightChanged;
      RemoveAllMarkers();

      if (_paletteMaterials != null)
      {
        foreach (var mat in _paletteMaterials) if (mat != null) UnityEngine.Object.Destroy(mat);
      }
      if (_sharedCrossMesh != null) UnityEngine.Object.Destroy(_sharedCrossMesh);
    }

    // ====================================================================
    // CONFIGURATION
    // ====================================================================

    private string GetConfigFilePath()
    {
      string localModsFolder = Path.Combine(UserDataFolder.Folder, "Mods");
      string actualModPath = _modRepository.Mods.FirstOrDefault(m => m.Manifest.Id == ModId)?.ModDirectory.Path;

      if (string.IsNullOrEmpty(actualModPath))
      {
        string fallback = Path.Combine(localModsFolder, "Grid");
        if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback);
        return Path.Combine(fallback, "markers.json");
      }

      string normalizedLocalMods = Path.GetFullPath(localModsFolder).Replace('\\', '/').TrimEnd('/');
      string normalizedModPath = Path.GetFullPath(actualModPath).Replace('\\', '/').TrimEnd('/');

      if (normalizedModPath.StartsWith(normalizedLocalMods, StringComparison.InvariantCultureIgnoreCase))
      {
        return Path.Combine(actualModPath, "markers.json");
      }
      else
      {
        string workshopConfigFolder = Path.Combine(localModsFolder, "Grid");
        if (!Directory.Exists(workshopConfigFolder)) Directory.CreateDirectory(workshopConfigFolder);
        return Path.Combine(workshopConfigFolder, "markers.json");
      }
    }

    private void EnsureSettingsLoaded()
    {
      try
      {
        string filePath = GetConfigFilePath();
        if (File.Exists(filePath))
        {
          string json = File.ReadAllText(filePath);
          JsonUtility.FromJsonOverwrite(json, Settings);
        }
        else
        {
          string json = JsonUtility.ToJson(Settings, true);
          File.WriteAllText(filePath, json);
          Debug.Log($"[Grid] Created user-friendly config: {filePath}");
        }

        Settings.InitializeColors();
      }
      catch (Exception e)
      {
        Debug.LogError($"[Grid] Failed to handle markers.json: {e.Message}");
      }
    }

    public void ReloadSettings()
    {
      EnsureSettingsLoaded();
    }

    // ====================================================================
    // INTERACTION & EVENTS
    // ====================================================================

    public void Interact(Vector3Int columnCoords, int colorIndex)
    {
      Vector2Int col = new Vector2Int(columnCoords.x, columnCoords.y);
      if (_activeMarkers.ContainsKey(col)) ChangeColor(col);
      else AddMarker(col, colorIndex);
    }

    public void DeleteMarker(Vector3Int columnCoords)
    {
      Vector2Int col = new Vector2Int(columnCoords.x, columnCoords.y);
      if (_activeMarkers.ContainsKey(col)) RemoveColumn(col);
    }

    [OnEvent]
    public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent maxVisibleLevelChangedEvent)
    {
      foreach (var col in _activeMarkers.Keys) UpdateMarkerVisuals(col);
    }

    [OnEvent]
    public void OnBlockObjectSet(BlockObjectSetEvent e) { ProcessBlockObjectChange(e.BlockObject); }

    [OnEvent]
    public void OnBlockObjectUnset(BlockObjectUnsetEvent e) { ProcessBlockObjectChange(e.BlockObject); }

    private void ProcessBlockObjectChange(BlockObject bo)
    {
      // OPTIMIZATION: Fast reject and zero-allocation deduplication
      if (_activeMarkers.Count == 0) return;

      _tempColumns.Clear();
      foreach (var coords in bo.PositionedBlocks.GetAllCoordinates())
      {
        Vector2Int col = new Vector2Int(coords.x, coords.y);
        // Only trigger the check if the column has a marker AND we haven't processed it this loop
        if (_activeMarkers.ContainsKey(col) && _tempColumns.Add(col))
        {
          CheckBlockChange(col);
        }
      }
    }

    private void CheckBlockChange(Vector2Int col)
    {
      if (_activeMarkers.TryGetValue(col, out MarkerData data))
      {
        int color = data.ColorIndex;
        RemoveColumn(col);
        AddMarker(col, color);
      }
    }

    private void OnTerrainHeightChanged(object sender, TerrainHeightChangeEventArgs e)
    {
      if (_activeMarkers.Count == 0) return;
      CheckBlockChange(new Vector2Int(e.Change.Coordinates.x, e.Change.Coordinates.y));
    }

    // ====================================================================
    // VISUALS & SCANNING
    // ====================================================================

    public void ToggleMarkers()
    {
      _markersVisible = !_markersVisible;

      foreach (var data in _activeMarkers.Values)
      {
        if (data.Container != null) data.Container.SetActive(_markersVisible);
      }

      string locKey = _markersVisible ? "Calloatti.Grid.Markers.NotificationOn" : "Calloatti.Grid.Markers.NotificationOff";
      _notificationService.SendNotification(_loc.T(locKey));
    }

    private void AddMarker(Vector2Int col, int colorIndex)
    {
      InitializeResources();
      MarkerData data = new MarkerData
      {
        Container = new GameObject($"Marker_{col.x}_{col.y}"),
        ColorIndex = colorIndex,
        VisualPairs = new List<GameObject>(),
        Surfaces = new List<SurfaceData>()
      };

      data.Container.SetActive(_markersVisible);
      _activeMarkers[col] = data;
      RecalculateColumnCache(col);
      UpdateMarkerVisuals(col);
    }

    public void UpdateMarkerVisuals(Vector2Int col)
    {
      if (!_activeMarkers.TryGetValue(col, out MarkerData data)) return;
      if (!_markersVisible) return;

      int maxV = _levelVisibilityService.MaxVisibleLevel;

      // OPTIMIZATION: Re-use class-level buffer instead of allocating new List per marker
      _targetHeightsBuffer.Clear();

      foreach (var surf in data.Surfaces)
      {
        if (maxV >= surf.RequiredMaxV) _targetHeightsBuffer.Add(surf.RoofZ + SurfaceBaseHeight + HeightOffset);
      }

      if (maxV >= 0 && maxV < _terrainService.Size.z && _terrainService.Underground(new Vector3Int(col.x, col.y, maxV)))
      {
        float sliceHeight = maxV + SliceBaseHeight + HeightOffset;
        if (!_targetHeightsBuffer.Contains(sliceHeight)) _targetHeightsBuffer.Add(sliceHeight);
      }

      Material sharedMat = _paletteMaterials[data.ColorIndex];

      while (data.VisualPairs.Count < _targetHeightsBuffer.Count)
        data.VisualPairs.Add(CreateMarkerVisualObject(data.Container.transform, sharedMat));

      Vector3 worldPos = CoordinateSystem.GridToWorld(new Vector3(col.x + 0.5f, col.y + 0.5f, 0));

      for (int i = 0; i < data.VisualPairs.Count; i++)
      {
        if (i < _targetHeightsBuffer.Count)
        {
          data.VisualPairs[i].SetActive(true);
          data.VisualPairs[i].transform.position = new Vector3(worldPos.x, _targetHeightsBuffer[i], worldPos.z);
        }
        else
        {
          data.VisualPairs[i].SetActive(false);
        }
      }
    }

    private void ChangeColor(Vector2Int col)
    {
      if (_activeMarkers.TryGetValue(col, out MarkerData data))
      {
        data.ColorIndex = (data.ColorIndex + 1) % Settings.MarkerPalette.Count;
        Material newSharedMat = _paletteMaterials[data.ColorIndex];

        foreach (var obj in data.VisualPairs)
        {
          MeshRenderer mr = obj.GetComponent<MeshRenderer>();
          if (mr != null) mr.sharedMaterial = newSharedMat;
        }
      }
    }

    public void RemoveColumn(Vector2Int col)
    {
      if (_activeMarkers.TryGetValue(col, out MarkerData data))
      {
        UnityEngine.Object.Destroy(data.Container);
        _activeMarkers.Remove(col);
      }
    }

    public void RemoveAllMarkers()
    {
      foreach (var data in _activeMarkers.Values)
        if (data.Container != null) UnityEngine.Object.Destroy(data.Container);
      _activeMarkers.Clear();
    }

    private void RecalculateColumnCache(Vector2Int col)
    {
      if (!_activeMarkers.TryGetValue(col, out MarkerData data)) return;
      data.Surfaces.Clear();

      int mapZ = _terrainService.Size.z;

      bool hasBedrock = !_blockService.AnyObjectAt(new Vector3Int(col.x, col.y, 0)) && !_terrainService.Underground(new Vector3Int(col.x, col.y, 0));
      if (hasBedrock)
      {
        data.Surfaces.Add(new SurfaceData { RoofZ = -1, RequiredMaxV = 0 });
      }

      BlockObject activeStructure = null;

      for (int z = 0; z < mapZ; z++)
      {
        Vector3Int pos = new Vector3Int(col.x, col.y, z);

        bool isTerrain = _terrainService.Underground(pos);
        BlockObject topObj = null;

        foreach (BlockObject obj in _blockService.GetObjectsAt(pos))
        {
          if (obj.GetComponent<NaturalResource>() != null || obj.GetComponent<Ruin>() != null) continue;

          if (obj.Solid || obj.GetComponent<Building>() != null)
          {
            topObj = obj;
            activeStructure = obj;
            break;
          }
        }

        if (topObj == null && activeStructure != null && activeStructure.PositionedBlocks.HasBlockAt(pos))
        {
          topObj = activeStructure;
        }
        else if (topObj == null)
        {
          activeStructure = null;
        }

        if (isTerrain || topObj != null)
        {
          Vector3Int posAbove = new Vector3Int(col.x, col.y, z + 1);
          bool terrainAbove = z + 1 < mapZ && _terrainService.Underground(posAbove);
          bool bottomOccupiedAbove = false;

          if (z + 1 < mapZ)
          {
            BlockObject objAbove = _blockService.GetBottomObjectAt(posAbove);

            if (objAbove != null && objAbove.Solid && objAbove != topObj)
            {
              bottomOccupiedAbove = true;
            }

            if (!isTerrain && topObj != null && topObj.PositionedBlocks.HasBlockAt(posAbove))
            {
              bottomOccupiedAbove = true;
            }
          }

          if (!terrainAbove && !bottomOccupiedAbove)
          {
            int reqMaxV = z + 1;

            if (!isTerrain && topObj != null)
            {
              if (_blockService.GetBottomObjectAt(topObj.Coordinates) == topObj)
              {
                reqMaxV = topObj.Coordinates.z;
              }
            }

            data.Surfaces.Add(new SurfaceData { RoofZ = z, RequiredMaxV = reqMaxV });
          }
        }
      }
    }

    private GameObject CreateMarkerVisualObject(Transform parent, Material sharedMat)
    {
      GameObject obj = new GameObject("MarkerVisual");
      obj.transform.SetParent(parent, false);
      MeshFilter mf = obj.AddComponent<MeshFilter>();
      MeshRenderer mr = obj.AddComponent<MeshRenderer>();
      mf.sharedMesh = _sharedCrossMesh;
      mr.sharedMaterial = sharedMat;
      return obj;
    }

    private void InitializeResources()
    {
      if (_sharedCrossMesh == null) _sharedCrossMesh = CreateCrossMesh();
      if (_paletteMaterials == null)
      {
        _paletteMaterials = new Material[Settings.MarkerPalette.Count];
        Shader spriteShader = Shader.Find("Sprites/Default");
        for (int i = 0; i < Settings.MarkerPalette.Count; i++)
        {
          _paletteMaterials[i] = new Material(spriteShader) { color = Settings.MarkerPalette[i] };
        }
      }
    }

    private Mesh CreateCrossMesh()
    {
      Mesh mesh = new Mesh();
      float halfW = Thickness / 2f;
      float halfH = DiagonalLength / 2f;
      Vector3[] vertices = new Vector3[8];

      Quaternion q1 = Quaternion.Euler(90, 45, 0);
      vertices[0] = q1 * new Vector3(-halfW, -halfH, 0);
      vertices[1] = q1 * new Vector3(halfW, -halfH, 0);
      vertices[2] = q1 * new Vector3(-halfW, halfH, 0);
      vertices[3] = q1 * new Vector3(halfW, halfH, 0);

      Quaternion q2 = Quaternion.Euler(90, -45, 0);
      vertices[4] = q2 * new Vector3(-halfW, -halfH, 0);
      vertices[5] = q2 * new Vector3(halfW, -halfH, 0);
      vertices[6] = q2 * new Vector3(-halfW, halfH, 0);
      vertices[7] = q2 * new Vector3(halfW, halfH, 0);

      mesh.vertices = vertices;
      mesh.triangles = new int[12] { 0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7 };
      mesh.RecalculateNormals();
      return mesh;
    }

    // ====================================================================
    // INTERNAL CLASSES
    // ====================================================================

    private struct SurfaceData
    {
      public int RoofZ;
      public int RequiredMaxV;
    }

    private class MarkerData
    {
      public GameObject Container;
      public int ColorIndex;
      public List<GameObject> VisualPairs;
      public List<SurfaceData> Surfaces;
    }
  }
}