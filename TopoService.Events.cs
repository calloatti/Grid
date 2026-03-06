using Bindito.Core;
using Timberborn.LevelVisibilitySystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using UnityEngine;

namespace Calloatti.TopoData
{
  public partial class TopoService
  {
    private void OnTerrainHeightChanged(object s, TerrainHeightChangeEventArgs e)
    {
      Vector2Int chunkIdx = new Vector2Int(e.Change.Coordinates.x / 16, e.Change.Coordinates.y / 16);

      if (_chunks.TryGetValue(chunkIdx, out var chunk))
      {
        chunk.IsDirty = true;

        if (_isActive)
        {
          GenerateChunkSnapshot(chunk);
          chunk.UpdateVisibility(true, _levelVisibilityService.MaxVisibleLevel);
          chunk.IsDirty = false;
        }
      }
      _isDirty = true;
    }

    [OnEvent]
    public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent e)
    {
      if (_isActive)
      {
        UpdateVisibility();
      }
    }
  }
}