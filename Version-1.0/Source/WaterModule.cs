using Bindito.Core;

namespace Calloatti.Grid
{
  // =========================================================================
  // 1. CONFIGURATOR
  // =========================================================================
  [Context("Game")]
  public class WaterConfigurator : Configurator
  {
    protected override void Configure()
    {
      // Backend Services
      Bind<WaterPlannedArea>().AsSingleton();
      Bind<WaterPlannedAreaVisualizer>().AsSingleton();
      Bind<WaterSpreadSimulator>().AsSingleton();
      Bind<MoistureSpreadVisualizer>().AsSingleton();

      // UI Tools (Renamed for alphabetical sorting)
      Bind<WaterToolPlanner>().AsSingleton();
      Bind<WaterToolEraser>().AsSingleton();
      Bind<WaterToolRise>().AsSingleton();
      Bind<WaterToolLower>().AsSingleton();
      Bind<WaterToolDeleteAll>().AsSingleton();
    }
  }

  // =========================================================================
  // 2. EVENTS
  // =========================================================================
  public class WaterPlannedAreaChangedEvent { }
  public class MoistureSpreadChangedEvent { }
}