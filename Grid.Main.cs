using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bindito.Core;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.LevelVisibilitySystem;
using Timberborn.PlatformUtilities;
using Timberborn.Modding;
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

    // Changed to strings for user-friendly Hex format in JSON
    public string GridColorHex = "#00000066";

    public List<string> MarkerPaletteHex = new List<string>
    {
        "#FF8C00", "#0073FF", "#1AE61A", "#F21A80",
        "#FFF200", "#00F2F2", "#9933FF", "#FFFFFF"
    };

    // Helper properties to get Unity Colors easily in other files
    [NonSerialized] public Color GridColor;
    [NonSerialized] public List<Color> MarkerPalette = new List<Color>();

    // Converts the Hex strings from JSON into usable Unity Colors
    public void InitializeColors()
    {
      GridColor = HexToColor(GridColorHex);
      MarkerPalette = MarkerPaletteHex.Select(HexToColor).ToList();
    }

    private Color HexToColor(string hex)
    {
      if (ColorUtility.TryParseHtmlString(hex, out Color color))
        return color;
      return Color.white;
    }
  }

  public partial class GridRenderer : ILoadableSingleton, IPostLoadableSingleton, ILateUpdatableSingleton
  {
    private const string ModId = "Calloatti.Grid";

    private readonly EventBus _eventBus;
    private readonly GridInputService _gridInputService;
    private readonly ITerrainService _terrainService;
    private readonly ILevelVisibilityService _levelVisibilityService;
    private readonly ModRepository _modRepository;

    public GridSettings Settings { get; private set; } = new GridSettings();

    [Inject]
    public GridRenderer(
        EventBus eventBus,
        GridInputService gridInputService,
        ITerrainService terrainService,
        ILevelVisibilityService levelVisibilityService,
        ModRepository modRepository)
    {
      _eventBus = eventBus;
      _gridInputService = gridInputService;
      _terrainService = terrainService;
      _levelVisibilityService = levelVisibilityService;
      _modRepository = modRepository;
    }

    public void Load()
    {
      EnsureSettingsLoaded();
    }

    public void PostLoad()
    {
      _eventBus.Register(this);
      _gridInputService.OnToggleTerrainGrid += ToggleTerrainGrid;
      _terrainService.TerrainHeightChanged += OnTerrainHeightChanged;
      Debug.Log($"{GridConfigurator.Prefix} GridRenderer loaded.");
    }

    public void LateUpdateSingleton()
    {
      if (_terrainGridRoot != null && _terrainGridRoot.activeSelf)
      {
        ProcessDirtyLevels();
      }
    }

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
          // Save default settings if file doesn't exist
          string json = JsonUtility.ToJson(Settings, true);
          File.WriteAllText(filePath, json);
          Debug.Log($"{GridConfigurator.Prefix} Created user-friendly config: {filePath}");
        }

        // Convert the Strings to actual Colors for the mod to use
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
    }

    // Centralized access for MarkerService
    public GridSettings ReadConfigFile()
    {
      ReloadSettings();
      return Settings;
    }
  }
}