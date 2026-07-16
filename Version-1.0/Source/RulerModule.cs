using Bindito.Core;
using Timberborn.InputSystem;
using Timberborn.SingletonSystem;

namespace Calloatti.Grid
{
  // =========================================================================
  // 1. CONFIGURATOR
  // =========================================================================
  [Context("Game")]
  public class RulerConfigurator : Configurator
  {
    protected override void Configure()
    {
      // Core Logic & Memory Management
      Bind<RulerService>().AsSingleton();

      // Input handling (Toggle Hotkey)
      Bind<RulerInputService>().AsSingleton();

      // Tools
      Bind<RulerTool>().AsSingleton();
      Bind<RulerToolDeleteAll>().AsSingleton();
    }
  }

  // =========================================================================
  // 2. INPUT SERVICE
  // =========================================================================
  public class RulerInputService : ILoadableSingleton, IInputProcessor
  {
    private readonly InputService _inputService;

    [Inject]
    public RulerInputService(InputService inputService)
    {
      _inputService = inputService;
    }

    public void Load()
    {
      _inputService.AddInputProcessor(this);
    }

    public bool ProcessInput()
    {
      if (_inputService.IsKeyDown("Calloatti.Grid.KeyBind.Toggle.Rulers"))
      {
        RulerService.Instance.ToggleRulers();
        return true;
      }
      return false;
    }
  }
}