using System;
using System.Collections.Generic;
using Bindito.Core;
using UnityEngine;
using Timberborn.AssetSystem;
using Timberborn.CameraSystem;
using Timberborn.Coordinates;
using Timberborn.LevelVisibilitySystem;
using Timberborn.Localization;
using Timberborn.MapIndexSystem;
using Timberborn.QuickNotificationSystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;

namespace Calloatti.Grid
{
  public class TopoService : ILoadableSingleton, IPostLoadableSingleton, ILateUpdatableSingleton, IDisposable
  {
    #region Constants & Settings
    private const int GridColumns = 256;
    private const int GridRows = 5;
    private const int TopoDataRow = 2;
    private const float HeightOffset = 0.05f;
    private const int ChunkSize = 16;
    private const float RotationDelay = 0.25f;
    #endregion

    #region Injected Dependencies
    private readonly ITerrainService _terrainService;
    private readonly ILevelVisibilityService _levelVisibilityService;
    private readonly MapIndexService _mapIndexService;
    private readonly IAssetLoader _assetLoader;
    private readonly EventBus _eventBus;
    private readonly TopoInputService _topoInputService;
    private readonly QuickNotificationService _notificationService;
    private readonly CameraService _cameraService;
    private readonly ILoc _loc;
    #endregion

    #region State & Cache
    private bool _isActive;
    private bool _isDirty = true;
    private Material _topoMaterial;
    private Quaternion _lastRotation = Quaternion.identity;
    private GameObject _masterContainer;

    private Quaternion _targetRotation = Quaternion.identity;
    private float _rotationCooldown = 0f;

    private readonly Dictionary<Vector2Int, TopoChunk> _chunks = new Dictionary<Vector2Int, TopoChunk>();
    private readonly HashSet<TopoChunk> _dirtyChunks = new HashSet<TopoChunk>();

    private List<Vector3>[] _vWalkable;
    private List<int>[] _tWalkable;
    private List<Vector2>[] _uWalkable;

    private List<Vector3>[] _vBuried;
    private List<int>[] _tBuried;
    private List<Vector2>[] _uBuried;

    private readonly List<Vector3> _rotationBuffer = new List<Vector3>();
    #endregion

    [Inject]
    public TopoService(
        ITerrainService terrainService,
        ILevelVisibilityService levelVisibilityService,
        MapIndexService mapIndexService,
        IAssetLoader assetLoader,
        EventBus eventBus,
        TopoInputService topoInputService,
        QuickNotificationService notificationService,
        CameraService cameraService,
        ILoc loc)
    {
      _terrainService = terrainService;
      _levelVisibilityService = levelVisibilityService;
      _mapIndexService = mapIndexService;
      _assetLoader = assetLoader;
      _eventBus = eventBus;
      _topoInputService = topoInputService;
      _notificationService = notificationService;
      _cameraService = cameraService;
      _loc = loc;
    }

    public void Load()
    {
      Debug.Log("[GRID.TOPO] TopoService loaded.");
      Texture2D tex = _assetLoader.Load<Texture2D>("Sprites/grid-atlas");
      _topoMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
      InitializeVisuals();
    }

    public void PostLoad()
    {
      _eventBus.Register(this);
      _terrainService.TerrainHeightChanged += OnTerrainHeightChanged;
      _topoInputService.OnToggleTopoData += ToggleTopoData;
    }

