using Timberborn.LevelVisibilitySystem;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  public partial class GridRenderer
  {
    private readonly ILevelVisibilityService _levelVisibilityService;

    [OnEvent]
    public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent maxVisibleLevelChangedEvent)
    {
      bool isCurrentlyEnabled = _gridMeshRenderer != null && _gridMeshRenderer.enabled;

      // NUEVO: Solo actualizamos dinámicamente si la grilla está activa Y nació siguiendo la capa
      if (isCurrentlyEnabled && _isTrackingLayer)
      {
        float targetZHeight = GetTargetZHeight(out bool isLayerBased);

        // Actualizamos el estado interno por si el jugador apagó las capas (volvió a nivel 33)
        _isTrackingLayer = isLayerBased;

        if (Mathf.Abs(targetZHeight - _currentZHeight) > 0.001f)
        {
          Debug.Log($"{GridConfigurator.Prefix} [MaxLevelSync] Seguimiento de capa activo. Refrescando grilla a altura: {targetZHeight}");
          _currentZHeight = targetZHeight;
          GenerateBakedGrid(_currentZHeight);
        }
      }
    }
  }
}