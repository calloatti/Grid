using System;
using System.Collections.Generic;
using Timberborn.Coordinates;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.NaturalResources;
using Timberborn.Ruins;
using UnityEngine;
using UnityEngine.Rendering;

namespace Calloatti.Grid
{
  public partial class GridService
  {
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

    private HashSet<int> _dirtyLevels = new HashSet<int>();

    private Material _terrainMaterial;
    private Material _buildingMaterial;
    private Material _highlightMaterial;

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
      Vector3Int size = _terrainService.Size;
      _mapSizeX = size.x;
      _mapSizeY = size.y;
      _mapMaxZ = size.z;

      _isTerrainCache = new bool[_mapSizeX, _mapSizeY, _mapMaxZ];
      _isBuildingCache = new bool[_mapSizeX, _mapSizeY, _mapMaxZ];

      for (int x = 0; x < _mapSizeX; x++)
      {
        for (int y = 0; y < _mapSizeY; y++)
        {
          for (int z = 0; z < _mapMaxZ; z++)
          {
            Vector3Int pos = new Vector3Int(x, y, z);
            _isTerrainCache[x, y, z] = _terrainService.Underground(pos);
            _isBuildingCache[x, y, z] = CheckIfBuildingBlock(pos);
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

        if (obj.Solid || obj.GetComponent<Building>() != null || obj.GetComponent<Ruin>() != null)
        {
          return true;
        }
      }
      return false;
    }

    private bool IsSolid(int x, int y, int z, bool[,,] cache)
    {
      if (x < 0 || x >= _mapSizeX || y < 0 || y >= _mapSizeY || z < 0 || z >= _mapMaxZ) return false;
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

      _terrainSurfaceMeshes = new GameObject[_mapMaxZ];
      _terrainSurfaceHighlightMeshes = new GameObject[_mapMaxZ];
      _terrainSliceMeshes = new GameObject[_mapMaxZ];
      _terrainSliceHighlightMeshes = new GameObject[_mapMaxZ];

      _buildingSurfaceMeshes = new GameObject[_mapMaxZ];
      _buildingSurfaceHighlightMeshes = new GameObject[_mapMaxZ];
      _buildingSliceMeshes = new GameObject[_mapMaxZ];
      _buildingSliceHighlightMeshes = new GameObject[_mapMaxZ];

      BuildBedrockMesh();

      for (int z = 0; z < _mapMaxZ; z++)
      {
        BuildLevelMeshes(z);
      }

      _dirtyLevels.Clear();
      UpdateVisibleLevels();
    }

    private void BuildBedrockMesh()
    {
      List<Vector3> verts = new List<Vector3>();
      List<int> indices = new List<int>();
      List<Vector3> hVerts = new List<Vector3>();
      List<int> hIndices = new List<int>();
      float h = Settings.NormalVerticalOffset;

      void AddBedrockLine(Vector3 a, Vector3 b, bool isHighlight)
      {
        if (isHighlight && Settings.HighlightEnabled)
        {
          int start = hVerts.Count;
          hVerts.Add(a); hVerts.Add(b);
          hIndices.Add(start); hIndices.Add(start + 1);
        }
        else
        {
          int start = verts.Count;
          verts.Add(a); verts.Add(b);
          indices.Add(start); indices.Add(start + 1);
        }
      }

      // Draw horizontal segments (along X axis) only if adjacent bedrock is exposed
      for (int y = 0; y <= _mapSizeY; y++)
      {
        bool isH = (y % Settings.HighlightIntervalY == 0);
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

      // Draw vertical segments (along Y axis) only if adjacent bedrock is exposed
      for (int x = 0; x <= _mapSizeX; x++)
      {
        bool isH = (x % Settings.HighlightIntervalX == 0);
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

      CreateGridMesh("BedrockGrid", verts, indices, _terrainMaterial, out _bedrockMesh);
      _bedrockMesh.transform.SetParent(_terrainGridRoot.transform);

      CreateGridMesh("BedrockGrid_Highlight", hVerts, hIndices, _highlightMaterial, out _bedrockHighlightMesh);
      _bedrockHighlightMesh.transform.SetParent(_terrainGridRoot.transform);
    }

    private void BuildLevelMeshes(int z)
    {
      BuildGridLevelMeshes(z, _isTerrainCache, "Terrain", _terrainMaterial,
        out _terrainSurfaceMeshes[z], out _terrainSurfaceHighlightMeshes[z],
        out _terrainSliceMeshes[z], out _terrainSliceHighlightMeshes[z]);

      BuildGridLevelMeshes(z, _isBuildingCache, "Building", _buildingMaterial,
        out _buildingSurfaceMeshes[z], out _buildingSurfaceHighlightMeshes[z],
        out _buildingSliceMeshes[z], out _buildingSliceHighlightMeshes[z]);
    }

    private void BuildGridLevelMeshes(int z, bool[,,] cache, string namePrefix, Material mat,
      out GameObject surfaceMesh, out GameObject surfaceHighlightMesh,
      out GameObject sliceMesh, out GameObject sliceHighlightMesh)
    {
      List<Vector3> surfaceVerts = new List<Vector3>();
      List<int> surfaceIndices = new List<int>();
      List<Vector3> surfaceHVerts = new List<Vector3>();
      List<int> surfaceHIndices = new List<int>();

      List<Vector3> sliceVerts = new List<Vector3>();
      List<int> sliceIndices = new List<int>();
      List<Vector3> sliceHVerts = new List<Vector3>();
      List<int> sliceHIndices = new List<int>();

      float surfaceHeight = z + SurfaceBaseHeight + Settings.NormalVerticalOffset;
      float sliceHeight = z + SliceBaseHeight + Settings.SlicedVerticalOffset;
      float hBotNormal = z + Settings.NormalVerticalOffset;
      float hBotSliced = z + Settings.SlicedVerticalOffset;
      float hTopNormal = z + SurfaceBaseHeight + Settings.NormalVerticalOffset;

      void AddLineEx(Vector3 a, Vector3 b, bool isHighlight, List<Vector3> v, List<int> i, List<Vector3> hv, List<int> hi)
      {
        if (isHighlight && Settings.HighlightEnabled)
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

      void AddCellLines(int x, int y, int zLvl, float h, List<Vector3> v, List<int> i, List<Vector3> hv, List<int> hi, bool isSurfaceMesh)
      {
        bool IsSameGroup(int nx, int ny)
        {
          if (!IsSolid(nx, ny, zLvl, cache)) return false;
          if (isSurfaceMesh && IsSolid(nx, ny, zLvl + 1, cache)) return false;
          return true;
        }

        bool isHx = (y % Settings.HighlightIntervalY == 0);
        bool isHy = (x % Settings.HighlightIntervalX == 0);
        bool isHx2 = ((y + 1) % Settings.HighlightIntervalY == 0);
        bool isHy2 = ((x + 1) % Settings.HighlightIntervalX == 0);

        AddLineEx(GetWorldPos(x, y, h), GetWorldPos(x + 1, y, h), isHx, v, i, hv, hi);
        AddLineEx(GetWorldPos(x, y, h), GetWorldPos(x, y + 1, h), isHy, v, i, hv, hi);

        if (x == _mapSizeX - 1 || !IsSameGroup(x + 1, y))
          AddLineEx(GetWorldPos(x + 1, y, h), GetWorldPos(x + 1, y + 1, h), isHy2, v, i, hv, hi);

        if (y == _mapSizeY - 1 || !IsSameGroup(x, y + 1))
          AddLineEx(GetWorldPos(x, y + 1, h), GetWorldPos(x + 1, y + 1, h), isHx2, v, i, hv, hi);
      }

      for (int x = 0; x < _mapSizeX; x++)
      {
        for (int y = 0; y < _mapSizeY; y++)
        {
          if (!IsSolid(x, y, z, cache)) continue;

          AddCellLines(x, y, z, sliceHeight, sliceVerts, sliceIndices, sliceHVerts, sliceHIndices, false);
          if (!IsSolid(x, y, z + 1, cache))
            AddCellLines(x, y, z, surfaceHeight, surfaceVerts, surfaceIndices, surfaceHVerts, surfaceHIndices, true);

          bool hasAirAbove = !IsSolid(x, y, z + 1, cache);

          if (!IsSolid(x, y - 1, z, cache))
          {
            bool isH = (y % Settings.HighlightIntervalY == 0);
            AddLineEx(GetOffsetVertex(x, y, z, hBotNormal, cache), GetOffsetVertex(x + 1, y, z, hBotNormal, cache), isH, surfaceVerts, surfaceIndices, surfaceHVerts, surfaceHIndices);
            AddLineEx(GetOffsetVertex(x, y, z, hBotSliced, cache), GetOffsetVertex(x + 1, y, z, hBotSliced, cache), isH, sliceVerts, sliceIndices, sliceHVerts, sliceHIndices);
            if (!hasAirAbove) AddLineEx(GetOffsetVertex(x, y, z, hTopNormal, cache), GetOffsetVertex(x + 1, y, z, hTopNormal, cache), isH, surfaceVerts, surfaceIndices, surfaceHVerts, surfaceHIndices);
          }

          if (!IsSolid(x + 1, y, z, cache))
          {
            bool isH = ((x + 1) % Settings.HighlightIntervalX == 0);
            AddLineEx(GetOffsetVertex(x + 1, y, z, hBotNormal, cache), GetOffsetVertex(x + 1, y + 1, z, hBotNormal, cache), isH, surfaceVerts, surfaceIndices, surfaceHVerts, surfaceHIndices);
            AddLineEx(GetOffsetVertex(x + 1, y, z, hBotSliced, cache), GetOffsetVertex(x + 1, y + 1, z, hBotSliced, cache), isH, sliceVerts, sliceIndices, sliceHVerts, sliceHIndices);
            if (!hasAirAbove) AddLineEx(GetOffsetVertex(x + 1, y + 1, z, hTopNormal, cache), GetOffsetVertex(x + 1, y + 1, z, hTopNormal, cache), isH, surfaceVerts, surfaceIndices, surfaceHVerts, surfaceHIndices);
          }

          if (!IsSolid(x, y + 1, z, cache))
          {
            bool isH = ((y + 1) % Settings.HighlightIntervalY == 0);
            AddLineEx(GetOffsetVertex(x + 1, y + 1, z, hBotNormal, cache), GetOffsetVertex(x, y + 1, z, hBotNormal, cache), isH, surfaceVerts, surfaceIndices, surfaceHVerts, surfaceHIndices);
            AddLineEx(GetOffsetVertex(x + 1, y + 1, z, hBotSliced, cache), GetOffsetVertex(x, y + 1, z, hBotSliced, cache), isH, sliceVerts, sliceIndices, sliceHVerts, sliceHIndices);
            if (!hasAirAbove) AddLineEx(GetOffsetVertex(x + 1, y + 1, z, hTopNormal, cache), GetOffsetVertex(x, y + 1, z, hTopNormal, cache), isH, surfaceVerts, surfaceIndices, surfaceHVerts, surfaceHIndices);
          }

          if (!IsSolid(x - 1, y, z, cache))
          {
            bool isH = (x % Settings.HighlightIntervalX == 0);
            AddLineEx(GetOffsetVertex(x, y + 1, z, hBotNormal, cache), GetOffsetVertex(x, y, z, hBotNormal, cache), isH, surfaceVerts, surfaceIndices, surfaceHVerts, surfaceHIndices);
            AddLineEx(GetOffsetVertex(x, y + 1, z, hBotSliced, cache), GetOffsetVertex(x, y, z, hBotSliced, cache), isH, sliceVerts, sliceIndices, sliceHVerts, sliceHIndices);
            if (!hasAirAbove) AddLineEx(GetOffsetVertex(x, y + 1, z, hTopNormal, cache), GetOffsetVertex(x, y, z, hTopNormal, cache), isH, surfaceVerts, surfaceIndices, surfaceHVerts, surfaceHIndices);
          }
        }
      }

      for (int vx = 0; vx <= _mapSizeX; vx++)
      {
        for (int vy = 0; vy <= _mapSizeY; vy++)
        {
          bool q1 = IsSolid(vx, vy, z, cache);
          bool q2 = IsSolid(vx - 1, vy, z, cache);
          bool q3 = IsSolid(vx - 1, vy - 1, z, cache);
          bool q4 = IsSolid(vx, vy - 1, z, cache);
          int solidCount = (q1 ? 1 : 0) + (q2 ? 1 : 0) + (q3 ? 1 : 0) + (q4 ? 1 : 0);
          if (solidCount == 0 || solidCount == 4) continue;

          bool isH = (vx % Settings.HighlightIntervalX == 0) || (vy % Settings.HighlightIntervalY == 0);

          AddLineEx(GetOffsetVertex(vx, vy, z, hBotNormal, cache), GetOffsetVertex(vx, vy, z, hTopNormal, cache), isH, surfaceVerts, surfaceIndices, surfaceHVerts, surfaceHIndices);
          AddLineEx(GetOffsetVertex(vx, vy, z, hBotSliced, cache), GetOffsetVertex(vx, vy, z, sliceHeight, cache), isH, sliceVerts, sliceIndices, sliceHVerts, sliceHIndices);
        }
      }

      CreateGridMesh($"{namePrefix}_Surface_Z{z}", surfaceVerts, surfaceIndices, mat, out surfaceMesh);
      surfaceMesh.transform.SetParent(_terrainGridRoot.transform);

      CreateGridMesh($"{namePrefix}_SurfaceHighlight_Z{z}", surfaceHVerts, surfaceHIndices, _highlightMaterial, out surfaceHighlightMesh);
      surfaceHighlightMesh.transform.SetParent(_terrainGridRoot.transform);

      CreateGridMesh($"{namePrefix}_Slice_Z{z}", sliceVerts, sliceIndices, mat, out sliceMesh);
      sliceMesh.transform.SetParent(_terrainGridRoot.transform);

      CreateGridMesh($"{namePrefix}_SliceHighlight_Z{z}", sliceHVerts, sliceHIndices, _highlightMaterial, out sliceHighlightMesh);
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

      for (int z = 0; z < _mapMaxZ; z++)
      {
        if (_terrainSurfaceMeshes[z] != null) _terrainSurfaceMeshes[z].SetActive(showTerrain && z < maxV);
        if (_terrainSurfaceHighlightMeshes[z] != null) _terrainSurfaceHighlightMeshes[z].SetActive(showTerrain && z < maxV);

        if (_terrainSliceMeshes[z] != null) _terrainSliceMeshes[z].SetActive(showTerrain && z == maxV);
        if (_terrainSliceHighlightMeshes[z] != null) _terrainSliceHighlightMeshes[z].SetActive(showTerrain && z == maxV);

        if (_buildingSurfaceMeshes[z] != null) _buildingSurfaceMeshes[z].SetActive(showBuilding && z < maxV);
        if (_buildingSurfaceHighlightMeshes[z] != null) _buildingSurfaceHighlightMeshes[z].SetActive(showBuilding && z < maxV);

        if (_buildingSliceMeshes[z] != null) _buildingSliceMeshes[z].SetActive(showBuilding && z == maxV);
        if (_buildingSliceHighlightMeshes[z] != null) _buildingSliceHighlightMeshes[z].SetActive(showBuilding && z == maxV);
      }
    }

    private void ProcessDirtyLevels()
    {
      if (_dirtyLevels.Count == 0 || _terrainSurfaceMeshes == null) return;

      bool rebuildBedrock = _dirtyLevels.Contains(0);

      foreach (int z in _dirtyLevels)
      {
        DestroyMeshObject(ref _terrainSurfaceMeshes[z]);
        DestroyMeshObject(ref _terrainSurfaceHighlightMeshes[z]);
        DestroyMeshObject(ref _terrainSliceMeshes[z]);
        DestroyMeshObject(ref _terrainSliceHighlightMeshes[z]);

        DestroyMeshObject(ref _buildingSurfaceMeshes[z]);
        DestroyMeshObject(ref _buildingSurfaceHighlightMeshes[z]);
        DestroyMeshObject(ref _buildingSliceMeshes[z]);
        DestroyMeshObject(ref _buildingSliceHighlightMeshes[z]);

        BuildLevelMeshes(z);
      }

      if (rebuildBedrock)
      {
        DestroyMeshObject(ref _bedrockMesh);
        DestroyMeshObject(ref _bedrockHighlightMesh);
        BuildBedrockMesh();
      }

      _dirtyLevels.Clear();
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