using Bindito.Core;

namespace Calloatti.TopoData
{
  [Context("Game")]
  [Context("MapEditor")]
  public class TopoConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<TopoInputService>().AsSingleton();
      Bind<TopoService>().AsSingleton();
    }
  }
}