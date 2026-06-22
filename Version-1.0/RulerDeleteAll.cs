using Timberborn.InputSystem;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using Timberborn.Localization;

namespace Calloatti.Grid
{
  public class RulerDeleteAll : ITool, IInputProcessor, IToolDescriptor
  {
    private readonly ToolService _toolService;
    private readonly InputService _inputService;
    private readonly RulerService _rulerService;
    private readonly ILoc _loc;

    public RulerDeleteAll(ToolService toolService, InputService inputService, ILoc loc, RulerService rulerService)
    {
      _toolService = toolService;
      _inputService = inputService;
      _loc = loc;
      _rulerService = rulerService;
    }

    public ToolDescription DescribeTool()
    {
      return new ToolDescription.Builder(_loc.T("Calloatti.Grid.DeleteAllRulersTitle"))
          .AddSection(_loc.T("Calloatti.Grid.DeleteAllRulersDescription"))
          .Build();
    }

    public void Enter()
    {
      // Borramos las reglas de la pantalla
      _rulerService.DeleteAllRulers();
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