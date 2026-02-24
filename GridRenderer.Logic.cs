using System.Collections.Generic;
using Timberborn.BlockObjectPickingSystem;
using Timberborn.ConstructionMode;
using Timberborn.Coordinates;
using Timberborn.CursorToolSystem;
using Timberborn.InputSystem;
using Timberborn.SelectionSystem;
using Timberborn.TerrainSystem;
using Timberborn.ToolSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  public partial class GridRenderer
  {
    private readonly ToolService _toolService;
    //private readonly SelectableObjectRaycaster _selectableObjectRaycaster;
    //private readonly InputService _inputService;
    private readonly EntitySelectionService _entitySelectionService;
    private readonly ConstructionModeService _constructionModeService;
    private readonly CursorCoordinatesPicker _cursorCoordinatesPicker;
    private readonly ITerrainService _terrainService;

    // NUEVO: Variable para recordar si la grilla actual se inició por la capa de la UI
    private bool _isTrackingLayer = false;

    // NUEVO: Añadido parámetro 'out bool isLayerBased' para informar a la clase principal
    private float GetTargetZHeight(out bool isLayerBased)
    {
      isLayerBased = false;
      float finalHeight = -1f;
      int currentMaxLevel = _levelVisibilityService.MaxVisibleLevel;

      // 1. Capa activa en la UI
      if (currentMaxLevel < 33)
      {
        finalHeight = (float)currentMaxLevel + 0.80f;
        isLayerBased = true; // Avisamos que la Condición 1 fue la ganadora
        Debug.Log($"{GridConfigurator.Prefix} [Logic] P1: Nivel {currentMaxLevel} detectado. Base grilla: {finalHeight}");
      }

      // 2. Herramienta de construcción activa
      if (finalHeight < 0 && !_toolService.IsDefaultToolActive)
      {
        CursorCoordinates? toolCoords = _cursorCoordinatesPicker.Pick();
        if (toolCoords.HasValue)
        {
          finalHeight = (float)toolCoords.GetValueOrDefault().TileCoordinates.z;
          Debug.Log($"{GridConfigurator.Prefix} [Logic] P2: Herramienta apoyada en -> Y:{finalHeight}");
        }
      }

      // 3. Edificio o entidad seleccionada explícitamente en el mapa
      if (finalHeight < 0 && _entitySelectionService.SelectedObject != null)
      {
        finalHeight = _entitySelectionService.SelectedObject.Transform.position.y;
        Debug.Log($"{GridConfigurator.Prefix} [Logic] P3: Edificio seleccionado -> Y:{finalHeight}");
      }

      // 4. Posición del mouse libre (sin herramienta ni selección)
      if (finalHeight < 0)
      {
        CursorCoordinates? cursorCoordinates = _cursorCoordinatesPicker.PickOnFinished();
        if (cursorCoordinates.HasValue)
        {
          finalHeight = (float)cursorCoordinates.GetValueOrDefault().TileCoordinates.z;
          Debug.Log($"{GridConfigurator.Prefix} [Logic] P4: Cursor nativo libre -> Y:{finalHeight}");
        }
      }

      // 5. Altura mayoritaria del terreno
      if (finalHeight < 0)
      {
        finalHeight = (float)GetMostCommonTerrainHeight();
        Debug.Log($"{GridConfigurator.Prefix} [Logic] P5: Terreno mayoritario -> Y:{finalHeight}");
      }

      // 6. Fallback final
      if (finalHeight < 0)
      {
        finalHeight = 5.0f;
        Debug.Log($"{GridConfigurator.Prefix} [Logic] P6: Fallback -> Y:{finalHeight}");
      }

      return finalHeight;
    }

    private int GetMostCommonTerrainHeight()
    {
      Dictionary<int, int> heights = new Dictionary<int, int>();
      Vector3Int size = _terrainService.Size;
      int maxCount = 0;
      int mostCommonZ = 0;

      for (int x = 0; x < size.x; x++)
      {
        for (int y = 0; y < size.y; y++)
        {
          int z = _terrainService.GetTerrainHeight(new Vector3Int(x, y, 0));
          if (heights.TryGetValue(z, out int count)) heights[z] = count + 1;
          else heights[z] = 1;

          if (heights[z] > maxCount)
          {
            maxCount = heights[z];
            mostCommonZ = z;
          }
        }
      }
      return mostCommonZ;
    }
  }
}