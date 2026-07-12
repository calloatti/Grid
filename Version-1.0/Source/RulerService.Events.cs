using Bindito.Core;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BlockSystem;
using Timberborn.LevelVisibilitySystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  public partial class RulerService
  {
    [OnEvent] public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent e) { UpdateRulersVisuals(); }
    [OnEvent] public void OnBlockObjectSet(BlockObjectSetEvent e) { CheckBlockChange(e.BlockObject); }
    [OnEvent] public void OnBlockObjectUnset(BlockObjectUnsetEvent e) { CheckBlockChange(e.BlockObject); }

    private void CheckBlockChange(BlockObject bo)
    {
      HashSet<Vector2Int> affected = new HashSet<Vector2Int>();
      foreach (var c in bo.PositionedBlocks.GetAllCoordinates())
      {
        Vector2Int col = new Vector2Int(c.x, c.y);
        if (_segmentMap.ContainsKey(col) && _segmentMap[col].Count > 0) affected.Add(col);
      }
      if (affected.Count == 0) return;
      int maxV = _levelVisibilityService.MaxVisibleLevel;
      foreach (var col in affected)
      {
        if (_sharedQuads.ContainsKey(col)) UpdateQuadHeight(_sharedQuads[col], col, maxV, 0, 0);
        foreach (var seg in _segmentMap[col]) UpdateQuadHeight(seg.Obj, col, maxV, seg.Ruler.RulerType, seg.Value);
      }
    }

    private void OnTerrainHeightChanged(object s, TerrainHeightChangeEventArgs e)
    {
      Vector2Int col = new Vector2Int(e.Change.Coordinates.x, e.Change.Coordinates.y);
      if (!_segmentMap.ContainsKey(col) || _segmentMap[col].Count == 0) return;
      int maxV = _levelVisibilityService.MaxVisibleLevel;
      if (_sharedQuads.ContainsKey(col)) UpdateQuadHeight(_sharedQuads[col], col, maxV, 0, 0);
      foreach (var seg in _segmentMap[col]) UpdateQuadHeight(seg.Obj, col, maxV, seg.Ruler.RulerType, seg.Value);
    }
  }
}