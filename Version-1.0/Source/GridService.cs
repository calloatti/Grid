using System;
using System.Collections.Generic;
using Bindito.Core;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.LevelVisibilitySystem;
using Timberborn.NaturalResources;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using UnityEngine;
using UnityEngine.Rendering;

namespace Calloatti.Grid
{
  public class GridService : ILoadableSingleton, IPostLoadableSingleton, ILateUpdatableSingleton, IDisposable
  {
    private const string ModId = "Calloatti.Grid";

    private readonly EventBus _eventBus;
    private readonly GridInputService _gridInputService;
    private readonly ITerrainService _terrainService;
    private readonly ILevelVisibilityService _levelVisibilityService;
    private readonly IBlockService _blockService;

    private float _rebuildCooldown = 0f;
    private const float RebuildDelay = 0.25f;

    public GridSettings Settings { get; private set; } = new GridSettings();

    private bool _showTerrain = false;
    private bool _showBuilding = false;

    private const float SliceBaseHeight = 0.85f;
    private const float SurfaceBaseHeight = 1.00f;

    private GameObject _terrainGridRoot;

    private GameObject _bedrockMesh;
    private GameObject _bedrockHighlightMesh;

    private GameObject[] _terrainSurfaceMeshes;
    private GameObject[] _terrainSurfaceHighlightMeshes;
    private GameObject[] _terrainSliceMeshes;
    private GameObject[] _terrainSliceHighlightMeshes;

    private GameObject[] _buildingSurfaceMeshes;
    private GameObject[] _buildingSurfaceHighlightMeshes;
    private GameObject[] _buildingSliceMeshes;
    private GameObject[] _buildingSliceHighlightMeshes;

    private bool[,,] _isTerrainCache;
    private bool[,,] _isBuildingCache;

    private int _mapSizeX;
    private int _mapSizeY;
    private int _mapMaxZ;
    private int _buildingMaxZ;

    private HashSet<int> _dirtyLevels = new HashSet<int>();

    private Material _terrainMaterial;
    private Material _buildingMaterial;
    private Material _highlightMaterial;

    private readonly List<Vector3> _surfaceVerts = new List<Vector3>();
    private readonly List<int> _surfaceIndices = new List<int>();
    private readonly List<Vector3> _surfaceHVerts = new List<Vector3>();
    private readonly List<int> _surfaceHIndices = new List<int>();
    private readonly List<Vector3> _sliceVerts = new List<Vector3>();
    private readonly List<int> _sliceIndices = new List<int>();
    private readonly List<Vector3> _sliceHVerts = new List<Vector3>();
    private readonly List<int> _sliceHIndices = new List<int>();

    [Inject]
    public GridService(
        EventBus eventBus,
        GridInputService gridInputService,
        ITerrainService terrainService,
        ILevelVisibilityService levelVisibilityService,
        IBlockService blockService)
    {
      _eventBus = eventBus;
      _gridInputService = gridInputService;
      _terrainService = terrainService;
      _levelVisibilityService = levelVisibilityService;
      _blockService = blockService;
    }

    public void Load()
    {
      EnsureSettingsLoaded();
      InitializeMaterials();
    }

    public void PostLoad()
    {
      _eventBus.Register(this);
      _gridInputService.OnToggleTerrainGrid += ToggleTerrainGrid;
      _gridInputService.OnToggleBuildingGrid += ToggleBuildingGrid;
      _terrainService.TerrainHeightChanged += OnTerrainHeightChanged;
      Debug.Log($"{GridConfigurator.Prefix} GridService loaded.");
    }

    public void LateUpdateSingleton()
    {
      if (_terrainGridRoot != null && _terrainGridRoot.activeSelf && _dirtyLevels.Count > 0)
      {
        _rebuildCooldown -= Time.deltaTime;
        if (_rebuildCooldown <= 0f)
        {
          ProcessDirtyLevels();
        }
      }
    }

