using UnityEngine;
using Timberborn.TerrainSystem;

namespace Calloatti.TopoData
{
  public class TopoChunk
  {
    private readonly Vector2Int _chunkCoords;
    private readonly int _maxZ;
    private readonly GameObject _chunkRoot;

    private readonly MeshFilter[] _walkableFilters;
    private readonly MeshRenderer[] _walkableRenderers;

    private readonly MeshFilter[] _buriedFilters;
    private readonly MeshRenderer[] _buriedRenderers;

    private readonly Material _material;

    public bool IsDirty { get; set; } = true;
    public Vector2Int ChunkCoords => _chunkCoords;

    public TopoChunk(Vector2Int chunkCoords, int maxZ, Transform parent, Material material)
    {
      _chunkCoords = chunkCoords;
      _maxZ = maxZ;
      _material = material;

      _chunkRoot = new GameObject($"TopoChunk_{chunkCoords.x}_{chunkCoords.y}");
      _chunkRoot.transform.SetParent(parent);

      _walkableFilters = new MeshFilter[maxZ];
      _walkableRenderers = new MeshRenderer[maxZ];

      _buriedFilters = new MeshFilter[maxZ];
      _buriedRenderers = new MeshRenderer[maxZ];

      for (int z = 0; z < maxZ; z++)
      {
        // 1. Walkable Mesh Setup
        GameObject walkableObj = new GameObject($"L_{z}_Walkable");
        walkableObj.transform.SetParent(_chunkRoot.transform);
        _walkableFilters[z] = walkableObj.AddComponent<MeshFilter>();
        _walkableRenderers[z] = walkableObj.AddComponent<MeshRenderer>();
        _walkableFilters[z].mesh = new Mesh();
        if (_material != null) _walkableRenderers[z].sharedMaterial = _material;
        walkableObj.SetActive(false);

        // 2. Buried Mesh Setup
        GameObject buriedObj = new GameObject($"L_{z}_Buried");
        buriedObj.transform.SetParent(_chunkRoot.transform);
        _buriedFilters[z] = buriedObj.AddComponent<MeshFilter>();
        _buriedRenderers[z] = buriedObj.AddComponent<MeshRenderer>();
        _buriedFilters[z].mesh = new Mesh();
        if (_material != null) _buriedRenderers[z].sharedMaterial = _material;
        buriedObj.SetActive(false);
      }
    }

    public void UpdateVisibility(bool isActive, int maxVisibleLevel)
    {
      for (int z = 0; z < _maxZ; z++)
      {
        bool isAtSlice = (z == maxVisibleLevel);
        bool isBelowSlice = (z < maxVisibleLevel);

        // Walkable is visible as long as it's not sliced off
        bool showWalkable = isActive && (isAtSlice || isBelowSlice);

        // Buried is ONLY visible if you are actively slicing into its specific layer
        bool showBuried = isActive && isAtSlice;

        if (_walkableFilters[z].gameObject.activeSelf != showWalkable)
        {
          _walkableFilters[z].gameObject.SetActive(showWalkable);
        }

        if (_buriedFilters[z].gameObject.activeSelf != showBuried)
        {
          _buriedFilters[z].gameObject.SetActive(showBuried);
        }
      }
    }

    public MeshFilter GetWalkableFilter(int z) => _walkableFilters[z];
    public MeshFilter GetBuriedFilter(int z) => _buriedFilters[z];

    public void Destroy()
    {
      for (int i = 0; i < _maxZ; i++)
      {
        if (_walkableFilters[i] != null && _walkableFilters[i].sharedMesh != null)
          Object.Destroy(_walkableFilters[i].sharedMesh);

        if (_buriedFilters[i] != null && _buriedFilters[i].sharedMesh != null)
          Object.Destroy(_buriedFilters[i].sharedMesh);
      }
      if (_chunkRoot != null) Object.Destroy(_chunkRoot);
    }
  }
}