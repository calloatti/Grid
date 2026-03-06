using Bindito.Core;

namespace Calloatti.Grid
{
  [Context("Game")]
  public class RulerConfigurator : Configurator
  {
    protected override void Configure()
    {
      // Core Logic & Memory Management
      Bind<RulerService>().AsSingleton();

      // Input handling (Toggle Hotkey)
      Bind<RulerInputService>().AsSingleton();

      // Tools
      Bind<RulerTool>().AsSingleton();
      Bind<RulerDeleteAll>().AsSingleton();
    }
  }
}