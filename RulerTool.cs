using Timberborn.AssetSystem;
using Timberborn.CursorToolSystem;
using Timberborn.InputSystem;
using Timberborn.Localization;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using UnityEngine;

namespace Calloatti.Grid
{
  public class RulerTool : ITool, IInputProcessor, IToolDescriptor
  {
    private readonly InputService _inputService;
    private readonly CursorCoordinatesPicker _cursorCoordinatesPicker;
    private readonly RulerService _rulerService;
    private readonly IAssetLoader _assetLoader;
    private readonly ILoc _loc;

    private Texture2D _cursor;

    public RulerTool(
        InputService inputService,
        CursorCoordinatesPicker cursorCoordinatesPicker,
        RulerService rulerService,
        IAssetLoader assetLoader,
        ILoc loc)
    {
      _inputService = inputService;
      _cursorCoordinatesPicker = cursorCoordinatesPicker;
      _rulerService = rulerService;
      _assetLoader = assetLoader;
      _loc = loc;
      LoadCursors();
    }

    private void LoadCursors()
    {
      _cursor = _assetLoader.Load<Texture2D>("Sprites/Cursors/ruler-cursor");
    }

    public void Enter()
    {
      _inputService.AddInputProcessor(this);
      if (_cursor != null)
      {
        Vector2 hotspot = new Vector2(_cursor.width / 2f, _cursor.height);
        Cursor.SetCursor(_cursor, hotspot, CursorMode.Auto);
      }
    }

    public void Exit()
    {
      _inputService.RemoveInputProcessor(this);
      Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
      // Informamos al servicio que la herramienta se cerró para limpiar estados
      _rulerService.CancelOperation();
    }

    public bool ProcessInput()
    {
      var picker = _cursorCoordinatesPicker.PickOnFinished();
      if (!picker.HasValue) return false;

      Vector3Int currentPoint = picker.Value.TileCoordinates;

      // 1. Informamos siempre del movimiento (el Service decidirá si hace preview o no)
      _rulerService.HandleMouseMove(currentPoint);

      // 2. Informamos del clic (el Service decidirá si empieza, termina o borra)
      if (_inputService.MainMouseButtonDown && !_inputService.MouseOverUI)
      {
        _rulerService.HandleClick(currentPoint);
        return true;
      }

      return false;
    }

    public ToolDescription DescribeTool()
    {
      return new ToolDescription.Builder(_loc.T("Calloatti.Grid.RulerToolTitle"))
          .AddSection(_loc.T("Calloatti.Grid.RulerToolDescription"))
          .Build();
    }
  }
}