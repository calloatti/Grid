using System;
using System.Collections.Generic;
using Timberborn.Coordinates;
using UnityEngine;

namespace Calloatti.Grid
{
  public partial class GridRenderer
  {
    private const float SliceBaseHeight = 0.85f;
    private const float SurfaceBaseHeight = 1.00f;

    // Locally cached settings, updated whenever the grid is toggled or generated
    private GridSettings _settings = new GridSettings();

    private GameObject _terrainGridRoot;
    private GameObject[] _surfaceMeshes;
    private GameObject[] _sliceMeshes;

    private bool[,,] _isSolidCache;
    private int _mapSizeX;
    private int _mapSizeY;
    private int _mapMaxZ;

    private HashSet<int> _dirtyLevels = new HashSet<int>();

    public void ToggleTerrainGrid()
    {
      bool isCurrentlyEnabled = _terrainGridRoot != null && _terrainGridRoot.activeSelf;

      if (isCurrentlyEnabled)
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
        // Reload settings so players can tweak the JSON and see changes instantly
        _settings = ReadConfigFile();
        _terrainGridRoot.SetActive(true);
        ProcessDirtyLevels();
        UpdateVisibleLevels();
      }
    }

    private void UpdateTerrainCache()
    {
      Vector3Int size = _terrainService.Size;
      _mapSizeX = size.x;
      _mapSizeY = size.y;
      _mapMaxZ = size.z;

      _isSolidCache = new bool[_mapSizeX, _mapSizeY, _mapMaxZ];

      for (int x = 0; x < _mapSizeX; x++)
      {
        for (int y = 0; y < _mapSizeY; y++)
        {
          for (int z = 0; z < _mapMaxZ; z++)
          {
            _isSolidCache[x, y, z] = _terrainService.Underground(new Vector3Int(x, y, z));
          }
        }
      }
    }

    private bool IsSolid(int x, int y, int z)
    {
      if (x < 0 || x >= _mapSizeX || y < 0 || y >= _mapSizeY || z < 0 || z >= _mapMaxZ) return false;
      return _isSolidCache[x, y, z];
    }

    private Vector3 GetWorldPos(float x, float y, float height)
    {
      Vector3 pos = CoordinateSystem.GridToWorld(new Vector3(x, y, 0));
      pos.y = height;
      return pos;
    }

    private Vector3 GetOffsetVertex(int vx, int vy, int z, float height)
    {
      bool q1 = IsSolid(vx, vy, z);
      bool q2 = IsSolid(vx - 1, vy, z);
      bool q3 = IsSolid(vx - 1, vy - 1, z);
      bool q4 = IsSolid(vx, vy - 1, z);

      int right = (q1 ? 1 : 0) + (q4 ? 1 : 0);
      int left = (q2 ? 1 : 0) + (q3 ? 1 : 0);
      int top = (q1 ? 1 : 0) + (q2 ? 1 : 0);
      int bottom = (q3 ? 1 : 0) + (q4 ? 1 : 0);

      float dx = 0f;
      if (left > right) dx = _settings.HorizontalOffsetEW;
      else if (right > left) dx = -_settings.HorizontalOffsetEW;

      float dy = 0f;
      if (bottom > top) dy = _settings.HorizontalOffsetNS;
      else if (top > bottom) dy = -_settings.HorizontalOffsetNS;

      return GetWorldPos(vx + dx, vy + dy, height);
    }

    public void GenerateFullTerrainGrid()
    {
      if (_terrainGridRoot != null) { UnityEngine.Object.Destroy(_terrainGridRoot); }

      _terrainGridRoot = new GameObject("TerrainGridRoot");

      // Request data from Main, creating the file if it didn't exist
      _settings = ReadConfigFile();
      UpdateTerrainCache();

      _surfaceMeshes = new GameObject[_mapMaxZ];
      _sliceMeshes = new GameObject[_mapMaxZ];

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
      float h = _settings.NormalVerticalOffset;

      for (int y = 0; y <= _mapSizeY; y++)
      {
        int start = verts.Count;
        verts.Add(GetWorldPos(0, y, h));
        verts.Add(GetWorldPos(_mapSizeX, y, h));
        indices.Add(start);
        indices.Add(start + 1);
      }

      for (int x = 0; x <= _mapSizeX; x++)
      {
        int start = verts.Count;
        verts.Add(GetWorldPos(x, 0, h));
        verts.Add(GetWorldPos(x, _mapSizeY, h));
        indices.Add(start);
        indices.Add(start + 1);
      }

      GameObject bedrockObj;
      CreateGridMesh("BedrockGrid", verts, indices, out bedrockObj);
      bedrockObj.transform.SetParent(_terrainGridRoot.transform);
    }

