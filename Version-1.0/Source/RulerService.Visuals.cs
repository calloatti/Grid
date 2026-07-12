using System.Linq;
using Timberborn.Coordinates;
using UnityEngine;

namespace Calloatti.Grid
{
  public partial class RulerService
  {
    private void UpdateRulersVisuals()
    {
      int maxV = _levelVisibilityService.MaxVisibleLevel;
      foreach (var r in _activeRulers) { foreach (var s in r.Segments) UpdateQuadHeight(s.Obj, s.Coords, maxV, r.RulerType, s.Value); }
      foreach (var pair in _sharedQuads) UpdateQuadHeight(pair.Value, pair.Key, maxV, 0, 0);
    }

    private void InternalSetupPreview()
    {
      if (_previewContainer == null)
      {
        _previewContainer = new GameObject("RulerPreviewContainer");
        _previewQuads = new GameObject[RULER_LENGTH];
        for (int i = 0; i < RULER_LENGTH; i++)
        {
          GameObject q = CreateRulerQuad(_previewContainer.transform, _lockedRotation);
          q.SetActive(false); _previewQuads[i] = q;
        }
      }
      foreach (var q in _previewQuads) q.transform.rotation = _lockedRotation;
      _previewContainer.SetActive(true);
    }

    private void InternalUpdatePreview(Vector3Int start, Vector3Int end)
    {
      Vector2Int step = GetStepDirection(start, end);
      int maxV = _levelVisibilityService.MaxVisibleLevel;
      for (int i = 0; i < RULER_LENGTH; i++)
      {
        GameObject q = _previewQuads[i]; Vector2Int tile = new Vector2Int(start.x + (step.x * i), start.y + (step.y * i));
        if (!_terrainService.Contains(tile)) { q.SetActive(false); continue; }
        q.SetActive(true);
        UpdateQuadHeight(q, tile, maxV, _drawingType, i + 1);
      }
    }

    private Vector2Int GetStepDirection(Vector3Int s, Vector3Int e)
    {
      int dx = e.x - s.x, dy = e.y - s.y;
      return Mathf.Abs(dx) >= Mathf.Abs(dy) ? new Vector2Int(dx >= 0 ? 1 : -1, 0) : new Vector2Int(0, dy >= 0 ? 1 : -1);
    }

    private Quaternion CalculateCameraRotation()
    {
      float snapped = Mathf.Round(_cameraService.HorizontalAngle / 90f) * 90f;
      return Quaternion.Euler(90, snapped, 0);
    }

    private void UpdateQuadHeight(GameObject q, Vector2Int c, int maxV, int rulerType, int logicalValue)
    {
      int mapZ = _terrainService.Size.z; float fY = -1f;

      if (maxV >= 0 && maxV < mapZ && _terrainService.Underground(new Vector3Int(c.x, c.y, maxV)))
      {
        fY = maxV + SliceBaseHeight + HeightOffset;
      }

      if (fY < 0f)
      {
        int hZ = -2;
        // Restored: Original bottom-up scan (0 to mapZ)
        for (int z = 0; z < mapZ; z++)
        {
          Vector3Int p = new Vector3Int(c.x, c.y, z);
          if (_terrainService.Underground(p) || _blockService.GetObjectsAt(p).Any(o => o.Solid))
          {
            if (maxV >= z + 1) hZ = z;
          }
        }
        if (hZ != -2) { fY = hZ + SurfaceBaseHeight + HeightOffset; }
      }

      if (fY < 0f)
      {
        int tZ = _terrainService.GetTerrainHeightBelow(new Vector3Int(c.x, c.y, mapZ - 1)) - 1;
        fY = (tZ < maxV) ? (tZ + SurfaceBaseHeight + HeightOffset) : (maxV + SliceBaseHeight + HeightOffset);
      }

      Vector3 wP = CoordinateSystem.GridToWorld(new Vector3(c.x + 0.5f, c.y + 0.5f, 0));
      q.transform.position = new Vector3(wP.x, fY, wP.z);

      AdjustSegmentUVs(q, logicalValue); // Timing restored exactly as original
    }

    private void AdjustSegmentUVs(GameObject go, int logicalValue)
    {
      Mesh mesh = go.GetComponent<MeshFilter>().mesh; Vector2[] uvs = new Vector2[4];
      int col = logicalValue % GRID_COLUMNS; int row = logicalValue / GRID_COLUMNS;
      float uStart = (float)col / GRID_COLUMNS; float uEnd = (float)(col + 1) / GRID_COLUMNS;
      float vTop = 1.0f - ((float)row / GRID_ROWS); float vBottom = 1.0f - ((float)(row + 1) / GRID_ROWS);
      uvs[0] = new Vector2(uStart, vBottom); uvs[1] = new Vector2(uEnd, vBottom); uvs[2] = new Vector2(uStart, vTop); uvs[3] = new Vector2(uEnd, vTop);
      mesh.uv = uvs;
    }
  }
}