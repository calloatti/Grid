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
    private GameObject[] _terrainSurfaceMeshes;
    private GameObject[] _terrainSliceMeshes;
    private GameObject[] _buildingSurfaceMeshes;
    private GameObject[] _buildingSliceMeshes;

    private bool[,,] _isTerrainCache;
    private bool[,,] _isBuildingCache;

    private int _mapSizeX;
    private int _mapSizeY;
    private int _mapMaxZ;

    private HashSet<int> _dirtyLevels = new HashSet<int>();

    private Material _terrainMaterial;
    private Material _buildingMaterial;

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
      if (_terrainGridRoot != null) { UnityEngine.Object.Destroy(_terrainGridRoot); }
      _terrainGridRoot = new GameObject("TerrainGridRoot");

      EnsureSettingsLoaded();
      InitializeMaterials();
      UpdateTerrainCache();

      _terrainSurfaceMeshes = new GameObject[_mapMaxZ];
      _terrainSliceMeshes = new GameObject[_mapMaxZ];
      _buildingSurfaceMeshes = new GameObject[_mapMaxZ];
      _buildingSliceMeshes = new GameObject[_mapMaxZ];

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
      float h = Settings.NormalVerticalOffset;

      for (int y = 0; y <= _mapSizeY; y++)
      {
        int start = verts.Count;
        verts.Add(GetWorldPos(0, y, h));
        verts.Add(GetWorldPos(_mapSizeX, y, h));
        indices.Add(start); indices.Add(start + 1);
      }

      for (int x = 0; x <= _mapSizeX; x++)
      {
        int start = verts.Count;
        verts.Add(GetWorldPos(x, 0, h));
        verts.Add(GetWorldPos(x, _mapSizeY, h));
        indices.Add(start); indices.Add(start + 1);
      }

      CreateGridMesh("BedrockGrid", verts, indices, _terrainMaterial, out _bedrockMesh);
      _bedrockMesh.transform.SetParent(_terrainGridRoot.transform);
    }

    private void BuildLevelMeshes(int z)
    {
      BuildGridLevelMeshes(z, _isTerrainCache, "Terrain", _terrainMaterial, out _terrainSurfaceMeshes[z], out _terrainSliceMeshes[z]);
      BuildGridLevelMeshes(z, _isBuildingCache, "Building", _buildingMaterial, out _buildingSurfaceMeshes[z], out _buildingSliceMeshes[z]);
    }

    private void BuildGridLevelMeshes(int z, bool[,,] cache, string namePrefix, Material mat, out GameObject surfaceMesh, out GameObject sliceMesh)
    {
      List<Vector3> surfaceVerts = new List<Vector3>();
      List<int> surfaceIndices = new List<int>();
      List<Vector3> sliceVerts = new List<Vector3>();
      List<int> sliceIndices = new List<int>();

      float surfaceHeight = z + SurfaceBaseHeight + Settings.NormalVerticalOffset;
      float sliceHeight = z + SliceBaseHeight + Settings.SlicedVerticalOffset;
      float hBotNormal = z + Settings.NormalVerticalOffset;
      float hBotSliced = z + Settings.SlicedVerticalOffset;
      float hTopNormal = z + SurfaceBaseHeight + Settings.NormalVerticalOffset;

      void AddLine(Vector3 a, Vector3 b, List<Vector3> v, List<int> i)
      {
        int start = v.Count;
        v.Add(a); v.Add(b);
        i.Add(start); i.Add(start + 1);
      }

      void AddCellLines(int x, int y, int zLvl, float h, List<Vector3> v, List<int> i, bool isSurfaceMesh)
      {
        bool IsSameGroup(int nx, int ny)
        {
          if (!IsSolid(nx, ny, zLvl, cache)) return false;
          if (isSurfaceMesh && IsSolid(nx, ny, zLvl + 1, cache)) return false;
          return true;
        }

        AddLine(GetWorldPos(x, y, h), GetWorldPos(x + 1, y, h), v, i);
        AddLine(GetWorldPos(x, y, h), GetWorldPos(x, y + 1, h), v, i);
        if (x == _mapSizeX - 1 || !IsSameGroup(x + 1, y)) AddLine(GetWorldPos(x + 1, y, h), GetWorldPos(x + 1, y + 1, h), v, i);
        if (y == _mapSizeY - 1 || !IsSameGroup(x, y + 1)) AddLine(GetWorldPos(x, y + 1, h), GetWorldPos(x + 1, y + 1, h), v, i);
      }

      for (int x = 0; x < _mapSizeX; x++)
      {
        for (int y = 0; y < _mapSizeY; y++)
        {
          if (!IsSolid(x, y, z, cache)) continue;

          AddCellLines(x, y, z, sliceHeight, sliceVerts, sliceIndices, false);
          if (!IsSolid(x, y, z + 1, cache)) AddCellLines(x, y, z, surfaceHeight, surfaceVerts, surfaceIndices, true);

          bool hasAirAbove = !IsSolid(x, y, z + 1, cache);

          if (!IsSolid(x, y - 1, z, cache))
          {
            AddLine(GetOffsetVertex(x, y, z, hBotNormal, cache), GetOffsetVertex(x + 1, y, z, hBotNormal, cache), surfaceVerts, surfaceIndices);
            AddLine(GetOffsetVertex(x, y, z, hBotSliced, cache), GetOffsetVertex(x + 1, y, z, hBotSliced, cache), sliceVerts, sliceIndices);
            if (!hasAirAbove) AddLine(GetOffsetVertex(x, y, z, hTopNormal, cache), GetOffsetVertex(x + 1, y, z, hTopNormal, cache), surfaceVerts, surfaceIndices);
          }

          if (!IsSolid(x + 1, y, z, cache))
          {
            AddLine(GetOffsetVertex(x + 1, y, z, hBotNormal, cache), GetOffsetVertex(x + 1, y + 1, z, hBotNormal, cache), surfaceVerts, surfaceIndices);
            AddLine(GetOffsetVertex(x + 1, y, z, hBotSliced, cache), GetOffsetVertex(x + 1, y + 1, z, hBotSliced, cache), sliceVerts, sliceIndices);
            if (!hasAirAbove) AddLine(GetOffsetVertex(x + 1, y + 1, z, hTopNormal, cache), GetOffsetVertex(x + 1, y + 1, z, hTopNormal, cache), surfaceVerts, surfaceIndices);
          }

          if (!IsSolid(x, y + 1, z, cache))
          {
            AddLine(GetOffsetVertex(x + 1, y + 1, z, hBotNormal, cache), GetOffsetVertex(x, y + 1, z, hBotNormal, cache), surfaceVerts, surfaceIndices);
            AddLine(GetOffsetVertex(x + 1, y + 1, z, hBotSliced, cache), GetOffsetVertex(x, y + 1, z, hBotSliced, cache), sliceVerts, sliceIndices);
            if (!hasAirAbove) AddLine(GetOffsetVertex(x + 1, y + 1, z, hTopNormal, cache), GetOffsetVertex(x, y + 1, z, hTopNormal, cache), surfaceVerts, surfaceIndices);
          }

          if (!IsSolid(x - 1, y, z, cache))
          {
            AddLine(GetOffsetVertex(x, y + 1, z, hBotNormal, cache), GetOffsetVertex(x, y, z, hBotNormal, cache), surfaceVerts, surfaceIndices);
            AddLine(GetOffsetVertex(x, y + 1, z, hBotSliced, cache), GetOffsetVertex(x, y, z, hBotSliced, cache), sliceVerts, sliceIndices);
            if (!hasAirAbove) AddLine(GetOffsetVertex(x, y + 1, z, hTopNormal, cache), GetOffsetVertex(x, y, z, hTopNormal, cache), surfaceVerts, surfaceIndices);
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
          AddLine(GetOffsetVertex(vx, vy, z, hBotNormal, cache), GetOffsetVertex(vx, vy, z, hTopNormal, cache), surfaceVerts, surfaceIndices);
          AddLine(GetOffsetVertex(vx, vy, z, hBotSliced, cache), GetOffsetVertex(vx, vy, z, sliceHeight, cache), sliceVerts, sliceIndices);
        }
      }

      CreateGridMesh($"{namePrefix}_Surface_Z{z}", surfaceVerts, surfaceIndices, mat, out surfaceMesh);
      surfaceMesh.transform.SetParent(_terrainGridRoot.transform);
      CreateGridMesh($"{namePrefix}_Slice_Z{z}", sliceVerts, sliceIndices, mat, out sliceMesh);
      sliceMesh.transform.SetParent(_terrainGridRoot.transform);
    }

    public void UpdateVisibleLevels()
    {
      if (_terrainGridRoot == null || !_terrainGridRoot.activeSelf) return;

      bool showTerrain = _gridState == 1 || _gridState == 2;
      bool showBuilding = _gridState == 1 || _gridState == 3;

      if (_bedrockMesh != null)
      {
        _bedrockMesh.SetActive(showTerrain);
      }

      int maxV = _levelVisibilityService.MaxVisibleLevel;

      for (int z = 0; z < _mapMaxZ; z++)
      {
        if (_terrainSurfaceMeshes[z] != null) _terrainSurfaceMeshes[z].SetActive(showTerrain && z < maxV);
        if (_terrainSliceMeshes[z] != null) _terrainSliceMeshes[z].SetActive(showTerrain && z == maxV);

        if (_buildingSurfaceMeshes[z] != null) _buildingSurfaceMeshes[z].SetActive(showBuilding && z < maxV);
        if (_buildingSliceMeshes[z] != null) _buildingSliceMeshes[z].SetActive(showBuilding && z == maxV);
      }
    }

    private void ProcessDirtyLevels()
    {
      if (_dirtyLevels.Count == 0 || _terrainSurfaceMeshes == null) return;

      foreach (int z in _dirtyLevels)
      {
        if (_terrainSurfaceMeshes[z] != null) UnityEngine.Object.Destroy(_terrainSurfaceMeshes[z]);
        if (_terrainSliceMeshes[z] != null) UnityEngine.Object.Destroy(_terrainSliceMeshes[z]);

        if (_buildingSurfaceMeshes[z] != null) UnityEngine.Object.Destroy(_buildingSurfaceMeshes[z]);
        if (_buildingSliceMeshes[z] != null) UnityEngine.Object.Destroy(_buildingSliceMeshes[z]);

        BuildLevelMeshes(z);
      }

      _dirtyLevels.Clear();
      UpdateVisibleLevels();
    }
  }
}