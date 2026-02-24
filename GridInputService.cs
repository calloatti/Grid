using System;
using Bindito.Core;
using Timberborn.InputSystem;
using Timberborn.SingletonSystem;
using UnityEngine; // Añadido para Debug.Log

namespace Calloatti.Grid
{
  public class GridInputService : ILoadableSingleton, IInputProcessor
  {
    private readonly InputService _inputService;

    // Evento al que se suscribirá el Renderer
    public event Action OnToggleGrid;

    [Inject]
    public GridInputService(InputService inputService)
    {
      _inputService = inputService;
      Debug.Log($"{GridConfigurator.Prefix} GridInputService Inyectado.");
    }

    public void Load()
    {
      // Registramos esta clase para que Timberborn empiece a enviarle los inputs
      Debug.Log($"{GridConfigurator.Prefix} Registrando GridInputService como InputProcessor.");
      _inputService.AddInputProcessor(this);
    }

    public bool ProcessInput()
    {
      // Timberborn llama a este método automáticamente
      if (_inputService.IsKeyDown("Grid.Toggle"))
      {
        Debug.Log($"{GridConfigurator.Prefix} Tecla 'Grid.Toggle' detectada. Disparando evento OnToggleGrid.");
        OnToggleGrid?.Invoke();
      }

      return false; // Retornamos false para no bloquear otros posibles usos de esta tecla
    }
  }
}