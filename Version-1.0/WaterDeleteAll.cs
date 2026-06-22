using Timberborn.InputSystem;
using Timberborn.Localization;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;

namespace Calloatti.Grid
{
  public class WaterDeleteAll : ITool, IInputProcessor, IToolDescriptor
  {
    private readonly ToolService _toolService;
    private readonly InputService _inputService;
    private readonly WaterPlannedArea _waterPlannedArea;
    private readonly ILoc _loc;

    public WaterDeleteAll(ToolService toolService, InputService inputService, ILoc loc, WaterPlannedArea waterPlannedArea)
    {
      _toolService = toolService;
      _inputService = inputService;
      _loc = loc;
      _waterPlannedArea = waterPlannedArea;
    }

    public ToolDescription DescribeTool()
    {
      return new ToolDescription.Builder(_loc.T("Calloatti.Grid.DeleteAllWaterTitle"))
          .AddSection(_loc.T("Calloatti.Grid.DeleteAllWaterDescription"))
          .Build();
    }

    public void Enter()
    {
      _waterPlannedArea.ClearAll();
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