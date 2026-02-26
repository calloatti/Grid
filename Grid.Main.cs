using Bindito.Core;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.LevelVisibilitySystem;
using UnityEngine;

namespace Calloatti.Grid
{
  public partial class GridRenderer : IPostLoadableSingleton
  {
    private readonly EventBus _eventBus;
    private readonly GridInputService _gridInputService;
    private readonly ITerrainService _terrainService;
    private readonly ILevelVisibilityService _levelVisibilityService;

    [Inject]
    public GridRenderer(
        EventBus eventBus,
        GridInputService gridInputService,
        ITerrainService terrainService,
        ILevelVisibilityService levelVisibilityService)
    {
      _eventBus = eventBus;
      _gridInputService = gridInputService;
      _terrainService = terrainService;
      _levelVisibilityService = levelVisibilityService;
    }

    public void PostLoad()
    {
      // Registra eventos como OnMaxVisibleLevelChanged
      _eventBus.Register(this);

      // Suscripción a los eventos del InputService
      _gridInputService.OnToggleTerrainGrid += ToggleTerrainGrid;
    }
  }
}