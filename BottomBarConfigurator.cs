using Bindito.Core;
using Timberborn.BottomBarSystem;

namespace Calloatti.Grid
{
  [Context("Game")]
  internal class BottomBarConfigurator : IConfigurator
  {
    public void Configure(IContainerDefinition containerDefinition)
    {
      // 1. Vinculamos los servicios y herramientas de los Marcadores
      containerDefinition.Bind<MarkerService>().AsSingleton();
      containerDefinition.Bind<MarkerDeleteAll>().AsSingleton();

      // 2. Vinculamos el servicio de las Reglas (NUEVO)
      containerDefinition.Bind<RulerService>().AsSingleton();
      containerDefinition.Bind<RulerTool>().AsSingleton();
      containerDefinition.Bind<RulerDeleteAll>().AsSingleton();

      // 3. Vinculamos nuestro nuevo proveedor de UI (la clase parcial)
      containerDefinition.Bind<BottomBarButtonGroup>().AsSingleton();

      // 4. Inyectamos nuestro grupo en la barra inferior de Timberborn
      containerDefinition.MultiBind<BottomBarModule>().ToProvider<BottomBarModuleProvider>().AsSingleton();
    }

    private class BottomBarModuleProvider : IProvider<BottomBarModule>
    {
      private readonly BottomBarButtonGroup _bottomBarButtonGroup;

      public BottomBarModuleProvider(BottomBarButtonGroup bottomBarButtonGroup)
      {
        _bottomBarButtonGroup = bottomBarButtonGroup;
      }

      public BottomBarModule Get()
      {
        BottomBarModule.Builder builder = new BottomBarModule.Builder();
        builder.AddMiddleSectionElements(_bottomBarButtonGroup);
        return builder.Build();
      }
    }
  }
}