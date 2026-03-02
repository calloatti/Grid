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

    public string GridColorHex = "#00000066";
    public string BuildingGridColorHex = "#00F2F266";

    public List<string> MarkerPaletteHex = new List<string>
    {
        "#FF8C00", "#0073FF", "#1AE61A", "#F21A80",
        "#FFF200", "#00F2F2", "#9933FF", "#FFFFFF"
    };

    [NonSerialized] public Color GridColor;
    [NonSerialized] public Color BuildingGridColor;
    [NonSerialized] public List<Color> MarkerPalette = new List<Color>();

    public void InitializeColors()
    {
      GridColor = HexToColor(GridColorHex);
      BuildingGridColor = HexToColor(BuildingGridColorHex);
      MarkerPalette = MarkerPaletteHex.Select(HexToColor).ToList();
    }

    private Color HexToColor(string hex)
    {
      if (ColorUtility.TryParseHtmlString(hex, out Color color))
        return color;
      return Color.white;
    }
  }

  public partial class GridService
  {
    public GridSettings Settings { get; private set; } = new GridSettings();

    private string GetConfigFilePath()
    {
      string localModsFolder = Path.Combine(UserDataFolder.Folder, "Mods");
      string actualModPath = _modRepository.Mods.FirstOrDefault(m => m.Manifest.Id == ModId)?.ModDirectory.Path;

      if (string.IsNullOrEmpty(actualModPath))
      {
        string fallback = Path.Combine(localModsFolder, "Grid");
        if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback);
        return Path.Combine(fallback, "grid.json");
      }

      string normalizedLocalMods = Path.GetFullPath(localModsFolder).Replace('\\', '/').TrimEnd('/');
      string normalizedModPath = Path.GetFullPath(actualModPath).Replace('\\', '/').TrimEnd('/');

      if (normalizedModPath.StartsWith(normalizedLocalMods, StringComparison.InvariantCultureIgnoreCase))
      {
        return Path.Combine(actualModPath, "grid.json");
      }
      else
      {
        string workshopConfigFolder = Path.Combine(localModsFolder, "Grid");
        if (!Directory.Exists(workshopConfigFolder)) Directory.CreateDirectory(workshopConfigFolder);
        return Path.Combine(workshopConfigFolder, "grid.json");
      }
    }

    private void EnsureSettingsLoaded()
    {
      try
      {
        string filePath = GetConfigFilePath();
        if (File.Exists(filePath))
        {
          string json = File.ReadAllText(filePath);
          JsonUtility.FromJsonOverwrite(json, Settings);
        }
        else
        {
          string json = JsonUtility.ToJson(Settings, true);
          File.WriteAllText(filePath, json);
          Debug.Log($"{GridConfigurator.Prefix} Created user-friendly config: {filePath}");
        }

        Settings.InitializeColors();
      }
      catch (Exception e)
      {
        Debug.LogError($"{GridConfigurator.Prefix} Failed to handle grid.json: {e.Message}");
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