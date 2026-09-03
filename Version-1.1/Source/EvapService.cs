using System;
using System.Collections.Generic;
using Bindito.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Timberborn.AssetSystem;
using Timberborn.CameraSystem;
using Timberborn.Coordinates;
using Timberborn.LevelVisibilitySystem;
using Timberborn.Localization;
using Timberborn.MapIndexSystem;
using Timberborn.QuickNotificationSystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.TickSystem;
using Timberborn.WaterSystem;

namespace Calloatti.Grid
{
  public class EvapService : ILoadableSingleton, IPostLoadableSingleton, ITickableSingleton, ILateUpdatableSingleton, IDisposable
  {
    #region Constants & Settings
    private const int ChunkSize = 32;
    private const float RotationDelay = 0.25f;
    #endregion

    #region Injected Dependencies
    private readonly ITerrainService _terrainService;
    private readonly ILevelVisibilityService _levelVisibilityService;
    private readonly MapIndexService _mapIndexService;
    private readonly IAssetLoader _assetLoader;
    private readonly EventBus _eventBus;
    private readonly EvapInputService _evapInputService;
    private readonly QuickNotificationService _notificationService;
    private readonly CameraService _cameraService;
    private readonly ILoc _loc;
    private readonly IThreadSafeWaterMap _waterMap;
    private readonly IThreadSafeWaterEvaporationMap _evaporationMap;
    #endregion

    #region State
    private bool _isActive;
    private Material _evapMaterial;
    private Quaternion _lastRotation = Quaternion.identity;
    private Quaternion _targetRotation = Quaternion.identity;
    private float _rotationCooldown = 0f;
    private GameObject _masterContainer;

    private readonly Dictionary<Vector2Int, EvapChunk> _chunks = new Dictionary<Vector2Int, EvapChunk>();
    private readonly List<Vector2Int> _chunkOrder = new List<Vector2Int>();
    private readonly Dictionary<Vector2Int, (int StartX, int StartY, int EndX, int EndY)> _chunkBounds = new Dictionary<Vector2Int, (int, int, int, int)>();
    private int _chunkCounter;
    #endregion

    [Inject]
    public EvapService(
        ITerrainService terrainService,
        ILevelVisibilityService levelVisibilityService,
        MapIndexService mapIndexService,
        IAssetLoader assetLoader,
        EventBus eventBus,
        EvapInputService evapInputService,
        QuickNotificationService notificationService,
        CameraService cameraService,
        ILoc loc,
        IThreadSafeWaterMap waterMap,
        IThreadSafeWaterEvaporationMap evaporationMap)
    {
      _terrainService = terrainService;
      _levelVisibilityService = levelVisibilityService;
      _mapIndexService = mapIndexService;
      _assetLoader = assetLoader;
      _eventBus = eventBus;
      _evapInputService = evapInputService;
      _notificationService = notificationService;
      _cameraService = cameraService;
      _loc = loc;
      _waterMap = waterMap;
      _evaporationMap = evaporationMap;
    }

    public void Load()
    {
      _evapMaterial = _assetLoader.Load<Material>("Materials/EvapAtlasMaterial");
      if (_evapMaterial == null)
      {
        Debug.LogError("[GRID.EVAP] Failed to load EvapAtlasMaterial!");
        return;
      }

      Texture2D tex = _assetLoader.Load<Texture2D>("Sprites/grid-atlas");
      _evapMaterial.mainTexture = tex;

      InitializeVisuals();
    }

    public void PostLoad()
    {
      _eventBus.Register(this);
      _evapInputService.OnToggleEvapData += ToggleEvapData;
    }

    public void LateUpdateSingleton()
    {
      if (!_isActive) return;

      Quaternion currentSnappedRot = CalculateCameraRotation();

      if (currentSnappedRot != _lastRotation)
      {
        if (currentSnappedRot != _targetRotation)
        {
          _targetRotation = currentSnappedRot;
          _rotationCooldown = RotationDelay;
        }
        else
        {
          _rotationCooldown -= Time.unscaledDeltaTime;
          if (_rotationCooldown <= 0f)
          {
            // Rotation finalized - tell all chunks to rotate
            foreach (var chunk in _chunks.Values)
            {
              chunk.Rotate(_targetRotation);
            }
            _lastRotation = _targetRotation;
          }
        }
      }
    }

