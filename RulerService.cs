using Bindito.Core;
using System.Collections.Generic;
using Timberborn.AssetSystem;
using Timberborn.BlockSystem;
using Timberborn.CameraSystem;
using Timberborn.Coordinates;
using Timberborn.LevelVisibilitySystem;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.WorldPersistence;
using UnityEngine;
using System.Linq;

namespace Calloatti.Grid
{
  public partial class RulerService : ILoadableSingleton, IPostLoadableSingleton, ISaveableSingleton
  {
    private readonly IAssetLoader _assetLoader;
    private readonly ITerrainService _terrainService;
    private readonly IBlockService _blockService;
    private readonly ILevelVisibilityService _levelVisibilityService;
    private readonly CameraService _cameraService;
    private readonly EventBus _eventBus;
    private readonly ISingletonLoader _singletonLoader;

    private Material _rulerMaterial;
    private GameObject _previewContainer;
    private GameObject[] _previewQuads;
    private Quaternion _lockedRotation;

    private readonly List<RulerInstance> _activeRulers = new List<RulerInstance>();

    private readonly Dictionary<Vector2Int, List<RulerSegment>> _segmentMap = new Dictionary<Vector2Int, List<RulerSegment>>();
    private readonly Dictionary<Vector2Int, GameObject> _sharedQuads = new Dictionary<Vector2Int, GameObject>();

    private bool _isDrawing = false;
    private Vector3Int _startCoords;
    private int _drawingType = 0;

    private const int RULER_LENGTH = 128;
    private const int GRID_COLUMNS = 32;
    private const int GRID_ROWS = 16;
    private const float HeightOffset = 0.05f;
    private const float SurfaceBaseHeight = 1.00f;
    private const float SliceBaseHeight = 0.85f;

    [Inject]
    public RulerService(IAssetLoader assetLoader, ITerrainService terrainService, IBlockService blockService, ILevelVisibilityService levelVisibilityService, CameraService cameraService, EventBus eventBus, ISingletonLoader singletonLoader)
    {
      _assetLoader = assetLoader; _terrainService = terrainService; _blockService = blockService; _levelVisibilityService = levelVisibilityService; _cameraService = cameraService; _eventBus = eventBus; _singletonLoader = singletonLoader;
    }

    private void InternalFinalizeRuler(Vector3Int start, Vector3Int end, Quaternion rotation, List<int> explicitValues, int rType, int rPeriod, int rGap)
    {
      if (_previewContainer != null) _previewContainer.SetActive(false);
      Vector2Int step = GetStepDirection(start, end);

      int limit;
      if (explicitValues != null) { limit = explicitValues.Count; }
      else
      {
        int dx = end.x - start.x; int dy = end.y - start.y;
        int dist = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        limit = (dx == step.x * dist && dy == step.y * dist) ? dist + 1 : RULER_LENGTH;
        if (limit > RULER_LENGTH) limit = RULER_LENGTH;
      }

      GameObject container = new GameObject("ActiveRuler");
      var instance = new RulerInstance { Container = container, Segments = new List<RulerSegment>(), Start = start, Rotation = rotation, RulerType = rType, Period = rPeriod, GapSize = rGap };
      instance.End = start + new Vector3Int(step.x * (limit - 1), step.y * (limit - 1), 0);
      int maxV = _levelVisibilityService.MaxVisibleLevel;

      for (int i = 0; i < limit; i++)
      {
        Vector2Int tile = new Vector2Int(start.x + (step.x * i), start.y + (step.y * i));
        if (!_terrainService.Contains(tile)) continue;

        int val = (explicitValues != null) ? explicitValues[i] : i + 1;
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(container.transform); quad.transform.rotation = rotation;
        quad.GetComponent<MeshRenderer>().material = _rulerMaterial;

        var seg = new RulerSegment { Obj = quad, Coords = tile, Value = val, Ruler = instance };
        UpdateQuadHeight(quad, tile, maxV, instance.RulerType, val);

        instance.Segments.Add(seg);
        RegisterOverlap(tile, seg);
      }
      _activeRulers.Add(instance);
    }

    private void RegisterOverlap(Vector2Int tile, RulerSegment seg)
    {
      if (!_segmentMap.ContainsKey(tile)) _segmentMap[tile] = new List<RulerSegment>();
      _segmentMap[tile].Add(seg);

      if (_segmentMap[tile].Count == 1) { seg.Obj.SetActive(true); }
      else if (_segmentMap[tile].Count == 2)
      {
        _segmentMap[tile][0].Obj.SetActive(false);
        if (!_sharedQuads.ContainsKey(tile))
        {
          GameObject shared = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(shared.GetComponent<Collider>());
          shared.transform.rotation = _lockedRotation; shared.GetComponent<MeshRenderer>().material = _rulerMaterial;
          UpdateQuadHeight(shared, tile, _levelVisibilityService.MaxVisibleLevel, 0, 0);
          _sharedQuads[tile] = shared;
        }
        _sharedQuads[tile].SetActive(true);
        seg.Obj.SetActive(false);
      }
      else { seg.Obj.SetActive(false); }
    }

    private void CleanupOverlapForRuler(RulerInstance r)
    {
      foreach (var seg in r.Segments)
      {
        if (_segmentMap.ContainsKey(seg.Coords))
        {
          _segmentMap[seg.Coords].Remove(seg);
          int remaining = _segmentMap[seg.Coords].Count;
          if (remaining == 1)
          {
            if (_sharedQuads.ContainsKey(seg.Coords)) _sharedQuads[seg.Coords].SetActive(false);
            _segmentMap[seg.Coords][0].Obj.SetActive(true);
          }
          else if (remaining <= 0)
          {
            if (_sharedQuads.ContainsKey(seg.Coords)) _sharedQuads[seg.Coords].SetActive(false);
            _segmentMap.Remove(seg.Coords);
          }
        }
      }
    }

    private class RulerInstance { public GameObject Container; public List<RulerSegment> Segments; public Vector3Int Start, End; public Quaternion Rotation; public int RulerType; public int Period; public int GapSize; }
    private class RulerSegment { public GameObject Obj; public Vector2Int Coords; public int Value; public RulerInstance Ruler; }
  }
}