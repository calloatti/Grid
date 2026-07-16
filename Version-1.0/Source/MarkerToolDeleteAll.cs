using Timberborn.InputSystem;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using Timberborn.Localization;

namespace Calloatti.Grid
{
  public class MarkerToolDeleteAll : ITool, IInputProcessor, IToolDescriptor
  {
    private readonly ToolService _toolService;
    private readonly InputService _inputService;
    private readonly ILoc _loc;
    private readonly MarkerService _markerService;

    public MarkerToolDeleteAll(
        ToolService toolService,
        InputService inputService,
        ILoc loc,
        MarkerService markerService)
    {
      _toolService = toolService;
      _inputService = inputService;
      _loc = loc;
      _markerService = markerService;
    }

    public ToolDescription DescribeTool()
    {
      return new ToolDescription.Builder(_loc.T("Calloatti.Grid.DeleteAllTitle"))
          .AddSection(_loc.T("Calloatti.Grid.DeleteAllDescription"))
          .Build();
    }

    public void Enter()
    {
      _markerService.RemoveAllMarkers();
      _inputService.AddInputProcessor(this);
    }

    public void Exit()
    {
      _inputService.RemoveInputProcessor(this);
    }

    public bool ProcessInput()
    {
      _toolService.SwitchToDefaultTool();
      return true;
    }
  }
}