    public void Dispose()
    {
      OnDispose();
    }

    private void EnsureSettingsLoaded()
    {
      try
      {
        Settings.LoadFromSimpleConfig();
      }
      catch (Exception e)
      {
        Debug.LogError($"{GridConfigurator.Prefix} Failed to load settings from SimpleConfig: {e.Message}");
      }
    }

    public void ReloadSettings()
    {
      EnsureSettingsLoaded();
      InitializeMaterials();
    }

    public GridSettings ReadConfigFile()
    {
      ReloadSettings();
      return Settings;
    }

    public void ToggleTerrainGrid()
    {
      _showTerrain = !_showTerrain;
      UpdateGridState();
    }

    public void ToggleBuildingGrid()
    {
      _showBuilding = !_showBuilding;
      UpdateGridState();
    }

    private void UpdateGridState()
    {
      if (!_showTerrain && !_showBuilding)
      {
        TurnOffTerrainGrid();
        return;
      }

      if (_terrainGridRoot == null)
      {
        GenerateFullTerrainGrid();
      }
      else
      {
        _terrainGridRoot.SetActive(true);
        ProcessDirtyLevels();
        UpdateVisibleLevels();
      }
    }

    [OnEvent]
    public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent maxVisibleLevelChangedEvent)
    {
      UpdateVisibleLevels();
    }

    private void OnTerrainHeightChanged(object sender, Timberborn.TerrainSystem.TerrainHeightChangeEventArgs e)
    {
      if (_isTerrainCache == null) return;
      Timberborn.TerrainSystem.TerrainHeightChange change = e.Change;
      int x = change.Coordinates.x;
      int y = change.Coordinates.y;

      for (int z = 0; z < _mapMaxZ; z++)
      {
        _isTerrainCache[x, y, z] = _terrainService.Underground(new Vector3Int(x, y, z));
      }

      int minZ = Math.Max(0, change.From - 1);
      int maxZ = Math.Min(_mapMaxZ - 1, change.To);

      for (int z = minZ; z <= maxZ; z++)
      {
        _dirtyLevels.Add(z);
      }

      _rebuildCooldown = RebuildDelay;
    }

    [OnEvent]
    public void OnBlockObjectSet(BlockObjectSetEvent e) { ProcessBlockObjectChange(e.BlockObject); }

    [OnEvent]
    public void OnBlockObjectUnset(BlockObjectUnsetEvent e) { ProcessBlockObjectChange(e.BlockObject); }

    private void ProcessBlockObjectChange(BlockObject bo)
    {
      if (_isBuildingCache == null || bo == null || bo.PositionedBlocks == null) return;

      if (bo.GetComponent<NaturalResource>() != null) return;

      bool levelDirtied = false;
      foreach (var coords in bo.PositionedBlocks.GetAllCoordinates())
      {
        int x = coords.x; int y = coords.y; int z = coords.z;
        if (x >= 0 && x < _mapSizeX && y >= 0 && y < _mapSizeY && z >= 0 && z < _buildingMaxZ)
        {
          _isBuildingCache[x, y, z] = CheckIfBuildingBlock(new Vector3Int(x, y, z));
          _dirtyLevels.Add(z);
          levelDirtied = true;
        }
      }

      if (levelDirtied)
      {
        _rebuildCooldown = RebuildDelay;
      }
    }

    private void InitializeMaterials()
    {
      if (_terrainMaterial == null)
      {
        _terrainMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
        _terrainMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
      }
      _terrainMaterial.color = Settings.GridColor;

      if (_buildingMaterial == null)
      {
        _buildingMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
        _buildingMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
      }
      _buildingMaterial.color = Settings.BuildingGridColor;

      if (_highlightMaterial == null)
      {
        _highlightMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
        _highlightMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
      }
      _highlightMaterial.color = Settings.HighlightColor;
    }

    private void TurnOffTerrainGrid()
    {
      if (_terrainGridRoot != null) _terrainGridRoot.SetActive(false);
    }