    public void Tick()
    {
      if (!_isActive) return;
      if (_chunkOrder.Count == 0) return;

      EvapChunk chunk = _chunks[_chunkOrder[_chunkCounter % _chunkOrder.Count]];
      _chunkCounter++;

      var bounds = _chunkBounds[chunk.ChunkCoords];
      chunk.Refresh(bounds.StartX, bounds.StartY, bounds.EndX, bounds.EndY, _terrainService.Size.z, _waterMap, _evaporationMap, _mapIndexService, _lastRotation);
      chunk.SetVisibility(true, _levelVisibilityService.MaxVisibleLevel);
    }

    public void ToggleEvapData()
    {
      _isActive = !_isActive;

      if (_isActive)
      {
        _lastRotation = CalculateCameraRotation();
        _targetRotation = _lastRotation;

        foreach (var chunk in _chunks.Values)
        {
          var bounds = _chunkBounds[chunk.ChunkCoords];
          chunk.Refresh(bounds.StartX, bounds.StartY, bounds.EndX, bounds.EndY, _terrainService.Size.z, _waterMap, _evaporationMap, _mapIndexService, _lastRotation);
        }
        UpdateVisibility();
        _notificationService.SendNotification(_loc.T("Calloatti.Grid.EvapData.NotificationOn"));
      }
      else
      {
        HideAll();
        _notificationService.SendNotification(_loc.T("Calloatti.Grid.EvapData.NotificationOff"));
      }
    }

    [OnEvent]
    public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent e)
    {
      if (_isActive) UpdateVisibility();
    }

    private void InitializeVisuals()
    {
      _masterContainer = new GameObject("EvapData_MasterContainer");
      int maxZ = _terrainService.Size.z;

      int chunksX = Mathf.CeilToInt(_terrainService.Size.x / (float)ChunkSize);
      int chunksY = Mathf.CeilToInt(_terrainService.Size.y / (float)ChunkSize);

      for (int y = 0; y < chunksY; y++)
      {
        for (int x = 0; x < chunksX; x++)
        {
          Vector2Int coord = new Vector2Int(x, y);
          _chunks[coord] = new EvapChunk(coord, maxZ, _masterContainer.transform, _evapMaterial);
          _chunkOrder.Add(coord);

          int startX = x * ChunkSize;
          int startY = y * ChunkSize;
          int endX = Mathf.Min(startX + ChunkSize, _terrainService.Size.x);
          int endY = Mathf.Min(startY + ChunkSize, _terrainService.Size.y);
          _chunkBounds[coord] = (startX, startY, endX, endY);
        }
      }
    }

    private Quaternion CalculateCameraRotation()
    {
      float angle = Mathf.Repeat(_cameraService.HorizontalAngle, 360f);
      float snapped = Mathf.Floor((angle + 22.5f) / 90f) * 90f;
      return Quaternion.Euler(90, snapped, 0);
    }

    private void UpdateVisibility()
    {
      int maxVisibleLevel = _levelVisibilityService.MaxVisibleLevel;
      foreach (var chunk in _chunks.Values)
      {
        chunk.SetVisibility(_isActive, maxVisibleLevel);
      }
    }

    private void HideAll()
    {
      foreach (var chunk in _chunks.Values)
      {
        chunk.SetVisibility(false, 0);
      }
    }

