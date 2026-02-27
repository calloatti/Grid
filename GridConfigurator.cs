using Bindito.Core;
using Timberborn.KeyBindingSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  // Binds this configurator to the "Game" context of Timberborn's Dependency Injection framework.
  [Bindito.Core.Context("Game")]
  public class GridConfigurator : Configurator
  {
    public const string Prefix = "[Grid]";

    // Protected override required by Bindito to configure dependency injection.
    protected override void Configure()
    {
      Debug.Log($"{Prefix} Initializing mod and binding dependencies...");

      // Bind the input service as a singleton so it can listen to keyboard events continuously.
      Bind<GridInputService>().AsSingleton();

      // Bind the main renderer logic as a singleton.
      Bind<GridRenderer>().AsSingleton();

      Debug.Log($"{Prefix} Configuration completed successfully.");
    }
  }
}