    private void BuildLevelMeshes(int z)
    {
      List<Vector3> surfaceVerts = new List<Vector3>();
      List<int> surfaceIndices = new List<int>();

      List<Vector3> sliceVerts = new List<Vector3>();
      List<int> sliceIndices = new List<int>();

      float surfaceHeight = z + SurfaceBaseHeight + _settings.NormalVerticalOffset;
      float sliceHeight = z + SliceBaseHeight + _settings.SlicedVerticalOffset;

      float hBotNormal = z + _settings.NormalVerticalOffset;
      float hBotSliced = z + _settings.SlicedVerticalOffset;
      float hTopNormal = z + SurfaceBaseHeight + _settings.NormalVerticalOffset;

      void AddLine(Vector3 a, Vector3 b, List<Vector3> v, List<int> i)
      {
        int start = v.Count;
        v.Add(a); v.Add(b);
        i.Add(start); i.Add(start + 1);
      }

      for (int x = 0; x < _mapSizeX; x++)
      {
        for (int y = 0; y < _mapSizeY; y++)
        {
          if (!IsSolid(x, y, z)) continue;

          AddCellLines(x, y, z, sliceHeight, sliceVerts, sliceIndices, false);
          if (!IsSolid(x, y, z + 1)) AddCellLines(x, y, z, surfaceHeight, surfaceVerts, surfaceIndices, true);

          bool hasAirAbove = !IsSolid(x, y, z + 1);

          if (!IsSolid(x, y - 1, z))
          {
            AddLine(GetOffsetVertex(x, y, z, hBotNormal), GetOffsetVertex(x + 1, y, z, hBotNormal), surfaceVerts, surfaceIndices);
            AddLine(GetOffsetVertex(x, y, z, hBotSliced), GetOffsetVertex(x + 1, y, z, hBotSliced), sliceVerts, sliceIndices);
            if (!hasAirAbove) AddLine(GetOffsetVertex(x, y, z, hTopNormal), GetOffsetVertex(x + 1, y, z, hTopNormal), surfaceVerts, surfaceIndices);
          }

          if (!IsSolid(x + 1, y, z))
          {
            AddLine(GetOffsetVertex(x + 1, y, z, hBotNormal), GetOffsetVertex(x + 1, y + 1, z, hBotNormal), surfaceVerts, surfaceIndices);
            AddLine(GetOffsetVertex(x + 1, y, z, hBotSliced), GetOffsetVertex(x + 1, y + 1, z, hBotSliced), sliceVerts, sliceIndices);
            if (!hasAirAbove) AddLine(GetOffsetVertex(x + 1, y, z, hTopNormal), GetOffsetVertex(x + 1, y + 1, z, hTopNormal), surfaceVerts, surfaceIndices);
          }

          if (!IsSolid(x, y + 1, z))
          {
            AddLine(GetOffsetVertex(x + 1, y + 1, z, hBotNormal), GetOffsetVertex(x, y + 1, z, hBotNormal), surfaceVerts, surfaceIndices);
            AddLine(GetOffsetVertex(x + 1, y + 1, z, hBotSliced), GetOffsetVertex(x, y + 1, z, hBotSliced), sliceVerts, sliceIndices);
            if (!hasAirAbove) AddLine(GetOffsetVertex(x + 1, y + 1, z, hTopNormal), GetOffsetVertex(x, y + 1, z, hTopNormal), surfaceVerts, surfaceIndices);
          }

          if (!IsSolid(x - 1, y, z))
          {
            AddLine(GetOffsetVertex(x, y + 1, z, hBotNormal), GetOffsetVertex(x, y, z, hBotNormal), surfaceVerts, surfaceIndices);
            AddLine(GetOffsetVertex(x, y + 1, z, hBotSliced), GetOffsetVertex(x, y, z, hBotSliced), sliceVerts, sliceIndices);
            if (!hasAirAbove) AddLine(GetOffsetVertex(x, y + 1, z, hTopNormal), GetOffsetVertex(x, y, z, hTopNormal), surfaceVerts, surfaceIndices);
          }
        }
      }

      for (int vx = 0; vx <= _mapSizeX; vx++)
      {
        for (int vy = 0; vy <= _mapSizeY; vy++)
        {
          bool q1 = IsSolid(vx, vy, z);
          bool q2 = IsSolid(vx - 1, vy, z);
          bool q3 = IsSolid(vx - 1, vy - 1, z);
          bool q4 = IsSolid(vx, vy - 1, z);

          int solidCount = (q1 ? 1 : 0) + (q2 ? 1 : 0) + (q3 ? 1 : 0) + (q4 ? 1 : 0);

          if (solidCount == 0 || solidCount == 4) continue;

          Vector3 pBotNormal = GetOffsetVertex(vx, vy, z, hBotNormal);
          Vector3 pTopNormal = GetOffsetVertex(vx, vy, z, hTopNormal);

          Vector3 pBotSliced = GetOffsetVertex(vx, vy, z, hBotSliced);
          Vector3 pTopSliced = GetOffsetVertex(vx, vy, z, sliceHeight);

          AddLine(pBotNormal, pTopNormal, surfaceVerts, surfaceIndices);
          AddLine(pBotSliced, pTopSliced, sliceVerts, sliceIndices);
        }
      }

      CreateGridMesh($"Surface_Z{z}", surfaceVerts, surfaceIndices, out _surfaceMeshes[z]);
      _surfaceMeshes[z].transform.SetParent(_terrainGridRoot.transform);

      CreateGridMesh($"Slice_Z{z}", sliceVerts, sliceIndices, out _sliceMeshes[z]);
      _sliceMeshes[z].transform.SetParent(_terrainGridRoot.transform);
    }

