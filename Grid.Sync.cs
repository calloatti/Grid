using System;
using Timberborn.LevelVisibilitySystem;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  public partial class GridRenderer
  {
    [OnEvent]
    public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent maxVisibleLevelChangedEvent)
    {
      // Sincronización para la TERRAIN GRID
      UpdateVisibleLevels();
    }
  }
}