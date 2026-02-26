using System.Collections.Generic;
using Timberborn.BottomBarSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;
using Timberborn.InputSystem;
using Timberborn.AssetSystem;
using Timberborn.CursorToolSystem;
using Timberborn.Localization;

namespace Calloatti.Grid
{
  public class MarkerMenuButton : IBottomBarElementsProvider
  {
    private readonly ToolButtonFactory _toolButtonFactory;
    private readonly ToolGroupButtonFactory _toolGroupButtonFactory;
    private readonly ToolGroupService _toolGroupService;
    private readonly MarkerService _markerService;
    private readonly DeleteAllMarkersTool _deleteTool;

    // Add these fields to pass to the tools
    private readonly InputService _inputService;
    private readonly CursorCoordinatesPicker _cursorCoordinatesPicker;
    private readonly IAssetLoader _assetLoader;
    private readonly ILoc _loc;

    public MarkerMenuButton(
        ToolButtonFactory toolButtonFactory,
        ToolGroupButtonFactory toolGroupButtonFactory,
        ToolGroupService toolGroupService,
        MarkerService markerService,
        DeleteAllMarkersTool deleteTool,
        InputService inputService,
        CursorCoordinatesPicker cursorCoordinatesPicker,
        IAssetLoader assetLoader,
        ILoc loc)
    {
      _toolButtonFactory = toolButtonFactory;
      _toolGroupButtonFactory = toolGroupButtonFactory;
      _toolGroupService = toolGroupService;
      _markerService = markerService;
      _deleteTool = deleteTool;
      _inputService = inputService;
      _cursorCoordinatesPicker = cursorCoordinatesPicker;
      _assetLoader = assetLoader;
      _loc = loc;
    }

    public IEnumerable<BottomBarElement> GetElements()
    {
      ToolGroupSpec toolGroup = _toolGroupService.GetGroup("MarkerToolGroup"); //
      ToolGroupButton toolGroupButton = _toolGroupButtonFactory.CreateGreen(toolGroup); //

      // Loop to create 8 tools and 8 buttons
      for (int i = 0; i < 8; i++)
      {
        var colorTool = new MarkerTool(
            _inputService,
            _cursorCoordinatesPicker,
            _assetLoader,
            _markerService,
            _loc,
            i); // Each tool gets its unique color index

        AddTool(colorTool, $"map-marker-cross-{i}", toolGroup, toolGroupButton);
      }

      AddTool(_deleteTool, "trash", toolGroup, toolGroupButton);
      yield return BottomBarElement.CreateMultiLevel(toolGroupButton.Root, toolGroupButton.ToolButtonsElement);
    }

    private void AddTool(ITool tool, string imageName, ToolGroupSpec toolGroup, ToolGroupButton toolGroupButton)
    {
      ToolButton button = _toolButtonFactory.Create(tool, imageName, toolGroupButton.ToolButtonsElement);
      toolGroupButton.AddTool(button);
      _toolGroupService.AssignToGroup(toolGroup, tool);
    }
  }
}