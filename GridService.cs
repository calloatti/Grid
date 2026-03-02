using Bindito.Core;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.LevelVisibilitySystem;
using Timberborn.Modding;
using Timberborn.BlockSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  public partial class GridService : ILoadableSingleton, IPostLoadableSingleton, ILateUpdatableSingleton
  {
    private const string ModId = "Calloatti.Grid";

    private readonly EventBus _eventBus;
    private readonly GridInputService _gridInputService;
    private readonly ITerrainService _terrainService;
    private readonly ILevelVisibilityService _levelVisibilityService;
    private readonly ModRepository _modRepository;
    private readonly IBlockService _blockService;

    [Inject]
    public GridService(
        EventBus eventBus,
        GridInputService gridInputService,
        ITerrainService terrainService,
        ILevelVisibilityService levelVisibilityService,
        ModRepository modRepository,
        IBlockService blockService)
    {
      _eventBus = eventBus;
      _gridInputService = gridInputService;
      _terrainService = terrainService;
      _levelVisibilityService = levelVisibilityService;
      _modRepository = modRepository;
      _blockService = blockService;
    }

    public void Load()
    {
      EnsureSettingsLoaded();
      InitializeMaterials();
    }

    public void PostLoad()
    {
      _eventBus.Register(this);
      _gridInputService.OnToggleTerrainGrid += ToggleTerrainGrid;
      _terrainService.TerrainHeightChanged += OnTerrainHeightChanged;
      Debug.Log($"{GridConfigurator.Prefix} GridService loaded.");
    }

    public void LateUpdateSingleton()
    {
      if (_terrainGridRoot != null && _terrainGridRoot.activeSelf)
      {
        ProcessDirtyLevels();
      }
    }
  }
}