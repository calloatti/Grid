using Bindito.Core;
using Timberborn.KeyBindingSystem;
using UnityEngine; // Añadido para Debug.Log

namespace Calloatti.Grid
{
  [Bindito.Core.Context("Game")]
  public class GridConfigurator : Configurator
  {
    public const string Prefix = "[Grid]";

    protected override void Configure() // <--- Se queda protected porque tu Bindito lo exige así.
    {
      Debug.Log($"{Prefix} Configurando dependencias del Mod...");

      Bind<GridInputService>().AsSingleton();
      Debug.Log($"{Prefix} GridInputService vinculado.");

      Bind<GridRenderer>().AsSingleton();
      Debug.Log($"{Prefix} GridRenderer vinculado.");

      Debug.Log($"{Prefix} Configuración completada.");
    }
  }
}