    private MeshRenderer CreateGridMesh(string name, List<Vector3> vertices, List<int> indices, Material mat, out GameObject obj)
    {
      obj = new GameObject(name);
      MeshFilter mf = obj.AddComponent<MeshFilter>();
      MeshRenderer mr = obj.AddComponent<MeshRenderer>();
      Mesh mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
      mesh.SetVertices(vertices);
      mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
      mf.mesh = mesh;
      mr.material = mat;
      return mr;
    }

    private void UpdateTerrainCache()
    {
      Vector3Int terrainSize = _terrainService.Size;
      Vector3Int blockSize = _blockService.Size;
      _mapSizeX = terrainSize.x;
      _mapSizeY = terrainSize.y;
      _mapMaxZ = terrainSize.z;
      _buildingMaxZ = blockSize.z;

      _isTerrainCache = new bool[_mapSizeX, _mapSizeY, _mapMaxZ];
      _isBuildingCache = new bool[_mapSizeX, _mapSizeY, _buildingMaxZ];

      for (int x = 0; x < _mapSizeX; x++)
      {
        for (int y = 0; y < _mapSizeY; y++)
        {
          for (int z = 0; z < _mapMaxZ; z++)
          {
            _isTerrainCache[x, y, z] = _terrainService.Underground(new Vector3Int(x, y, z));
          }
        }
      }

      for (int x = 0; x < _mapSizeX; x++)
      {
        for (int y = 0; y < _mapSizeY; y++)
        {
          for (int z = 0; z < _buildingMaxZ; z++)
          {
            _isBuildingCache[x, y, z] = CheckIfBuildingBlock(new Vector3Int(x, y, z));
          }
        }
      }
    }

    private bool CheckIfBuildingBlock(Vector3Int pos)
    {
      foreach (BlockObject obj in _blockService.GetObjectsAt(pos))
      {
        if (obj.GetComponent<NaturalResource>() != null) continue;
        if (!obj.PositionedBlocks.HasBlockAt(pos)) continue;

        Block runtimeBlock = obj.PositionedBlocks.GetBlock(pos);
        if (runtimeBlock.Occupation == BlockOccupations.Path) continue;

        return true;
      }
      return false;
    }

    private bool IsSolid(int x, int y, int z, bool[,,] cache)
    {
      if (x < 0 || x >= _mapSizeX || y < 0 || y >= _mapSizeY || z < 0 || z >= cache.GetLength(2)) return false;
      return cache[x, y, z];
    }

    private Vector3 GetWorldPos(float x, float y, float height)
    {
      Vector3 pos = CoordinateSystem.GridToWorld(new Vector3(x, y, 0));
      pos.y = height;
      return pos;
    }

    private Vector3 GetOffsetVertex(int vx, int vy, int z, float height, bool[,,] cache)
    {
      bool q1 = IsSolid(vx, vy, z, cache);
      bool q2 = IsSolid(vx - 1, vy, z, cache);
      bool q3 = IsSolid(vx - 1, vy - 1, z, cache);
      bool q4 = IsSolid(vx, vy - 1, z, cache);

      int right = (q1 ? 1 : 0) + (q4 ? 1 : 0);
      int left = (q2 ? 1 : 0) + (q3 ? 1 : 0);
      int top = (q1 ? 1 : 0) + (q2 ? 1 : 0);
      int bottom = (q3 ? 1 : 0) + (q4 ? 1 : 0);

      float dx = 0f;
      if (left > right) dx = Settings.HorizontalOffsetEW;
      else if (right > left) dx = -Settings.HorizontalOffsetEW;

      float dy = 0f;
      if (bottom > top) dy = Settings.HorizontalOffsetNS;
      else if (top > bottom) dy = -Settings.HorizontalOffsetNS;

      return GetWorldPos(vx + dx, vy + dy, height);
    }

