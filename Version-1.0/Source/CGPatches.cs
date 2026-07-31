using System.Collections.Generic;
using HarmonyLib;
using Timberborn.ConstructionGuidelines;
using UnityEngine;

namespace Calloatti.Grid
{
  [HarmonyPatch(typeof(ConstructionGuidelinesRenderingService), "AddCoordinatesToGuidelines")]
  internal static class CGPatches
  {
    private const string Tag = "[Grid] Guidelines:";

    [HarmonyPostfix]
    internal static void Postfix(
        Vector3 center,
        List<Matrix4x4> ____tilesAtSameLevel,
        List<Matrix4x4> ____tilesBelow,
        List<Matrix4x4> ____tilesAbove,
        CrossParameters ____lastCrossParameters)
    {
      CGService svc = CGService.Instance;
      if (svc == null) return;

      int cx = ____lastCrossParameters.Center.x;
      int cy = ____lastCrossParameters.Center.y;
      int minx = ____lastCrossParameters.Min.x;
      int miny = ____lastCrossParameters.Min.y;
      int maxx = ____lastCrossParameters.Max.x;
      int maxy = ____lastCrossParameters.Max.y;

      int rawCount = ____tilesAtSameLevel.Count + ____tilesBelow.Count + ____tilesAbove.Count;

      var topmost = new Dictionary<(int gx, int gy), (int gz, Vector3 world)>();
      foreach (Matrix4x4 m in ____tilesAtSameLevel)
        InsertTopmost(topmost, m);
      foreach (Matrix4x4 m in ____tilesBelow)
        InsertTopmost(topmost, m);
      foreach (Matrix4x4 m in ____tilesAbove)
        InsertTopmost(topmost, m);

      int wCount = 0, eCount = 0, sCount = 0, nCount = 0, offCount = 0;
      svc.Tiles.Clear();

      foreach (var entry in topmost)
      {
        int gx = entry.Key.gx;
        int gy = entry.Key.gy;
        Vector3 world = entry.Value.world;

        int distance = 0;
        if (gy == cy && gx < cx) { distance = minx - gx; wCount++; }
        else if (gy == cy && gx > cx) { distance = gx - maxx; eCount++; }
        else if (gx == cx && gy < cy) { distance = miny - gy; sCount++; }
        else if (gx == cx && gy > cy) { distance = gy - maxy; nCount++; }
        else { offCount++; continue; }

        if (distance >= 1 && distance <= svc.MaxNumber)
          svc.Tiles.Add((world, distance));
      }

      Debug.Log($"{Tag} center=({cx},{cy}) raw={rawCount} dup={topmost.Count} " +
                $"W:{wCount} E:{eCount} S:{sCount} N:{nCount} off={offCount} nums={svc.Tiles.Count}");
    }

    private static void InsertTopmost(Dictionary<(int gx, int gy), (int gz, Vector3 world)> dict, Matrix4x4 m)
    {
      Vector3 p = m.GetColumn(3);
      int gx = (int)p.x;
      int gy = (int)p.z;
      int gz = (int)p.y;
      var key = (gx, gy);
      if (!dict.TryGetValue(key, out var existing) || gz > existing.gz)
        dict[key] = (gz, p);
    }
  }

  [HarmonyPatch(typeof(CrossParameters), "Reset")]
  internal static class CGClearPatch
  {
    [HarmonyPostfix]
    internal static void Postfix()
    {
      CGService.Instance?.Tiles.Clear();
    }
  }
}
