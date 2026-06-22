using System.Collections.Generic;
using System.Linq;
using Timberborn.AssetSystem;
using Timberborn.CursorToolSystem;
using Timberborn.InputSystem;
using Timberborn.Localization;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using UnityEngine;

namespace Calloatti.Grid
{
  public class WaterLowerTool : ITool, IInputProcessor, IToolDescriptor
  {
    private readonly InputService _inputService;
    private readonly CursorCoordinatesPicker _cursorCoordinatesPicker;
    private readonly WaterPlannedArea _waterPlannedArea;
    private readonly IAssetLoader _assetLoader;
    private readonly ILoc _loc;

    private Texture2D _cursor;

    public WaterLowerTool(
        InputService inputService,
        CursorCoordinatesPicker cursorCoordinatesPicker,
        WaterPlannedArea waterPlannedArea,
        IAssetLoader assetLoader,
        ILoc loc)
    {
      _inputService = inputService;
      _cursorCoordinatesPicker = cursorCoordinatesPicker;
      _waterPlannedArea = waterPlannedArea;
      _assetLoader = assetLoader;
      _loc = loc;
      LoadCursors();
    }

    private void LoadCursors()
    {
      _cursor = _assetLoader.Load<Texture2D>("Resources/ui/cursors/water-cursor-lower");
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
    }

    public bool ProcessInput()
    {
      if (_inputService.MouseOverUI) return false;

      if (_inputService.MainMouseButtonDown)
      {
        var picker = _cursorCoordinatesPicker.PickOnFinished();
        if (!picker.HasValue) return false;

        Vector2Int currentXY = new Vector2Int(picker.Value.TileCoordinates.x, picker.Value.TileCoordinates.y);

        // Check the entire column for any blue highlight. If multiple, pick the highest one.
        var blocksInColumn = _waterPlannedArea.Area.Where(b => b.x == currentXY.x && b.y == currentXY.y).ToList();
        if (blocksInColumn.Count == 0) return false;

        int targetZ = blocksInColumn.Max(b => b.z);
        Vector3Int startNode = new Vector3Int(currentXY.x, currentXY.y, targetZ);

        var connected = GetConnectedWaterSameZ(startNode);

        if (connected.Count > 0)
        {
          var newBlocks = connected.Select(v => new Vector3Int(v.x, v.y, Mathf.Max(0, v.z - 1))).ToList();
          _waterPlannedArea.RemoveCoordinates(connected);
          _waterPlannedArea.AddCoordinates(newBlocks);
          return true;
        }
      }

      return false;
    }

    private HashSet<Vector3Int> GetConnectedWaterSameZ(Vector3Int startNode)
    {
      var connected = new HashSet<Vector3Int>();

      // Filter the area to only blocks on the exact same Z plane
      var validXYs = new HashSet<Vector2Int>(_waterPlannedArea.Area
          .Where(b => b.z == startNode.z)
          .Select(b => new Vector2Int(b.x, b.y)));

      Vector2Int startXY = new Vector2Int(startNode.x, startNode.y);
      if (!validXYs.Contains(startXY)) return connected;

      var queue = new Queue<Vector2Int>();
      var visited = new HashSet<Vector2Int>();

      queue.Enqueue(startXY);
      visited.Add(startXY);

      Vector2Int[] offsets = {
          new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0),
          new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
      };

      while (queue.Count > 0)
      {
        var curr = queue.Dequeue();
        connected.Add(new Vector3Int(curr.x, curr.y, startNode.z));

        foreach (var offset in offsets)
        {
          var neighbor = curr + offset;
          if (!visited.Contains(neighbor) && validXYs.Contains(neighbor))
          {
            visited.Add(neighbor);
            queue.Enqueue(neighbor);
          }
        }
      }
      return connected;
    }

    public ToolDescription DescribeTool()
    {
      return new ToolDescription.Builder(_loc.T("Calloatti.Grid.WaterLowerToolTitle"))
          .AddSection(_loc.T("Calloatti.Grid.WaterLowerToolDescription"))
          .Build();
    }
  }
}