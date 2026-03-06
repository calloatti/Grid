using UnityEngine;

namespace Calloatti.TopoData
{
  public partial class TopoService
  {
    public void ToggleTopoData()
    {
      _isActive = !_isActive;

      if (_isActive)
      {
        Quaternion currentRotation = CalculateCameraRotation();

        if (_isDirty)
        {
          // Si el terreno cambió, construimos desde cero
          GenerateSnapshot();
          _isDirty = false;
          _lastRotation = currentRotation;
        }
        else if (currentRotation != _lastRotation)
        {
          // Si el terreno está igual pero la cámara giró, rotamos los vértices directamente
          RotateExistingMeshes(currentRotation);
          _lastRotation = currentRotation;
        }

        // Ahora llama a la función sin parámetros, la cual calculará el nivel correcto
        UpdateVisibility();
        _notificationService.SendNotification("Topo Data: ON");
      }
      else
      {
        HideAll();
        _notificationService.SendNotification("Topo Data: OFF");
      }
    }
  }
}