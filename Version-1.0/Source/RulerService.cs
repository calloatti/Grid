using Bindito.Core;
using System.Collections.Generic;
using System.Linq;
using Timberborn.AssetSystem;
using Timberborn.BlockSystem;
using Timberborn.CameraSystem;
using Timberborn.Coordinates;
using Timberborn.LevelVisibilitySystem;
using Timberborn.Localization;
using Timberborn.Persistence;
using Timberborn.QuickNotificationSystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.WorldPersistence;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Calloatti.Grid
{
  public class RulerService : ILoadableSingleton, IPostLoadableSingleton, ISaveableSingleton, System.IDisposable
  {
    public static RulerService Instance { get; private set; }

    // --- DEPENDENCIES ---
    private readonly QuickNotificationService _notificationService;
    private readonly ILoc _loc;
    private readonly IAssetLoader _assetLoader;
    private readonly ITerrainService _terrainService;
    private readonly IBlockService _blockService;
    private readonly ILevelVisibilityService _levelVisibilityService;
    private readonly CameraService _cameraService;
    private readonly EventBus _eventBus;
    private readonly ISingletonLoader _singletonLoader;

    // --- CONSTANTS ---
    public const int RedSquareValue = 1024;
    private const int RULER_LENGTH = 255;
    private const int GRID_COLUMNS = 256;
    private const int GRID_ROWS = 5;
    private const float HeightOffset = 0.05f;
    private const float SurfaceBaseHeight = 1.00f;
    private const float SliceBaseHeight = 0.85f;

    // --- PERSISTENCE KEYS ---
    private static readonly SingletonKey RulersKey = new SingletonKey("Calloatti.Grid.Rulers");
    private static readonly ListKey<Vector3Int> StartsKey = new ListKey<Vector3Int>("Starts");
    private static readonly ListKey<Vector3Int> EndsKey = new ListKey<Vector3Int>("Ends");
    private static readonly ListKey<Quaternion> RotationsKey = new ListKey<Quaternion>("Rotations");
    private static readonly ListKey<int> FlattenedValuesKey = new ListKey<int>("FlattenedValues");
    private static readonly ListKey<int> StatesKey = new ListKey<int>("States");

    // --- STATE ---
    private Material _rulerMaterial;
    private Mesh _sharedQuadMesh;
    private GameObject _previewContainer;
    private GameObject[] _previewQuads;
    private Quaternion _lockedRotation;
    private bool _rulersVisible = true;

    private readonly List<RulerInstance> _activeRulers = new List<RulerInstance>();
    private readonly Dictionary<Vector2Int, List<RulerSegment>> _segmentMap = new Dictionary<Vector2Int, List<RulerSegment>>();
    private readonly Dictionary<Vector2Int, GameObject> _sharedQuads = new Dictionary<Vector2Int, GameObject>();

    private bool _isDrawing = false;
    private Vector3Int _startCoords;
    private int _drawingType = 0;

    // --- LOADED DATA ---
    private List<Vector3Int> _loadedStarts;
    private List<Vector3Int> _loadedEnds;
    private List<Quaternion> _loadedRotations;
    private List<int> _loadedFlattenedValues;
    private List<int> _loadedStates;

    [Inject]
    public RulerService(
        IAssetLoader assetLoader,
        ITerrainService terrainService,
        IBlockService blockService,
        ILevelVisibilityService levelVisibilityService,
        CameraService cameraService,
        EventBus eventBus,
        ISingletonLoader singletonLoader,
        QuickNotificationService notificationService,
        ILoc loc)
    {
      _assetLoader = assetLoader;
      _terrainService = terrainService;
      _blockService = blockService;
      _levelVisibilityService = levelVisibilityService;
      _cameraService = cameraService;
      _eventBus = eventBus;
      _singletonLoader = singletonLoader;
      _notificationService = notificationService;
      _loc = loc;
      Instance = this;
    }

    // ====================================================================
    // LIFECYCLE & PERSISTENCE
    // ====================================================================

    public void Load()
    {
      if (_singletonLoader.TryGetSingleton(RulersKey, out IObjectLoader loader))
      {
        if (loader.Has(StartsKey))
        {
          _loadedStarts = loader.Get(StartsKey);
          _loadedEnds = loader.Get(EndsKey);
          _loadedRotations = loader.Get(RotationsKey);
          _loadedFlattenedValues = loader.Has(FlattenedValuesKey) ? loader.Get(FlattenedValuesKey) : null;
          _loadedStates = loader.Has(StatesKey) ? loader.Get(StatesKey) : null;
        }
      }
    }

    public void PostLoad()
    {
      _eventBus.Register(this);
      _terrainService.TerrainHeightChanged += OnTerrainHeightChanged;

      Texture2D tex = _assetLoader.Load<Texture2D>("Sprites/grid-atlas");
      _rulerMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };

      if (_loadedStarts != null)
      {
        int idx = 0;
        for (int i = 0; i < _loadedStarts.Count; i++)
        {
          List<int> vals = null;
          if (_loadedFlattenedValues != null)
          {
            int c = _loadedFlattenedValues[idx++];
            vals = _loadedFlattenedValues.GetRange(idx, c);

            for (int j = 0; j < vals.Count; j++)
            {
              if (vals[j] == 511) vals[j] = RedSquareValue; // Convert legacy
            }

            idx += c;
          }

          int t = 0, p = 0, g = 0;
          if (_loadedStates != null)
          {
            t = _loadedStates[i * 3];
            p = _loadedStates[i * 3 + 1];
            g = _loadedStates[i * 3 + 2];
          }

          InternalFinalizeRuler(_loadedStarts[i], _loadedEnds[i], _loadedRotations[i], vals, t, p, g);
        }
      }
    }

    public void Save(ISingletonSaver saver)
    {
      IObjectSaver os = saver.GetSingleton(RulersKey);
      List<Vector3Int> s = new List<Vector3Int>(), e = new List<Vector3Int>();
      List<Quaternion> r = new List<Quaternion>();
      List<int> f = new List<int>(), st = new List<int>();

      foreach (var ruler in _activeRulers)
      {
        s.Add(ruler.Start);
        e.Add(ruler.End);
        r.Add(ruler.Rotation);

        st.Add(ruler.RulerType);
        st.Add(ruler.Period);
        st.Add(ruler.GapSize);

        f.Add(ruler.Segments.Count);
        foreach (var seg in ruler.Segments) f.Add(seg.Value);
      }

      os.Set(StartsKey, s);
      os.Set(EndsKey, e);
      os.Set(RotationsKey, r);
      os.Set(FlattenedValuesKey, f);
      os.Set(StatesKey, st);
    }

    public void Dispose()
    {
      _eventBus.Unregister(this);
      if (_terrainService != null) _terrainService.TerrainHeightChanged -= OnTerrainHeightChanged;
      if (_rulerMaterial != null) Object.Destroy(_rulerMaterial);
      if (_sharedQuadMesh != null) Object.Destroy(_sharedQuadMesh);
      if (_previewContainer != null) Object.Destroy(_previewContainer);
      foreach (var quad in _sharedQuads.Values) if (quad != null) Object.Destroy(quad);
      foreach (var ruler in _activeRulers) if (ruler.Container != null) Object.Destroy(ruler.Container);
      _activeRulers.Clear(); _sharedQuads.Clear(); _segmentMap.Clear();
    }

    // ====================================================================
    // EVENTS
    // ====================================================================

    [OnEvent] public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent e) { UpdateRulersVisuals(); }
    [OnEvent] public void OnBlockObjectSet(BlockObjectSetEvent e) { CheckBlockChange(e.BlockObject); }
    [OnEvent] public void OnBlockObjectUnset(BlockObjectUnsetEvent e) { CheckBlockChange(e.BlockObject); }

    private void CheckBlockChange(BlockObject bo)
    {
      HashSet<Vector2Int> affected = new HashSet<Vector2Int>();
      foreach (var c in bo.PositionedBlocks.GetAllCoordinates())
      {
        Vector2Int col = new Vector2Int(c.x, c.y);
        if (_segmentMap.ContainsKey(col) && _segmentMap[col].Count > 0) affected.Add(col);
      }
      if (affected.Count == 0) return;
      int maxV = _levelVisibilityService.MaxVisibleLevel;
      foreach (var col in affected)
      {
        if (_sharedQuads.ContainsKey(col)) UpdateQuadHeight(_sharedQuads[col], col, maxV, 0, 0);
        foreach (var seg in _segmentMap[col]) UpdateQuadHeight(seg.Obj, col, maxV, seg.Ruler.RulerType, seg.Value);
      }
    }

    private void OnTerrainHeightChanged(object s, TerrainHeightChangeEventArgs e)
    {
      Vector2Int col = new Vector2Int(e.Change.Coordinates.x, e.Change.Coordinates.y);
      if (!_segmentMap.ContainsKey(col) || _segmentMap[col].Count == 0) return;
      int maxV = _levelVisibilityService.MaxVisibleLevel;
      if (_sharedQuads.ContainsKey(col)) UpdateQuadHeight(_sharedQuads[col], col, maxV, 0, 0);
      foreach (var seg in _segmentMap[col]) UpdateQuadHeight(seg.Obj, col, maxV, seg.Ruler.RulerType, seg.Value);
    }

    // ====================================================================
    // INPUT HANDLING
    // ====================================================================

    public void HandleClick(Vector3Int clickCoords)
    {
      bool ctrl = Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;
      bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

      if (!_isDrawing)
      {
        Vector2Int c2d = new Vector2Int(clickCoords.x, clickCoords.y);

        for (int i = _activeRulers.Count - 1; i >= 0; i--)
        {
          var r = _activeRulers[i];
          if (r.Segments.Any(seg => seg.Coords == c2d))
          {
            if (shift)
            {
              CleanupOverlapForRuler(r);
              Object.Destroy(r.Container);
              _activeRulers.RemoveAt(i);
              return;
            }
            if (ctrl)
            {
              var clickedSeg = r.Segments.First(seg => seg.Coords == c2d);
              int clickedValue = clickedSeg.Value;
              Vector3Int s = r.Start, e = r.End;
              Quaternion rot = r.Rotation;
              int dist = Mathf.Max(Mathf.Abs(e.x - s.x), Mathf.Abs(e.y - s.y));

              int newType = r.RulerType;
              int newPeriod = r.Period;
              int newGapSize = r.GapSize;

              if (r.RulerType == 0)
              {
                newType = 1;
                newPeriod = Mathf.Max(1, clickedValue - 1);
                newGapSize = 1;
              }
              else if (r.RulerType == 1)
              {
                if (clickedValue == RedSquareValue)
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

              List<int> newVals = new List<int>();
              int newLimit = dist + 1;

              if (newType == 0)
              {
                for (int n = 1; n <= newLimit; n++) newVals.Add(n);
              }
              else
              {
                int currentNum = 1;
                int currentGap = 0;

                for (int n = 1; n <= newLimit; n++)
                {
                  if (currentNum <= newPeriod)
                  {
                    newVals.Add(currentNum);
                    currentNum++;
                  }
                  else
                  {
                    newVals.Add(RedSquareValue);
                    currentGap++;
                    if (currentGap >= newGapSize)
                    {
                      currentNum = 1;
                      currentGap = 0;
                    }
                  }
                }
              }

              CleanupOverlapForRuler(r);
              Object.Destroy(r.Container);
              _activeRulers.RemoveAt(i);

              InternalFinalizeRuler(s, e, rot, newVals, newType, newPeriod, newGapSize);
              return;
            }
          }
        }

        if (shift || ctrl) return;

        _isDrawing = true;
        _startCoords = clickCoords;
        _lockedRotation = CalculateCameraRotation();

        InternalSetupPreview();
      }
      else
      {
        InternalFinalizeRuler(_startCoords, clickCoords, _lockedRotation, null, 0, 0, 0);
        _isDrawing = false;
        _drawingType = 0;
      }
    }

    public void HandleMouseMove(Vector3Int c) { if (_isDrawing) InternalUpdatePreview(_startCoords, c); }

    public void CancelOperation() { _isDrawing = false; _drawingType = 0; if (_previewContainer != null) _previewContainer.SetActive(false); }

    public void DeleteAllRulers()
    {
      foreach (var r in _activeRulers) if (r.Container != null) Object.Destroy(r.Container);
      _activeRulers.Clear();
      _segmentMap.Clear();
      foreach (var q in _sharedQuads.Values) Object.Destroy(q);
      _sharedQuads.Clear();
      CancelOperation();
    }

    public void ToggleRulers()
    {
      _rulersVisible = !_rulersVisible;
      foreach (var r in _activeRulers) if (r.Container != null) r.Container.SetActive(_rulersVisible);
      foreach (var q in _sharedQuads.Values) if (q != null) q.SetActive(_rulersVisible);

      string locKey = _rulersVisible ? "Calloatti.Grid.Rulers.NotificationOn" : "Calloatti.Grid.Rulers.NotificationOff";
      _notificationService.SendNotification(_loc.T(locKey));
    }

    // ====================================================================
    // VISUALS & LOGIC
    // ====================================================================

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
      container.SetActive(_rulersVisible);
      var instance = new RulerInstance { Container = container, Segments = new List<RulerSegment>(), Start = start, Rotation = rotation, RulerType = rType, Period = rPeriod, GapSize = rGap };
      instance.End = start + new Vector3Int(step.x * (limit - 1), step.y * (limit - 1), 0);
      int maxV = _levelVisibilityService.MaxVisibleLevel;

      for (int i = 0; i < limit; i++)
      {
        Vector2Int tile = new Vector2Int(start.x + (step.x * i), start.y + (step.y * i));
        if (!_terrainService.Contains(tile)) continue;

        int val = (explicitValues != null) ? explicitValues[i] : i + 1;
        GameObject quad = CreateRulerQuad(container.transform, rotation);
        var seg = new RulerSegment { Obj = quad, Coords = tile, Value = val, Ruler = instance };
        UpdateQuadHeight(quad, tile, maxV, instance.RulerType, val);

        instance.Segments.Add(seg);
        RegisterOverlap(tile, seg);
      }
      _activeRulers.Add(instance);
    }

    private GameObject CreateRulerQuad(Transform parent, Quaternion rotation)
    {
      if (_sharedQuadMesh == null) InitializeSharedMesh();
      GameObject q = new GameObject("RulerSegment");
      q.transform.SetParent(parent);
      q.transform.rotation = rotation;
      q.AddComponent<MeshFilter>().sharedMesh = _sharedQuadMesh;
      q.AddComponent<MeshRenderer>().sharedMaterial = _rulerMaterial;
      return q;
    }

    private void InitializeSharedMesh()
    {
      GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
      _sharedQuadMesh = temp.GetComponent<MeshFilter>().sharedMesh;
      Object.Destroy(temp);
    }

    private void RegisterOverlap(Vector2Int tile, RulerSegment seg)
    {
      if (!_segmentMap.ContainsKey(tile)) _segmentMap[tile] = new List<RulerSegment>();
      _segmentMap[tile].Add(seg);

      if (_segmentMap[tile].Count == 1) { seg.Obj.SetActive(_rulersVisible); }
      else if (_segmentMap[tile].Count == 2)
      {
        _segmentMap[tile][0].Obj.SetActive(false);
        if (!_sharedQuads.ContainsKey(tile))
        {
          GameObject shared = CreateRulerQuad(null, seg.Ruler.Rotation);
          UpdateQuadHeight(shared, tile, _levelVisibilityService.MaxVisibleLevel, 0, 0);
          _sharedQuads[tile] = shared;
        }
        _sharedQuads[tile].SetActive(_rulersVisible);
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
            _segmentMap[seg.Coords][0].Obj.SetActive(_rulersVisible);
          }
          else if (remaining <= 0)
          {
            if (_sharedQuads.ContainsKey(seg.Coords)) _sharedQuads[seg.Coords].SetActive(false);
            _segmentMap.Remove(seg.Coords);
          }
        }
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

      AdjustSegmentUVs(q, logicalValue);
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

    // ====================================================================
    // INTERNAL CLASSES
    // ====================================================================

    private class RulerInstance { public GameObject Container; public List<RulerSegment> Segments; public Vector3Int Start, End; public Quaternion Rotation; public int RulerType; public int Period; public int GapSize; }
    private class RulerSegment { public GameObject Obj; public Vector2Int Coords; public int Value; public RulerInstance Ruler; }
  }
}