using System.Collections.Generic;
using System.Linq;
using Timberborn.Common;
using Timberborn.MapIndexSystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  public class WaterSpreadSimulator : ILoadableSingleton
  {
    private readonly EventBus _eventBus;
    private readonly WaterPlannedArea _waterPlannedArea;
    private readonly ITerrainService _terrainService;
    private readonly MapIndexService _mapIndexService;

    private readonly HashSet<Vector3Int> _moistTiles = new HashSet<Vector3Int>();
    public IEnumerable<Vector3Int> MoistTiles => _moistTiles.AsReadOnlyEnumerable();

    private const int MaxClusterSaturation = 8;
    private const float EnergyPerSaturation = 2f;
    private const float VerticalSpreadCostMultiplier = 3f;
    private const float MinimumMoisture = 0.01f;

    public WaterSpreadSimulator(
        EventBus eventBus,
        WaterPlannedArea waterPlannedArea,
        ITerrainService terrainService,
        MapIndexService mapIndexService)
    {
      _eventBus = eventBus;
      _waterPlannedArea = waterPlannedArea;
      _terrainService = terrainService;
      _mapIndexService = mapIndexService;
    }

    public void Load()
    {
      _eventBus.Register(this);
      _terrainService.TerrainHeightChanged += OnTerrainHeightChanged;
      RecalculateSpread();
    }

    [OnEvent]
    public void OnWaterPlannedAreaChanged(WaterPlannedAreaChangedEvent e) => RecalculateSpread();
    private void OnTerrainHeightChanged(object sender, TerrainHeightChangeEventArgs e) => RecalculateSpread();

    private void RecalculateSpread()
    {
      _moistTiles.Clear();

      if (_waterPlannedArea.IsEmpty)
      {
        _eventBus.Post(new MoistureSpreadChangedEvent());
        return;
      }

      // --- 1. NATIVE CLUSTER SATURATION MATH ---

      HashSet<Vector2Int> water2D = new HashSet<Vector2Int>(_waterPlannedArea.Area.Select(t => t.XY()));
      Dictionary<Vector2Int, int> wateredNeighbors = new Dictionary<Vector2Int, int>();

      Vector2Int[] neighbors8 = {
                new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
            };

      Vector2Int[] neighbors4 = {
                new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0)
            };

      foreach (Vector2Int tile in water2D)
      {
        int count = 0;
        foreach (Vector2Int offset in neighbors8)
        {
          if (water2D.Contains(tile + offset)) count++;
        }
        wateredNeighbors[tile] = count + 1;
      }

      Dictionary<Vector2Int, int> clusterSaturations = new Dictionary<Vector2Int, int>();
      foreach (Vector2Int tile in water2D)
      {
        int maxCount = wateredNeighbors[tile];
        foreach (Vector2Int offset in neighbors4)
        {
          if (wateredNeighbors.TryGetValue(tile + offset, out int neighborCount))
          {
            if (neighborCount > maxCount)
            {
              maxCount = neighborCount - 1;
            }
          }
        }
        clusterSaturations[tile] = Mathf.Min(MaxClusterSaturation, maxCount);
      }

      // --- 2. NATIVE BFS MOISTURE SPREAD ---

      Queue<Vector3Int> queue = new Queue<Vector3Int>();
      Dictionary<Vector3Int, float> bestEnergyAtTile = new Dictionary<Vector3Int, float>();

      foreach (Vector3Int waterTile in _waterPlannedArea.Area)
      {
        float initialEnergy = clusterSaturations[waterTile.XY()] * EnergyPerSaturation;
        queue.Enqueue(waterTile);
        bestEnergyAtTile[waterTile] = initialEnergy;
        // Do not add the initial blue tile directly to _moistTiles
      }

      (Vector2Int offset, float cost, bool isOrthogonal)[] spreadCosts = new (Vector2Int, float, bool)[] {
                (new Vector2Int(0, 0), 0f, true), // Evaluates the current column for tiles under/over the water
                (new Vector2Int(0, 1), 1f, true), (new Vector2Int(0, -1), 1f, true),
                (new Vector2Int(1, 0), 1f, true), (new Vector2Int(-1, 0), 1f, true),
                (new Vector2Int(1, 1), 1.414f, false), (new Vector2Int(1, -1), 1.414f, false),
                (new Vector2Int(-1, 1), 1.414f, false), (new Vector2Int(-1, -1), 1.414f, false)
            };

      while (queue.Count > 0)
      {
        Vector3Int currentPos = queue.Dequeue();
        float currentEnergy = bestEnergyAtTile[currentPos];
        bool isCurrentWater = _waterPlannedArea.Contains(currentPos);

        foreach (var spread in spreadCosts)
        {
          Vector2Int neighbor2D = currentPos.XY() + spread.offset;

          if (!_terrainService.Contains(neighbor2D)) continue;

          foreach (Vector3Int neighborPos in _terrainService.GetAllHeightsInCell(neighbor2D))
          {
            int neighborHeight = neighborPos.z;

            // Secret 1: Free orthogonal wetting from water
            float baseCost = spread.cost;
            if (isCurrentWater && spread.isOrthogonal)
            {
              baseCost = 0f;
            }

            // Secret 2: Water Ceiling Math
            float climbPenalty = 0f;
            int heightDiff = neighborHeight - currentPos.z;
            if (isCurrentWater)
            {
              int waterEffectiveHeight = currentPos.z + 1; // Mathf.CeilToInt(Z + 0.65)
              heightDiff = neighborHeight - waterEffectiveHeight;
            }

            if (heightDiff > 0)
            {
              climbPenalty = heightDiff * VerticalSpreadCostMultiplier;
            }

            float totalCost = baseCost + climbPenalty;
            float newEnergy = currentEnergy - totalCost;

            if (newEnergy >= MinimumMoisture)
            {
              if (!bestEnergyAtTile.TryGetValue(neighborPos, out float existingEnergy) || newEnergy > existingEnergy)
              {
                bestEnergyAtTile[neighborPos] = newEnergy;
                queue.Enqueue(neighborPos);

                // Exclude drawing a green tile on the exact Z of a blue water tile
                if (!_waterPlannedArea.Contains(neighborPos))
                {
                  _moistTiles.Add(neighborPos);
                }
              }
            }
          }
        }
      }

      _eventBus.Post(new MoistureSpreadChangedEvent());
    }
  }
}