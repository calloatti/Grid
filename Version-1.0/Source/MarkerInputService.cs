using Bindito.Core;
using Timberborn.InputSystem;
using Timberborn.SingletonSystem;

namespace Calloatti.Grid
{
  public class MarkerInputService : ILoadableSingleton, IInputProcessor
  {
    private readonly InputService _inputService;

    [Inject]
    public MarkerInputService(InputService inputService)
    {
      _inputService = inputService;
    }

    public void Load()
    {
      _inputService.AddInputProcessor(this);
    }

    public bool ProcessInput()
    {
      if (_inputService.IsKeyDown("Calloatti.Grid.KeyBind.Toggle.Markers"))
      {
        MarkerService.Instance.ToggleMarkers();
        return true;
      }
      return false;
    }
  }
}