    private bool IsHighlight(int pos, int interval, int width)
    {
      if (interval == 0 && width == 0) return false;
      if (width == 0) return pos % interval == 0;
      if (interval == 0) return pos % width == 0;

      int cycle = interval + width;
      return (pos % cycle == 0) || (pos % cycle == width);
    }

    public void GenerateFullTerrainGrid()
    {
      if (_terrainGridRoot != null)
      {
        MeshFilter[] filters = _terrainGridRoot.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter mf in filters)
        {
          if (mf.sharedMesh != null) UnityEngine.Object.Destroy(mf.sharedMesh);
        }
        UnityEngine.Object.Destroy(_terrainGridRoot);
      }
      _terrainGridRoot = new GameObject("TerrainGridRoot");

      EnsureSettingsLoaded();
      InitializeMaterials();
      UpdateTerrainCache();

      int maxMeshZ = Math.Max(_mapMaxZ, _buildingMaxZ);
      _terrainSurfaceMeshes = new GameObject[maxMeshZ];
      _terrainSurfaceHighlightMeshes = new GameObject[maxMeshZ];
      _terrainSliceMeshes = new GameObject[maxMeshZ];
      _terrainSliceHighlightMeshes = new GameObject[maxMeshZ];

      _buildingSurfaceMeshes = new GameObject[maxMeshZ];
      _buildingSurfaceHighlightMeshes = new GameObject[maxMeshZ];
      _buildingSliceMeshes = new GameObject[maxMeshZ];
      _buildingSliceHighlightMeshes = new GameObject[maxMeshZ];

      BuildBedrockMesh();

      for (int z = 0; z < Math.Max(_mapMaxZ, _buildingMaxZ); z++)
      {
        BuildLevelMeshes(z);
      }

