using System.Collections.Generic;
using Bindito.Core;
using Timberborn.AssetSystem;
using Timberborn.BlueprintSystem;
using Timberborn.CameraSystem;
using Timberborn.ConstructionGuidelines;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  public class CGService : IPostLoadableSingleton, ILateUpdatableSingleton, System.IDisposable
  {
    public static CGService Instance { get; private set; }
    internal int MaxNumber { get; private set; }

    private const int GRID_COLUMNS = 256;
    private const int GRID_ROWS = 6;
    private const int NUMBER_ROW = 5;
    private const float FADE_START_ALPHA = 0.6f;
    private const int FADE_COUNT = 6;

    private readonly IAssetLoader _assetLoader;
    private readonly CameraService _cameraService;
    private readonly ISpecService _specService;
    private bool _debugLogged;
    private Quaternion? _lastRotation = null;
    private readonly List<bool> _active = new List<bool>();

    private GameObject _root;
    private Mesh[] _numberMeshes;
    private Material[] _numberMaterials;
    private readonly List<GameObject> _quads = new List<GameObject>();
    private readonly List<MeshFilter> _filters = new List<MeshFilter>();
    private readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
    private readonly List<int> _lastNumber = new List<int>();
    internal List<(Vector3 world, int distance)> Tiles = new List<(Vector3, int)>();

    [Inject]
    public CGService(IAssetLoader assetLoader, CameraService cameraService, ISpecService specService)
    {
      _assetLoader = assetLoader;
      _cameraService = cameraService;
      _specService = specService;
    }

    public void PostLoad()
    {
      Instance = this;

      MaxNumber = _specService.GetSingleSpec<ConstructionGuidelinesSpec>().Radius;
      int poolSize = MaxNumber * 4;

      _root = new GameObject("CGService");

      GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
      Mesh quadMesh = tempQuad.GetComponent<MeshFilter>().sharedMesh;
      Object.Destroy(tempQuad);

      Texture2D tex = _assetLoader.Load<Texture2D>("Sprites/grid-atlas");
      Shader shader = Shader.Find("Sprites/Default");

      _numberMeshes = new Mesh[MaxNumber + 1];
      _numberMaterials = new Material[MaxNumber + 1];
      for (int n = 1; n <= MaxNumber; n++)
      {
        _numberMeshes[n] = BakeNumberMesh(quadMesh, n);
        Material mat = new Material(shader) { mainTexture = tex };
        mat.color = new Color(1f, 1f, 1f, GetNumberAlpha(n));
        _numberMaterials[n] = mat;
      }

      for (int i = 0; i < poolSize; i++)
      {
        GameObject q = new GameObject($"CGNum_{i}");
        q.transform.SetParent(_root.transform);
        q.transform.rotation = Quaternion.Euler(90, 0, 0);
        MeshFilter mf = q.AddComponent<MeshFilter>();
        MeshRenderer mr = q.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _numberMaterials[1];
        mf.sharedMesh = _numberMeshes[1];
        q.SetActive(false);
        _quads.Add(q);
        _filters.Add(mf);
        _renderers.Add(mr);
        _lastNumber.Add(0);
        _active.Add(false);
      }
    }

    public void LateUpdateSingleton()
    {
      if (!_debugLogged)
      {
        _debugLogged = true;
        Debug.Log("[Grid] CGService LateUpdate: quads=" + _quads.Count +
                  " filters=" + _filters.Count +
                  " material=" + (_numberMaterials != null) +
                  " meshes=" + (_numberMeshes != null ? _numberMeshes.Length : 0) +
                  " tiles=" + Tiles.Count +
                  " rootActive=" + (_root != null ? _root.activeSelf : false));
      }

      int tileCount = Tiles.Count;
      if (tileCount == 0)
      {
        for (int i = 0; i < _active.Count; i++)
        {
          if (_active[i])
          {
            _active[i] = false;
            _quads[i].SetActive(false);
          }
        }
        return;
      }

      Quaternion rot = Quaternion.Euler(90, CalculateCameraRotation(), 0);
      bool rotationChanged = _lastRotation == null || rot != _lastRotation.Value;
      _lastRotation = rot;

      for (int i = 0; i < _quads.Count; i++)
      {
        if (i < tileCount)
        {
          var (world, distance) = Tiles[i];
          if (!_active[i])
          {
            _active[i] = true;
            _quads[i].SetActive(true);
          }
          _quads[i].transform.position = new Vector3(world.x, world.y + 0.022f, world.z);
          if (rotationChanged)
            _quads[i].transform.rotation = rot;
          if (_lastNumber[i] != distance)
          {
            _lastNumber[i] = distance;
            _filters[i].sharedMesh = _numberMeshes[distance];
            _renderers[i].sharedMaterial = _numberMaterials[distance];
          }
        }
        else if (_active[i])
        {
          _active[i] = false;
          _quads[i].SetActive(false);
          _lastNumber[i] = 0;
        }
      }
    }

    private float CalculateCameraRotation()
    {
      float angle = Mathf.Repeat(_cameraService.HorizontalAngle, 360f);
      return Mathf.Floor((angle + 22.5f) / 90f) * 90f;
    }

    private float GetNumberAlpha(int n)
    {
      int fadeStart = MaxNumber - FADE_COUNT;
      if (n <= fadeStart) return FADE_START_ALPHA;
      float t = (float)(n - fadeStart) / FADE_COUNT;
      return FADE_START_ALPHA * (1f - t);
    }

    private static Mesh BakeNumberMesh(Mesh template, int number)
    {
      Mesh mesh = Object.Instantiate(template);
      float uStart = (float)number / GRID_COLUMNS;
      float uEnd = (float)(number + 1) / GRID_COLUMNS;
      float vTop = 1.0f - ((float)NUMBER_ROW / GRID_ROWS);
      float vBottom = 1.0f - ((float)(NUMBER_ROW + 1) / GRID_ROWS);
      mesh.uv = new Vector2[]
      {
        new Vector2(uStart, vBottom),
        new Vector2(uEnd, vBottom),
        new Vector2(uStart, vTop),
        new Vector2(uEnd, vTop)
      };
      return mesh;
    }

    public void Dispose()
    {
      if (_numberMaterials != null)
      {
        foreach (Material mat in _numberMaterials)
          if (mat != null) Object.Destroy(mat);
        _numberMaterials = null;
      }
      if (_numberMeshes != null)
      {
        foreach (Mesh mesh in _numberMeshes)
          if (mesh != null) Object.Destroy(mesh);
        _numberMeshes = null;
      }
      if (_root != null)
        Object.Destroy(_root);
      _quads.Clear();
      _filters.Clear();
      _renderers.Clear();
      _lastNumber.Clear();
      _active.Clear();
      Tiles.Clear();
      if (Instance == this) Instance = null;
    }
  }
}