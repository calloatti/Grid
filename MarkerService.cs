using System.Collections.Generic;
using Timberborn.TerrainSystem;
using Timberborn.Coordinates;
using Timberborn.LevelVisibilitySystem;
using Timberborn.SingletonSystem;
using Bindito.Core;
using UnityEngine;

namespace Calloatti.Grid
{
  public class MarkerService : IPostLoadableSingleton
  {
    public static MarkerService Instance { get; private set; }

    private readonly ITerrainService _terrainService;
    private readonly ILevelVisibilityService _levelVisibilityService;
    private readonly EventBus _eventBus;

    // Injected to access the ReadConfigFile() method natively
    private readonly GridRenderer _gridRenderer;

    private readonly Dictionary<int, List<GameObject>> _surfaceMarkerSlices = new Dictionary<int, List<GameObject>>();
    private readonly Dictionary<int, List<GameObject>> _sliceMarkerSlices = new Dictionary<int, List<GameObject>>();
    private readonly Dictionary<Vector2Int, int> _columnColors = new Dictionary<Vector2Int, int>();

    private Material _markerMaterial;
    private const float Thickness = 0.12f;
    private const float DiagonalLength = 0.65f;
    private const float HeightOffset = 0.05f;
    private const float SliceBaseHeight = 0.85f;
    private const float SurfaceBaseHeight = 1.00f;

    private List<Color> _palette;

    // Palette exposed to the Menu UI, populated during PostLoad
    public List<Color> Palette => _palette;

    [Inject]
    public MarkerService(
        ITerrainService terrainService,
        ILevelVisibilityService levelVisibilityService,
        EventBus eventBus,
        GridRenderer gridRenderer)
    {
      _terrainService = terrainService;
      _levelVisibilityService = levelVisibilityService;
      _eventBus = eventBus;
      _gridRenderer = gridRenderer;
      Instance = this;
    }

    public void PostLoad()
    {
      _eventBus.Register(this);

      // Request settings from GridRenderer. If the file doesn't exist, it will be created automatically.
      _palette = _gridRenderer.ReadConfigFile().MarkerPalette;
    }

    [OnEvent]
    public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent maxVisibleLevelChangedEvent)
    {
      UpdateVisibility();
    }

    public void Interact(Vector3Int columnCoords, int colorIndex)
    {
      Vector2Int col = new Vector2Int(columnCoords.x, columnCoords.y);
      if (_columnColors.ContainsKey(col))
      {
        ChangeColor(columnCoords);
      }
      else
      {
        AddMarker(columnCoords, colorIndex);
      }
    }

    public void DeleteMarker(Vector3Int columnCoords)
    {
      Vector2Int col = new Vector2Int(columnCoords.x, columnCoords.y);
      if (_columnColors.ContainsKey(col))
      {
        RemoveColumn(col);
      }
    }

    private void AddMarker(Vector3Int columnCoords, int colorIndex)
    {
      int x = columnCoords.x;
      int y = columnCoords.y;
      _columnColors[new Vector2Int(x, y)] = colorIndex;

      CreateMarkerPair(new Vector3Int(x, y, -1), colorIndex);

      for (int z = 0; z < _terrainService.Size.z; z++)
      {
        if (_terrainService.Underground(new Vector3Int(x, y, z)))
        {
          CreateMarkerPair(new Vector3Int(x, y, z), colorIndex);
        }
      }
      UpdateVisibility();
    }

    private void ChangeColor(Vector3Int clickedBlock)
    {
      Vector2Int column = new Vector2Int(clickedBlock.x, clickedBlock.y);
      int currentIndex = _columnColors[column];
      int nextIndex = (currentIndex + 1) % _palette.Count;
      _columnColors[column] = nextIndex;

      Color newColor = _palette[nextIndex];
      string searchKey = $"{column.x}_{column.y}_";

      UpdateMarkersInGroup(_surfaceMarkerSlices, searchKey, newColor);
      UpdateMarkersInGroup(_sliceMarkerSlices, searchKey, newColor);
    }

