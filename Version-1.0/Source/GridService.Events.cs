using System;
using Timberborn.LevelVisibilitySystem;
using Timberborn.SingletonSystem;
using Timberborn.BlockSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  public partial class GridService
  {
    [OnEvent]
    public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent maxVisibleLevelChangedEvent)
    {
      UpdateVisibleLevels();
    }

    private void OnTerrainHeightChanged(object sender, Timberborn.TerrainSystem.TerrainHeightChangeEventArgs e)
    {
      if (_isTerrainCache == null) return;
      Timberborn.TerrainSystem.TerrainHeightChange change = e.Change;
      int x = change.Coordinates.x;
      int y = change.Coordinates.y;

      for (int z = 0; z < _mapMaxZ; z++)
      {
        _isTerrainCache[x, y, z] = _terrainService.Underground(new Vector3Int(x, y, z));
      }

      int minZ = Math.Max(0, change.From - 1);
      int maxZ = Math.Min(_mapMaxZ - 1, change.To);

      for (int z = minZ; z <= maxZ; z++)
      {
        _dirtyLevels.Add(z);
      }
    }

    [OnEvent]
    public void OnBlockObjectSet(BlockObjectSetEvent e) { ProcessBlockObjectChange(e.BlockObject); }

    [OnEvent]
    public void OnBlockObjectUnset(BlockObjectUnsetEvent e) { ProcessBlockObjectChange(e.BlockObject); }

    private void ProcessBlockObjectChange(BlockObject bo)
    {
      if (_isBuildingCache == null || bo == null || bo.PositionedBlocks == null) return;

      foreach (var coords in bo.PositionedBlocks.GetAllCoordinates())
      {
        int x = coords.x; int y = coords.y; int z = coords.z;
        if (x >= 0 && x < _mapSizeX && y >= 0 && y < _mapSizeY && z >= 0 && z < _mapMaxZ)
        {
          _isBuildingCache[x, y, z] = CheckIfBuildingBlock(new Vector3Int(x, y, z));
          _dirtyLevels.Add(z);
        }
      }
    }
  }
}