using System;
using Bindito.Core;
using Timberborn.InputSystem;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace Calloatti.TopoData
{
  // =========================================================================
  // 1. CONFIGURATOR
  // =========================================================================
  [Context("Game")]
  [Context("MapEditor")]
  public class TopoConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<TopoInputService>().AsSingleton();
      Bind<TopoService>().AsSingleton();
    }
  }

  // =========================================================================
  // 2. INPUT SERVICE
  // =========================================================================
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