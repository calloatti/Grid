using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Timberborn.PlatformUtilities;
using UnityEngine;

namespace Calloatti.Grid
{
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

      GridColorHex = ModStarter.Config.GetString("GridTerrainColorHex");
      BuildingGridColorHex = ModStarter.Config.GetString("GridBuildingColorHex");
      HighlightColorHex = ModStarter.Config.GetString("GridHighlightColorHex");

      InitializeColors();
    }
  }

  public partial class GridService
  {
    public GridSettings Settings { get; private set; } = new GridSettings();

    private void EnsureSettingsLoaded()
    {
      try
      {
        Settings.LoadFromSimpleConfig();
      }
      catch (Exception e)
      {
        Debug.LogError($"{GridConfigurator.Prefix} Failed to load settings from SimpleConfig: {e.Message}");
      }
    }

    public void ReloadSettings()
    {
      EnsureSettingsLoaded();
      InitializeMaterials();
    }

    public GridSettings ReadConfigFile()
    {
      ReloadSettings();
      return Settings;
    }
  }
}