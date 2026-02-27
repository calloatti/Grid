using Timberborn.ToolSystem;
using Timberborn.ToolButtonSystem;

namespace Calloatti.Grid
{
  public partial class BottomBarButtonGroup
  {
    private MarkerService _markerService;
    private MarkerDeleteAll _MarkerDeleteAll;

    private void InitializeMarkers(MarkerService markerService, MarkerDeleteAll MarkerDeleteAll)
    {
      _markerService = markerService;
      _MarkerDeleteAll = MarkerDeleteAll;
    }

    private void AddMarkerTools(ToolGroupSpec toolGroup, ToolGroupButton toolGroupButton)
    {
      for (int i = 0; i < 8; i++)
      {
        var colorTool = new MarkerTool(_inputService, _cursorCoordinatesPicker, _assetLoader, _markerService, _loc, i);
        AddToolButton(colorTool, $"map-marker-cross-{i}", toolGroup, toolGroupButton);
      }

      AddToolButton(_MarkerDeleteAll, "trash", toolGroup, toolGroupButton);
    }
  }
}