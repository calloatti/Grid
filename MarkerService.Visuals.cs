using System.Collections.Generic;
using Timberborn.Coordinates;
using UnityEngine;

namespace Calloatti.Grid
{
  public partial class MarkerService
  {
    private Material[] _paletteMaterials;
    private Mesh _sharedCrossMesh;
    private bool _markersVisible = true;

    private const float Thickness = 0.12f;
    private const float DiagonalLength = 0.65f;
    private const float HeightOffset = 0.05f;
    private const float SliceBaseHeight = 0.85f;
    private const float SurfaceBaseHeight = 1.00f;

    public void ToggleMarkers()
    {
      _markersVisible = !_markersVisible;

      foreach (var data in _activeMarkers.Values)
      {
        if (data.Container != null) data.Container.SetActive(_markersVisible);
      }

      string status = _markersVisible ? "ON" : "OFF";
      _notificationService.SendNotification($"Markers: {status}");
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
      List<float> targetHeights = new List<float>();

      foreach (var surf in data.Surfaces)
      {
        if (maxV >= surf.RequiredMaxV) targetHeights.Add(surf.RoofZ + SurfaceBaseHeight + HeightOffset);
      }

      if (maxV >= 0 && maxV < _terrainService.Size.z && _terrainService.Underground(new Vector3Int(col.x, col.y, maxV)))
      {
        float sliceHeight = maxV + SliceBaseHeight + HeightOffset;
        if (!targetHeights.Contains(sliceHeight)) targetHeights.Add(sliceHeight);
      }

      Material sharedMat = _paletteMaterials[data.ColorIndex];

      while (data.VisualPairs.Count < targetHeights.Count)
        data.VisualPairs.Add(CreateMarkerVisualObject(data.Container.transform, sharedMat));

      Vector3 worldPos = CoordinateSystem.GridToWorld(new Vector3(col.x + 0.5f, col.y + 0.5f, 0));

      for (int i = 0; i < data.VisualPairs.Count; i++)
      {
        if (i < targetHeights.Count)
        {
          data.VisualPairs[i].SetActive(true);
          data.VisualPairs[i].transform.position = new Vector3(worldPos.x, targetHeights[i], worldPos.z);
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

    private void OnDispose()
    {
      _terrainService.TerrainHeightChanged -= OnTerrainHeightChanged;
      RemoveAllMarkers();

      if (_paletteMaterials != null)
      {
        foreach (var mat in _paletteMaterials) if (mat != null) UnityEngine.Object.Destroy(mat);
      }
      if (_sharedCrossMesh != null) UnityEngine.Object.Destroy(_sharedCrossMesh);
    }
  }
}