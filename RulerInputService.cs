using Bindito.Core;
using Timberborn.InputSystem;
using Timberborn.SingletonSystem;

namespace Calloatti.Grid
{
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
        return true; // Successfully consumed the hotkey
      }
      return false;
    }
  }
}