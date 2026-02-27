using System;
using Timberborn.LevelVisibilitySystem;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  public partial class GridRenderer
  {
    // [OnEvent] attribute tells the EventBus to route this specific event here automatically.
    // Triggers whenever the player changes the camera's Z-level slice view.
    [OnEvent]
    public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent maxVisibleLevelChangedEvent)
    {
      // Synchronize the terrain grid visibility with the new maximum visible level.
      UpdateVisibleLevels();
    }
  }
}