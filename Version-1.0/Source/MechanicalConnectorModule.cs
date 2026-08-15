using System;
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
  public class MechanicalConnectorConfigurator : Configurator
  {
    public const string Prefix = "[MechanicalConnectors]";

    protected override void Configure()
    {
      Debug.Log($"{Prefix} Initializing mod and binding dependencies...");

      Bind<MechanicalConnectorInputService>().AsSingleton();
      Bind<MechanicalConnectorService>().AsSingleton();

      Debug.Log($"{Prefix} Configuration completed successfully.");
    }
  }

  // =========================================================================
  // 1b. SETTINGS
  // =========================================================================
  public class MechanicalConnectorSettings
  {
    public string MarkerColorHex = "#4D80CC80";
    public string FloorColorHex = "#8155A74D";

    [NonSerialized] public Color MarkerColor;
    [NonSerialized] public Color FloorColor;

    public void InitializeColors()
    {
      MarkerColor = HexToColor(MarkerColorHex);
      FloorColor = HexToColor(FloorColorHex);
    }

    public void LoadFromSimpleConfig()
    {
      if (ModStarter.Config == null) return;

      MarkerColorHex = ModStarter.Config.GetString("MechanicalMarkerColorHex");
      FloorColorHex = ModStarter.Config.GetString("MechanicalFloorColorHex");

      InitializeColors();
    }

    private Color HexToColor(string hex)
    {
      if (ColorUtility.TryParseHtmlString(hex, out Color color))
        return color;
      return Color.white;
    }
  }

  // =========================================================================
  // 2. INPUT SERVICE
  // =========================================================================
  public class MechanicalConnectorInputService : ILoadableSingleton, IInputProcessor
  {
    private readonly InputService _inputService;

    public event Action OnToggleConnectors;

    [Inject]
    public MechanicalConnectorInputService(InputService inputService)
    {
      _inputService = inputService;
    }

    public void Load()
    {
      _inputService.AddInputProcessor(this);
      Debug.Log($"{MechanicalConnectorConfigurator.Prefix} Input service loaded and listening for hotkeys.");
    }

    public bool ProcessInput()
    {
      if (_inputService.IsKeyDown("Calloatti.Grid.KeyBind.Toggle.Connectors"))
      {
        OnToggleConnectors?.Invoke();
        return true;
      }

      return false;
    }
  }
}