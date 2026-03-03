using Bindito.Core;
using Calloatti.Grid;
using System;
using Timberborn.InputSystem;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace TimberbornModding.TopoData
{
  public class TopoInputService : ILoadableSingleton, IInputProcessor
  {
    private readonly InputService _inputService;

    public event Action OnToggleTopoData;

    [Inject]
    public TopoInputService(InputService inputService)
    {
      _inputService = inputService;
    }

    public void Load()
    {
      _inputService.AddInputProcessor(this);
      Debug.Log("[GRID.TOPO] Input service loaded and listening for hotkeys.");
    }

    public bool ProcessInput()
    {
      // Ensure you register this specific keybinding in your mod's config!
      if (_inputService.IsKeyDown("Calloatti.Grid.KeyBind.Toggle.Topo"))
      {
        Debug.Log("[GRID.TOPO] HOTKEY DETECTED.");
        OnToggleTopoData?.Invoke();
        return false;
      }
      return false;
    }
  }
}