using System.Collections.Generic;
using Bindito.Core;
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
  [Context("MapEditor")]
  internal class MapEditorBottomBarConfigurator : IConfigurator
  {
    public void Configure(IContainerDefinition containerDefinition)
    {
      containerDefinition.Bind<MapEditorBottomBarButtonGroup>().AsSingleton();
      containerDefinition.MultiBind<BottomBarModule>().ToProvider<MapEditorBottomBarModuleProvider>().AsSingleton();
    }

    private class MapEditorBottomBarModuleProvider : IProvider<BottomBarModule>
    {
      private readonly MapEditorBottomBarButtonGroup _bottomBarButtonGroup;

      public MapEditorBottomBarModuleProvider(MapEditorBottomBarButtonGroup bottomBarButtonGroup)
      {
        _bottomBarButtonGroup = bottomBarButtonGroup;
      }

      public BottomBarModule Get()
      {
        BottomBarModule.Builder builder = new BottomBarModule.Builder();
        builder.AddMiddleSectionElements(_bottomBarButtonGroup);
        return builder.Build();
      }
    }
  }

  internal class MapEditorBottomBarButtonGroup : IBottomBarElementsProvider
  {
    private readonly ToolButtonFactory _toolButtonFactory;
    private readonly ToolGroupButtonFactory _toolGroupButtonFactory;
    private readonly ToolGroupService _toolGroupService;
    private readonly InputService _inputService;
    private readonly CursorCoordinatesPicker _cursorCoordinatesPicker;
    private readonly IAssetLoader _assetLoader;
    private readonly ILoc _loc;
    private readonly AreaHighlightingService _areaHighlightingService;
    private readonly MarkerService _markerService;
    private readonly MarkerToolDeleteAll _markerToolDeleteAll;
    private readonly RulerTool _rulerTool;
    private readonly RulerToolDeleteAll _rulerToolDeleteAll;

    public MapEditorBottomBarButtonGroup(
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
        RulerToolDeleteAll rulerToolDeleteAll)
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
    }

    public IEnumerable<BottomBarElement> GetElements()
    {
      ToolGroupSpec toolGroup = _toolGroupService.GetGroup("Calloatti.GridToolGroup");
      ToolGroupButton toolGroupButton = _toolGroupButtonFactory.CreateGreen(toolGroup);

      AddMarkerTools(toolGroup, toolGroupButton);
      AddRulerTools(toolGroup, toolGroupButton);

      yield return BottomBarElement.CreateMultiLevel(toolGroupButton.Root, toolGroupButton.ToolButtonsElement);
    }

    private void AddToolButton(ITool tool, string imageName, ToolGroupSpec toolGroup, ToolGroupButton toolGroupButton)
    {
      ToolButton button = _toolButtonFactory.Create(tool, imageName, toolGroupButton.ToolButtonsElement);
      toolGroupButton.AddTool(button);
      _toolGroupService.AssignToGroup(toolGroup, tool);
    }

    private void AddMarkerTools(ToolGroupSpec toolGroup, ToolGroupButton toolGroupButton)
    {
      for (int i = 0; i < 8; i++)
      {
        var colorTool = new MarkerTool(_inputService, _cursorCoordinatesPicker, _assetLoader, _markerService, _loc, _areaHighlightingService, i);
        AddToolButton(colorTool, $"map-marker-cross-{i}", toolGroup, toolGroupButton);
      }

      AddToolButton(_markerToolDeleteAll, "trash", toolGroup, toolGroupButton);
    }

    private void AddRulerTools(ToolGroupSpec toolGroup, ToolGroupButton toolGroupButton)
    {
      AddToolButton(_rulerTool, "ruler-button", toolGroup, toolGroupButton);
      AddToolButton(_rulerToolDeleteAll, "trash", toolGroup, toolGroupButton);
    }
  }
}