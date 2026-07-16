using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bindito.Core;
using Timberborn.InputSystem;
using Timberborn.Modding;
using Timberborn.PlatformUtilities;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  // =========================================================================
  // 1. CONFIGURATOR
  // =========================================================================
  [Context("Game")]
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
  // 3. SETTINGS DATA
  // =========================================================================
  [Serializable]
  public class MarkerSettings
  {
    public List<string> MarkerPaletteHex = new List<string>
    {
        "#FF8C00", "#0073FF", "#1AE61A", "#F21A80",
        "#FFF200", "#00F2F2", "#9933FF", "#FFFFFF"
    };

    [NonSerialized] public List<Color> MarkerPalette = new List<Color>();

    public void InitializeColors()
    {
      MarkerPalette = MarkerPaletteHex.Select(HexToColor).ToList();
    }

    private Color HexToColor(string hex)
    {
      if (ColorUtility.TryParseHtmlString(hex, out Color color))
        return color;
      return Color.white;
    }
  }
}