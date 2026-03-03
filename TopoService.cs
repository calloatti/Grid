using Bindito.Core;
using Timberborn.AssetSystem;
using Timberborn.LevelVisibilitySystem;
using Timberborn.MapIndexSystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.QuickNotificationSystem;
using UnityEngine;
using System;

namespace TimberbornModding.TopoData
{
  public partial class TopoService : ILoadableSingleton, IPostLoadableSingleton, IDisposable
  {
    private readonly ITerrainService _terrainService;
    private readonly ILevelVisibilityService _levelVisibilityService;
    private readonly MapIndexService _mapIndexService;
    private readonly IAssetLoader _assetLoader;
    private readonly EventBus _eventBus;
    private readonly TopoInputService _topoInputService;
    private readonly QuickNotificationService _notificationService;

    private bool _isActive;
    private bool _isDirty = true; // Initialized to true so first toggle builds the map
    private Material _topoMaterial;

    [Inject]
    public TopoService(
        ITerrainService terrainService,
        ILevelVisibilityService levelVisibilityService,
        MapIndexService mapIndexService,
        IAssetLoader assetLoader,
        EventBus eventBus,
        TopoInputService topoInputService,
        QuickNotificationService notificationService)
    {
      _terrainService = terrainService;
      _levelVisibilityService = levelVisibilityService;
      _mapIndexService = mapIndexService;
      _assetLoader = assetLoader;
      _eventBus = eventBus;
      _topoInputService = topoInputService;
      _notificationService = notificationService;
    }

    public void Load()
    {
      Debug.Log("[GRID.TOPO] TopoService loaded.");
      InitializeVisuals();
    }

    public void PostLoad()
    {
      _eventBus.Register(this);
      _terrainService.TerrainHeightChanged += OnTerrainHeightChanged;
      _topoInputService.OnToggleTopoData += ToggleTopoData;

      Texture2D tex = _assetLoader.Load<Texture2D>("Sprites/ruler-atlas");
      _topoMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
    }

    public void Dispose()
    {
      OnDispose();
    }
  }
}