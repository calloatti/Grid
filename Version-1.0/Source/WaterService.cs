using System.Collections.Generic;
using System.Linq;
using Timberborn.Common;
using Timberborn.LevelVisibilitySystem;
using Timberborn.MapIndexSystem;
using Timberborn.MapStateSystem; // <-- FIXED: Restored to the correct namespace
using Timberborn.Persistence;
using Timberborn.Rendering;
using Timberborn.RootProviders;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace Calloatti.Grid
{
  // =========================================================================
  // CORE DATA STORAGE
  // =========================================================================
  public class WaterPlannedArea : ISaveableSingleton, ILoadableSingleton
  {
    private static readonly SingletonKey WaterAreaKey = new SingletonKey("Calloatti.Grid.WaterPlannedArea");
    private static readonly ListKey<Vector3Int> AreaKey = new ListKey<Vector3Int>("Area");

    private readonly ISingletonLoader _singletonLoader;
    private readonly EventBus _eventBus;
    private readonly MapEditorMode _mapEditorMode;

    private readonly HashSet<Vector3Int> _area = new HashSet<Vector3Int>();

    public IEnumerable<Vector3Int> Area => _area.AsReadOnlyEnumerable();
    public bool IsEmpty => _area.Count == 0;

    public WaterPlannedArea(ISingletonLoader singletonLoader, EventBus eventBus, MapEditorMode mapEditorMode)
    {
      _singletonLoader = singletonLoader;
      _eventBus = eventBus;
      _mapEditorMode = mapEditorMode;
    }

    public void Load()
    {
      if (_singletonLoader.TryGetSingleton(WaterAreaKey, out var objectLoader))
      {
        _area.AddRange(objectLoader.Get(AreaKey));
      }
    }

    public void Save(ISingletonSaver singletonSaver)
    {
      if (!_mapEditorMode.IsMapEditor)
      {
        singletonSaver.GetSingleton(WaterAreaKey).Set(AreaKey, _area);
      }
    }

    public bool Contains(Vector3Int coordinates) => _area.Contains(coordinates);

    public void AddCoordinates(IEnumerable<Vector3Int> coordinates)
    {
      foreach (var coordinate in coordinates) _area.Add(coordinate);
      _eventBus.Post(new WaterPlannedAreaChangedEvent());
    }

    public void RemoveCoordinates(IEnumerable<Vector3Int> coordinates)
    {
      foreach (var coordinate in coordinates) _area.Remove(coordinate);
      _eventBus.Post(new WaterPlannedAreaChangedEvent());
    }

    public void ClearAll()
    {
      _area.Clear();
      _eventBus.Post(new WaterPlannedAreaChangedEvent());
    }
  }

  // =========================================================================
  // WATER VISUALIZER
  // =========================================================================
  public class WaterPlannedAreaVisualizer : ILoadableSingleton
  {
    private readonly EventBus _eventBus;
    private readonly WaterPlannedArea _waterPlannedArea;
    private readonly AreaTileDrawerFactory _areaTileDrawerFactory;
    private readonly RootObjectProvider _rootObjectProvider;
    private readonly ILevelVisibilityService _levelVisibilityService;

    private AreaTileDrawer _areaTileDrawer;
    private GameObject _parent;

    public WaterPlannedAreaVisualizer(EventBus eventBus, WaterPlannedArea waterPlannedArea, AreaTileDrawerFactory areaTileDrawerFactory, RootObjectProvider rootObjectProvider, ILevelVisibilityService levelVisibilityService)
    {
      _eventBus = eventBus;
      _waterPlannedArea = waterPlannedArea;
      _areaTileDrawerFactory = areaTileDrawerFactory;
      _rootObjectProvider = rootObjectProvider;
      _levelVisibilityService = levelVisibilityService;
    }

    public void Load()
    {
      _parent = _rootObjectProvider.CreateRootObject("WaterPlannedAreaVisualizer");
      _levelVisibilityService.MaxVisibleLevelChanged += OnMaxVisibleLevelChanged;

      Color blueColor = new Color(0.1f, 0.2f, 0.9f, 0.9f);
      _areaTileDrawer = _areaTileDrawerFactory.Create(blueColor, _parent);

      _eventBus.Register(this);
      UpdateArea();
    }

    [OnEvent] public void OnWaterPlannedAreaChanged(WaterPlannedAreaChangedEvent e) => UpdateArea();
    private void OnMaxVisibleLevelChanged(object sender, int e) => UpdateArea();

    private void UpdateArea()
    {
      _areaTileDrawer.UpdateArea(_waterPlannedArea.Area.Where(coords => coords.z <= _levelVisibilityService.MaxVisibleLevel));
      _areaTileDrawer.ShowAllTiles();
    }
  }

  // =========================================================================
  // MOISTURE SPREAD SIMULATOR (GC OPTIMIZED)
  // =========================================================================
  public class WaterSpreadSimulator : ILoadableSingleton
  {
    private readonly EventBus _eventBus;
    private readonly WaterPlannedArea _waterPlannedArea;
    private readonly ITerrainService _terrainService;
    private readonly MapIndexService _mapIndexService;

    private readonly HashSet<Vector3Int> _moistTiles = new HashSet<Vector3Int>();
    public IEnumerable<Vector3Int> MoistTiles => _moistTiles.AsReadOnlyEnumerable();

    private const int MaxClusterSaturation = 8;
    private const float EnergyPerSaturation = 2f;
    private const float VerticalSpreadCostMultiplier = 3f;
    private const float MinimumMoisture = 0.01f;

    // --- OPTIMIZATION: Reusable buffers to eliminate GC allocations ---
    private readonly HashSet<Vector2Int> _water2D = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, int> _wateredNeighbors = new Dictionary<Vector2Int, int>();
    private readonly Dictionary<Vector2Int, int> _clusterSaturations = new Dictionary<Vector2Int, int>();
    private readonly Queue<Vector3Int> _queue = new Queue<Vector3Int>();
    private readonly Dictionary<Vector3Int, float> _bestEnergyAtTile = new Dictionary<Vector3Int, float>();

    private static readonly Vector2Int[] Neighbors8 = {
        new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    private static readonly Vector2Int[] Neighbors4 = {
        new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0)
    };

    private static readonly (Vector2Int offset, float cost, bool isOrthogonal)[] SpreadCosts = {
        (new Vector2Int(0, 0), 0f, true),
        (new Vector2Int(0, 1), 1f, true), (new Vector2Int(0, -1), 1f, true),
        (new Vector2Int(1, 0), 1f, true), (new Vector2Int(-1, 0), 1f, true),
        (new Vector2Int(1, 1), 1.414f, false), (new Vector2Int(1, -1), 1.414f, false),
        (new Vector2Int(-1, 1), 1.414f, false), (new Vector2Int(-1, -1), 1.414f, false)
    };

    public WaterSpreadSimulator(EventBus eventBus, WaterPlannedArea waterPlannedArea, ITerrainService terrainService, MapIndexService mapIndexService)
    {
      _eventBus = eventBus;
      _waterPlannedArea = waterPlannedArea;
      _terrainService = terrainService;
      _mapIndexService = mapIndexService;
    }

    public void Load()
    {
      _eventBus.Register(this);
      _terrainService.TerrainHeightChanged += OnTerrainHeightChanged;
      RecalculateSpread();
    }

    [OnEvent] public void OnWaterPlannedAreaChanged(WaterPlannedAreaChangedEvent e) => RecalculateSpread();
    private void OnTerrainHeightChanged(object sender, TerrainHeightChangeEventArgs e) => RecalculateSpread();

    private void RecalculateSpread()
    {
      _moistTiles.Clear();
      _water2D.Clear();
      _wateredNeighbors.Clear();
      _clusterSaturations.Clear();
      _queue.Clear();
      _bestEnergyAtTile.Clear();

      if (_waterPlannedArea.IsEmpty)
      {
        _eventBus.Post(new MoistureSpreadChangedEvent());
        return;
      }

      foreach (var t in _waterPlannedArea.Area) _water2D.Add(t.XY());

      foreach (Vector2Int tile in _water2D)
      {
        int count = 0;
        for (int i = 0; i < Neighbors8.Length; i++)
        {
          if (_water2D.Contains(tile + Neighbors8[i])) count++;
        }
        _wateredNeighbors[tile] = count + 1;
      }

      foreach (Vector2Int tile in _water2D)
      {
        int maxCount = _wateredNeighbors[tile];
        for (int i = 0; i < Neighbors4.Length; i++)
        {
          if (_wateredNeighbors.TryGetValue(tile + Neighbors4[i], out int neighborCount))
          {
            if (neighborCount > maxCount) maxCount = neighborCount - 1;
          }
        }
        _clusterSaturations[tile] = Mathf.Min(MaxClusterSaturation, maxCount);
      }

      foreach (Vector3Int waterTile in _waterPlannedArea.Area)
      {
        float initialEnergy = _clusterSaturations[waterTile.XY()] * EnergyPerSaturation;
        _queue.Enqueue(waterTile);
        _bestEnergyAtTile[waterTile] = initialEnergy;
      }

      while (_queue.Count > 0)
      {
        Vector3Int currentPos = _queue.Dequeue();
        float currentEnergy = _bestEnergyAtTile[currentPos];
        bool isCurrentWater = _waterPlannedArea.Contains(currentPos);

        for (int i = 0; i < SpreadCosts.Length; i++)
        {
          var spread = SpreadCosts[i];
          Vector2Int neighbor2D = currentPos.XY() + spread.offset;

          if (!_terrainService.Contains(neighbor2D)) continue;

          foreach (Vector3Int neighborPos in _terrainService.GetAllHeightsInCell(neighbor2D))
          {
            int neighborHeight = neighborPos.z;

            float baseCost = spread.cost;
            if (isCurrentWater && spread.isOrthogonal) baseCost = 0f;

            float climbPenalty = 0f;
            int heightDiff = neighborHeight - currentPos.z;
            if (isCurrentWater)
            {
              int waterEffectiveHeight = currentPos.z + 1;
              heightDiff = neighborHeight - waterEffectiveHeight;
            }

            if (heightDiff > 0) climbPenalty = heightDiff * VerticalSpreadCostMultiplier;

            float totalCost = baseCost + climbPenalty;
            float newEnergy = currentEnergy - totalCost;

            if (newEnergy >= MinimumMoisture)
            {
              if (!_bestEnergyAtTile.TryGetValue(neighborPos, out float existingEnergy) || newEnergy > existingEnergy)
              {
                _bestEnergyAtTile[neighborPos] = newEnergy;
                _queue.Enqueue(neighborPos);

                if (!_waterPlannedArea.Contains(neighborPos))
                {
                  _moistTiles.Add(neighborPos);
                }
              }
            }
          }
        }
      }

      _eventBus.Post(new MoistureSpreadChangedEvent());
    }
  }

  // =========================================================================
  // MOISTURE VISUALIZER
  // =========================================================================
  public class MoistureSpreadVisualizer : ILoadableSingleton
  {
    private readonly EventBus _eventBus;
    private readonly WaterSpreadSimulator _waterSpreadSimulator;
    private readonly AreaTileDrawerFactory _areaTileDrawerFactory;
    private readonly RootObjectProvider _rootObjectProvider;
    private readonly ILevelVisibilityService _levelVisibilityService;

    private AreaTileDrawer _areaTileDrawer;
    private GameObject _parent;

    public MoistureSpreadVisualizer(EventBus eventBus, WaterSpreadSimulator waterSpreadSimulator, AreaTileDrawerFactory areaTileDrawerFactory, RootObjectProvider rootObjectProvider, ILevelVisibilityService levelVisibilityService)
    {
      _eventBus = eventBus;
      _waterSpreadSimulator = waterSpreadSimulator;
      _areaTileDrawerFactory = areaTileDrawerFactory;
      _rootObjectProvider = rootObjectProvider;
      _levelVisibilityService = levelVisibilityService;
    }

    public void Load()
    {
      _parent = _rootObjectProvider.CreateRootObject("MoistureSpreadVisualizer");
      _levelVisibilityService.MaxVisibleLevelChanged += OnMaxVisibleLevelChanged;

      Color greenColor = new Color(0.2f, 0.8f, 0.2f, 0.9f);
      _areaTileDrawer = _areaTileDrawerFactory.Create(greenColor, _parent);

      _eventBus.Register(this);
      UpdateArea();
    }

    [OnEvent] public void OnMoistureSpreadChanged(MoistureSpreadChangedEvent e) => UpdateArea();
    private void OnMaxVisibleLevelChanged(object sender, int e) => UpdateArea();

    private void UpdateArea()
    {
      _areaTileDrawer.UpdateArea(_waterSpreadSimulator.MoistTiles.Where(coords => coords.z <= _levelVisibilityService.MaxVisibleLevel));
      _areaTileDrawer.ShowAllTiles();
    }
  }
}