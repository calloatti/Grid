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
  public class MarkerToolClear : ITool, IInputProcessor, IToolDescriptor
  {
    private readonly InputService _inputService;
    private readonly CursorCoordinatesPicker _cursorCoordinatesPicker;
    private readonly IAssetLoader _assetLoader;
    private readonly MarkerService _markerService;
    private readonly ILoc _loc;
    private readonly AreaHighlightingService _areaHighlightingService;

    private Texture2D _cursor;

    public MarkerToolClear(
        InputService inputService,
        CursorCoordinatesPicker cursorCoordinatesPicker,
        IAssetLoader assetLoader,
        MarkerService markerService,
        ILoc loc,
        AreaHighlightingService areaHighlightingService)
    {
      _inputService = inputService;
      _cursorCoordinatesPicker = cursorCoordinatesPicker;
      _assetLoader = assetLoader;
      _markerService = markerService;
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
      _areaHighlightingService.UnhighlightAll();
    }

    public bool ProcessInput()
    {
      _areaHighlightingService.UnhighlightAll();

      if (!_inputService.MouseOverUI)
      {
        var picker = _cursorCoordinatesPicker.PickOnFinished();
        if (picker.HasValue)
        {
          _areaHighlightingService.DrawTile(picker.Value.TileCoordinates, new Color(0.2f, 0.8f, 0.2f, 0.4f));
          _areaHighlightingService.Highlight();

          if (_inputService.MainMouseButtonDown)
          {
            _markerService.DeleteMarker(picker.Value.TileCoordinates);
            return true;
          }
        }
      }
      return false;
    }

    public ToolDescription DescribeTool()
    {
      return new ToolDescription.Builder(_loc.T("Calloatti.Grid.ClearMarkersTitle"))
          .AddSection(_loc.T("Calloatti.Grid.ClearMarkersDescription"))
          .Build();
    }
  }
}
