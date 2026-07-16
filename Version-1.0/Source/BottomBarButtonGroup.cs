using System.Collections.Generic;
using Timberborn.AreaSelectionSystemUI;
using Timberborn.AssetSystem;
using Timberborn.BottomBarSystem;
using Timberborn.CursorToolSystem;
using Timberborn.InputSystem;
using Timberborn.Localization;
using Timberborn.SelectionSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;

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

    // --- MARKER TOOLS ---
    private readonly MarkerService _markerService;
    private readonly MarkerToolDeleteAll _markerToolDeleteAll;

    // --- RULER TOOLS ---
    private readonly RulerTool _rulerTool;
    private readonly RulerToolDeleteAll _rulerToolDeleteAll;

    // --- WATER TOOLS ---
    private readonly WaterToolPlanner _waterToolPlanner;
    private readonly WaterToolEraser _waterToolEraser;
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
        MarkerService markerService,
        MarkerToolDeleteAll markerToolDeleteAll,
        RulerTool rulerTool,
        RulerToolDeleteAll rulerToolDeleteAll,
        WaterToolPlanner waterToolPlanner,
        WaterToolEraser waterToolEraser,
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

      _markerService = markerService;
      _markerToolDeleteAll = markerToolDeleteAll;

      _rulerTool = rulerTool;
      _rulerToolDeleteAll = rulerToolDeleteAll;

      _waterToolPlanner = waterToolPlanner;
      _waterToolEraser = waterToolEraser;
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

    private void AddToolButton(ITool tool, string imageName, ToolGroupSpec toolGroup, ToolGroupButton toolGroupButton)
    {
      ToolButton button = _toolButtonFactory.Create(tool, imageName, toolGroupButton.ToolButtonsElement);
      toolGroupButton.AddTool(button);
      _toolGroupService.AssignToGroup(toolGroup, tool);
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

      AddToolButton(_markerToolDeleteAll, "trash", toolGroup, toolGroupButton);
    }

    // ====================================================================
    // RULERS
    // ====================================================================
    private void AddRulerTools(ToolGroupSpec toolGroup, ToolGroupButton toolGroupButton)
    {
      AddToolButton(_rulerTool, "ruler-button", toolGroup, toolGroupButton);
      AddToolButton(_rulerToolDeleteAll, "trash", toolGroup, toolGroupButton);
    }

    // ====================================================================
    // WATER PLANNER
    // ====================================================================
    private void AddWaterTools(ToolGroupSpec toolGroup, ToolGroupButton toolGroupButton)
    {
      AddToolButton(_waterToolPlanner, "water", toolGroup, toolGroupButton);
      AddToolButton(_waterToolEraser, "CancelToolIcon", toolGroup, toolGroupButton);
      AddToolButton(_waterToolRise, "water-rise", toolGroup, toolGroupButton);
      AddToolButton(_waterToolLower, "water-lower", toolGroup, toolGroupButton);
      AddToolButton(_waterToolDeleteAll, "trash", toolGroup, toolGroupButton);
    }
  }
}