      _dirtyLevels.Clear();
      UpdateVisibleLevels();
    }

    private void BuildBedrockMesh()
    {
      _surfaceVerts.Clear();
      _surfaceIndices.Clear();
      _surfaceHVerts.Clear();
      _surfaceHIndices.Clear();

      float h = Settings.NormalVerticalOffset;

      void AddBedrockLine(Vector3 a, Vector3 b, bool isHighlight)
      {
        if (isHighlight && Settings.HighlightEnabled)
        {
          int start = _surfaceHVerts.Count;
          _surfaceHVerts.Add(a); _surfaceHVerts.Add(b);
          _surfaceHIndices.Add(start); _surfaceHIndices.Add(start + 1);
        }
        else
        {
          int start = _surfaceVerts.Count;
          _surfaceVerts.Add(a); _surfaceVerts.Add(b);
          _surfaceIndices.Add(start); _surfaceIndices.Add(start + 1);
        }
      }

      for (int y = 0; y <= _mapSizeY; y++)
      {
        bool isH = IsHighlight(y, Settings.HighlightIntervalY, Settings.HighlightWidthY);
        for (int x = 0; x < _mapSizeX; x++)
        {
          bool exposedCurrent = (y < _mapSizeY) && !IsSolid(x, y, 0, _isTerrainCache);
          bool exposedBelow = (y > 0) && !IsSolid(x, y - 1, 0, _isTerrainCache);

          if (exposedCurrent || exposedBelow)
          {
            AddBedrockLine(GetWorldPos(x, y, h), GetWorldPos(x + 1, y, h), isH);
          }
        }
      }

      for (int x = 0; x <= _mapSizeX; x++)
      {
        bool isH = IsHighlight(x, Settings.HighlightIntervalX, Settings.HighlightWidthX);
        for (int y = 0; y < _mapSizeY; y++)
        {
          bool exposedCurrent = (x < _mapSizeX) && !IsSolid(x, y, 0, _isTerrainCache);
          bool exposedLeft = (x > 0) && !IsSolid(x - 1, y, 0, _isTerrainCache);

          if (exposedCurrent || exposedLeft)
          {
            AddBedrockLine(GetWorldPos(x, y, h), GetWorldPos(x, y + 1, h), isH);
          }
        }
      }

      CreateGridMesh("BedrockGrid", _surfaceVerts, _surfaceIndices, _terrainMaterial, out _bedrockMesh);
      _bedrockMesh.transform.SetParent(_terrainGridRoot.transform);

      CreateGridMesh("BedrockGrid_Highlight", _surfaceHVerts, _surfaceHIndices, _highlightMaterial, out _bedrockHighlightMesh);
      _bedrockHighlightMesh.transform.SetParent(_terrainGridRoot.transform);
    }

    private void BuildLevelMeshes(int z)
    {
      if (z < _mapMaxZ)
      {
        BuildGridLevelMeshes(z, _isTerrainCache, "Terrain", _terrainMaterial,
          out _terrainSurfaceMeshes[z], out _terrainSurfaceHighlightMeshes[z],
          out _terrainSliceMeshes[z], out _terrainSliceHighlightMeshes[z]);
      }

      if (z < _buildingMaxZ)
      {
        BuildGridLevelMeshes(z, _isBuildingCache, "Building", _buildingMaterial,
          out _buildingSurfaceMeshes[z], out _buildingSurfaceHighlightMeshes[z],
          out _buildingSliceMeshes[z], out _buildingSliceHighlightMeshes[z]);
      }
    }

    private void BuildGridLevelMeshes(int z, bool[,,] cache, string namePrefix, Material mat,
      out GameObject surfaceMesh, out GameObject surfaceHighlightMesh,
      out GameObject sliceMesh, out GameObject sliceHighlightMesh)
    {
      _surfaceVerts.Clear();
      _surfaceIndices.Clear();
      _surfaceHVerts.Clear();
      _surfaceHIndices.Clear();
      _sliceVerts.Clear();
      _sliceIndices.Clear();
      _sliceHVerts.Clear();
      _sliceHIndices.Clear();

      float surfaceHeight = z + SurfaceBaseHeight + Settings.NormalVerticalOffset;
      float sliceHeight = z + SliceBaseHeight + Settings.SlicedVerticalOffset;
      float hBotNormal = z + Settings.NormalVerticalOffset;
      float hBotSliced = z + Settings.SlicedVerticalOffset;
      float hTopNormal = z + SurfaceBaseHeight + Settings.NormalVerticalOffset;

      void AddLineEx(Vector3 a, Vector3 b, bool isHighlight, List<Vector3> v, List<int> i, List<Vector3> hv, List<int> hi)
      {
        if (isHighlight && Settings.HighlightEnabled && namePrefix != "Building")
        {
          int start = hv.Count;
          hv.Add(a); hv.Add(b);
          hi.Add(start); hi.Add(start + 1);
        }
        else
        {
          int start = v.Count;
          v.Add(a); v.Add(b);
          i.Add(start); i.Add(start + 1);
        }
      }

      void AddCellLines(int x, int y, int zLvl, float h, List<Vector3> v, List<int> i, List<Vector3> hv, List<int> hi, bool isSurfaceMesh, bool isHX, bool isHY, bool isHXNext, bool isHYNext)
      {
        bool IsSameGroup(int nx, int ny)
        {
          if (!IsSolid(nx, ny, zLvl, cache)) return false;
          if (isSurfaceMesh && IsSolid(nx, ny, zLvl + 1, cache)) return false;
          return true;
        }

        AddLineEx(GetWorldPos(x, y, h), GetWorldPos(x + 1, y, h), isHY, v, i, hv, hi);
        AddLineEx(GetWorldPos(x, y, h), GetWorldPos(x, y + 1, h), isHX, v, i, hv, hi);

        if (x == _mapSizeX - 1 || !IsSameGroup(x + 1, y))
          AddLineEx(GetWorldPos(x + 1, y, h), GetWorldPos(x + 1, y + 1, h), isHXNext, v, i, hv, hi);

        if (y == _mapSizeY - 1 || !IsSameGroup(x, y + 1))
          AddLineEx(GetWorldPos(x, y + 1, h), GetWorldPos(x + 1, y + 1, h), isHYNext, v, i, hv, hi);
      }

      for (int x = 0; x < _mapSizeX; x++)
      {
        bool isHX = IsHighlight(x, Settings.HighlightIntervalX, Settings.HighlightWidthX);
        bool isHXNext = IsHighlight(x + 1, Settings.HighlightIntervalX, Settings.HighlightWidthX);

        for (int y = 0; y < _mapSizeY; y++)
        {
          if (!IsSolid(x, y, z, cache)) continue;

          bool isHY = IsHighlight(y, Settings.HighlightIntervalY, Settings.HighlightWidthY);
          bool isHYNext = IsHighlight(y + 1, Settings.HighlightIntervalY, Settings.HighlightWidthY);

          AddCellLines(x, y, z, sliceHeight, _sliceVerts, _sliceIndices, _sliceHVerts, _sliceHIndices, false, isHX, isHY, isHXNext, isHYNext);
          if (!IsSolid(x, y, z + 1, cache))
            AddCellLines(x, y, z, surfaceHeight, _surfaceVerts, _surfaceIndices, _surfaceHVerts, _surfaceHIndices, true, isHX, isHY, isHXNext, isHYNext);

          bool hasAirAbove = !IsSolid(x, y, z + 1, cache);

          if (!IsSolid(x, y - 1, z, cache))
          {
            AddLineEx(GetOffsetVertex(x, y, z, hBotNormal, cache), GetOffsetVertex(x + 1, y, z, hBotNormal, cache), isHY, _surfaceVerts, _surfaceIndices, _surfaceHVerts, _surfaceHIndices);
            AddLineEx(GetOffsetVertex(x, y, z, hBotSliced, cache), GetOffsetVertex(x + 1, y, z, hBotSliced, cache), isHY, _sliceVerts, _sliceIndices, _sliceHVerts, _sliceHIndices);
            if (!hasAirAbove) AddLineEx(GetOffsetVertex(x, y, z, hTopNormal, cache), GetOffsetVertex(x + 1, y, z, hTopNormal, cache), isHY, _surfaceVerts, _surfaceIndices, _surfaceHVerts, _surfaceHIndices);
          }

          if (!IsSolid(x + 1, y, z, cache))
          {
            AddLineEx(GetOffsetVertex(x + 1, y, z, hBotNormal, cache), GetOffsetVertex(x + 1, y + 1, z, hBotNormal, cache), isHXNext, _surfaceVerts, _surfaceIndices, _surfaceHVerts, _surfaceHIndices);
            AddLineEx(GetOffsetVertex(x + 1, y, z, hBotSliced, cache), GetOffsetVertex(x + 1, y + 1, z, hBotSliced, cache), isHXNext, _sliceVerts, _sliceIndices, _sliceHVerts, _sliceHIndices);
            if (!hasAirAbove) AddLineEx(GetOffsetVertex(x + 1, y + 1, z, hTopNormal, cache), GetOffsetVertex(x + 1, y + 1, z, hTopNormal, cache), isHXNext, _surfaceVerts, _surfaceIndices, _surfaceHVerts, _surfaceHIndices);
          }

          if (!IsSolid(x, y + 1, z, cache))
          {
            AddLineEx(GetOffsetVertex(x + 1, y + 1, z, hBotNormal, cache), GetOffsetVertex(x, y + 1, z, hBotNormal, cache), isHYNext, _surfaceVerts, _surfaceIndices, _surfaceHVerts, _surfaceHIndices);
            AddLineEx(GetOffsetVertex(x + 1, y + 1, z, hBotSliced, cache), GetOffsetVertex(x, y + 1, z, hBotSliced, cache), isHYNext, _sliceVerts, _sliceIndices, _sliceHVerts, _sliceHIndices);
            if (!hasAirAbove) AddLineEx(GetOffsetVertex(x + 1, y + 1, z, hTopNormal, cache), GetOffsetVertex(x, y + 1, z, hTopNormal, cache), isHYNext, _surfaceVerts, _surfaceIndices, _surfaceHVerts, _surfaceHIndices);
          }

          if (!IsSolid(x - 1, y, z, cache))
          {
            AddLineEx(GetOffsetVertex(x, y + 1, z, hBotNormal, cache), GetOffsetVertex(x, y, z, hBotNormal, cache), isHX, _surfaceVerts, _surfaceIndices, _surfaceHVerts, _surfaceHIndices);
            AddLineEx(GetOffsetVertex(x, y + 1, z, hBotSliced, cache), GetOffsetVertex(x, y, z, hBotSliced, cache), isHX, _sliceVerts, _sliceIndices, _sliceHVerts, _sliceHIndices);
            if (!hasAirAbove) AddLineEx(GetOffsetVertex(x, y + 1, z, hTopNormal, cache), GetOffsetVertex(x, y, z, hTopNormal, cache), isHX, _surfaceVerts, _surfaceIndices, _surfaceHVerts, _surfaceHIndices);
          }
        }
      }

      for (int vx = 0; vx <= _mapSizeX; vx++)
      {
        bool isHighlightX = IsHighlight(vx, Settings.HighlightIntervalX, Settings.HighlightWidthX);

        for (int vy = 0; vy <= _mapSizeY; vy++)
        {
          bool q1 = IsSolid(vx, vy, z, cache);
          bool q2 = IsSolid(vx - 1, vy, z, cache);
          bool q3 = IsSolid(vx - 1, vy - 1, z, cache);
          bool q4 = IsSolid(vx, vy - 1, z, cache);
          int solidCount = (q1 ? 1 : 0) + (q2 ? 1 : 0) + (q3 ? 1 : 0) + (q4 ? 1 : 0);
          if (solidCount == 0 || solidCount == 4) continue;

          bool isH = isHighlightX || IsHighlight(vy, Settings.HighlightIntervalY, Settings.HighlightWidthY);

          AddLineEx(GetOffsetVertex(vx, vy, z, hBotNormal, cache), GetOffsetVertex(vx, vy, z, hTopNormal, cache), isH, _surfaceVerts, _surfaceIndices, _surfaceHVerts, _surfaceHIndices);
          AddLineEx(GetOffsetVertex(vx, vy, z, hBotSliced, cache), GetOffsetVertex(vx, vy, z, sliceHeight, cache), isH, _sliceVerts, _sliceIndices, _sliceHVerts, _sliceHIndices);
        }
      }

      CreateGridMesh($"{namePrefix}_Surface_Z{z}", _surfaceVerts, _surfaceIndices, mat, out surfaceMesh);
      surfaceMesh.transform.SetParent(_terrainGridRoot.transform);

      CreateGridMesh($"{namePrefix}_SurfaceHighlight_Z{z}", _surfaceHVerts, _surfaceHIndices, _highlightMaterial, out surfaceHighlightMesh);
      surfaceHighlightMesh.transform.SetParent(_terrainGridRoot.transform);

      CreateGridMesh($"{namePrefix}_Slice_Z{z}", _sliceVerts, _sliceIndices, mat, out sliceMesh);
      sliceMesh.transform.SetParent(_terrainGridRoot.transform);

      CreateGridMesh($"{namePrefix}_SliceHighlight_Z{z}", _sliceHVerts, _sliceHIndices, _highlightMaterial, out sliceHighlightMesh);
      sliceHighlightMesh.transform.SetParent(_terrainGridRoot.transform);
    }

    public void UpdateVisibleLevels()
    {
      if (_terrainGridRoot == null || !_terrainGridRoot.activeSelf) return;

      bool showTerrain = _showTerrain;
      bool showBuilding = _showBuilding;

      if (_bedrockMesh != null) _bedrockMesh.SetActive(showTerrain);
      if (_bedrockHighlightMesh != null) _bedrockHighlightMesh.SetActive(showTerrain);

      int maxV = _levelVisibilityService.MaxVisibleLevel;

      int maxMeshZ = Math.Max(_mapMaxZ, _buildingMaxZ);
      for (int z = 0; z < maxMeshZ; z++)
      {
        if (z < _mapMaxZ)
        {
          if (_terrainSurfaceMeshes[z] != null) _terrainSurfaceMeshes[z].SetActive(showTerrain && z < maxV);
          if (_terrainSurfaceHighlightMeshes[z] != null) _terrainSurfaceHighlightMeshes[z].SetActive(showTerrain && z < maxV);

          if (_terrainSliceMeshes[z] != null) _terrainSliceMeshes[z].SetActive(showTerrain && z == maxV);
          if (_terrainSliceHighlightMeshes[z] != null) _terrainSliceHighlightMeshes[z].SetActive(showTerrain && z == maxV);
        }

        if (_buildingSurfaceMeshes[z] != null) _buildingSurfaceMeshes[z].SetActive(showBuilding && z < maxV);
        if (_buildingSurfaceHighlightMeshes[z] != null) _buildingSurfaceHighlightMeshes[z].SetActive(showBuilding && z < maxV);

        if (_buildingSliceMeshes[z] != null) _buildingSliceMeshes[z].SetActive(showBuilding && z == maxV);
        if (_buildingSliceHighlightMeshes[z] != null) _buildingSliceHighlightMeshes[z].SetActive(showBuilding && z == maxV);
      }
    }

    private void ProcessDirtyLevels()
    {
      if (_dirtyLevels.Count == 0 || _terrainSurfaceMeshes == null) return;

      if (_dirtyLevels.Contains(0))
      {
        _dirtyLevels.Remove(0);
        DestroyMeshObject(ref _bedrockMesh);
        DestroyMeshObject(ref _bedrockHighlightMesh);
        BuildBedrockMesh();
        UpdateVisibleLevels();
        return;
      }

      var enumerator = _dirtyLevels.GetEnumerator();
      enumerator.MoveNext();
      int z = enumerator.Current;
      _dirtyLevels.Remove(z);

      DestroyMeshObject(ref _terrainSurfaceMeshes[z]);
      DestroyMeshObject(ref _terrainSurfaceHighlightMeshes[z]);
      DestroyMeshObject(ref _terrainSliceMeshes[z]);
      DestroyMeshObject(ref _terrainSliceHighlightMeshes[z]);

      DestroyMeshObject(ref _buildingSurfaceMeshes[z]);
      DestroyMeshObject(ref _buildingSurfaceHighlightMeshes[z]);
      DestroyMeshObject(ref _buildingSliceMeshes[z]);
      DestroyMeshObject(ref _buildingSliceHighlightMeshes[z]);

      BuildLevelMeshes(z);
      UpdateVisibleLevels();
    }

    private void DestroyMeshObject(ref GameObject obj)
    {
      if (obj != null)
      {
        MeshFilter mf = obj.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
          UnityEngine.Object.Destroy(mf.sharedMesh);
        }
        UnityEngine.Object.Destroy(obj);
        obj = null;
      }
    }

    private void OnDispose()
    {
      if (_terrainGridRoot != null)
      {
        MeshFilter[] filters = _terrainGridRoot.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter mf in filters)
        {
          if (mf.sharedMesh != null) UnityEngine.Object.Destroy(mf.sharedMesh);
        }
        UnityEngine.Object.Destroy(_terrainGridRoot);
      }

      if (_terrainMaterial != null) UnityEngine.Object.Destroy(_terrainMaterial);
      if (_buildingMaterial != null) UnityEngine.Object.Destroy(_buildingMaterial);
      if (_highlightMaterial != null) UnityEngine.Object.Destroy(_highlightMaterial);
    }
  }
}