using System.Collections.Generic;
using System.Linq;
using Timberborn.LevelVisibilitySystem;
using Timberborn.Rendering;
using Timberborn.RootProviders;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace Calloatti.Grid
{
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

      Color blueColor = new Color(0.1f, 0.2f, 0.9f, 0.9f); // Deep, solid blue
      _areaTileDrawer = _areaTileDrawerFactory.Create(blueColor, _parent);

      _eventBus.Register(this);
      UpdateArea();
    }

    [OnEvent]
    public void OnWaterPlannedAreaChanged(WaterPlannedAreaChangedEvent e) => UpdateArea();
    private void OnMaxVisibleLevelChanged(object sender, int e) => UpdateArea();

    private void UpdateArea()
    {
      _areaTileDrawer.UpdateArea(_waterPlannedArea.Area.Where(coords => coords.z <= _levelVisibilityService.MaxVisibleLevel));
      _areaTileDrawer.ShowAllTiles();
    }
  }

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

      Color greenColor = new Color(0.2f, 0.8f, 0.2f, 0.9f); // Vanilla-style green overlay
      _areaTileDrawer = _areaTileDrawerFactory.Create(greenColor, _parent);

      _eventBus.Register(this);
      UpdateArea();
    }

    [OnEvent]
    public void OnMoistureSpreadChanged(MoistureSpreadChangedEvent e) => UpdateArea();
    private void OnMaxVisibleLevelChanged(object sender, int e) => UpdateArea();

    private void UpdateArea()
    {
      _areaTileDrawer.UpdateArea(_waterSpreadSimulator.MoistTiles.Where(coords => coords.z <= _levelVisibilityService.MaxVisibleLevel));
      _areaTileDrawer.ShowAllTiles();
    }
  }
}