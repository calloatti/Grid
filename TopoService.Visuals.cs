using System.Collections.Generic;
using UnityEngine;
using Timberborn.Coordinates;

namespace TimberbornModding.TopoData
{
  public partial class TopoService
  {
    private const int GridColumns = 32;
    private const int GridRows = 16;
    private const int TopoDataRow = 12;
    private const float HeightOffset = 0.05f;

    private GameObject _masterContainer;
    private GameObject[] _layerObjects;
    private MeshFilter[] _layerFilters;
    private MeshRenderer[] _layerRenderers;

    private void InitializeVisuals()
    {
      _masterContainer = new GameObject("TopoData_MasterContainer");
      int maxZ = _terrainService.Size.z;
      _layerObjects = new GameObject[maxZ];
      _layerFilters = new MeshFilter[maxZ];
      _layerRenderers = new MeshRenderer[maxZ];

      for (int z = 0; z < maxZ; z++)
      {
        GameObject layerObj = new GameObject($"TopoData_Layer_{z}");
        layerObj.transform.SetParent(_masterContainer.transform);
        _layerFilters[z] = layerObj.AddComponent<MeshFilter>();
        _layerRenderers[z] = layerObj.AddComponent<MeshRenderer>();
        _layerFilters[z].mesh = new Mesh();
        layerObj.SetActive(false);
        _layerObjects[z] = layerObj;
      }
    }

    private void OnDispose()
    {
      if (_masterContainer != null) UnityEngine.Object.Destroy(_masterContainer);
    }

    public void GenerateSnapshot()
    {
      int maxZ = _terrainService.Size.z;
      int sizeX = _terrainService.Size.x;
      int sizeY = _terrainService.Size.y;

      List<Vector3>[] verticesArray = new List<Vector3>[maxZ];
      List<int>[] trianglesArray = new List<int>[maxZ];
      List<Vector2>[] uvsArray = new List<Vector2>[maxZ];

      for (int z = 0; z < maxZ; z++)
      {
        verticesArray[z] = new List<Vector3>();
        trianglesArray[z] = new List<int>();
        uvsArray[z] = new List<Vector2>();
      }

      for (int y = 0; y < sizeY; y++)
      {
        for (int x = 0; x < sizeX; x++)
        {
          Vector2Int cell = new Vector2Int(x, y);

          foreach (Vector3Int heightCoords in _terrainService.GetAllHeightsInCell(cell))
          {
            int displayValue = heightCoords.z;
            int surfaceZ = displayValue - 1;

            int currentZ = surfaceZ;
            while (currentZ >= -1)
            {
              if (currentZ >= 0 && !_terrainService.Underground(new Vector3Int(x, y, currentZ)))
              {
                break;
              }

              int layerIndex = Mathf.Clamp(currentZ, 0, maxZ - 1);
              AddQuadToLayer(currentZ, x, y, verticesArray[layerIndex], trianglesArray[layerIndex], uvsArray[layerIndex], displayValue);

              if (currentZ == -1) break;

              currentZ--;
            }
          }
        }
      }

      for (int z = 0; z < maxZ; z++)
      {
        Mesh mesh = _layerFilters[z].mesh;
        mesh.Clear();
        if (verticesArray[z].Count == 0) continue;

        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verticesArray[z]);
        mesh.SetTriangles(trianglesArray[z], 0);
        mesh.SetUVs(0, uvsArray[z]);

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        if (_topoMaterial != null) _layerRenderers[z].sharedMaterial = _topoMaterial;
      }
    }

    private void AddQuadToLayer(int z, int x, int y, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, int displayValue)
    {
      int vIndex = vertices.Count;
      float unityHeight = z + 1.0f + HeightOffset;

      // MODIFICADO: Ahora el Quad mapea desde 0.0 hasta 1.0, cubriendo el 100% de la celda.
      Vector3 p0 = CoordinateSystem.GridToWorld(new Vector3(x, y, 0));
      Vector3 p1 = CoordinateSystem.GridToWorld(new Vector3(x + 1.0f, y, 0));
      Vector3 p2 = CoordinateSystem.GridToWorld(new Vector3(x, y + 1.0f, 0));
      Vector3 p3 = CoordinateSystem.GridToWorld(new Vector3(x + 1.0f, y + 1.0f, 0));

      vertices.Add(new Vector3(p0.x, unityHeight, p0.z));
      vertices.Add(new Vector3(p1.x, unityHeight, p1.z));
      vertices.Add(new Vector3(p2.x, unityHeight, p2.z));
      vertices.Add(new Vector3(p3.x, unityHeight, p3.z));

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

      for (int z = 0; z < _layerObjects.Length; z++)
      {
        if (_layerObjects[z] != null)
        {
          bool isVisible = _isActive && (z <= maxVisibleLevel);
          _layerObjects[z].SetActive(isVisible);
        }
      }
    }

    private void HideAll()
    {
      for (int z = 0; z < _layerObjects.Length; z++)
        if (_layerObjects[z] != null) _layerObjects[z].SetActive(false);
    }
  }
}