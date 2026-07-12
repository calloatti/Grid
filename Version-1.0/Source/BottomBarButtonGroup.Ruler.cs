using Timberborn.ToolSystem;
using Timberborn.ToolButtonSystem;

namespace Calloatti.Grid
{
  public partial class BottomBarButtonGroup
  {
    private RulerTool _rulerTool;
    private RulerDeleteAll _rulerDeleteAll; // <-- AQUÍ CAMBIÓ

    private void InitializeRulers(RulerTool rulerTool, RulerDeleteAll rulerDeleteAll) // <-- AQUÍ CAMBIÓ
    {
      _rulerTool = rulerTool;
      _rulerDeleteAll = rulerDeleteAll; // <-- AQUÍ CAMBIÓ
    }

    private void AddRulerTools(ToolGroupSpec toolGroup, ToolGroupButton toolGroupButton)
    {
      AddToolButton(_rulerTool, "ruler-button", toolGroup, toolGroupButton);
      AddToolButton(_rulerDeleteAll, "trash", toolGroup, toolGroupButton); // <-- AQUÍ CAMBIÓ
    }
  }
}