    public void Dispose()
    {
      _evapInputService.OnToggleEvapData -= ToggleEvapData;

      foreach (var chunk in _chunks.Values) chunk.Destroy();
      _chunks.Clear();
      _chunkOrder.Clear();
      _chunkBounds.Clear();

      if (_masterContainer != null) UnityEngine.Object.Destroy(_masterContainer);
      if (_evapMaterial != null) UnityEngine.Object.Destroy(_evapMaterial);
    }
  }

  // =========================================================================
  // EVAP CHUNK
  // =========================================================================
  public class EvapChunk
  {
    private const int GRID_COLUMNS = 256;
    private const int GRID_ROWS = 6;
    private const int EvapDataRow = 3;
    private const float HeightOffset = 0.01f;
    private const float EvapSpeed = 0.0001f;
    private const float DayFactor = 460.8f;

    private readonly Vector2Int _chunkCoords;
    private readonly int _maxZ;
    private readonly GameObject _chunkRoot;

    private readonly MeshFilter[] _filters;
    private readonly MeshRenderer[] _renderers;

    private readonly Material _material;

    // Per-chunk per-z buffers
    private List<Vector3>[] _vData;
    private List<int>[] _tData;
    private List<Vector2>[] _uData;

    // Dirty tracking - chunk-level hash
    private uint _lastChunkHash = 0;
    private bool _isFirstRefresh = true;
    private Quaternion _currentRotation = Quaternion.identity;

    public Vector2Int ChunkCoords => _chunkCoords;

    public EvapChunk(Vector2Int chunkCoords, int maxZ, Transform parent, Material material)
    {
      _chunkCoords = chunkCoords;
      _maxZ = maxZ;
      _material = material;

      _chunkRoot = new GameObject($"EvapChunk_{chunkCoords.x}_{chunkCoords.y}");
      _chunkRoot.transform.SetParent(parent);

      _filters = new MeshFilter[maxZ];
      _renderers = new MeshRenderer[maxZ];

      _vData = new List<Vector3>[maxZ];
      _tData = new List<int>[maxZ];
      _uData = new List<Vector2>[maxZ];

      for (int z = 0; z < maxZ; z++)
      {
        _vData[z] = new List<Vector3>();
        _tData[z] = new List<int>();
        _uData[z] = new List<Vector2>();

        GameObject layerObj = new GameObject($"L_{z}");
        layerObj.transform.SetParent(_chunkRoot.transform);
        _filters[z] = layerObj.AddComponent<MeshFilter>();
        _renderers[z] = layerObj.AddComponent<MeshRenderer>();
        _filters[z].mesh = new Mesh();
        if (_material != null) _renderers[z].sharedMaterial = _material;
        layerObj.SetActive(false);
      }
    }

    // Service calls this when camera rotation finalizes
    public void Rotate(Quaternion newRotation)
    {
      if (_currentRotation == newRotation) return;

      Quaternion deltaRot = newRotation * Quaternion.Inverse(_currentRotation);
      _currentRotation = newRotation;

      for (int z = 0; z < _maxZ; z++)
      {
        RotateMesh(_filters[z].mesh, deltaRot);
      }
    }

    // Service calls this for periodic data refresh
    public bool Refresh(int startX, int startY, int endX, int endY, int maxZ, IThreadSafeWaterMap waterMap, IThreadSafeWaterEvaporationMap evaporationMap, MapIndexService mapIndexService, Quaternion rot)
    {
      _currentRotation = rot;
      Vector3 localP0 = rot * new Vector3(-0.5f, -0.5f, 0);
      Vector3 localP1 = rot * new Vector3(0.5f, -0.5f, 0);
      Vector3 localP2 = rot * new Vector3(-0.5f, 0.5f, 0);
      Vector3 localP3 = rot * new Vector3(0.5f, 0.5f, 0);

      uint chunkHash = 0;
      bool dirty = false;

      for (int z = 0; z < maxZ; z++)
      {
        _vData[z].Clear(); _tData[z].Clear(); _uData[z].Clear();
      }

      for (int y = startY; y < endY; y++)
      {
        for (int x = startX; x < endX; x++)
        {
          int index2D = mapIndexService.CoordinatesToIndex3D(new Vector3Int(x, y, 0));
          int columnCount = waterMap.ColumnCount(index2D);

          for (int j = 0; j < columnCount; j++)
          {
            int index3D = index2D + j * mapIndexService.VerticalStride;
            float waterDepth = waterMap.WaterDepth(index3D);

            if (waterDepth <= 0f) continue;

            float modifier = evaporationMap.EvaporationModifiers[index3D];
            float evap = EvapSpeed * modifier * DayFactor;
            int spriteIndex = Mathf.Clamp(Mathf.RoundToInt(evap * 100f), 0, 255);

            // Accumulate hash of this column's state
            uint colHash = (uint)(waterDepth * 1000f) ^ (uint)(modifier * 1000f) ^ (uint)j;
            chunkHash = chunkHash * 31 + colHash;

            int floor = waterMap.ColumnFloor(index3D);
            float waterTop = floor + waterDepth;
            int topZ = Mathf.CeilToInt(waterTop);
            int layerIndex = Mathf.Clamp(topZ, 0, maxZ - 1);
            AddQuadToArrays(layerIndex, x, y, topZ, spriteIndex, localP0, localP1, localP2, localP3);
          }
        }
      }

      if (_isFirstRefresh)
      {
        dirty = true;
        _isFirstRefresh = false;
      }
      else if (chunkHash != _lastChunkHash)
      {
        dirty = true;
      }

      if (!dirty)
      {
        return false;
      }

      _lastChunkHash = chunkHash;

      for (int z = 0; z < maxZ; z++)
      {
        Mesh mesh = _filters[z].mesh;
        if (_vData[z].Count == 0)
        {
          if (mesh.vertexCount > 0)
          {
            mesh.Clear();
          }
          continue;
        }

        if (mesh.vertexCount == _vData[z].Count)
        {
          mesh.SetVertices(_vData[z]);
          mesh.SetTriangles(_tData[z], 0);
          mesh.SetUVs(0, _uData[z]);
        }
        else
        {
          mesh.Clear();
          mesh.indexFormat = IndexFormat.UInt32;
          mesh.SetVertices(_vData[z]);
          mesh.SetTriangles(_tData[z], 0);
          mesh.SetUVs(0, _uData[z]);
          mesh.RecalculateBounds();
        }
      }

      return true;
    }

    private void AddQuadToArrays(int layerIndex, int x, int y, int ceiling, int spriteIndex, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
      int vIndex = _vData[layerIndex].Count;
      float unityHeight = ceiling + HeightOffset;
      Vector3 worldPos = CoordinateSystem.GridToWorld(new Vector3(x + 0.5f, y + 0.5f, 0));
      worldPos.y = unityHeight;

      _vData[layerIndex].Add(worldPos + p0);
      _vData[layerIndex].Add(worldPos + p1);
      _vData[layerIndex].Add(worldPos + p2);
      _vData[layerIndex].Add(worldPos + p3);

      float uMin = (float)spriteIndex / GRID_COLUMNS;
      float uMax = (float)(spriteIndex + 1) / GRID_COLUMNS;
      float vMax = 1.0f - ((float)EvapDataRow / GRID_ROWS);
      float vMin = 1.0f - ((float)(EvapDataRow + 1) / GRID_ROWS);
      _uData[layerIndex].Add(new Vector2(uMin, vMin));
      _uData[layerIndex].Add(new Vector2(uMax, vMin));
      _uData[layerIndex].Add(new Vector2(uMin, vMax));
      _uData[layerIndex].Add(new Vector2(uMax, vMax));

      _tData[layerIndex].Add(vIndex); _tData[layerIndex].Add(vIndex + 2); _tData[layerIndex].Add(vIndex + 1);
      _tData[layerIndex].Add(vIndex + 1); _tData[layerIndex].Add(vIndex + 2); _tData[layerIndex].Add(vIndex + 3);
    }

    // Chunk handles its own mesh rotation
    private void RotateMesh(Mesh targetMesh, Quaternion deltaRot)
    {
      if (targetMesh != null && targetMesh.vertexCount > 0)
      {
        var buffer = new List<Vector3>();
        targetMesh.GetVertices(buffer);

        for (int i = 0; i < buffer.Count; i += 4)
        {
          Vector3 center = (buffer[i] + buffer[i + 3]) / 2f;
          buffer[i] = center + deltaRot * (buffer[i] - center);
          buffer[i + 1] = center + deltaRot * (buffer[i + 1] - center);
          buffer[i + 2] = center + deltaRot * (buffer[i + 2] - center);
          buffer[i + 3] = center + deltaRot * (buffer[i + 3] - center);
        }

        targetMesh.SetVertices(buffer);
        targetMesh.RecalculateBounds();
        targetMesh.RecalculateNormals();
      }
    }

    public void SetVisibility(bool isActive, int maxVisibleLevel)
    {
      for (int z = 0; z < _maxZ; z++)
      {
        bool show = isActive && (z <= maxVisibleLevel);
        if (_filters[z].gameObject.activeSelf != show)
        {
          _filters[z].gameObject.SetActive(show);
        }
      }
    }

    public void Destroy()
    {
      for (int i = 0; i < _maxZ; i++)
      {
        if (_filters[i] != null && _filters[i].sharedMesh != null)
          UnityEngine.Object.Destroy(_filters[i].sharedMesh);
      }
      
      if (_vData != null)
      {
        for (int z = 0; z < _vData.Length; z++)
        {
          _vData[z]?.Clear();
          _tData[z]?.Clear();
          _uData[z]?.Clear();
        }
        _vData = null;
        _tData = null;
        _uData = null;
      }
      
      _lastChunkHash = 0;

      if (_chunkRoot != null) UnityEngine.Object.Destroy(_chunkRoot);
    }
  }
}