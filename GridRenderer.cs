using Bindito.Core;
using System.Collections.Generic;
using Timberborn.BlockObjectPickingSystem;
using Timberborn.BlockSystem;
using Timberborn.ConstructionMode;
using Timberborn.Coordinates;
using Timberborn.CursorToolSystem;
using Timberborn.InputSystem;
using Timberborn.LevelVisibilitySystem;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.ToolSystem;
using UnityEngine;
using UnityEngine.Rendering;

namespace Calloatti.Grid
{
  public partial class GridRenderer : IPostLoadableSingleton
  {
    private readonly IBlockService _blockService;
    private readonly EventBus _eventBus;
    private readonly GridInputService _gridInputService;

    private GameObject _gridObject;
    private MeshRenderer _gridMeshRenderer;
    private float _currentZHeight = -1f;

    [Inject]
    public GridRenderer(
        IBlockService blockService,
        EventBus eventBus,
        InputService inputService,
        GridInputService gridInputService,
        ToolService toolService,
        EntitySelectionService entitySelectionService,
        ITerrainService terrainService,
        SelectableObjectRaycaster selectableObjectRaycaster,
        ILevelVisibilityService levelVisibilityService,
        ConstructionModeService constructionModeService,
        CursorCoordinatesPicker cursorCoordinatesPicker)
    {
      _blockService = blockService;
      _eventBus = eventBus;
      _gridInputService = gridInputService;

      _toolService = toolService;
      _entitySelectionService = entitySelectionService;
      _terrainService = terrainService;
      _constructionModeService = constructionModeService;
      _cursorCoordinatesPicker = cursorCoordinatesPicker;

      _levelVisibilityService = levelVisibilityService;
    }

    public void PostLoad()
    {
      _eventBus.Register(this);
      _gridInputService.OnToggleGrid += ToggleOrUpdateGrid;
    }

    private void ToggleOrUpdateGrid()
    {
      // Recibimos si la altura calculada proviene de la capa de UI
      float targetZHeight = GetTargetZHeight(out bool isLayerBased);
      bool isCurrentlyEnabled = _gridMeshRenderer != null && _gridMeshRenderer.enabled;

      if (isCurrentlyEnabled)
      {
        if (Mathf.Abs(targetZHeight - _currentZHeight) > 0.001f)
        {
          _currentZHeight = targetZHeight;
          _isTrackingLayer = isLayerBased; // Guardamos el estado
          GenerateBakedGrid(_currentZHeight);
          _gridMeshRenderer.enabled = true;
          return;
        }
        else
        {
          _gridMeshRenderer.enabled = false;
          _isTrackingLayer = false; // Al apagarse, borramos el estado
          return;
        }
      }

      if (Mathf.Abs(targetZHeight - _currentZHeight) > 0.001f)
      {
        _currentZHeight = targetZHeight;
        GenerateBakedGrid(_currentZHeight);
      }

      if (_gridMeshRenderer != null)
      {
        _gridMeshRenderer.enabled = true;
        _isTrackingLayer = isLayerBased; // Guardamos el estado inicial al encender
      }
    }

    private void GenerateBakedGrid(float baseHeight)
    {
      float finalRenderHeight = baseHeight + 0.05f;

      if (_gridObject != null) { UnityEngine.Object.Destroy(_gridObject); }

      Vector3Int size = _blockService.Size;
      List<Vector3> vertices = new List<Vector3>();
      List<int> indices = new List<int>();

      for (int y = 0; y <= size.y; y++)
      {
        Vector3 start = CoordinateSystem.GridToWorld(new Vector3(0, y, 0));
        Vector3 end = CoordinateSystem.GridToWorld(new Vector3(size.x, y, 0));
        start.y = finalRenderHeight;
        end.y = finalRenderHeight;
        vertices.Add(start);
        vertices.Add(end);
        indices.Add(vertices.Count - 2);
        indices.Add(vertices.Count - 1);
      }

      for (int x = 0; x <= size.x; x++)
      {
        Vector3 start = CoordinateSystem.GridToWorld(new Vector3(x, 0, 0));
        Vector3 end = CoordinateSystem.GridToWorld(new Vector3(x, size.y, 0));
        start.y = finalRenderHeight;
        end.y = finalRenderHeight;
        vertices.Add(start);
        vertices.Add(end);
        indices.Add(vertices.Count - 2);
        indices.Add(vertices.Count - 1);
      }

      _gridObject = new GameObject("GridBakedMesh");
      MeshFilter mf = _gridObject.AddComponent<MeshFilter>();
      _gridMeshRenderer = _gridObject.AddComponent<MeshRenderer>();

      Mesh mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
      mesh.SetVertices(vertices);
      mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
      mf.mesh = mesh;

      Material mat = new Material(Shader.Find("Hidden/Internal-Colored"));
      mat.color = new Color(0, 0, 0, 0.4f);
      mat.SetInt("_ZTest", (int)CompareFunction.LessEqual);
      _gridMeshRenderer.material = mat;
    }
  }
}