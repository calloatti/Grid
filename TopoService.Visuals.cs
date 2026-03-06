using System.Collections.Generic;
using UnityEngine;
using Timberborn.Coordinates;
using Timberborn.TerrainSystem;

namespace Calloatti.TopoData
{
  public partial class TopoService
  {
    private const int GridColumns = 32;
    private const int GridRows = 16;
    private const int TopoDataRow = 12;
    private const float HeightOffset = 0.05f;

    private GameObject _masterContainer;

    // Zero-Allocation Cache Lists
    private List<Vector3>[] _vWalkable;
    private List<int>[] _tWalkable;
    private List<Vector2>[] _uWalkable;

    private List<Vector3>[] _vBuried;
    private List<int>[] _tBuried;
    private List<Vector2>[] _uBuried;

    private void InitializeVisuals()
    {
      _masterContainer = new GameObject("TopoData_MasterContainer");
      int maxZ = _terrainService.Size.z;

      // Initialize Zero-Allocation Lists once
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

      int chunksX = Mathf.CeilToInt(_terrainService.Size.x / 16f);
      int chunksY = Mathf.CeilToInt(_terrainService.Size.y / 16f);

      for (int y = 0; y < chunksY; y++)
      {
        for (int x = 0; x < chunksX; x++)
        {
          Vector2Int coord = new Vector2Int(x, y);
          _chunks[coord] = new TopoChunk(coord, maxZ, _masterContainer.transform, _topoMaterial);
        }
      }
    }

    private void OnDispose()
    {
      foreach (var chunk in _chunks.Values)
      {
        chunk.Destroy();
      }
      _chunks.Clear();

      if (_masterContainer != null)
      {
        UnityEngine.Object.Destroy(_masterContainer);
      }

      // ADD THIS LINE: Explicitly destroy the dynamically created material
      if (_topoMaterial != null)
      {
        UnityEngine.Object.Destroy(_topoMaterial);
      }
    }

    private Quaternion CalculateCameraRotation()
    {
      float snapped = Mathf.Round(_cameraService.HorizontalAngle / 90f) * 90f;
      return Quaternion.Euler(90, snapped, 0);
    }

    private void RotateExistingMeshes(Quaternion newRotation)
    {
      Quaternion deltaRot = newRotation * Quaternion.Inverse(_lastRotation);

      foreach (var chunk in _chunks.Values)
      {
        for (int z = 0; z < _terrainService.Size.z; z++)
        {
          // Rotate Walkable
          Mesh wMesh = chunk.GetWalkableFilter(z).mesh;
          if (wMesh != null && wMesh.vertexCount > 0)
          {
            Vector3[] vertices = wMesh.vertices;
            for (int i = 0; i < vertices.Length; i += 4)
            {
              Vector3 center = (vertices[i] + vertices[i + 3]) / 2f;
              vertices[i] = center + deltaRot * (vertices[i] - center);
              vertices[i + 1] = center + deltaRot * (vertices[i + 1] - center);
              vertices[i + 2] = center + deltaRot * (vertices[i + 2] - center);
              vertices[i + 3] = center + deltaRot * (vertices[i + 3] - center);
            }
            wMesh.vertices = vertices;
            wMesh.RecalculateBounds();
            wMesh.RecalculateNormals();
          }

          // Rotate Buried
          Mesh bMesh = chunk.GetBuriedFilter(z).mesh;
          if (bMesh != null && bMesh.vertexCount > 0)
          {
            Vector3[] vertices = bMesh.vertices;
            for (int i = 0; i < vertices.Length; i += 4)
            {
              Vector3 center = (vertices[i] + vertices[i + 3]) / 2f;
              vertices[i] = center + deltaRot * (vertices[i] - center);
              vertices[i + 1] = center + deltaRot * (vertices[i + 1] - center);
              vertices[i + 2] = center + deltaRot * (vertices[i + 2] - center);
              vertices[i + 3] = center + deltaRot * (vertices[i + 3] - center);
            }
            bMesh.vertices = vertices;
            bMesh.RecalculateBounds();
            bMesh.RecalculateNormals();
          }
        }
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

      // Clear the cached lists instead of allocating new ones
      for (int z = 0; z < maxZ; z++)
      {
        _vWalkable[z].Clear();
        _tWalkable[z].Clear();
        _uWalkable[z].Clear();

        _vBuried[z].Clear();
        _tBuried[z].Clear();
        _uBuried[z].Clear();
      }

      int startX = chunk.ChunkCoords.x * 16;
      int startY = chunk.ChunkCoords.y * 16;
      int endX = Mathf.Min(startX + 16, _terrainService.Size.x);
      int endY = Mathf.Min(startY + 16, _terrainService.Size.y);

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

              // Split logic to Walkable or Buried lists
              if (isWalkable)
              {
                AddQuadToArrays(currentZ, x, y, _vWalkable[layerIndex], _tWalkable[layerIndex], _uWalkable[layerIndex], displayValue, rot);
              }
              else
              {
                AddQuadToArrays(currentZ, x, y, _vBuried[layerIndex], _tBuried[layerIndex], _uBuried[layerIndex], displayValue, rot);
              }

              if (currentZ == -1) break;
              currentZ--;
            }
          }
        }
      }

      for (int z = 0; z < maxZ; z++)
      {
        // Apply Walkable Data
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

        // Apply Buried Data
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

    private void AddQuadToArrays(int z, int x, int y, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, int displayValue, Quaternion rotation)
    {
      int vIndex = vertices.Count;
      float unityHeight = z + 1.0f + HeightOffset;
      Vector3 worldPos = CoordinateSystem.GridToWorld(new Vector3(x + 0.5f, y + 0.5f, 0));
      worldPos.y = unityHeight;

      Vector3 localP0 = new Vector3(-0.5f, -0.5f, 0);
      Vector3 localP1 = new Vector3(0.5f, -0.5f, 0);
      Vector3 localP2 = new Vector3(-0.5f, 0.5f, 0);
      Vector3 localP3 = new Vector3(0.5f, 0.5f, 0);

      vertices.Add(worldPos + (rotation * localP0));
      vertices.Add(worldPos + (rotation * localP1));
      vertices.Add(worldPos + (rotation * localP2));
      vertices.Add(worldPos + (rotation * localP3));

      int spriteIndex = Mathf.Clamp(displayValue, 0, 31);
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
  }
}