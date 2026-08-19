using System;
using Bindito.Core;
using Timberborn.InputSystem;
using Timberborn.SingletonSystem;
using Timberborn.ModManagerScene;
using UnityEngine;

namespace Calloatti.Grid
{
  // =========================================================================
  // 1. CONFIGURATOR
  // =========================================================================
  [Context("Game")]
  [Context("MapEditor")]
  public class EvapConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<EvapInputService>().AsSingleton();
      Bind<EvapService>().AsSingleton();
    }
  }

  // =========================================================================
  // 2. INPUT SERVICE
  // =========================================================================
  public class EvapInputService : ILoadableSingleton, IInputProcessor
  {
    private readonly InputService _inputService;

    public event Action OnToggleEvapData;

    [Inject]
    public EvapInputService(InputService inputService)
    {
      _inputService = inputService;
    }

    public void Load()
    {
      _inputService.AddInputProcessor(this);
      Debug.Log("[GRID.EVAP] Input service loaded and listening for hotkeys.");
    }

    public bool ProcessInput()
    {
      if (_inputService.IsKeyDown("Calloatti.Grid.KeyBind.Toggle.Evap"))
      {
        Debug.Log("[GRID.EVAP] HOTKEY DETECTED.");
        OnToggleEvapData?.Invoke();
        return false;
      }
      return false;
    }
  }
}