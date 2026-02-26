using Timberborn.Coordinates;
using Timberborn.CursorToolSystem;
using Timberborn.InputSystem;
using Timberborn.ToolSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  public class MeasuringTool : ITool, IInputProcessor
  {
    private readonly InputService _inputService;
    private readonly CursorCoordinatesPicker _cursorCoordinatesPicker;
    private readonly MeasuringTapeRenderer _tapeRenderer;

    public MeasuringTool(
        InputService inputService,
        CursorCoordinatesPicker cursorCoordinatesPicker,
        MeasuringTapeRenderer tapeRenderer)
    {
      _inputService = inputService;
      _cursorCoordinatesPicker = cursorCoordinatesPicker;
      _tapeRenderer = tapeRenderer;
    }

    public void Enter() => _inputService.AddInputProcessor(this);
    public void Exit() => _inputService.RemoveInputProcessor(this);

    public bool ProcessInput()
    {
      if (_inputService.MainMouseButtonDown && !_inputService.MouseOverUI)
      {
        var picker = _cursorCoordinatesPicker.PickOnFinished();
        if (picker.HasValue)
        {
          // Dibujamos la cinta donde hicimos clic
          _tapeRenderer.DrawTape(picker.Value.TileCoordinates);
          return true;
        }
      }
      return false;
    }
  }
}