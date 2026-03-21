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
  public partial class BottomBarButtonGroup : IBottomBarElementsProvider
  {
    private readonly ToolButtonFactory _toolButtonFactory;
    private readonly ToolGroupButtonFactory _toolGroupButtonFactory;
    private readonly ToolGroupService _toolGroupService;

    private readonly InputService _inputService;
    private readonly CursorCoordinatesPicker _cursorCoordinatesPicker;
    private readonly IAssetLoader _assetLoader;
    private readonly ILoc _loc;

    public BottomBarButtonGroup(
        ToolButtonFactory toolButtonFactory,
        ToolGroupButtonFactory toolGroupButtonFactory,
        ToolGroupService toolGroupService,
        MarkerService markerService,
        MarkerDeleteAll MarkerDeleteAll,
        RulerTool rulerTool,
        RulerDeleteAll deleteAllRulersTool,
        WaterPlannerTool waterPlannerTool,
        WaterEraserTool waterEraserTool,
        WaterDeleteAll waterDeleteAll,
        WaterRiseTool waterRiseTool,
        WaterLowerTool waterLowerTool,
        InputService inputService,
        CursorCoordinatesPicker cursorCoordinatesPicker,
        IAssetLoader assetLoader,
        ILoc loc,
        AreaHighlightingService areaHighlightingService)
    {
      _toolButtonFactory = toolButtonFactory;
      _toolGroupButtonFactory = toolGroupButtonFactory;
      _toolGroupService = toolGroupService;
      _inputService = inputService;
      _cursorCoordinatesPicker = cursorCoordinatesPicker;
      _assetLoader = assetLoader;
      _loc = loc;

      InitializeMarkers(markerService, MarkerDeleteAll, areaHighlightingService);
      InitializeRulers(rulerTool, deleteAllRulersTool);
      InitializeWater(waterPlannerTool, waterEraserTool, waterDeleteAll, waterRiseTool, waterLowerTool);
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
  }
}