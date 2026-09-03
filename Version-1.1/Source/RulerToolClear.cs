using Timberborn.AreaSelectionSystemUI;
using Timberborn.AssetSystem;
using Timberborn.CursorToolSystem;
using Timberborn.InputSystem;
using Timberborn.Localization;
using Timberborn.SelectionSystem;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using UnityEngine;

namespace Calloatti.Grid
{
  public class RulerToolClear : ITool, IInputProcessor, IToolDescriptor
  {
    private readonly InputService _inputService;
    private readonly CursorCoordinatesPicker _cursorCoordinatesPicker;
    private readonly IAssetLoader _assetLoader;
    private readonly RulerService _rulerService;
    private readonly ILoc _loc;
    private readonly AreaHighlightingService _areaHighlightingService;

    private Texture2D _cursor;

    public RulerToolClear(
        InputService inputService,
        CursorCoordinatesPicker cursorCoordinatesPicker,
        IAssetLoader assetLoader,
        RulerService rulerService,
        ILoc loc,
        AreaHighlightingService areaHighlightingService)
    {
      _inputService = inputService;
      _cursorCoordinatesPicker = cursorCoordinatesPicker;
      _assetLoader = assetLoader;
      _rulerService = rulerService;
      _loc = loc;
      _areaHighlightingService = areaHighlightingService;

      LoadCursors();
    }

    private void LoadCursors()
    {
      _cursor = _assetLoader.Load<Texture2D>("UI/Cursors/CancelCursorLarge");
    }

    public void Enter()
    {
      _inputService.AddInputProcessor(this);
      if (_cursor != null)
      {
        Cursor.SetCursor(_cursor, Vector2.zero, CursorMode.Auto);
      }
    }

    public void Exit()
    {
      _inputService.RemoveInputProcessor(this);
      Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
      _rulerService.CancelOperation();
      _areaHighlightingService.UnhighlightAll();
    }

    public bool ProcessInput()
    {
      _areaHighlightingService.UnhighlightAll();

      if (_inputService.MouseOverUI) return false;

      var picker = _cursorCoordinatesPicker.PickOnFinished();
      if (!picker.HasValue) return false;

      Vector3Int currentPoint = picker.Value.TileCoordinates;

      _areaHighlightingService.DrawTile(currentPoint, new Color(0.2f, 0.8f, 0.2f, 0.4f));
      _areaHighlightingService.Highlight();

      if (_inputService.MainMouseButtonDown)
      {
        _rulerService.DeleteRulerAt(currentPoint);
        return true;
      }

      return false;
    }

    public ToolDescription DescribeTool()
    {
      return new ToolDescription.Builder(_loc.T("Calloatti.Grid.ClearRulersTitle"))
          .AddSection(_loc.T("Calloatti.Grid.ClearRulersDescription"))
          .Build();
    }
  }
}
