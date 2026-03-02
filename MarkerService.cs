using System.Collections.Generic;
using Timberborn.TerrainSystem;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.LevelVisibilitySystem;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;
using Timberborn.Modding;
using Bindito.Core;
using UnityEngine;

namespace Calloatti.Grid
{
  public partial class MarkerService : ILoadableSingleton, IPostLoadableSingleton, ISaveableSingleton
  {
    public static MarkerService Instance { get; private set; }

    private readonly ITerrainService _terrainService;
    private readonly IBlockService _blockService;
    private readonly ILevelVisibilityService _levelVisibilityService;
    private readonly EventBus _eventBus;
    private readonly ISingletonLoader _singletonLoader;
    private readonly ModRepository _modRepository;

    private readonly Dictionary<Vector2Int, MarkerData> _activeMarkers = new Dictionary<Vector2Int, MarkerData>();

    [Inject]
    public MarkerService(
        ITerrainService terrainService,
        IBlockService blockService,
        ILevelVisibilityService levelVisibilityService,
        EventBus eventBus,
        ISingletonLoader singletonLoader,
        ModRepository modRepository)
    {
      _terrainService = terrainService;
      _blockService = blockService;
      _levelVisibilityService = levelVisibilityService;
      _eventBus = eventBus;
      _singletonLoader = singletonLoader;
      _modRepository = modRepository;
      Instance = this;
    }

    public void Load()
    {
      EnsureSettingsLoaded(); // Loads the markers.json config
      LoadState();            // Loads the saved markers from the save file
    }

    // =========================================================
    // COMANDOS PÚBLICOS
    // =========================================================

    public void Interact(Vector3Int columnCoords, int colorIndex)
    {
      Vector2Int col = new Vector2Int(columnCoords.x, columnCoords.y);
      if (_activeMarkers.ContainsKey(col)) ChangeColor(col);
      else AddMarker(col, colorIndex);
    }

    public void DeleteMarker(Vector3Int columnCoords)
    {
      Vector2Int col = new Vector2Int(columnCoords.x, columnCoords.y);
      if (_activeMarkers.ContainsKey(col)) RemoveColumn(col);
    }

    // =========================================================
    // EVENT LISTENERS
    // =========================================================

    [OnEvent]
    public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent maxVisibleLevelChangedEvent)
    {
      foreach (var col in _activeMarkers.Keys) UpdateMarkerVisuals(col);
    }

    [OnEvent]
    public void OnBlockObjectSet(BlockObjectSetEvent e)
    {
      HashSet<Vector2Int> columns = new HashSet<Vector2Int>();
      foreach (var coords in e.BlockObject.PositionedBlocks.GetAllCoordinates())
        columns.Add(new Vector2Int(coords.x, coords.y));
      foreach (var col in columns) CheckBlockChange(col);
    }

    [OnEvent]
    public void OnBlockObjectUnset(BlockObjectUnsetEvent e)
    {
      HashSet<Vector2Int> columns = new HashSet<Vector2Int>();
      foreach (var coords in e.BlockObject.PositionedBlocks.GetAllCoordinates())
        columns.Add(new Vector2Int(coords.x, coords.y));
      foreach (var col in columns) CheckBlockChange(col);
    }

    private void CheckBlockChange(Vector2Int col)
    {
      if (_activeMarkers.TryGetValue(col, out MarkerData data))
      {
        int color = data.ColorIndex;
        RemoveColumn(col);
        AddMarker(col, color);
      }
    }

    private void OnTerrainHeightChanged(object sender, TerrainHeightChangeEventArgs e)
    {
      CheckBlockChange(new Vector2Int(e.Change.Coordinates.x, e.Change.Coordinates.y));
    }

    // =========================================================
    // DATA STRUCTURES
    // =========================================================

    private struct SurfaceData
    {
      public int RoofZ;
      public int RequiredMaxV;
    }

    private class MarkerData
    {
      public GameObject Container;
      public int ColorIndex;
      public List<GameObject> VisualPairs;
      public List<SurfaceData> Surfaces;
    }
  }
}