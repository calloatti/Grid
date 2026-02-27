using Bindito.Core;
using System.Collections.Generic;
using Timberborn.AssetSystem;
using Timberborn.CameraSystem;
using Timberborn.Coordinates;
using Timberborn.LevelVisibilitySystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Calloatti.Grid
{
  public class RulerService : IPostLoadableSingleton
  {
    private readonly IAssetLoader _assetLoader;
    private readonly ITerrainService _terrainService;
    private readonly ILevelVisibilityService _levelVisibilityService;
    private readonly CameraService _cameraService;
    private readonly EventBus _eventBus;

    private Material _rulerMaterial;

    private GameObject _previewContainer;
    private GameObject[] _previewQuads;
    private Quaternion _lockedRotation;

    private readonly List<RulerInstance> _activeRulers = new List<RulerInstance>();

    // Estados internos del Servicio
    private bool _isDrawing = false;
    private Vector3Int _startCoords;

    private const int RULER_LENGTH = 65;
    private const float HeightOffset = 0.05f;
    private const float SurfaceBaseHeight = 1.00f;
    private const float SliceBaseHeight = 0.85f;

    [Inject]
    public RulerService(
        IAssetLoader assetLoader,
        ITerrainService terrainService,
        ILevelVisibilityService levelVisibilityService,
        CameraService cameraService,
        EventBus eventBus)
    {
      _assetLoader = assetLoader;
      _terrainService = terrainService;
      _levelVisibilityService = levelVisibilityService;
      _cameraService = cameraService;
      _eventBus = eventBus;
    }

    public void PostLoad()
    {
      _eventBus.Register(this);
      Texture2D tex = _assetLoader.Load<Texture2D>("Sprites/ruler-strip0");
      _rulerMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
    }

    [OnEvent]
    public void OnMaxVisibleLevelChanged(MaxVisibleLevelChangedEvent maxVisibleLevelChangedEvent)
    {
      int maxV = _levelVisibilityService.MaxVisibleLevel;
      foreach (var ruler in _activeRulers)
      {
        foreach (var seg in ruler.Segments) UpdateQuadHeight(seg.Obj, seg.Coords, maxV);
      }
    }

    // =========================================================
    // COMANDOS PÚBLICOS
    // =========================================================

    public void HandleClick(Vector3Int clickCoords)
    {
      if (!_isDrawing)
      {
        // Usamos la misma lógica que en MarkerTool.cs
        bool isShiftDown = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

        if (isShiftDown)
        {
          // 1. Intentar borrar regla existente
          Vector2Int coords2D = new Vector2Int(clickCoords.x, clickCoords.y);
          for (int i = _activeRulers.Count - 1; i >= 0; i--)
          {
            var ruler = _activeRulers[i];
            foreach (var seg in ruler.Segments)
            {
              if (seg.Coords == coords2D)
              {
                Object.Destroy(ruler.Container);
                _activeRulers.RemoveAt(i);
                return; // Borrado exitoso, salimos
              }
            }
          }
          return; // Si presionó Shift pero no había nada, no iniciamos dibujo
        }

        // 2. Iniciar dibujo normal (sin Shift)
        _isDrawing = true;
        _startCoords = clickCoords;
        _lockedRotation = CalculateCameraRotation();
        InternalSetupPreview();
      }
      else
      {
        // Finalizar dibujo
        InternalFinalizeRuler(_startCoords, clickCoords);
        _isDrawing = false;
      }
    }

    public void HandleMouseMove(Vector3Int currentCoords)
    {
      if (_isDrawing) InternalUpdatePreview(_startCoords, currentCoords);
    }

    // 1. Para abortar el dibujo actual (ESC, cambio de herramienta o segundo clic)
    public void CancelOperation()
    {
      _isDrawing = false;
      if (_previewContainer != null) _previewContainer.SetActive(false);
    }

    // 2. El método que usa tu botón de la interfaz para limpiar el mapa
    public void DeleteAllRulers()
    {
      // Destruimos los GameObjects de todas las reglas guardadas
      foreach (var r in _activeRulers)
      {
        if (r.Container != null) Object.Destroy(r.Container);
      }

      // Vaciamos la lista de datos
      _activeRulers.Clear();

      // Por seguridad, también cancelamos cualquier preview que estuviera activo
      CancelOperation();
    }
    // =========================================================
    // LÓGICA PRIVADA
    // =========================================================

    private void InternalSetupPreview()
    {
      if (_previewContainer == null)
      {
        _previewContainer = new GameObject("RulerPreviewContainer");
        _previewQuads = new GameObject[RULER_LENGTH];
        for (int i = 0; i < RULER_LENGTH; i++)
        {
          GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
          Object.Destroy(quad.GetComponent<Collider>());
          quad.transform.SetParent(_previewContainer.transform);
          quad.GetComponent<MeshRenderer>().material = _rulerMaterial;
          quad.SetActive(false);
          _previewQuads[i] = quad;
        }
      }

      // Aplicar la rotación fija a los quads del pool
      for (int i = 0; i < RULER_LENGTH; i++) _previewQuads[i].transform.rotation = _lockedRotation;

      _previewContainer.SetActive(true);
    }

    private void InternalUpdatePreview(Vector3Int start, Vector3Int current)
    {
      if (_previewContainer == null || !_previewContainer.activeSelf) return;

      Vector2Int step = GetStepDirection(start, current);
      int maxV = _levelVisibilityService.MaxVisibleLevel;

      for (int i = 0; i < RULER_LENGTH; i++)
      {
        GameObject quad = _previewQuads[i];
        Vector2Int tile = new Vector2Int(start.x + (step.x * i), start.y + (step.y * i));

        if (!_terrainService.Contains(tile)) { quad.SetActive(false); continue; }

        quad.SetActive(true);
        AdjustSegmentUVs(quad, i);
        UpdateQuadHeight(quad, tile, maxV);
      }
    }

    private void InternalFinalizeRuler(Vector3Int start, Vector3Int end)
    {
      if (_previewContainer != null) _previewContainer.SetActive(false);

      GameObject container = new GameObject("ActiveRuler");
      var instance = new RulerInstance { Container = container, Segments = new List<RulerSegment>() };
      Vector2Int step = GetStepDirection(start, end);
      int maxV = _levelVisibilityService.MaxVisibleLevel;

      for (int i = 0; i < RULER_LENGTH; i++)
      {
        Vector2Int tile = new Vector2Int(start.x + (step.x * i), start.y + (step.y * i));
        if (!_terrainService.Contains(tile)) continue;

        bool keepOldOrigin = false;

        // Limpiar superposiciones antes de crear la nueva baldosa
        for (int r = _activeRulers.Count - 1; r >= 0; r--)
        {
          var oldRuler = _activeRulers[r];
          for (int s = oldRuler.Segments.Count - 1; s >= 0; s--)
          {
            if (oldRuler.Segments[s].Coords == tile)
            {
              if (i == 0)
              {
                // Si estamos en el origen (primer clic) y hay una regla vieja, LA CONSERVAMOS
                keepOldOrigin = true;
              }
              else
              {
                // Para el resto del trayecto, sí destruimos la superposición
                Object.Destroy(oldRuler.Segments[s].Obj);
                oldRuler.Segments.RemoveAt(s);
              }
            }
          }

          // Si la regla vieja se quedó sin segmentos, limpiamos el contenedor y la lista
          if (oldRuler.Segments.Count == 0)
          {
            Object.Destroy(oldRuler.Container);
            _activeRulers.RemoveAt(r);
          }
        }

        // Si decidimos conservar la baldosa vieja de origen, saltamos la creación del 
        // nuevo cuadrado vacío para evitar z-fighting visual
        if (keepOldOrigin) continue;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(container.transform);
        quad.transform.rotation = _lockedRotation; // USA LA ROTACIÓN DEL CLIC 1
        quad.GetComponent<MeshRenderer>().material = _rulerMaterial;

        AdjustSegmentUVs(quad, i);
        UpdateQuadHeight(quad, tile, maxV);
        instance.Segments.Add(new RulerSegment { Obj = quad, Coords = tile });
      }
      _activeRulers.Add(instance);
    }

    private Vector2Int GetStepDirection(Vector3Int start, Vector3Int end)
    {
      int dx = end.x - start.x;
      int dy = end.y - start.y;
      if (Mathf.Abs(dx) >= Mathf.Abs(dy)) return new Vector2Int(dx >= 0 ? 1 : -1, 0);
      return new Vector2Int(0, dy >= 0 ? 1 : -1);
    }

    private Quaternion CalculateCameraRotation()
    {
      float camAngle = _cameraService.HorizontalAngle;
      float snappedAngle = Mathf.Round(camAngle / 90f) * 90f;
      return Quaternion.Euler(90, snappedAngle, 0);
    }

    private void UpdateQuadHeight(GameObject quad, Vector2Int coords, int maxV)
    {
      int terrainHeight = _terrainService.GetTerrainHeightBelow(new Vector3Int(coords.x, coords.y, _terrainService.Size.z - 1));
      int terrainZ = terrainHeight - 1;

      Vector3 worldPos = CoordinateSystem.GridToWorld(new Vector3(coords.x + 0.5f, coords.y + 0.5f, 0));
      float finalY = (terrainZ < maxV)
          ? (terrainZ + SurfaceBaseHeight + HeightOffset)
          : (maxV + SliceBaseHeight + HeightOffset);

      quad.transform.position = new Vector3(worldPos.x, finalY, worldPos.z);
    }

    private void AdjustSegmentUVs(GameObject go, int index)
    {
      Mesh mesh = go.GetComponent<MeshFilter>().mesh;
      Vector2[] uvs = new Vector2[4];
      float s = (float)index / RULER_LENGTH;
      float e = (float)(index + 1) / RULER_LENGTH;
      uvs[0] = new Vector2(s, 0); uvs[1] = new Vector2(e, 0);
      uvs[2] = new Vector2(s, 1); uvs[3] = new Vector2(e, 1);
      mesh.uv = uvs;
    }

    private class RulerInstance { public GameObject Container; public List<RulerSegment> Segments; }
    private class RulerSegment { public GameObject Obj; public Vector2Int Coords; }
  }
}