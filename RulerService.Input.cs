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
      if (!_isDrawing)
      {
        bool ctrl = Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;
        bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
        Vector2Int c2d = new Vector2Int(clickCoords.x, clickCoords.y);

        for (int i = _activeRulers.Count - 1; i >= 0; i--)
        {
          var r = _activeRulers[i];
          if (r.Segments.Any(seg => seg.Coords == c2d))
          {
            if (shift)
            {
              CleanupOverlapForRuler(r);
              Object.Destroy(r.Container);
              _activeRulers.RemoveAt(i);
              return;
            }
            if (ctrl)
            {
              // Ruler modification logic
              int dist = Mathf.Max(Mathf.Abs(r.End.x - r.Start.x), Mathf.Abs(r.End.y - r.Start.y));
              // ... Cycle types ...
              return;
            }
          }
        }
        if (shift || ctrl) return;
        _isDrawing = true; _startCoords = clickCoords; _lockedRotation = CalculateCameraRotation(); InternalSetupPreview();
      }
      else { InternalFinalizeRuler(_startCoords, clickCoords, _lockedRotation, null, 0, 0, 0); _isDrawing = false; }
    }

    public void HandleMouseMove(Vector3Int c) { if (_isDrawing) InternalUpdatePreview(_startCoords, c); }
    public void CancelOperation() { _isDrawing = false; if (_previewContainer != null) _previewContainer.SetActive(false); }
    public void DeleteAllRulers() { foreach (var r in _activeRulers) if (r.Container != null) Object.Destroy(r.Container); _activeRulers.Clear(); _segmentMap.Clear(); foreach (var q in _sharedQuads.Values) Object.Destroy(q); _sharedQuads.Clear(); CancelOperation(); }
  }
}