using Bindito.Core;
using Timberborn.BottomBarSystem;

namespace Calloatti.Grid
{
  [Context("Game")]
  internal class MarkerConfigurator : IConfigurator
  {
    public void Configure(IContainerDefinition containerDefinition)
    {
      // 1. MUST BE BOUND to prevent the NullReferenceException!
      containerDefinition.Bind<MarkerService>().AsSingleton();

      //containerDefinition.Bind<MarkerTool>().AsSingleton();
      containerDefinition.Bind<DeleteAllMarkersTool>().AsSingleton();
      containerDefinition.Bind<MarkerMenuButton>().AsSingleton();

      containerDefinition.MultiBind<BottomBarModule>().ToProvider<BottomBarModuleProvider>().AsSingleton();
    }

    private class BottomBarModuleProvider : IProvider<BottomBarModule>
    {
      private readonly MarkerMenuButton _markerMenuButton;

      public BottomBarModuleProvider(MarkerMenuButton markerMenuButton)
      {
        _markerMenuButton = markerMenuButton;
      }

      public BottomBarModule Get()
      {
        BottomBarModule.Builder builder = new BottomBarModule.Builder();

        // 2. THIS MOVES IT TO THE MIDDLE
        builder.AddMiddleSectionElements(_markerMenuButton);

        return builder.Build();
      }
    }
  }
}