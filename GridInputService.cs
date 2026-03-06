using System;
using Bindito.Core;
using Timberborn.InputSystem;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  public class GridInputService : ILoadableSingleton, IInputProcessor
  {
    private readonly InputService _inputService;

    public event Action OnToggleTerrainGrid;
    public event Action OnToggleBuildingGrid;

    [Inject]
    public GridInputService(InputService inputService)
    {
      _inputService = inputService;
    }

    public void Load()
    {
      _inputService.AddInputProcessor(this);
      Debug.Log($"{GridConfigurator.Prefix} Input service loaded and listening for hotkeys.");
    }

    public bool ProcessInput()
    {
      if (_inputService.IsKeyDown("Calloatti.Grid.KeyBind.Toggle.Grid"))
      {
        OnToggleTerrainGrid?.Invoke();
        return false;
      }

      if (_inputService.IsKeyDown("Calloatti.Grid.KeyBind.Toggle.Grid.Buildings"))
      {
        OnToggleBuildingGrid?.Invoke();
        return false;
      }

      return false;
    }
  }
}