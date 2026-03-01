using System.Collections.Generic;
using Timberborn.Coordinates;
using UnityEngine;

namespace Calloatti.Grid
{
  public partial class MarkerService
  {
    private Material _markerMaterial;
    private const float Thickness = 0.12f;
    private const float DiagonalLength = 0.65f;
    private const float HeightOffset = 0.05f;
    private const float SliceBaseHeight = 0.85f;
    private const float SurfaceBaseHeight = 1.00f;

    private List<Color> _palette;
    public List<Color> Palette => _palette;

    private void AddMarker(Vector2Int col, int colorIndex)
    {
      InitializeMaterial();
      MarkerData data = new MarkerData
      {
        Container = new GameObject($"Marker_{col.x}_{col.y}"),
        ColorIndex = colorIndex,
        VisualPairs = new List<GameObject>(),
        Surfaces = new List<SurfaceData>()
      };

      _activeMarkers[col] = data;
      RecalculateColumnCache(col);
      UpdateMarkerVisuals(col);
    }

    private void UpdateMarkerVisuals(Vector2Int col)
    {
      if (!_activeMarkers.TryGetValue(col, out MarkerData data)) return;

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

      Color color = _palette[data.ColorIndex];
      while (data.VisualPairs.Count < targetHeights.Count)
        data.VisualPairs.Add(CreateMarkerVisualPair(data.Container.transform, color));

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
        data.ColorIndex = (data.ColorIndex + 1) % _palette.Count;
        Color newColor = _palette[data.ColorIndex];
        foreach (var pair in data.VisualPairs)
          foreach (MeshRenderer mr in pair.GetComponentsInChildren<MeshRenderer>())
            mr.material.color = newColor;
      }
    }

    public void RemoveColumn(Vector2Int col)
    {
      if (_activeMarkers.TryGetValue(col, out MarkerData data))
      {
        Object.Destroy(data.Container);
        _activeMarkers.Remove(col);
      }
    }

    public void RemoveAllMarkers()
    {
      foreach (var data in _activeMarkers.Values)
        if (data.Container != null) Object.Destroy(data.Container);
      _activeMarkers.Clear();
    }

    private GameObject CreateMarkerVisualPair(Transform parent, Color color)
    {
      GameObject pair = new GameObject("MarkerPair");
      pair.transform.SetParent(parent, false);
      CreateQuad(45f, color).transform.SetParent(pair.transform, false);
      CreateQuad(-45f, color).transform.SetParent(pair.transform, false);
      return pair;
    }

    private GameObject CreateQuad(float rotY, Color color)
    {
      GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
      Object.Destroy(q.GetComponent<Collider>());
      MeshRenderer mr = q.GetComponent<MeshRenderer>();
      mr.material = new Material(_markerMaterial);
      mr.material.color = color;
      q.transform.localScale = new Vector3(Thickness, DiagonalLength, 1f);
      q.transform.localRotation = Quaternion.Euler(90, rotY, 0);
      return q;
    }

    private void InitializeMaterial()
    {
      if (_markerMaterial == null) _markerMaterial = new Material(Shader.Find("Sprites/Default"));
    }
  }
}