namespace TimberbornModding.TopoData
{
  public partial class TopoService
  {
    public void ToggleTopoData()
    {
      _isActive = !_isActive;

      if (_isActive)
      {
        // Only regenerate if the terrain changed since the last build
        if (_isDirty)
        {
          GenerateSnapshot();
          _isDirty = false; // Reset the flag after building
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