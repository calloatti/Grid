using Bindito.Core;
using Timberborn.InputSystem;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  // =========================================================================
  // 1. CONFIGURATOR
  // =========================================================================
  [Context("Game")]
  [Context("MapEditor")]
  public class MarkerConfigurator : Configurator
  {
    protected override void Configure()
    {
      // Core Marker Logic
      Bind<MarkerService>().AsSingleton();

      // Marker Input Handling (Toggle Hotkey)
      Bind<MarkerInputService>().AsSingleton();

      // Tools (Note: Renamed to MarkerToolDeleteAll)
      Bind<MarkerToolDeleteAll>().AsSingleton();
      Bind<MarkerToolClear>().AsSingleton();
    }
  }

  // =========================================================================
  // 2. INPUT SERVICE
  // =========================================================================
  public class MarkerInputService : ILoadableSingleton, IInputProcessor
  {
    private readonly InputService _inputService;

    [Inject]
    public MarkerInputService(InputService inputService)
    {
      _inputService = inputService;
    }

    public void Load()
    {
      _inputService.AddInputProcessor(this);
    }

    public bool ProcessInput()
    {
      if (_inputService.IsKeyDown("Calloatti.Grid.KeyBind.Toggle.Markers"))
      {
        MarkerService.Instance.ToggleMarkers();
        return true;
      }
      return false;
    }
  }

  // =========================================================================
  // 3. FIXED COLOR PALETTE
  // =========================================================================
  public static class MarkerPalette
  {
    public static readonly Color[] Colors =
    {
      new Color(1.00f, 0.55f, 0.00f), // #FF8C00
      new Color(0.00f, 0.45f, 1.00f), // #0073FF
      new Color(0.10f, 0.90f, 0.10f), // #1AE61A
      new Color(0.95f, 0.10f, 0.50f), // #F21A80
      new Color(1.00f, 0.95f, 0.00f), // #FFF200
      new Color(0.00f, 0.95f, 0.95f), // #00F2F2
      new Color(0.60f, 0.20f, 1.00f), // #9933FF
      new Color(1.00f, 1.00f, 1.00f)  // #FFFFFF
    };
  }
}