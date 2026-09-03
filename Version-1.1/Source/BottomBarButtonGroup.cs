using System.Collections.Generic;
using System.IO;
using Timberborn.AreaSelectionSystemUI;
using Timberborn.AssetSystem;
using Timberborn.BottomBarSystem;
using Timberborn.CursorToolSystem;
using Timberborn.InputSystem;
using Timberborn.Localization;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.Grid
{
  public class BottomBarButtonGroup : IBottomBarElementsProvider
  {
    // --- CORE UI DEPENDENCIES ---
    private readonly ToolButtonFactory _toolButtonFactory;
    private readonly ToolGroupButtonFactory _toolGroupButtonFactory;
    private readonly ToolGroupService _toolGroupService;

    // --- SHARED TOOL DEPENDENCIES ---
    private readonly InputService _inputService;
    private readonly CursorCoordinatesPicker _cursorCoordinatesPicker;
    private readonly IAssetLoader _assetLoader;
    private readonly ILoc _loc;
    private readonly AreaHighlightingService _areaHighlightingService;
    private readonly EventBus _eventBus;

    // --- CUSTOM BACKGROUNDS ---
    private readonly Dictionary<ITool, (VisualElement Background, Sprite CustomSprite)> _customBackgrounds =
        new Dictionary<ITool, (VisualElement Background, Sprite CustomSprite)>();
    private Sprite _activeBackgroundSprite;

    // --- MARKER TOOLS ---
    private readonly MarkerService _markerService;
    private readonly MarkerToolDeleteAll _markerToolDeleteAll;
    private readonly MarkerToolClear _markerToolClear;

    // --- RULER TOOLS ---
    private readonly RulerTool _rulerTool;
    private readonly RulerToolDeleteAll _rulerToolDeleteAll;
    private readonly RulerToolClear _rulerToolClear;
    private readonly RulerCircleTool _rulerCircleTool;

    // --- WATER TOOLS ---
    private readonly WaterToolPlanner _waterToolPlanner;
    private readonly WaterToolClear _waterToolClear;
    private readonly WaterToolDeleteAll _waterToolDeleteAll;
    private readonly WaterToolRise _waterToolRise;
    private readonly WaterToolLower _waterToolLower;

    public BottomBarButtonGroup(
        ToolButtonFactory toolButtonFactory,
        ToolGroupButtonFactory toolGroupButtonFactory,
        ToolGroupService toolGroupService,
        InputService inputService,
        CursorCoordinatesPicker cursorCoordinatesPicker,
        IAssetLoader assetLoader,
        ILoc loc,
        AreaHighlightingService areaHighlightingService,
        EventBus eventBus,
        MarkerService markerService,
        MarkerToolDeleteAll markerToolDeleteAll,
        MarkerToolClear markerToolClear,
        RulerTool rulerTool,
        RulerToolDeleteAll rulerToolDeleteAll,
        RulerToolClear rulerToolClear,
        RulerCircleTool rulerCircleTool,
        WaterToolPlanner waterToolPlanner,
        WaterToolClear waterToolClear,
        WaterToolDeleteAll waterToolDeleteAll,
        WaterToolRise waterToolRise,
        WaterToolLower waterToolLower)
    {
      _toolButtonFactory = toolButtonFactory;
      _toolGroupButtonFactory = toolGroupButtonFactory;
      _toolGroupService = toolGroupService;
      _inputService = inputService;
      _cursorCoordinatesPicker = cursorCoordinatesPicker;
      _assetLoader = assetLoader;
      _loc = loc;
      _areaHighlightingService = areaHighlightingService;
      _eventBus = eventBus;
      _eventBus.Register(this);

      _markerService = markerService;
      _markerToolDeleteAll = markerToolDeleteAll;
      _markerToolClear = markerToolClear;

      _rulerTool = rulerTool;
      _rulerToolDeleteAll = rulerToolDeleteAll;
      _rulerToolClear = rulerToolClear;
      _rulerCircleTool = rulerCircleTool;

      _waterToolPlanner = waterToolPlanner;
      _waterToolClear = waterToolClear;
      _waterToolDeleteAll = waterToolDeleteAll;
      _waterToolRise = waterToolRise;
      _waterToolLower = waterToolLower;
    }

    public IEnumerable<BottomBarElement> GetElements()
    {
      ToolGroupSpec toolGroup = _toolGroupService.GetGroup("Calloatti.GridToolGroup");
      ToolGroupButton toolGroupButton = _toolGroupButtonFactory.CreateGreen(toolGroup);

      AddMarkerTools(toolGroup, toolGroupButton);
      AddRulerTools(toolGroup, toolGroupButton);
      AddWaterTools(toolGroup, toolGroupButton);

      yield return BottomBarElement.CreateMultiLevel(toolGroupButton.Root, toolGroupButton.ToolButtonsElement);
    }

    private void AddToolButton(ITool tool, string imageName, ToolGroupSpec toolGroup, ToolGroupButton toolGroupButton, string backgroundImageName = null)
    {
      ToolButton button = _toolButtonFactory.Create(tool, imageName, toolGroupButton.ToolButtonsElement);
      if (backgroundImageName != null)
      {
        Sprite background = _assetLoader.Load<Sprite>(Path.Combine("Sprites/BottomBar", backgroundImageName));
        VisualElement backgroundElement = button.Root.Q<VisualElement>("Background");
        backgroundElement.style.backgroundImage = new StyleBackground(background);
        _customBackgrounds[tool] = (backgroundElement, background);
      }
      toolGroupButton.AddTool(button);
      _toolGroupService.AssignToGroup(toolGroup, tool);
    }

    [OnEvent]
    public void OnToolEntered(ToolEnteredEvent toolEnteredEvent)
    {
      if (_customBackgrounds.TryGetValue(toolEnteredEvent.Tool, out var entry))
      {
        entry.Background.style.backgroundImage = new StyleBackground(GetActiveBackgroundSprite());
      }
    }

    [OnEvent]
    public void OnToolExited(ToolExitedEvent toolExitedEvent)
    {
      if (_customBackgrounds.TryGetValue(toolExitedEvent.Tool, out var entry))
      {
        entry.Background.style.backgroundImage = new StyleBackground(entry.CustomSprite);
      }
    }

    private Sprite GetActiveBackgroundSprite()
    {
      if (_activeBackgroundSprite == null)
      {
        _activeBackgroundSprite = _assetLoader.Load<Sprite>("UI/Images/BottomBar/subbutton-bg-02");
      }
      return _activeBackgroundSprite;
    }

    // ====================================================================
    // MARKERS
    // ====================================================================
    private void AddMarkerTools(ToolGroupSpec toolGroup, ToolGroupButton toolGroupButton)
    {
      for (int i = 0; i < 8; i++)
      {
        var colorTool = new MarkerTool(_inputService, _cursorCoordinatesPicker, _assetLoader, _markerService, _loc, _areaHighlightingService, i);
        AddToolButton(colorTool, $"map-marker-cross-{i}", toolGroup, toolGroupButton);
      }

      AddToolButton(_markerToolClear, "CancelToolIcon", toolGroup, toolGroupButton);
      AddToolButton(_markerToolDeleteAll, "trash", toolGroup, toolGroupButton);
    }

    // ====================================================================
    // RULERS
    // ====================================================================
    private void AddRulerTools(ToolGroupSpec toolGroup, ToolGroupButton toolGroupButton)
    {
      AddToolButton(_rulerTool, "ruler-button", toolGroup, toolGroupButton, "subbutton-bg-ruler");
      AddToolButton(_rulerCircleTool, "ruler-circle", toolGroup, toolGroupButton, "subbutton-bg-ruler");
      AddToolButton(_rulerToolClear, "CancelToolIcon", toolGroup, toolGroupButton, "subbutton-bg-ruler");
      AddToolButton(_rulerToolDeleteAll, "trash", toolGroup, toolGroupButton, "subbutton-bg-ruler");
    }

    // ====================================================================
    // WATER PLANNER
    // ====================================================================
    private void AddWaterTools(ToolGroupSpec toolGroup, ToolGroupButton toolGroupButton)
    {
      AddToolButton(_waterToolPlanner, "water", toolGroup, toolGroupButton, "subbutton-bg-water");
      AddToolButton(_waterToolRise, "water-rise", toolGroup, toolGroupButton, "subbutton-bg-water");
      AddToolButton(_waterToolLower, "water-lower", toolGroup, toolGroupButton, "subbutton-bg-water");
      AddToolButton(_waterToolClear, "CancelToolIcon", toolGroup, toolGroupButton, "subbutton-bg-water");
      AddToolButton(_waterToolDeleteAll, "trash", toolGroup, toolGroupButton, "subbutton-bg-water");
    }
  }
}