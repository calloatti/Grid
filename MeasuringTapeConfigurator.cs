using Bindito.Core;
using Timberborn.BottomBarSystem;

namespace Calloatti.Grid
{
  [Context("Game")]
  internal class MeasuringTapeConfigurator : IConfigurator
  {
    public void Configure(IContainerDefinition containerDefinition)
    {
      containerDefinition.Bind<MeasuringTapeRenderer>().AsSingleton();
      containerDefinition.Bind<MeasuringTool>().AsSingleton();
      containerDefinition.Bind<MeasuringTapeButton>().AsSingleton();

      containerDefinition.MultiBind<BottomBarModule>().ToProvider<BottomBarModuleProvider>().AsSingleton();
    }

    private class BottomBarModuleProvider : IProvider<BottomBarModule>
    {
      private readonly MeasuringTapeButton _measuringTapeButton;

      public BottomBarModuleProvider(MeasuringTapeButton measuringTapeButton)
      {
        _measuringTapeButton = measuringTapeButton;
      }

      public BottomBarModule Get()
      {
        // Corregido: Separamos las llamadas para evitar el error de 'void'
        BottomBarModule.Builder builder = new BottomBarModule.Builder();

        // Agregamos el botón a la derecha
        builder.AddRightSectionElement(_measuringTapeButton);

        // Retornamos la construcción final
        return builder.Build();
      }
    }
  }
}