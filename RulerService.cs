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
using UnityEngine.InputSystem;

namespace Calloatti.Grid
{
  public class RulerService : ILoadableSingleton, IPostLoadableSingleton, ISaveableSingleton
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

    private bool _isDrawing = false;
    private Vector3Int _startCoords;

    private const int RULER_LENGTH = 129;
    private const int GRID_COLUMNS = 16;
    private const int GRID_ROWS = 32;
    private const float HeightOffset = 0.05f;
    private const float SurfaceBaseHeight = 1.00f;
    private const float SliceBaseHeight = 0.85f;

    private static readonly SingletonKey RulersKey = new SingletonKey("Calloatti.Grid.Rulers");
    private static readonly ListKey<Vector3Int> StartsKey = new ListKey<Vector3Int>("Starts");
    private static readonly ListKey<Vector3Int> EndsKey = new ListKey<Vector3Int>("Ends");
    private static readonly ListKey<Quaternion> RotationsKey = new ListKey<Quaternion>("Rotations");
    private static readonly ListKey<int> FlattenedValuesKey = new ListKey<int>("FlattenedValues");
    private static readonly ListKey<int> StatesKey = new ListKey<int>("States");

    private List<Vector3Int> _loadedStarts;
    private List<Vector3Int> _loadedEnds;
    private List<Quaternion> _loadedRotations;
    private List<int> _loadedFlattenedValues;
    private List<int> _loadedStates;

    [Inject]
    public RulerService(IAssetLoader assetLoader, ITerrainService terrainService, IBlockService blockService, ILevelVisibilityService levelVisibilityService, CameraService cameraService, EventBus eventBus, ISingletonLoader singletonLoader)
    {
      _assetLoader = assetLoader; _terrainService = terrainService; _blockService = blockService; _levelVisibilityService = levelVisibilityService; _cameraService = cameraService; _eventBus = eventBus; _singletonLoader = singletonLoader;
    }

    public void Load()
    {
      if (_singletonLoader.TryGetSingleton(RulersKey, out IObjectLoader objectLoader))
      {
        if (objectLoader.Has(StartsKey))
        {
          _loadedStarts = objectLoader.Get(StartsKey);
          _loadedEnds = objectLoader.Get(EndsKey);
          _loadedRotations = objectLoader.Get(RotationsKey);
          _loadedFlattenedValues = objectLoader.Has(FlattenedValuesKey) ? objectLoader.Get(FlattenedValuesKey) : null;
          _loadedStates = objectLoader.Has(StatesKey) ? objectLoader.Get(StatesKey) : null;
        }
      }
    }

    public void Save(ISingletonSaver saver)
    {
      IObjectSaver objectSaver = saver.GetSingleton(RulersKey);
      List<Vector3Int> starts = new List<Vector3Int>();
      List<Vector3Int> ends = new List<Vector3Int>();
      List<Quaternion> rotations = new List<Quaternion>();
      List<int> flattenedValues = new List<int>();
      List<int> states = new List<int>();

      foreach (var ruler in _activeRulers)
      {
        starts.Add(ruler.Start); ends.Add(ruler.End); rotations.Add(ruler.Rotation);

        // Save the 3 explicit state variables sequentially
        states.Add(ruler.RulerType);
        states.Add(ruler.Period);
        states.Add(ruler.GapSize);

        flattenedValues.Add(ruler.Segments.Count);
        foreach (var s in ruler.Segments) { flattenedValues.Add(s.Value); }
      }
      objectSaver.Set(StartsKey, starts); objectSaver.Set(EndsKey, ends); objectSaver.Set(RotationsKey, rotations);
      objectSaver.Set(FlattenedValuesKey, flattenedValues);
      objectSaver.Set(StatesKey, states);
    }

    public void PostLoad()
    {
      _eventBus.Register(this);
      _terrainService.TerrainHeightChanged += OnTerrainHeightChanged;

      Texture2D tex = _assetLoader.Load<Texture2D>("Sprites/ruler-atlas");
      _rulerMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };

