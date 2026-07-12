using Timberborn.ToolSystem;
using Timberborn.ToolButtonSystem;

namespace Calloatti.Grid
{
  public partial class BottomBarButtonGroup
  {
    private WaterPlannerTool _waterPlannerTool;
    private WaterEraserTool _waterEraserTool;
    private WaterDeleteAll _waterDeleteAll;
    private WaterRiseTool _waterRiseTool;
    private WaterLowerTool _waterLowerTool;

    private void InitializeWater(
        WaterPlannerTool waterPlannerTool,
        WaterEraserTool waterEraserTool,
        WaterDeleteAll waterDeleteAll,
        WaterRiseTool waterRiseTool,
        WaterLowerTool waterLowerTool)
    {
      _waterPlannerTool = waterPlannerTool;
      _waterEraserTool = waterEraserTool;
      _waterDeleteAll = waterDeleteAll;
      _waterRiseTool = waterRiseTool;
      _waterLowerTool = waterLowerTool;
    }

    private void AddWaterTools(ToolGroupSpec toolGroup, ToolGroupButton toolGroupButton)
    {
      AddToolButton(_waterPlannerTool, "water", toolGroup, toolGroupButton);
      AddToolButton(_waterEraserTool, "CancelToolIcon", toolGroup, toolGroupButton);
      AddToolButton(_waterRiseTool, "water-rise", toolGroup, toolGroupButton);
      AddToolButton(_waterLowerTool, "water-lower", toolGroup, toolGroupButton);
      AddToolButton(_waterDeleteAll, "trash", toolGroup, toolGroupButton);
    }
  }
}