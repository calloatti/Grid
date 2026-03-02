using Bindito.Core;
using Timberborn.KeyBindingSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  [Bindito.Core.Context("Game")]
  public class GridConfigurator : Configurator
  {
    public const string Prefix = "[Grid]";

    protected override void Configure()
    {
      Debug.Log($"{Prefix} Initializing mod and binding dependencies...");

      Bind<GridInputService>().AsSingleton();
      Bind<GridService>().AsSingleton(); // Now properly binds GridService

      Debug.Log($"{Prefix} Configuration completed successfully.");
    }
  }
}