using Bindito.Core;
using Timberborn.AssetSystem;
using Timberborn.LevelVisibilitySystem;
using Timberborn.MapIndexSystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.QuickNotificationSystem;
using Timberborn.CameraSystem;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace Calloatti.TopoData
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
    private readonly CameraService _cameraService;

    private bool _isActive;
    private bool _isDirty = true; // Initialized to true so first toggle builds the map
    private Material _topoMaterial;
    private Quaternion _lastRotation = Quaternion.identity;

    // Chunk Management
    private readonly Dictionary<Vector2Int, TopoChunk> _chunks = new Dictionary<Vector2Int, TopoChunk>();

    [Inject]
    public TopoService(
        ITerrainService terrainService,
        ILevelVisibilityService levelVisibilityService,
        MapIndexService mapIndexService,
        IAssetLoader assetLoader,
        EventBus eventBus,
        TopoInputService topoInputService,
        QuickNotificationService notificationService,
        CameraService cameraService)
    {
      _terrainService = terrainService;
      _levelVisibilityService = levelVisibilityService;
      _mapIndexService = mapIndexService;
      _assetLoader = assetLoader;
      _eventBus = eventBus;
      _topoInputService = topoInputService;
      _notificationService = notificationService;
      _cameraService = cameraService;
    }

    public void Load()
    {
      Debug.Log("[GRID.TOPO] TopoService loaded.");
      Texture2D tex = _assetLoader.Load<Texture2D>("Sprites/ruler-atlas");
      _topoMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
      InitializeVisuals();
    }

    public void PostLoad()
    {
      _eventBus.Register(this);
      _terrainService.TerrainHeightChanged += OnTerrainHeightChanged;
      _topoInputService.OnToggleTopoData += ToggleTopoData;
    }

    public void Dispose()
    {
      OnDispose();
    }
  }
}