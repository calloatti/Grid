using System.Collections.Generic;
using Timberborn.Common;
using Timberborn.MapStateSystem;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace Calloatti.Grid
{
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
      foreach (var coordinate in coordinates)
      {
        _area.Add(coordinate);
      }
      _eventBus.Post(new WaterPlannedAreaChangedEvent());
    }

    public void RemoveCoordinates(IEnumerable<Vector3Int> coordinates)
    {
      foreach (var coordinate in coordinates)
      {
        _area.Remove(coordinate);
      }
      _eventBus.Post(new WaterPlannedAreaChangedEvent());
    }

    public void ClearAll()
    {
      _area.Clear();
      _eventBus.Post(new WaterPlannedAreaChangedEvent());
    }
  }
}