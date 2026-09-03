using HarmonyLib;
using Timberborn.ModManagerScene;
using Calloatti.Config;
using UnityEngine;

namespace Calloatti.Grid
{
  public class ModStarter : IModStarter
  {
    public static SimpleConfig Config { get; private set; }
    public static string ModPath { get; private set; }

    public void StartMod(IModEnvironment modEnvironment)
    {
      Debug.Log("[Grid] IModStarter.StartMod");

      ModPath = modEnvironment.ModPath;
      Config = new SimpleConfig(ModPath);

      new Harmony("Calloatti.Grid").PatchAll();
    }
  }
}