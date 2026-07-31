using Bindito.Core;
using Timberborn.SingletonSystem;

namespace Calloatti.Grid
{
  [Context("Game")]
  [Context("MapEditor")]
  public class CGConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<CGService>().AsSingleton();
    }
  }
}
