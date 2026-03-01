using System.Collections.Generic;
using Timberborn.Persistence;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace Calloatti.Grid
{
  public partial class MarkerService
  {
    private static readonly SingletonKey MarkersKey = new SingletonKey("Calloatti.Grid.Markers");
    private static readonly ListKey<int> XsKey = new ListKey<int>("Xs");
    private static readonly ListKey<int> YsKey = new ListKey<int>("Ys");
    private static readonly ListKey<int> ColorsKey = new ListKey<int>("Colors");

    private List<int> _loadedXs;
    private List<int> _loadedYs;
    private List<int> _loadedColors;

    public void Load()
    {
      if (_singletonLoader.TryGetSingleton(MarkersKey, out IObjectLoader objectLoader) && objectLoader.Has(XsKey))
      {
        _loadedXs = objectLoader.Get(XsKey);
        _loadedYs = objectLoader.Get(YsKey);
        _loadedColors = objectLoader.Get(ColorsKey);
      }
    }

    public void Save(ISingletonSaver saver)
    {
      IObjectSaver objectSaver = saver.GetSingleton(MarkersKey);

      List<int> xs = new List<int>(_activeMarkers.Count);
      List<int> ys = new List<int>(_activeMarkers.Count);
      List<int> colors = new List<int>(_activeMarkers.Count);

      foreach (var kvp in _activeMarkers)
      {
        xs.Add(kvp.Key.x);
        ys.Add(kvp.Key.y);
        colors.Add(kvp.Value.ColorIndex);
      }

      objectSaver.Set(XsKey, xs);
      objectSaver.Set(YsKey, ys);
      objectSaver.Set(ColorsKey, colors);
    }

    public void PostLoad()
    {
      _eventBus.Register(this);
      _terrainService.TerrainHeightChanged += OnTerrainHeightChanged;
      _palette = _gridRenderer.ReadConfigFile().MarkerPalette;

      if (_loadedXs != null)
      {
        for (int i = 0; i < _loadedXs.Count; i++)
        {
          AddMarker(new Vector2Int(_loadedXs[i], _loadedYs[i]), _loadedColors[i]);
        }

        _loadedXs = null;
        _loadedYs = null;
        _loadedColors = null;
      }
    }
  }
}