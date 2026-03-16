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
  public class RulerTool : ITool, IInputProcessor, IToolDescriptor
  {
    private readonly InputService _inputService;
    private readonly CursorCoordinatesPicker _cursorCoordinatesPicker;
    private readonly RulerService _rulerService;
    private readonly IAssetLoader _assetLoader;
    private readonly ILoc _loc;
    private readonly AreaHighlightingService _areaHighlightingService;

    private Texture2D _cursor;

    public RulerTool(
        InputService inputService,
        CursorCoordinatesPicker cursorCoordinatesPicker,
        RulerService rulerService,
        IAssetLoader assetLoader,
        ILoc loc,
        AreaHighlightingService areaHighlightingService)
    {
      _inputService = inputService;
      _cursorCoordinatesPicker = cursorCoordinatesPicker;
      _rulerService = rulerService;
      _assetLoader = assetLoader;
      _loc = loc;
      _areaHighlightingService = areaHighlightingService;
      LoadCursors();
    }

    private void LoadCursors()
    {
      _cursor = _assetLoader.Load<Texture2D>("Resources/ui/cursors/ruler-cursor");
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
      // Notify the service that the tool was closed to clean up states
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

      // 1. Always report movement (the Service will decide whether to draw a preview)
      _rulerService.HandleMouseMove(currentPoint);

      // 2. Report the click (the Service will decide to start, finish, or delete)
      if (_inputService.MainMouseButtonDown)
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