    private void AddCellLines(int x, int y, int z, float h, List<Vector3> verts, List<int> indices, bool isSurfaceMesh)
    {
      void AddLine(Vector3 a, Vector3 b)
      {
        int start = verts.Count;
        verts.Add(a); verts.Add(b);
        indices.Add(start); indices.Add(start + 1);
      }

      bool IsSameGroup(int nx, int ny)
      {
        if (!IsSolid(nx, ny, z)) return false;
        if (isSurfaceMesh && IsSolid(nx, ny, z + 1)) return false;
        return true;
      }

      AddLine(GetWorldPos(x, y, h), GetWorldPos(x + 1, y, h));
      AddLine(GetWorldPos(x, y, h), GetWorldPos(x, y + 1, h));

      if (x == _mapSizeX - 1 || !IsSameGroup(x + 1, y))
      {
        AddLine(GetWorldPos(x + 1, y, h), GetWorldPos(x + 1, y + 1, h));
      }

      if (y == _mapSizeY - 1 || !IsSameGroup(x, y + 1))
      {
        AddLine(GetWorldPos(x, y + 1, h), GetWorldPos(x + 1, y + 1, h));
      }
    }

    public void UpdateVisibleLevels()
    {
      if (_terrainGridRoot == null || !_terrainGridRoot.activeSelf) return;

      int maxV = _levelVisibilityService.MaxVisibleLevel;

      for (int z = 0; z < _mapMaxZ; z++)
      {
        if (_surfaceMeshes[z] != null) _surfaceMeshes[z].SetActive(z < maxV);
        if (_sliceMeshes[z] != null) _sliceMeshes[z].SetActive(z == maxV);
      }
    }

    private void OnTerrainHeightChanged(object sender, Timberborn.TerrainSystem.TerrainHeightChangeEventArgs e)
    {
      if (_isSolidCache == null) return;

      Timberborn.TerrainSystem.TerrainHeightChange change = e.Change;
      int x = change.Coordinates.x;
      int y = change.Coordinates.y;

      for (int z = 0; z < _mapMaxZ; z++)
      {
        _isSolidCache[x, y, z] = _terrainService.Underground(new Vector3Int(x, y, z));
      }

      int minZ = Math.Max(0, change.From - 1);
      int maxZ = Math.Min(_mapMaxZ - 1, change.To);

      for (int z = minZ; z <= maxZ; z++)
      {
        _dirtyLevels.Add(z);
      }
    }

    private void ProcessDirtyLevels()
    {
      if (_dirtyLevels.Count == 0 || _surfaceMeshes == null) return;

      foreach (int z in _dirtyLevels)
      {
        if (_surfaceMeshes[z] != null) UnityEngine.Object.Destroy(_surfaceMeshes[z]);
        if (_sliceMeshes[z] != null) UnityEngine.Object.Destroy(_sliceMeshes[z]);
        BuildLevelMeshes(z);
      }

      _dirtyLevels.Clear();
      UpdateVisibleLevels();
    }
  }
}