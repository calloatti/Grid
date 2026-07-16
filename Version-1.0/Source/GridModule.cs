using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using Timberborn.InputSystem;
using Timberborn.Modding;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  // =========================================================================
  // 1. CONFIGURATOR
  // =========================================================================
  [Context("Game")]
  [Context("MapEditor")]
  public class GridConfigurator : Configurator
  {
    public const string Prefix = "[Grid]";

    protected override void Configure()
    {
      Debug.Log($"{Prefix} Initializing mod and binding dependencies...");

      Bind<GridInputService>().AsSingleton();
      Bind<GridService>().AsSingleton();

      Debug.Log($"{Prefix} Configuration completed successfully.");
    }
  }

  // =========================================================================
  // 2. INPUT SERVICE
  // =========================================================================
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
        return true;
      }

      if (_inputService.IsKeyDown("Calloatti.Grid.KeyBind.Toggle.Grid.Buildings"))
      {
        OnToggleBuildingGrid?.Invoke();
        return true;
      }

      return false;
    }
  }

  // =========================================================================
  // 3. SETTINGS DATA
  // =========================================================================
  [Serializable]
  public class GridSettings
  {
    public float NormalVerticalOffset = 0.0f;
    public float SlicedVerticalOffset = 0.0f;
    public float HorizontalOffsetEW = 0.0f;
    public float HorizontalOffsetNS = 0.0f;

    public bool HighlightEnabled = false;
    public int HighlightIntervalX = 10;
    public int HighlightIntervalY = 10;

    public int HighlightWidthX = 0;
    public int HighlightWidthY = 0;

    public string GridColorHex = "#00000066";
    public string BuildingGridColorHex = "#00F2F266";
    public string HighlightColorHex = "#FF000066";

    public List<string> MarkerPaletteHex = new List<string>
    {
        "#FF8C00", "#0073FF", "#1AE61A", "#F21A80",
        "#FFF200", "#00F2F2", "#9933FF", "#FFFFFF"
    };

    [NonSerialized] public Color GridColor;
    [NonSerialized] public Color BuildingGridColor;
    [NonSerialized] public Color HighlightColor;
    [NonSerialized] public List<Color> MarkerPalette = new List<Color>();

    public void InitializeColors()
    {
      GridColor = HexToColor(GridColorHex);
      BuildingGridColor = HexToColor(BuildingGridColorHex);
      HighlightColor = HexToColor(HighlightColorHex);
      MarkerPalette = MarkerPaletteHex.Select(HexToColor).ToList();
    }

    private Color HexToColor(string hex)
    {
      if (ColorUtility.TryParseHtmlString(hex, out Color color))
        return color;
      return Color.white;
    }

    public void LoadFromSimpleConfig()
    {
      if (ModStarter.Config == null) return;

      NormalVerticalOffset = ModStarter.Config.GetFloat("GridNormalVerticalOffset");
      SlicedVerticalOffset = ModStarter.Config.GetFloat("GridSlicedVerticalOffset");
      HorizontalOffsetEW = ModStarter.Config.GetFloat("GridHorizontalOffsetEW");
      HorizontalOffsetNS = ModStarter.Config.GetFloat("GridHorizontalOffsetNS");

      HighlightEnabled = ModStarter.Config.GetBool("GridHighlightEnabled");
      HighlightIntervalX = ModStarter.Config.GetInt("GridHighlightIntervalX");
      HighlightIntervalY = ModStarter.Config.GetInt("GridHighlightIntervalY");

      HighlightWidthX = ModStarter.Config.GetInt("GridHighlightWidthX");
      HighlightWidthY = ModStarter.Config.GetInt("GridHighlightWidthY");

      GridColorHex = ModStarter.Config.GetString("GridTerrainColorHex");
      BuildingGridColorHex = ModStarter.Config.GetString("GridBuildingColorHex");
      HighlightColorHex = ModStarter.Config.GetString("GridHighlightColorHex");

      InitializeColors();
    }
  }
}