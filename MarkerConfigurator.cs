using Bindito.Core;

namespace Calloatti.Grid
{
  [Context("Game")]
  public class MarkerConfigurator : Configurator
  {
    protected override void Configure()
    {
      // Core Marker Logic
      Bind<MarkerService>().AsSingleton();

      // Marker Input Handling (Toggle Hotkey)
      Bind<MarkerInputService>().AsSingleton();

      // Global Delete Tool
      Bind<MarkerDeleteAll>().AsSingleton();
    }
  }
}