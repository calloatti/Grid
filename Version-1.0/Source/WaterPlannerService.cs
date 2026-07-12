using Bindito.Core;
using System.Collections.Generic;
using Timberborn.Coordinates;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  public class WaterPlannerService : IPostLoadableSingleton, System.IDisposable
  {
    private readonly Dictionary<Vector2Int, GameObject> _plannedWaterTiles = new Dictionary<Vector2Int, GameObject>();
    private readonly List<GameObject> _previewQuads = new List<GameObject>();

    private Material _waterMat;
    private Material _previewMat;
    private Mesh _quadMesh;

    public void PostLoad()
    {
      _waterMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.2f, 0.4f, 0.8f, 0.9f) };
      _previewMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.4f, 0.6f, 1.0f, 0.4f) };

      GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
      _quadMesh = temp.GetComponent<MeshFilter>().sharedMesh;
      Object.Destroy(temp);
    }

    public void Dispose()
    {
      if (_waterMat != null) Object.Destroy(_waterMat);
      if (_previewMat != null) Object.Destroy(_previewMat);
      if (_quadMesh != null) Object.Destroy(_quadMesh);
      DeleteAllWaterPlans();
      ClearPreview();
    }

    public bool HasWaterAt(Vector2Int coord) => _plannedWaterTiles.ContainsKey(coord);

    public void AddWater(IEnumerable<Vector3Int> blocks)
    {
      foreach (var block in blocks)
      {
        Vector2Int c2d = new Vector2Int(block.x, block.y);
        if (!_plannedWaterTiles.ContainsKey(c2d))
        {
          _plannedWaterTiles[c2d] = CreateQuad(block, _waterMat);
        }
      }
    }

    public void RemoveWater(IEnumerable<Vector3Int> blocks)
    {
      foreach (var block in blocks)
      {
        Vector2Int c2d = new Vector2Int(block.x, block.y);
        if (_plannedWaterTiles.TryGetValue(c2d, out GameObject quad))
        {
          Object.Destroy(quad);
          _plannedWaterTiles.Remove(c2d);
        }
      }
    }

    public void DeleteAllWaterPlans()
    {
      foreach (var quad in _plannedWaterTiles.Values) Object.Destroy(quad);
      _plannedWaterTiles.Clear();
    }

    public void ShowPreview(IEnumerable<Vector3Int> blocks)
    {
      ClearPreview();
      foreach (var block in blocks) _previewQuads.Add(CreateQuad(block, _previewMat));
    }

    public void ClearPreview()
    {
      foreach (var quad in _previewQuads) Object.Destroy(quad);
      _previewQuads.Clear();
    }

    private GameObject CreateQuad(Vector3Int coords, Material mat)
    {
      GameObject quad = new GameObject("WaterQuad");
      quad.AddComponent<MeshFilter>().sharedMesh = _quadMesh;
      quad.AddComponent<MeshRenderer>().sharedMaterial = mat;

      Vector3 worldPos = CoordinateSystem.GridToWorld(new Vector3(coords.x + 0.5f, coords.y + 0.5f, 0));
      quad.transform.position = new Vector3(worldPos.x, coords.z + 1.05f, worldPos.z);
      quad.transform.rotation = Quaternion.Euler(90, 0, 0);

      return quad;
    }
  }
}