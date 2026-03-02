using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Timberborn.PlatformUtilities;
using UnityEngine;

namespace Calloatti.Grid
{
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

  public partial class MarkerService
  {
    private const string ModId = "Calloatti.Grid";
    public MarkerSettings Settings { get; private set; } = new MarkerSettings();

    private string GetConfigFilePath()
    {
      string localModsFolder = Path.Combine(UserDataFolder.Folder, "Mods");
      string actualModPath = _modRepository.Mods.FirstOrDefault(m => m.Manifest.Id == ModId)?.ModDirectory.Path;

      if (string.IsNullOrEmpty(actualModPath))
      {
        string fallback = Path.Combine(localModsFolder, "Grid");
        if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback);
        return Path.Combine(fallback, "markers.json");
      }

      string normalizedLocalMods = Path.GetFullPath(localModsFolder).Replace('\\', '/').TrimEnd('/');
      string normalizedModPath = Path.GetFullPath(actualModPath).Replace('\\', '/').TrimEnd('/');

      if (normalizedModPath.StartsWith(normalizedLocalMods, StringComparison.InvariantCultureIgnoreCase))
      {
        return Path.Combine(actualModPath, "markers.json");
      }
      else
      {
        string workshopConfigFolder = Path.Combine(localModsFolder, "Grid");
        if (!Directory.Exists(workshopConfigFolder)) Directory.CreateDirectory(workshopConfigFolder);
        return Path.Combine(workshopConfigFolder, "markers.json");
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
          Debug.Log($"[Grid] Created user-friendly config: {filePath}");
        }

        Settings.InitializeColors();
      }
      catch (Exception e)
      {
        Debug.LogError($"[Grid] Failed to handle markers.json: {e.Message}");
      }
    }

    public void ReloadSettings()
    {
      EnsureSettingsLoaded();
    }
  }
}