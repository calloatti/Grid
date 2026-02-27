using Timberborn.AssetSystem;
using Timberborn.CursorToolSystem;
using Timberborn.InputSystem;
using Timberborn.Localization;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.GridBrushBase;

namespace Calloatti.Grid
{
  public class MarkerTool : ITool, IInputProcessor, IToolDescriptor
  {
    private readonly InputService _inputService;
    private readonly CursorCoordinatesPicker _cursorCoordinatesPicker;
    private readonly IAssetLoader _assetLoader;
    private readonly MarkerService _markerService;
    private readonly ILoc _loc;
    private readonly int _colorIndex; //

    private Texture2D _cursor;

    public MarkerTool(
        InputService inputService,
        CursorCoordinatesPicker cursorCoordinatesPicker,
        IAssetLoader assetLoader,
        MarkerService markerService,
        ILoc loc,
        int colorIndex) // Received from the loop
    {
      _inputService = inputService;
      _cursorCoordinatesPicker = cursorCoordinatesPicker;
      _assetLoader = assetLoader;
      _markerService = markerService;
      _loc = loc;
      _colorIndex = colorIndex;

      LoadCursors();
    }

    private void LoadCursors()
    {
      _cursor = _assetLoader.Load<Texture2D>($"Sprites/Cursors/map-marker-cursor-{_colorIndex}");
    }

    public void Enter()
    {
      _inputService.AddInputProcessor(this);

      // Pivot the cursor at the bottom center of the image
      Vector2 hotspot = new Vector2(_cursor.width / 2f, _cursor.height);
      Cursor.SetCursor(_cursor, hotspot, CursorMode.Auto);
    }

    public void Exit()
    {
      _inputService.RemoveInputProcessor(this);
      Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    public bool ProcessInput()
    {
      if (_inputService.MainMouseButtonDown && !_inputService.MouseOverUI)
      {
        var picker = _cursorCoordinatesPicker.PickOnFinished();
        if (picker.HasValue)
        {
          bool isShiftDown = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

          if (isShiftDown)
          {
            _markerService.DeleteMarker(picker.Value.TileCoordinates);
          }
          else
          {
            // Place the marker with the tool's assigned color index
            _markerService.Interact(picker.Value.TileCoordinates, _colorIndex);
          }
          return true;
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