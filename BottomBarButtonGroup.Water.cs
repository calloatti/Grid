using Timberborn.ToolSystem;
using Timberborn.ToolButtonSystem;

namespace Calloatti.Grid
{
  public partial class BottomBarButtonGroup
  {
    private WaterPlannerTool _waterPlannerTool;
    private WaterDeleteAll _waterDeleteAll;

    private void InitializeWater(WaterPlannerTool waterPlannerTool, WaterDeleteAll waterDeleteAll)
    {
      _waterPlannerTool = waterPlannerTool;
      _waterDeleteAll = waterDeleteAll;
    }

    private void AddWaterTools(ToolGroupSpec toolGroup, ToolGroupButton toolGroupButton)
    {
      
      AddToolButton(_waterPlannerTool, "water", toolGroup, toolGroupButton);
      AddToolButton(_waterDeleteAll, "trash", toolGroup, toolGroupButton);
    }
  }
}