    private void CreateMarkerPair(Vector3Int coords, int colorIndex)
    {
      InitializeMaterial();
      Vector3 worldPos = CoordinateSystem.GridToWorld(new Vector3(coords.x + 0.5f, coords.y + 0.5f, 0));
      Color chosenColor = _palette[colorIndex];

      if (coords.z == -1)
      {
        Vector3 bedrockPos = new Vector3(worldPos.x, 0.0f + HeightOffset, worldPos.z);
        string bName = $"Surface_{coords.x}_{coords.y}_{-1}";
        AddToSliceGroup(_surfaceMarkerSlices, -1, CreateMarkerQuads(bedrockPos, bName, chosenColor));
        return;
      }

      string sName = $"Surface_{coords.x}_{coords.y}_{coords.z}";
      Vector3 sPos = new Vector3(worldPos.x, coords.z + SurfaceBaseHeight + HeightOffset, worldPos.z);
      AddToSliceGroup(_surfaceMarkerSlices, coords.z, CreateMarkerQuads(sPos, sName, chosenColor));

      string slName = $"Slice_{coords.x}_{coords.y}_{coords.z}";
      Vector3 slPos = new Vector3(worldPos.x, coords.z + SliceBaseHeight + HeightOffset, worldPos.z);
      AddToSliceGroup(_sliceMarkerSlices, coords.z, CreateMarkerQuads(slPos, slName, chosenColor));
    }

    public void RemoveColumn(Vector2Int col)
    {
      string searchKey = $"{col.x}_{col.y}_";
      void RemoveFromGroup(Dictionary<int, List<GameObject>> groups)
      {
        foreach (var slice in groups.Values)
        {
          for (int i = slice.Count - 1; i >= 0; i--)
          {
            GameObject quad = slice[i];
            if (quad != null && quad.name.Contains(searchKey))
            {
              Object.Destroy(quad);
              slice.RemoveAt(i);
            }
          }
        }
      }
      RemoveFromGroup(_surfaceMarkerSlices);
      RemoveFromGroup(_sliceMarkerSlices);
      _columnColors.Remove(col);
    }

    private void UpdateMarkersInGroup(Dictionary<int, List<GameObject>> groups, string key, Color color)
    {
      foreach (var slice in groups.Values)
      {
        foreach (var quad in slice)
        {
          if (quad != null && quad.name.Contains(key))
            quad.GetComponent<MeshRenderer>().material.color = color;
        }
      }
    }

    public void UpdateVisibility()
    {
      int maxV = _levelVisibilityService.MaxVisibleLevel;
      foreach (var kvp in _surfaceMarkerSlices)
      {
        bool isVisible = (kvp.Key < maxV);
        foreach (var m in kvp.Value) m.SetActive(isVisible);
      }
      foreach (var kvp in _sliceMarkerSlices)
      {
        bool isVisible = (kvp.Key == maxV);
        foreach (var m in kvp.Value) m.SetActive(isVisible);
      }
    }

    private void AddToSliceGroup(Dictionary<int, List<GameObject>> dict, int z, List<GameObject> quads)
    {
      if (!dict.ContainsKey(z)) dict[z] = new List<GameObject>();
      dict[z].AddRange(quads);
    }

    public void RemoveAllMarkers()
    {
      void Clear(Dictionary<int, List<GameObject>> d)
      {
        foreach (var l in d.Values) foreach (var o in l) if (o != null) Object.Destroy(o);
        d.Clear();
      }
      Clear(_surfaceMarkerSlices);
      Clear(_sliceMarkerSlices);
      _columnColors.Clear();
    }

    private List<GameObject> CreateMarkerQuads(Vector3 pos, string name, Color color)
    {
      return new List<GameObject> { CreateQuad(pos, 45f, name, color), CreateQuad(pos, -45f, name, color) };
    }

    private GameObject CreateQuad(Vector3 pos, float rotY, string name, Color color)
    {
      GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
      q.name = name;
      Object.Destroy(q.GetComponent<Collider>());
      MeshRenderer mr = q.GetComponent<MeshRenderer>();
      mr.material = new Material(_markerMaterial);
      mr.material.color = color;
      q.transform.localScale = new Vector3(Thickness, DiagonalLength, 1f);
      q.transform.position = pos;
      q.transform.rotation = Quaternion.Euler(90, rotY, 0);
      return q;
    }

    private void InitializeMaterial()
    {
      if (_markerMaterial == null) _markerMaterial = new Material(Shader.Find("Sprites/Default"));
    }
  }
}