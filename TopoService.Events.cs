using Bindito.Core;
using Timberborn.LevelVisibilitySystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;

namespace TimberbornModding.TopoData
{
  public partial class TopoService
  {
    private void OnTerrainHeightChanged(object s, TerrainHeightChangeEventArgs e)
    {
      // Flip the flag so the next hotkey press knows to regenerate
      _isDirty = true;
    }

    // El atributo [OnEvent] es obligatorio para que el EventBus de Timberborn lo llame
    [OnEvent]
    public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent e)
    {
      // Si el mod está activo y el jugador cambia el nivel de corte, actualizamos las capas
      if (_isActive)
      {
        UpdateVisibility();
      }
    }
  }
}