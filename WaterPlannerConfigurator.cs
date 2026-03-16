using Bindito.Core;

namespace Calloatti.Grid
{
  [Context("Game")]
  [Context("MapEditor")]
  public class WaterPlannerConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<WaterPlannedArea>().AsSingleton();
      Bind<WaterPlannedAreaVisualizer>().AsSingleton();
      Bind<WaterPlannerTool>().AsSingleton();
      Bind<WaterDeleteAll>().AsSingleton();
      Bind<WaterSpreadSimulator>().AsSingleton();
      Bind<MoistureSpreadVisualizer>().AsSingleton();
    }
  }
}