using Timberborn.AreaSelectionSystemUI;
using Timberborn.SelectionSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;

namespace Calloatti.Grid
{
  public partial class BottomBarButtonGroup
  {
    private MarkerService _markerService;
    private MarkerDeleteAll _MarkerDeleteAll;
    private AreaHighlightingService _areaHighlightingService;

    private void InitializeMarkers(MarkerService markerService, MarkerDeleteAll MarkerDeleteAll, AreaHighlightingService areaHighlightingService)
    {
      _markerService = markerService;
      _MarkerDeleteAll = MarkerDeleteAll;
      _areaHighlightingService = areaHighlightingService;
    }

    private void AddMarkerTools(ToolGroupSpec toolGroup, ToolGroupButton toolGroupButton)
    {
      for (int i = 0; i < 8; i++)
      {
        var colorTool = new MarkerTool(_inputService, _cursorCoordinatesPicker, _assetLoader, _markerService, _loc, _areaHighlightingService, i);
        AddToolButton(colorTool, $"map-marker-cross-{i}", toolGroup, toolGroupButton);
      }

      AddToolButton(_MarkerDeleteAll, "trash", toolGroup, toolGroupButton);
    }
  }
}