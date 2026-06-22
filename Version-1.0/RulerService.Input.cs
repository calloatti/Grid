using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

namespace Calloatti.Grid
{
  public partial class RulerService
  {
    public void HandleClick(Vector3Int clickCoords)
    {
      bool ctrl = Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;
      bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

      if (!_isDrawing)
      {
        Vector2Int c2d = new Vector2Int(clickCoords.x, clickCoords.y);

        for (int i = _activeRulers.Count - 1; i >= 0; i--)
        {
          var r = _activeRulers[i];
          if (r.Segments.Any(seg => seg.Coords == c2d))
          {
            if (shift)
            {
              // Tu optimización intacta
              CleanupOverlapForRuler(r);
              Object.Destroy(r.Container);
              _activeRulers.RemoveAt(i);
              return;
            }
            if (ctrl)
            {
              var clickedSeg = r.Segments.First(seg => seg.Coords == c2d);
              int clickedValue = clickedSeg.Value;
              Vector3Int s = r.Start, e = r.End;
              Quaternion rot = r.Rotation;
              int dist = Mathf.Max(Mathf.Abs(e.x - s.x), Mathf.Abs(e.y - s.y));

              int newType = r.RulerType;
              int newPeriod = r.Period;
              int newGapSize = r.GapSize;

              // --- MÁQUINA DE ESTADOS EXACTA ---
              if (r.RulerType == 0)
              {
                // 1er CTRL-CLICK: Regla Normal -> Periódica
                newType = 1;
                newPeriod = Mathf.Max(1, clickedValue - 1); // Selecciona el periodo
                newGapSize = 1; // Inicia con 1 cuadro rojo
              }
              else if (r.RulerType == 1)
              {
                if (clickedValue == 511)
                {
                  // 3er CTRL-CLICK (sobre cuadro rojo): Vuelve a Normal
                  newType = 0;
                  newPeriod = 0;
                  newGapSize = 0;
                }
                else
                {
                  // 2do CTRL-CLICK (sobre número): Extiende el Gap
                  newGapSize = r.GapSize + clickedValue;
                }
              }

              // --- GENERACIÓN DE LA SECUENCIA ---
              List<int> newVals = new List<int>();
              int newLimit = dist + 1;

              if (newType == 0)
              {
                for (int n = 1; n <= newLimit; n++)
                {
                  newVals.Add(n);
                }
              }
              else
              {
                int currentNum = 1;
                int currentGap = 0;

                for (int n = 1; n <= newLimit; n++)
                {
                  if (currentNum <= newPeriod)
                  {
                    newVals.Add(currentNum);
                    currentNum++;
                  }
                  else
                  {
                    newVals.Add(511); // 0 = Cuadro Rojo
                    currentGap++;
                    if (currentGap >= newGapSize)
                    {
                      currentNum = 1;
                      currentGap = 0;
                    }
                  }
                }
              }

              // --- LIMPIEZA Y RECONSTRUCCIÓN CON TU SISTEMA ---
              CleanupOverlapForRuler(r);
              Object.Destroy(r.Container);
              _activeRulers.RemoveAt(i);

              InternalFinalizeRuler(s, e, rot, newVals, newType, newPeriod, newGapSize);
              return;
            }
          }
        }

        if (shift || ctrl) return;

        _isDrawing = true;
        _startCoords = clickCoords;
        _lockedRotation = CalculateCameraRotation();

        InternalSetupPreview();
      }
      else
      {
        InternalFinalizeRuler(_startCoords, clickCoords, _lockedRotation, null, 0, 0, 0);
        _isDrawing = false;
        _drawingType = 0;
      }
    }

    public void HandleMouseMove(Vector3Int c) { if (_isDrawing) InternalUpdatePreview(_startCoords, c); }

    public void CancelOperation() { _isDrawing = false; _drawingType = 0; if (_previewContainer != null) _previewContainer.SetActive(false); }

    // Tus optimizaciones de diccionarios intactas en el limpiador general
    public void DeleteAllRulers()
    {
      foreach (var r in _activeRulers) if (r.Container != null) Object.Destroy(r.Container);
      _activeRulers.Clear();
      _segmentMap.Clear();
      foreach (var q in _sharedQuads.Values) Object.Destroy(q);
      _sharedQuads.Clear();
      CancelOperation();
    }
  }
}