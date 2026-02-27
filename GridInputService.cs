using System;
using Bindito.Core;
using Timberborn.InputSystem;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  // ILoadableSingleton ensures Load() is called during early game initialization.
  // IInputProcessor allows this class to hook into Timberborn's native input event loop.
  public class GridInputService : ILoadableSingleton, IInputProcessor
  {
    private readonly InputService _inputService;

    // Event triggered when the toggle hotkey is pressed, consumed by GridRenderer.
    public event Action OnToggleTerrainGrid;

    [Inject]
    public GridInputService(InputService inputService)
    {
      _inputService = inputService;
    }

    public void Load()
    {
      // Register this class to listen for input events every frame.
      _inputService.AddInputProcessor(this);
      Debug.Log($"{GridConfigurator.Prefix} Input service loaded and listening for hotkeys.");
    }

    public bool ProcessInput()
    {
      // 1. Detect the custom keybind assigned to the Terrain Grid toggle.
      if (_inputService.IsKeyDown("Calloatti.Grid.Terrain.Toggle"))
      {
        OnToggleTerrainGrid?.Invoke();
        return false; // Return false to allow other systems to process inputs if needed.
      }

      return false;
    }
  }
}