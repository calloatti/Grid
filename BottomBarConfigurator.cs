using Bindito.Core;
using Timberborn.BottomBarSystem;

namespace Calloatti.Grid
{
  [Context("Game")]
  internal class BottomBarConfigurator : IConfigurator
  {
    public void Configure(IContainerDefinition containerDefinition)
    {
      // 1. Bind the UI Provider (Markers/Rulers are now in their own Configurators)
      containerDefinition.Bind<BottomBarButtonGroup>().AsSingleton();

      // 2. Register the UI with Timberborn
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