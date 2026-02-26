using Timberborn.InputSystem;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using Timberborn.Localization; // <-- Added Localization namespace

namespace Calloatti.Grid
{
  public class DeleteAllMarkersTool : ITool, IInputProcessor, IToolDescriptor
  {
    private readonly ToolService _toolService;
    private readonly InputService _inputService;
    private readonly ILoc _loc; // <-- Added ILoc

    // Inject ILoc here
    public DeleteAllMarkersTool(ToolService toolService, InputService inputService, ILoc loc)
    {
      _toolService = toolService;
      _inputService = inputService;
      _loc = loc;
    }

    public ToolDescription DescribeTool()
    {
      // Ask the localization system for the text using unique keys
      return new ToolDescription.Builder(_loc.T("Calloatti.Grid.DeleteAllTitle"))
          .AddSection(_loc.T("Calloatti.Grid.DeleteAllDescription"))
          .Build();
    }

    public void Enter()
    {
      MarkerService.Instance.RemoveAllMarkers();
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