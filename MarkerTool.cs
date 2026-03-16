using Timberborn.AreaSelectionSystemUI;
using Timberborn.AssetSystem;
using Timberborn.CursorToolSystem;
using Timberborn.InputSystem;
using Timberborn.Localization;
using Timberborn.SelectionSystem;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Calloatti.Grid
{
  public class MarkerTool : ITool, IInputProcessor, IToolDescriptor
  {
    private readonly InputService _inputService;
    private readonly CursorCoordinatesPicker _cursorCoordinatesPicker;
    private readonly IAssetLoader _assetLoader;
    private readonly MarkerService _markerService;
    private readonly ILoc _loc;
    private readonly AreaHighlightingService _areaHighlightingService;
    private readonly int _colorIndex;

    private Texture2D _cursor;

    public MarkerTool(
        InputService inputService,
        CursorCoordinatesPicker cursorCoordinatesPicker,
        IAssetLoader assetLoader,
        MarkerService markerService,
        ILoc loc,
        AreaHighlightingService areaHighlightingService,
        int colorIndex)
    {
      _inputService = inputService;
      _cursorCoordinatesPicker = cursorCoordinatesPicker;
      _assetLoader = assetLoader;
      _markerService = markerService;
      _loc = loc;
      _areaHighlightingService = areaHighlightingService;
      _colorIndex = colorIndex;

      LoadCursors();
    }

    private void LoadCursors()
    {
      _cursor = _assetLoader.Load<Texture2D>($"Resources/ui/cursors/map-marker-cursor-{_colorIndex}");
    }

    public void Enter()
    {
      _inputService.AddInputProcessor(this);
      Vector2 hotspot = new Vector2(_cursor.width / 2f, _cursor.height);
      Cursor.SetCursor(_cursor, hotspot, CursorMode.Auto);
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
            bool isShiftDown = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

            if (isShiftDown)
            {
              _markerService.DeleteMarker(picker.Value.TileCoordinates);
            }
            else
            {
              _markerService.Interact(picker.Value.TileCoordinates, _colorIndex);
            }
            return true;
          }
        }
      }
      return false;
    }

    public ToolDescription DescribeTool()
    {
      return new ToolDescription.Builder(_loc.T("Calloatti.Grid.MarkerToolTitle"))
          .AddSection(_loc.T("Calloatti.Grid.MarkerToolDescription"))
          .Build();
    }
  }
}