    public void LateUpdateSingleton()
    {
      if (!_isActive) return;

      if (_dirtyChunks.Count > 0)
      {
        foreach (var chunk in _dirtyChunks)
        {
          GenerateChunkSnapshot(chunk);
          chunk.UpdateVisibility(true, _levelVisibilityService.MaxVisibleLevel);
          chunk.IsDirty = false;
        }
        _dirtyChunks.Clear();
      }

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
            RotateExistingMeshes(_targetRotation);
            _lastRotation = _targetRotation;
          }
        }
      }
    }

    public void ToggleTopoData()
    {
      _isActive = !_isActive;

      if (_isActive)
      {
        Quaternion currentRotation = CalculateCameraRotation();

        if (_isDirty)
        {
          GenerateSnapshot();
          _isDirty = false;
          _lastRotation = currentRotation;
          _targetRotation = currentRotation;
        }
        else if (currentRotation != _lastRotation)
        {
          RotateExistingMeshes(currentRotation);
          _lastRotation = currentRotation;
          _targetRotation = currentRotation;
        }

        UpdateVisibility();
        _notificationService.SendNotification(_loc.T("Calloatti.Grid.TopoData.NotificationOn"));
      }
      else
      {
        HideAll();
        _notificationService.SendNotification(_loc.T("Calloatti.Grid.TopoData.NotificationOff"));
      }
    }

    private void OnTerrainHeightChanged(object s, TerrainHeightChangeEventArgs e)
    {
      Vector2Int chunkIdx = new Vector2Int(e.Change.Coordinates.x / ChunkSize, e.Change.Coordinates.y / ChunkSize);

      if (_chunks.TryGetValue(chunkIdx, out var chunk))
      {
        chunk.IsDirty = true;
        if (_isActive) _dirtyChunks.Add(chunk);
      }
      _isDirty = true;
    }

    [OnEvent]
    public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent e)
    {
      if (_isActive) UpdateVisibility();
    }

    private void InitializeVisuals()
    {
      _masterContainer = new GameObject("TopoData_MasterContainer");
      int maxZ = _terrainService.Size.z;

      _vWalkable = new List<Vector3>[maxZ];
      _tWalkable = new List<int>[maxZ];
      _uWalkable = new List<Vector2>[maxZ];
      _vBuried = new List<Vector3>[maxZ];
      _tBuried = new List<int>[maxZ];
      _uBuried = new List<Vector2>[maxZ];

      for (int z = 0; z < maxZ; z++)
      {
        _vWalkable[z] = new List<Vector3>();
        _tWalkable[z] = new List<int>();
        _uWalkable[z] = new List<Vector2>();
        _vBuried[z] = new List<Vector3>();
        _tBuried[z] = new List<int>();
        _uBuried[z] = new List<Vector2>();
      }

      int chunksX = Mathf.CeilToInt(_terrainService.Size.x / (float)ChunkSize);
      int chunksY = Mathf.CeilToInt(_terrainService.Size.y / (float)ChunkSize);

      for (int y = 0; y < chunksY; y++)
      {
        for (int x = 0; x < chunksX; x++)
        {
          Vector2Int coord = new Vector2Int(x, y);
          _chunks[coord] = new TopoChunk(coord, maxZ, _masterContainer.transform, _topoMaterial);
        }
      }
    }

    private Quaternion CalculateCameraRotation()
    {
      float angle = Mathf.Repeat(_cameraService.HorizontalAngle, 360f);
      float snapped = Mathf.Floor((angle + 22.5f) / 90f) * 90f;
      return Quaternion.Euler(90, snapped, 0);
    }

    private void RotateExistingMeshes(Quaternion newRotation)
    {
      Quaternion deltaRot = newRotation * Quaternion.Inverse(_lastRotation);

      foreach (var chunk in _chunks.Values)
      {
        for (int z = 0; z < _terrainService.Size.z; z++)
        {
          RotateMesh(chunk.GetWalkableFilter(z).mesh, deltaRot);
          RotateMesh(chunk.GetBuriedFilter(z).mesh, deltaRot);
        }
      }
    }

    private void RotateMesh(Mesh targetMesh, Quaternion deltaRot)
    {
      if (targetMesh != null && targetMesh.vertexCount > 0)
      {
        targetMesh.GetVertices(_rotationBuffer);

        for (int i = 0; i < _rotationBuffer.Count; i += 4)
        {
          Vector3 center = (_rotationBuffer[i] + _rotationBuffer[i + 3]) / 2f;
          _rotationBuffer[i] = center + deltaRot * (_rotationBuffer[i] - center);
          _rotationBuffer[i + 1] = center + deltaRot * (_rotationBuffer[i + 1] - center);
          _rotationBuffer[i + 2] = center + deltaRot * (_rotationBuffer[i + 2] - center);
          _rotationBuffer[i + 3] = center + deltaRot * (_rotationBuffer[i + 3] - center);
        }

        targetMesh.SetVertices(_rotationBuffer);
        targetMesh.RecalculateBounds();
        targetMesh.RecalculateNormals();
      }
    }

    public void GenerateSnapshot()
    {
      foreach (var chunk in _chunks.Values)
      {
        GenerateChunkSnapshot(chunk);
        chunk.IsDirty = false;
      }
    }

    private void GenerateChunkSnapshot(TopoChunk chunk)
    {
      int maxZ = _terrainService.Size.z;
      Quaternion rot = CalculateCameraRotation();

      Vector3 localP0 = rot * new Vector3(-0.5f, -0.5f, 0);
      Vector3 localP1 = rot * new Vector3(0.5f, -0.5f, 0);
      Vector3 localP2 = rot * new Vector3(-0.5f, 0.5f, 0);
      Vector3 localP3 = rot * new Vector3(0.5f, 0.5f, 0);

      for (int z = 0; z < maxZ; z++)
      {
        _vWalkable[z].Clear(); _tWalkable[z].Clear(); _uWalkable[z].Clear();
        _vBuried[z].Clear(); _tBuried[z].Clear(); _uBuried[z].Clear();
      }

      int startX = chunk.ChunkCoords.x * ChunkSize;
      int startY = chunk.ChunkCoords.y * ChunkSize;
      int endX = Mathf.Min(startX + ChunkSize, _terrainService.Size.x);
      int endY = Mathf.Min(startY + ChunkSize, _terrainService.Size.y);

      for (int y = startY; y < endY; y++)
      {
        for (int x = startX; x < endX; x++)
        {
          Vector2Int cell = new Vector2Int(x, y);
          foreach (Vector3Int heightCoords in _terrainService.GetAllHeightsInCell(cell))
          {
            int displayValue = heightCoords.z;
            int surfaceZ = displayValue - 1;
            int currentZ = surfaceZ;

            while (currentZ >= -1)
            {
              bool isWalkable = (currentZ == surfaceZ);
              if (currentZ >= 0 && !_terrainService.Underground(new Vector3Int(x, y, currentZ))) break;

              int layerIndex = Mathf.Clamp(currentZ, 0, maxZ - 1);

              if (isWalkable)
              {
                AddQuadToArrays(currentZ, x, y, _vWalkable[layerIndex], _tWalkable[layerIndex], _uWalkable[layerIndex], displayValue, localP0, localP1, localP2, localP3);
              }
              else
              {
                AddQuadToArrays(currentZ, x, y, _vBuried[layerIndex], _tBuried[layerIndex], _uBuried[layerIndex], displayValue, localP0, localP1, localP2, localP3);
              }

              if (currentZ == -1) break;
              currentZ--;
            }
          }
        }
      }

      for (int z = 0; z < maxZ; z++)
      {
        Mesh wMesh = chunk.GetWalkableFilter(z).mesh;
        wMesh.Clear();
        if (_vWalkable[z].Count > 0)
        {
          wMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
          wMesh.SetVertices(_vWalkable[z]);
          wMesh.SetTriangles(_tWalkable[z], 0);
          wMesh.SetUVs(0, _uWalkable[z]);
          wMesh.RecalculateBounds();
          wMesh.RecalculateNormals();
        }

        Mesh bMesh = chunk.GetBuriedFilter(z).mesh;
        bMesh.Clear();
        if (_vBuried[z].Count > 0)
        {
          bMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
          bMesh.SetVertices(_vBuried[z]);
          bMesh.SetTriangles(_tBuried[z], 0);
          bMesh.SetUVs(0, _uBuried[z]);
          bMesh.RecalculateBounds();
          bMesh.RecalculateNormals();
        }
      }
    }

    private void AddQuadToArrays(int z, int x, int y, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, int displayValue, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
      int vIndex = vertices.Count;
      float unityHeight = z + 1.0f + HeightOffset;
      Vector3 worldPos = CoordinateSystem.GridToWorld(new Vector3(x + 0.5f, y + 0.5f, 0));
      worldPos.y = unityHeight;

      vertices.Add(worldPos + p0);
      vertices.Add(worldPos + p1);
      vertices.Add(worldPos + p2);
      vertices.Add(worldPos + p3);

      int spriteIndex = Mathf.Clamp(displayValue, 0, 255);
      float uMin = (float)spriteIndex / GridColumns;
      float uMax = (float)(spriteIndex + 1) / GridColumns;
      float vMax = 1.0f - ((float)TopoDataRow / GridRows);
      float vMin = 1.0f - ((float)(TopoDataRow + 1) / GridRows);
      uvs.Add(new Vector2(uMin, vMin));
      uvs.Add(new Vector2(uMax, vMin));
      uvs.Add(new Vector2(uMin, vMax));
      uvs.Add(new Vector2(uMax, vMax));

      triangles.Add(vIndex); triangles.Add(vIndex + 2); triangles.Add(vIndex + 1);
      triangles.Add(vIndex + 1); triangles.Add(vIndex + 2); triangles.Add(vIndex + 3);
    }

    private void UpdateVisibility()
    {
      int maxVisibleLevel = _levelVisibilityService.MaxVisibleLevel;
      foreach (var chunk in _chunks.Values)
      {
        chunk.UpdateVisibility(_isActive, maxVisibleLevel);
      }
    }

    private void HideAll()
    {
      foreach (var chunk in _chunks.Values)
      {
        chunk.UpdateVisibility(false, 0);
      }
    }

    public void Dispose()
    {
      foreach (var chunk in _chunks.Values) chunk.Destroy();
      _chunks.Clear();

      if (_masterContainer != null) UnityEngine.Object.Destroy(_masterContainer);
      if (_topoMaterial != null) UnityEngine.Object.Destroy(_topoMaterial);
    }

  }

  // =========================================================================
  // 3. TOPO CHUNK
  // =========================================================================
  public class TopoChunk
  {
    private readonly Vector2Int _chunkCoords;
    private readonly int _maxZ;
    private readonly GameObject _chunkRoot;

    private readonly MeshFilter[] _walkableFilters;
    private readonly MeshRenderer[] _walkableRenderers;

    private readonly MeshFilter[] _buriedFilters;
    private readonly MeshRenderer[] _buriedRenderers;

    private readonly Material _material;

    public bool IsDirty { get; set; } = true;
    public Vector2Int ChunkCoords => _chunkCoords;

    public TopoChunk(Vector2Int chunkCoords, int maxZ, Transform parent, Material material)
    {
      _chunkCoords = chunkCoords;
      _maxZ = maxZ;
      _material = material;

      _chunkRoot = new GameObject($"TopoChunk_{chunkCoords.x}_{chunkCoords.y}");
      _chunkRoot.transform.SetParent(parent);

      _walkableFilters = new MeshFilter[maxZ];
      _walkableRenderers = new MeshRenderer[maxZ];

      _buriedFilters = new MeshFilter[maxZ];
      _buriedRenderers = new MeshRenderer[maxZ];

      for (int z = 0; z < maxZ; z++)
      {
        GameObject walkableObj = new GameObject($"L_{z}_Walkable");
        walkableObj.transform.SetParent(_chunkRoot.transform);
        _walkableFilters[z] = walkableObj.AddComponent<MeshFilter>();
        _walkableRenderers[z] = walkableObj.AddComponent<MeshRenderer>();
        _walkableFilters[z].mesh = new Mesh();
        if (_material != null) _walkableRenderers[z].sharedMaterial = _material;
        walkableObj.SetActive(false);

        GameObject buriedObj = new GameObject($"L_{z}_Buried");
        buriedObj.transform.SetParent(_chunkRoot.transform);
        _buriedFilters[z] = buriedObj.AddComponent<MeshFilter>();
        _buriedRenderers[z] = buriedObj.AddComponent<MeshRenderer>();
        _buriedFilters[z].mesh = new Mesh();
        if (_material != null) _buriedRenderers[z].sharedMaterial = _material;
        buriedObj.SetActive(false);
      }
    }

    public void UpdateVisibility(bool isActive, int maxVisibleLevel)
    {
      for (int z = 0; z < _maxZ; z++)
      {
        bool isAtSlice = (z == maxVisibleLevel);
        bool isBelowSlice = (z < maxVisibleLevel);

        bool showWalkable = isActive && (isAtSlice || isBelowSlice);
        bool showBuried = isActive && isAtSlice;

        if (_walkableFilters[z].gameObject.activeSelf != showWalkable)
        {
          _walkableFilters[z].gameObject.SetActive(showWalkable);
        }

        if (_buriedFilters[z].gameObject.activeSelf != showBuried)
        {
          _buriedFilters[z].gameObject.SetActive(showBuried);
        }
      }
    }

    public MeshFilter GetWalkableFilter(int z) => _walkableFilters[z];
    public MeshFilter GetBuriedFilter(int z) => _buriedFilters[z];

    public void Destroy()
    {
      for (int i = 0; i < _maxZ; i++)
      {
        if (_walkableFilters[i] != null && _walkableFilters[i].sharedMesh != null)
          UnityEngine.Object.Destroy(_walkableFilters[i].sharedMesh);

        if (_buriedFilters[i] != null && _buriedFilters[i].sharedMesh != null)
          UnityEngine.Object.Destroy(_buriedFilters[i].sharedMesh);
      }
      if (_chunkRoot != null) UnityEngine.Object.Destroy(_chunkRoot);
    }
  }
}