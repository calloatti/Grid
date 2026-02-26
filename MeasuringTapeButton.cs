using System.Collections.Generic;
using Timberborn.BottomBarSystem;
using Timberborn.ToolButtonSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  public class MeasuringTapeButton : IBottomBarElementsProvider
  {
    private readonly ToolButtonFactory _toolButtonFactory;
    private readonly MeasuringTool _measuringTool;

    public MeasuringTapeButton(
        ToolButtonFactory toolButtonFactory,
        MeasuringTool measuringTool)
    {
      _toolButtonFactory = toolButtonFactory;
      _measuringTool = measuringTool;
    }

    public IEnumerable<BottomBarElement> GetElements()
    {
      // PASAMOS SOLO EL NOMBRE: "MeasuringIcon"
      // El juego internamente busca en: Sprites/BottomBar/MeasuringIcon
      ToolButton toolButton = _toolButtonFactory.CreateGrouplessRed(_measuringTool, "MeasuringIcon");

      yield return BottomBarElement.CreateSingleLevel(toolButton.Root);
    }
  }
}