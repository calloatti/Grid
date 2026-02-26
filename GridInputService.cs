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

    [Inject]
    public GridInputService(InputService inputService)
    {
      _inputService = inputService;
    }

    public void Load()
    {
      _inputService.AddInputProcessor(this);
    }

    public bool ProcessInput()
    {
      // 1. Detectar la nueva tecla asignada al Terrain Grid
      if (_inputService.IsKeyDown("Calloatti.Grid.Terrain.Toggle"))
      {
        Debug.Log($"{GridConfigurator.Prefix} Tecla 'GridTerrain.Toggle' detectada. Disparando Terrain Grid.");
        OnToggleTerrainGrid?.Invoke();
        return false;
      }

      return false;
    }
  }
}