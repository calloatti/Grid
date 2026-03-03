using Bindito.Core;

namespace TimberbornModding.TopoData
{
  [Context("Game")]
  public class TopoConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<TopoInputService>().AsSingleton();
      Bind<TopoService>().AsSingleton();
    }
  }
}