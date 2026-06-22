using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.NaturalResources;
using Timberborn.Ruins;
using UnityEngine;

namespace Calloatti.Grid
{
  public partial class MarkerService
  {
    private void RecalculateColumnCache(Vector2Int col)
    {
      if (!_activeMarkers.TryGetValue(col, out MarkerData data)) return;
      data.Surfaces.Clear();

      int mapZ = _terrainService.Size.z;

      // 1. BEDROCK
      bool hasBedrock = !_blockService.AnyObjectAt(new Vector3Int(col.x, col.y, 0)) && !_terrainService.Underground(new Vector3Int(col.x, col.y, 0));
      if (hasBedrock)
      {
        data.Surfaces.Add(new SurfaceData { RoofZ = -1, RequiredMaxV = 0 });
      }

      BlockObject activeStructure = null;

      // 2. ESCANEO INTELIGENTE
      for (int z = 0; z < mapZ; z++)
      {
        Vector3Int pos = new Vector3Int(col.x, col.y, z);

        bool isTerrain = _terrainService.Underground(pos);
        BlockObject topObj = null;

        // A. Revisar qué objetos físicos están registrados en este voxel
        foreach (BlockObject obj in _blockService.GetObjectsAt(pos))
        {
          // Ignorar árboles, cultivos y ruinas (el marcador caerá al suelo)
          if (obj.GetComponent<NaturalResource>() != null || obj.GetComponent<Ruin>() != null) continue;

          // Si es un edificio real o un objeto sólido del mapa, lo registramos
          if (obj.Solid || obj.GetComponent<Building>() != null)
          {
            topObj = obj;
            activeStructure = obj; // Recordamos este edificio a medida que subimos
            break;
          }
        }

        // B. El motor dice que aquí hay aire. ¿Pero estamos dentro de la caja de un edificio?
        // (Esto atrapa los bloques "Occupations: None" en los techos como el Fermenter).
        if (topObj == null && activeStructure != null && activeStructure.PositionedBlocks.HasBlockAt(pos))
        {
          topObj = activeStructure;
        }
        else if (topObj == null)
        {
          activeStructure = null; // Ya salimos de la caja del edificio
        }

        // C. Si encontramos una superficie válida (Terreno o Edificio)
        if (isTerrain || topObj != null)
        {
          Vector3Int posAbove = new Vector3Int(col.x, col.y, z + 1);
          bool terrainAbove = z + 1 < mapZ && _terrainService.Underground(posAbove);
          bool bottomOccupiedAbove = false;

          if (z + 1 < mapZ)
          {
            BlockObject objAbove = _blockService.GetBottomObjectAt(posAbove);

            // Si el objeto de arriba es SÓLIDO y no es parte de nuestro edificio, nos tapa
            if (objAbove != null && objAbove.Solid && objAbove != topObj)
            {
              bottomOccupiedAbove = true;
            }

            // Si nuestro edificio continúa en el bloque de arriba (incluso como "None"), nos tapa
            if (!isTerrain && topObj != null && topObj.PositionedBlocks.HasBlockAt(posAbove))
            {
              bottomOccupiedAbove = true;
            }
          }

          // D. Si nada nos tapa, ¡este es el techo final!
          if (!terrainAbove && !bottomOccupiedAbove)
          {
            int reqMaxV = z + 1;

            if (!isTerrain && topObj != null)
            {
              if (_blockService.GetBottomObjectAt(topObj.Coordinates) == topObj)
              {
                reqMaxV = topObj.Coordinates.z;
              }
            }

            // El marcador se coloca exactamente en 'z'. Sin hacks de elevación.
            data.Surfaces.Add(new SurfaceData { RoofZ = z, RequiredMaxV = reqMaxV });
          }
        }
      }
    }
  }
}