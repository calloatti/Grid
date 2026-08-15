using System;
using System.Collections.Generic;
using HarmonyLib;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.MechanicalSystem;

namespace Calloatti.Grid
{
  [HarmonyPatch(typeof(BlockObjectTool), "ProcessInput")]
  internal static class MechanicalConnectorPreviewPatch
  {
    [HarmonyPostfix]
    internal static void Postfix(BlockObjectTool __instance, PreviewPlacer ____previewPlacer)
    {
      MechanicalConnectorService svc = MechanicalConnectorService.Instance;
      if (svc == null) return;

      TransputProviderSpec provider = __instance.Template?.GetSpec<TransputProviderSpec>();
      if (provider == null || provider.Transputs.IsDefaultOrEmpty)
      {
        svc.ClearPreviews();
        return;
      }

      bool generator = false;
      MechanicalNodeSpec nodeSpec = __instance.Template.GetSpec<MechanicalNodeSpec>();
      if (nodeSpec != null && nodeSpec.PowerOutput > 0) generator = true;

      var blocks = new List<BlockObject>();
      Preview[] previews = ____previewPlacer._previews.Value;
      foreach (Preview preview in previews)
      {
        if (preview.GameObject.activeSelf)
        {
          blocks.Add(preview.BlockObject);
        }
      }

      svc.SetPreviews(blocks, provider.Transputs, generator);
    }
  }
}