      if (_loadedStarts != null)
      {
        int currentIndex = 0;
        for (int i = 0; i < _loadedStarts.Count; i++)
        {
          List<int> rulerValues = null;
          if (_loadedFlattenedValues != null && currentIndex < _loadedFlattenedValues.Count)
          {
            int count = _loadedFlattenedValues[currentIndex++];
            rulerValues = _loadedFlattenedValues.GetRange(currentIndex, count);
            currentIndex += count;
          }

          int rType = 0, rPeriod = 0, rGap = 0;
          if (_loadedStates != null && (i * 3 + 2) < _loadedStates.Count)
          {
            rType = _loadedStates[i * 3];
            rPeriod = _loadedStates[i * 3 + 1];
            rGap = _loadedStates[i * 3 + 2];
          }

          InternalFinalizeRuler(_loadedStarts[i], _loadedEnds[i], _loadedRotations[i], rulerValues, rType, rPeriod, rGap);
        }
        _loadedStarts = null; _loadedEnds = null; _loadedRotations = null; _loadedFlattenedValues = null; _loadedStates = null;
      }
    }

    [OnEvent]
    public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent e) { UpdateRulersVisuals(); }

    [OnEvent]
    public void OnBlockObjectSet(BlockObjectSetEvent e) { CheckBlockChange(e.BlockObject); }

    [OnEvent]
    public void OnBlockObjectUnset(BlockObjectUnsetEvent e) { CheckBlockChange(e.BlockObject); }

    private void CheckBlockChange(BlockObject bo)
    {
      HashSet<Vector2Int> columns = new HashSet<Vector2Int>();
      foreach (var c in bo.PositionedBlocks.GetAllCoordinates()) columns.Add(new Vector2Int(c.x, c.y));
      List<RulerInstance> toRebuild = new List<RulerInstance>();
      for (int i = _activeRulers.Count - 1; i >= 0; i--)
      {
        var r = _activeRulers[i];
        bool hit = false;
        foreach (var s in r.Segments) if (columns.Contains(s.Coords)) { hit = true; break; }
        if (hit)
        {
          List<int> v = new List<int>(); foreach (var seg in r.Segments) v.Add(seg.Value);
          toRebuild.Add(new RulerInstance { Start = r.Start, End = r.End, Rotation = r.Rotation, SavedValues = v, RulerType = r.RulerType, Period = r.Period, GapSize = r.GapSize });
          Object.Destroy(r.Container); _activeRulers.RemoveAt(i);
        }
      }
      foreach (var r in toRebuild) InternalFinalizeRuler(r.Start, r.End, r.Rotation, r.SavedValues, r.RulerType, r.Period, r.GapSize);
    }

    private void OnTerrainHeightChanged(object s, TerrainHeightChangeEventArgs e)
    {
      Vector2Int col = new Vector2Int(e.Change.Coordinates.x, e.Change.Coordinates.y);
      List<RulerInstance> toRebuild = new List<RulerInstance>();
      for (int i = _activeRulers.Count - 1; i >= 0; i--)
      {
        var r = _activeRulers[i];
        bool hit = false;
        foreach (var seg in r.Segments) if (seg.Coords == col) { hit = true; break; }
        if (hit)
        {
          List<int> v = new List<int>(); foreach (var seg in r.Segments) v.Add(seg.Value);
          toRebuild.Add(new RulerInstance { Start = r.Start, End = r.End, Rotation = r.Rotation, SavedValues = v, RulerType = r.RulerType, Period = r.Period, GapSize = r.GapSize });
          Object.Destroy(r.Container); _activeRulers.RemoveAt(i);
        }
      }
      foreach (var r in toRebuild) InternalFinalizeRuler(r.Start, r.End, r.Rotation, r.SavedValues, r.RulerType, r.Period, r.GapSize);
    }

    private void UpdateRulersVisuals()
    {
      int maxV = _levelVisibilityService.MaxVisibleLevel;
      foreach (var r in _activeRulers) foreach (var s in r.Segments) UpdateQuadHeight(s.Obj, s.Coords, maxV);
    }

    public void HandleClick(Vector3Int clickCoords)
    {
      if (!_isDrawing)
      {
        bool ctrl = Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;
        bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
        Vector2Int c2d = new Vector2Int(clickCoords.x, clickCoords.y);

        for (int i = _activeRulers.Count - 1; i >= 0; i--)
        {
          var r = _activeRulers[i];
          foreach (var seg in r.Segments)
          {
            if (seg.Coords == c2d)
            {
              if (shift) { Object.Destroy(r.Container); _activeRulers.RemoveAt(i); return; }
              if (ctrl)
              {
                int clickedValue = seg.Value;
                Vector3Int s = r.Start, e = r.End;
                Quaternion rot = r.Rotation;

                int dist = Mathf.Max(Mathf.Abs(e.x - s.x), Mathf.Abs(e.y - s.y));

                int newType = r.RulerType;
                int newPeriod = r.Period;
                int newGapSize = r.GapSize;

                // 1. STATE MACHINE LOGIC
                if (r.RulerType == 0)
                {
                  if (clickedValue > 0)
                  {
                    newType = 1;
                    newPeriod = Mathf.Max(1, clickedValue - 1);
                    newGapSize = 1;
                  }
                  else
                  {
                    newType = 1;
                    newPeriod = 1;
                    newGapSize = 1;
                  }
                }
                else if (r.RulerType == 1)
                {
                  if (clickedValue == 0)
                  {
                    newType = 0;
                    newPeriod = 0;
                    newGapSize = 0;
                  }
                  else
                  {
                    newGapSize = r.GapSize + clickedValue;
                  }
                }

                // 2. GENERATE SEQUENCE & ADJUST LENGTH
                List<int> newVals = new List<int>();
                newVals.Add(0); // Start marker

                if (newType == 0)
                {
                  // Standard Ruler: Draw numbers up to the current distance
                  int newLimit = dist + 1;
                  for (int n = 1; n < newLimit; n++) newVals.Add(n);
                }
                else
                {
                  // Periodic Ruler: Calculate exactly how many cycles fit, and snap to the end of a gap
                  int cycle = newPeriod + newGapSize;

                  // Math to snap to the nearest full cycle
                  int cycles = (dist + newPeriod) / cycle;
                  if (cycles < 1) cycles = 1; // Guarantee at least one full cycle

                  int newLimit = (cycles * cycle) + 1; // +1 to account for the start marker

                  int currentNum = 1;
                  int currentGap = 0;
                  for (int n = 1; n < newLimit; n++)
                  {
                    if (currentNum <= newPeriod)
                    {
                      newVals.Add(currentNum);
                      currentNum++;
                    }
                    else
                    {
                      newVals.Add(0); // Draw Red Square
                      currentGap++;
                      if (currentGap >= newGapSize)
                      {
                        currentNum = 1; // Reset for next cycle
                        currentGap = 0;
                      }
                    }
                  }
                }

                Object.Destroy(r.Container);
                _activeRulers.RemoveAt(i);

                InternalFinalizeRuler(s, e, rot, newVals, newType, newPeriod, newGapSize);
                return;
              }
            }
          }
        }
        if (shift || ctrl) return;
        _isDrawing = true; _startCoords = clickCoords; _lockedRotation = CalculateCameraRotation(); InternalSetupPreview();
      }
      else { InternalFinalizeRuler(_startCoords, clickCoords, _lockedRotation, null, 0, 0, 0); _isDrawing = false; }
    }

    public void HandleMouseMove(Vector3Int c) { if (_isDrawing) InternalUpdatePreview(_startCoords, c); }
    public void CancelOperation() { _isDrawing = false; if (_previewContainer != null) _previewContainer.SetActive(false); }
    public void DeleteAllRulers() { foreach (var r in _activeRulers) if (r.Container != null) Object.Destroy(r.Container); _activeRulers.Clear(); CancelOperation(); }

    private void InternalSetupPreview()
    {
      if (_previewContainer == null)
      {
        _previewContainer = new GameObject("RulerPreviewContainer");
        _previewQuads = new GameObject[RULER_LENGTH];
        for (int i = 0; i < RULER_LENGTH; i++)
        {
          GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(q.GetComponent<Collider>());
          q.transform.SetParent(_previewContainer.transform); q.GetComponent<MeshRenderer>().material = _rulerMaterial;
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

        bool isPeriodicZero = false; // Preview is never periodic
        AdjustSegmentUVs(q, i, isPeriodicZero);
        UpdateQuadHeight(q, tile, maxV);
      }
    }

    private void InternalFinalizeRuler(Vector3Int start, Vector3Int end, Quaternion rotation, List<int> explicitValues, int rType, int rPeriod, int rGap)
    {
      if (_previewContainer != null) _previewContainer.SetActive(false);
      Vector2Int step = GetStepDirection(start, end);

      int limit;
      if (explicitValues != null)
      {
        limit = explicitValues.Count;
      }
      else
      {
        int dx = end.x - start.x;
        int dy = end.y - start.y;
        int dist = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));

        if (dx == step.x * dist && dy == step.y * dist)
        {
          limit = dist + 1;
        }
        else
        {
          limit = RULER_LENGTH;
        }

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
        bool keepOrigin = false;
        for (int r = _activeRulers.Count - 1; r >= 0; r--)
        {
          var old = _activeRulers[r];
          for (int s = old.Segments.Count - 1; s >= 0; s--)
            if (old.Segments[s].Coords == tile) { if (i == 0) keepOrigin = true; else { Object.Destroy(old.Segments[s].Obj); old.Segments.RemoveAt(s); } }
          if (old.Segments.Count == 0) { Object.Destroy(old.Container); _activeRulers.RemoveAt(r); }
        }
        if (keepOrigin) continue;

        int val = (explicitValues != null) ? explicitValues[i] : i;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(container.transform); quad.transform.rotation = rotation;
        quad.GetComponent<MeshRenderer>().material = _rulerMaterial;

        bool isPeriodicZero = (val == 0 && i > 0);
        AdjustSegmentUVs(quad, val, isPeriodicZero);
        UpdateQuadHeight(quad, tile, maxV);

        instance.Segments.Add(new RulerSegment { Obj = quad, Coords = tile, Value = val });
      }
      _activeRulers.Add(instance);
    }

    private Vector2Int GetStepDirection(Vector3Int s, Vector3Int e) { int dx = e.x - s.x, dy = e.y - s.y; return Mathf.Abs(dx) >= Mathf.Abs(dy) ? new Vector2Int(dx >= 0 ? 1 : -1, 0) : new Vector2Int(0, dy >= 0 ? 1 : -1); }
    private Quaternion CalculateCameraRotation() { float snapped = Mathf.Round(_cameraService.HorizontalAngle / 90f) * 90f; return Quaternion.Euler(90, snapped, 0); }

    private void UpdateQuadHeight(GameObject q, Vector2Int c, int maxV)
    {
      int mapZ = _terrainService.Size.z; float fY = -1f;
      if (maxV >= 0 && maxV < mapZ && _terrainService.Underground(new Vector3Int(c.x, c.y, maxV))) fY = maxV + SliceBaseHeight + HeightOffset;
      if (fY < 0f)
      {
        int hZ = -2;
        if (!_blockService.AnyObjectAt(new Vector3Int(c.x, c.y, 0)) && !_terrainService.Underground(new Vector3Int(c.x, c.y, 0))) hZ = -1;
        for (int z = 0; z < mapZ; z++)
        {
          Vector3Int p = new Vector3Int(c.x, c.y, z);
          bool isT = _terrainService.Underground(p); bool hasS = false; BlockObject sO = null;
          foreach (BlockObject o in _blockService.GetObjectsAt(p)) if (o.Solid) { hasS = true; sO = o; break; }
          if (isT || hasS)
          {
            Vector3Int pA = new Vector3Int(c.x, c.y, z + 1); bool tA = z + 1 < mapZ && _terrainService.Underground(pA); bool bOA = false;
            if (z + 1 < mapZ) { BlockObject oA = _blockService.GetBottomObjectAt(pA); if (oA != null && oA.Solid && oA != sO) bOA = true; if (!isT && sO != null && sO.PositionedBlocks.HasBlockAt(pA)) bOA = true; }
            if (!tA && !bOA) { int rV = z + 1; if (!isT && sO != null && _blockService.GetBottomObjectAt(sO.Coordinates) == sO) rV = sO.Coordinates.z; if (maxV >= rV) hZ = z; }
          }
        }
        if (hZ != -2) fY = hZ + SurfaceBaseHeight + HeightOffset;
      }
      if (fY < 0f) { int tH = _terrainService.GetTerrainHeightBelow(new Vector3Int(c.x, c.y, mapZ - 1)); int tZ = tH - 1; fY = (tZ < maxV) ? (tZ + SurfaceBaseHeight + HeightOffset) : (maxV + SliceBaseHeight + HeightOffset); }
      Vector3 wP = CoordinateSystem.GridToWorld(new Vector3(c.x + 0.5f, c.y + 0.5f, 0)); q.transform.position = new Vector3(wP.x, fY, wP.z);
    }

    private void AdjustSegmentUVs(GameObject go, int logicalValue, bool isPeriodicZero)
    {
      Mesh mesh = go.GetComponent<MeshFilter>().mesh;
      Vector2[] uvs = new Vector2[4];

      int spriteIndex = logicalValue;

      if (logicalValue == 0 && isPeriodicZero)
      {
        spriteIndex = 511;
      }

      int col = spriteIndex % GRID_COLUMNS;
      int row = spriteIndex / GRID_COLUMNS;

      float uStart = (float)col / GRID_COLUMNS;
      float uEnd = (float)(col + 1) / GRID_COLUMNS;

      float vTop = 1.0f - ((float)row / GRID_ROWS);
      float vBottom = 1.0f - ((float)(row + 1) / GRID_ROWS);

      uvs[0] = new Vector2(uStart, vBottom);
      uvs[1] = new Vector2(uEnd, vBottom);
      uvs[2] = new Vector2(uStart, vTop);
      uvs[3] = new Vector2(uEnd, vTop);

      mesh.uv = uvs;
    }

    private class RulerInstance { public GameObject Container; public List<RulerSegment> Segments; public Vector3Int Start, End; public Quaternion Rotation; public List<int> SavedValues; public int RulerType; public int Period; public int GapSize; }
    private class RulerSegment { public GameObject Obj; public Vector2Int Coords; public int Value; }
  }
}