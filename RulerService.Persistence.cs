using Bindito.Core;
using System.Collections.Generic;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace Calloatti.Grid
{
  public partial class RulerService
  {
    private static readonly SingletonKey RulersKey = new SingletonKey("Calloatti.Grid.Rulers");
    private static readonly ListKey<Vector3Int> StartsKey = new ListKey<Vector3Int>("Starts");
    private static readonly ListKey<Vector3Int> EndsKey = new ListKey<Vector3Int>("Ends");
    private static readonly ListKey<Quaternion> RotationsKey = new ListKey<Quaternion>("Rotations");
    private static readonly ListKey<int> FlattenedValuesKey = new ListKey<int>("FlattenedValues");
    private static readonly ListKey<int> StatesKey = new ListKey<int>("States");

    private List<Vector3Int> _loadedStarts;
    private List<Vector3Int> _loadedEnds;
    private List<Quaternion> _loadedRotations;
    private List<int> _loadedFlattenedValues;
    private List<int> _loadedStates;

    public void Load()
    {
      if (_singletonLoader.TryGetSingleton(RulersKey, out IObjectLoader loader))
      {
        if (loader.Has(StartsKey))
        {
          _loadedStarts = loader.Get(StartsKey); _loadedEnds = loader.Get(EndsKey); _loadedRotations = loader.Get(RotationsKey);
          _loadedFlattenedValues = loader.Has(FlattenedValuesKey) ? loader.Get(FlattenedValuesKey) : null;
          _loadedStates = loader.Has(StatesKey) ? loader.Get(StatesKey) : null;
        }
      }
    }

    public void Save(ISingletonSaver saver)
    {
      IObjectSaver os = saver.GetSingleton(RulersKey);
      List<Vector3Int> s = new List<Vector3Int>(), e = new List<Vector3Int>();
      List<Quaternion> r = new List<Quaternion>(); List<int> f = new List<int>(), st = new List<int>();
      foreach (var ruler in _activeRulers)
      {
        s.Add(ruler.Start); e.Add(ruler.End); r.Add(ruler.Rotation); st.Add(ruler.RulerType); st.Add(ruler.Period); st.Add(ruler.GapSize);
        f.Add(ruler.Segments.Count); foreach (var seg in ruler.Segments) f.Add(seg.Value);
      }
      os.Set(StartsKey, s); os.Set(EndsKey, e); os.Set(RotationsKey, r); os.Set(FlattenedValuesKey, f); os.Set(StatesKey, st);
    }

    public void PostLoad()
    {
      _eventBus.Register(this); _terrainService.TerrainHeightChanged += OnTerrainHeightChanged;
      Texture2D tex = _assetLoader.Load<Texture2D>("Sprites/ruler-atlas");
      _rulerMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
      if (_loadedStarts != null)
      {
        int idx = 0; for (int i = 0; i < _loadedStarts.Count; i++)
        {
          List<int> vals = null; if (_loadedFlattenedValues != null) { int c = _loadedFlattenedValues[idx++]; vals = _loadedFlattenedValues.GetRange(idx, c); idx += c; }
          int t = 0, p = 0, g = 0; if (_loadedStates != null) { t = _loadedStates[i * 3]; p = _loadedStates[i * 3 + 1]; g = _loadedStates[i * 3 + 2]; }
          InternalFinalizeRuler(_loadedStarts[i], _loadedEnds[i], _loadedRotations[i], vals, t, p, g);
        }
      }
    }
  }
}