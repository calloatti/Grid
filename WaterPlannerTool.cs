using System.Collections.Generic;
using System.Linq;
using Timberborn.AreaSelectionSystemUI;
using Timberborn.Localization;
using Timberborn.SelectionSystem;
using Timberborn.SelectionToolSystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainQueryingSystem;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using UnityEngine;

namespace Calloatti.Grid
{
  public class WaterPlannerTool : ITool, IToolDescriptor, ILoadableSingleton
  {
    private readonly WaterPlannedArea _waterPlannedArea;
    private readonly TerrainAreaService _terrainAreaService;
    private readonly AreaHighlightingService _areaHighlightingService;
    private readonly SelectionToolProcessorFactory _selectionToolProcessorFactory;
    private readonly ILoc _loc;

    private SelectionToolProcessor _selectionToolProcessor;
    private readonly Color _previewColor = new Color(0.5f, 0.7f, 1.0f, 0.9f); // Lighter, solid blue for dragging

    public WaterPlannerTool(
        WaterPlannedArea waterPlannedArea,
        TerrainAreaService terrainAreaService,
        AreaHighlightingService areaHighlightingService,
        SelectionToolProcessorFactory selectionToolProcessorFactory,
        ILoc loc)
    {
      _waterPlannedArea = waterPlannedArea;
      _terrainAreaService = terrainAreaService;
      _areaHighlightingService = areaHighlightingService;
      _selectionToolProcessorFactory = selectionToolProcessorFactory;
      _loc = loc;
    }

    public void Load()
    {
      _selectionToolProcessor = _selectionToolProcessorFactory.Create(OnPreview, OnAction, OnShowNone, "WaterPlannerCursor");
    }

    public void Enter()
    {
      _selectionToolProcessor.Enter();
    }

    public void Exit()
    {
      _areaHighlightingService.UnhighlightAll();
      _selectionToolProcessor.Exit();
    }

    private void OnPreview(IEnumerable<Vector3Int> inputBlocks, Ray ray)
    {
      _areaHighlightingService.UnhighlightAll();
      foreach (var item in _terrainAreaService.InMapLeveledCoordinates(inputBlocks, ray))
      {
        _areaHighlightingService.DrawTile(item, _previewColor);
      }
      _areaHighlightingService.Highlight();
    }

    private void OnAction(IEnumerable<Vector3Int> inputBlocks, Ray ray)
    {
      _areaHighlightingService.UnhighlightAll();
      var blocks = _terrainAreaService.InMapLeveledCoordinates(inputBlocks, ray).ToList();
      if (blocks.Count == 0) return;

      if (_waterPlannedArea.Contains(blocks[0]))
      {
        _waterPlannedArea.RemoveCoordinates(blocks);
      }
      else
      {
        _waterPlannedArea.AddCoordinates(blocks);
      }
    }

    private void OnShowNone()
    {
      _areaHighlightingService.UnhighlightAll();
    }

    public ToolDescription DescribeTool()
    {
      return new ToolDescription.Builder(_loc.T("Calloatti.Grid.WaterPlannerToolTitle"))
          .AddSection(_loc.T("Calloatti.Grid.WaterPlannerToolDescription"))
          .Build();
    }
  }
}