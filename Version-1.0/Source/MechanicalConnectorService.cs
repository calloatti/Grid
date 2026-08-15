using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Bindito.Core;
using Timberborn.AssetSystem;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.Localization;
using Timberborn.MechanicalSystem;
using Timberborn.QuickNotificationSystem;
using Timberborn.Rendering;
using Timberborn.SingletonSystem;
using Timberborn.ToolSystem;
using UnityEngine;
using UnityEngine.Rendering;

namespace Calloatti.Grid
{
  public enum ConnectorDisplayState
  {
    Off,
    Full,
    PreviewOnly
  }

  public class MechanicalConnectorService : ILoadableSingleton, IPostLoadableSingleton, ILateUpdatableSingleton, IDisposable
  {
    private const string InputMeshPath = "Markers/MechanicalMarkerInput";
    private const string OutputMeshPath = "Markers/MechanicalMarkerOutput";

    public MechanicalConnectorSettings Settings { get; private set; } = new MechanicalConnectorSettings();

    public static MechanicalConnectorService Instance { get; private set; }

    private readonly TransputMap _transputMap;
    private readonly MeshDrawerFactory _meshDrawerFactory;
    private readonly IAssetLoader _assetLoader;
    private readonly MechanicalConnectorInputService _inputService;
    private readonly ToolService _toolService;
    private readonly QuickNotificationService _notificationService;
    private readonly ILoc _loc;

    private MeshDrawer _inputDrawer;
    private MeshDrawer _outputDrawer;
    private MeshDrawer _floorInputDrawer;
    private MeshDrawer _floorOutputDrawer;

    private Material _markerMaterial;
    private Material _floorMaterial;

    private readonly HashSet<Transput> _transputs = new HashSet<Transput>();
    private readonly List<Matrix4x4> _inputMatrices = new List<Matrix4x4>();
    private readonly List<Matrix4x4> _outputMatrices = new List<Matrix4x4>();
    private readonly List<Matrix4x4> _floorInputMatrices = new List<Matrix4x4>();
    private readonly List<Matrix4x4> _floorOutputMatrices = new List<Matrix4x4>();

    private readonly List<BlockObject> _previewBlocks = new List<BlockObject>();
    private ImmutableArray<TransputSpec> _previewTransputSpecs;
    private bool _previewGenerator;

    public ConnectorDisplayState State { get; private set; } = ConnectorDisplayState.Off;

    [Inject]
    public MechanicalConnectorService(
        TransputMap transputMap,
        MeshDrawerFactory meshDrawerFactory,
        IAssetLoader assetLoader,
        MechanicalConnectorInputService inputService,
        ToolService toolService,
        QuickNotificationService notificationService,
        ILoc loc)
    {
      _transputMap = transputMap;
      _meshDrawerFactory = meshDrawerFactory;
      _assetLoader = assetLoader;
      _inputService = inputService;
      _toolService = toolService;
      _notificationService = notificationService;
      _loc = loc;
    }

    public void Load()
    {
      Instance = this;
      Settings.LoadFromSimpleConfig();
      CreateDrawers();

      _transputMap.TransputAdded += OnTransputAdded;
      _transputMap.TransputRemoved += OnTransputRemoved;
      Debug.Log($"{MechanicalConnectorConfigurator.Prefix} Service loaded.");
    }

    public void PostLoad()
    {
      _inputService.OnToggleConnectors += ToggleConnectors;
    }

    public void LateUpdateSingleton()
    {
      if (State == ConnectorDisplayState.Off) return;
      if (_inputDrawer == null || _outputDrawer == null || _floorInputDrawer == null || _floorOutputDrawer == null) return;

      if (!(_toolService.ActiveTool is BlockObjectTool))
      {
        _previewBlocks.Clear();
      }

      _inputMatrices.Clear();
      _outputMatrices.Clear();
      _floorInputMatrices.Clear();
      _floorOutputMatrices.Clear();

      DrawPreviews();

      if (State == ConnectorDisplayState.Full)
      {
        foreach (Transput transput in _transputs)
        {
          if (transput.Connected) continue;

          bool generator = IsGenerator(transput);
          if (transput.Direction == Direction3D.Bottom)
          {
            AddFloorMatrix(transput.Coordinates, transput.Direction, generator);
          }
          else
          {
            AddMatrix(transput.Coordinates, transput.Direction, generator);
          }
        }
      }

      _inputDrawer.DrawMultiple(_inputMatrices);
      _outputDrawer.DrawMultiple(_outputMatrices);
      _floorInputDrawer.DrawMultiple(_floorInputMatrices);
      _floorOutputDrawer.DrawMultiple(_floorOutputMatrices);
    }

    public void Dispose()
    {
      _transputMap.TransputAdded -= OnTransputAdded;
      _transputMap.TransputRemoved -= OnTransputRemoved;
      _inputService.OnToggleConnectors -= ToggleConnectors;

      if (Instance == this) Instance = null;

      if (_markerMaterial != null) UnityEngine.Object.Destroy(_markerMaterial);
      if (_floorMaterial != null) UnityEngine.Object.Destroy(_floorMaterial);

      _inputMatrices.Clear();
      _outputMatrices.Clear();
      _floorInputMatrices.Clear();
      _floorOutputMatrices.Clear();
      _previewBlocks.Clear();
    }

    public void SetPreviews(List<BlockObject> blocks, ImmutableArray<TransputSpec> transputSpecs, bool generator)
    {
      _previewBlocks.Clear();
      _previewBlocks.AddRange(blocks);
      _previewTransputSpecs = transputSpecs;
      _previewGenerator = generator;
    }

    public void ClearPreviews()
    {
      _previewBlocks.Clear();
      _previewTransputSpecs = default;
      _previewGenerator = false;
    }

    private void CreateDrawers()
    {
      try
      {
        // Bottom gauges render always-on-top so they pierce terrain/buildings;
        // all other gauges respect the depth buffer and are hidden by both.
        Shader unlitShader = Shader.Find("Hidden/Internal-Colored");
        if (unlitShader == null)
        {
          Debug.LogError($"{MechanicalConnectorConfigurator.Prefix} Internal-Colored shader not found.");
          return;
        }

        _markerMaterial = CreateMarkerMaterial(Settings.MarkerColor, unlitShader, alwaysOnTop: false);
        _floorMaterial = CreateMarkerMaterial(Settings.FloorColor, unlitShader, alwaysOnTop: true);

        Mesh inputMesh = _assetLoader.Load<Mesh>(InputMeshPath);
        Mesh outputMesh = _assetLoader.Load<Mesh>(OutputMeshPath);
        _inputDrawer = _meshDrawerFactory.Create(inputMesh, _markerMaterial);
        _outputDrawer = _meshDrawerFactory.Create(outputMesh, _markerMaterial);
        _floorInputDrawer = _meshDrawerFactory.Create(inputMesh, _floorMaterial);
        _floorOutputDrawer = _meshDrawerFactory.Create(outputMesh, _floorMaterial);
      }
      catch (Exception e)
      {
        Debug.LogError($"{MechanicalConnectorConfigurator.Prefix} Failed to create connector drawers: {e.Message}");
      }
    }

    private static Material CreateMarkerMaterial(Color color, Shader shader, bool alwaysOnTop)
    {
      Material material = new Material(shader);
      material.SetColor("_Color", color);
      if (alwaysOnTop)
      {
        material.SetInt("_ZTest", (int)CompareFunction.Always);
      }
      material.SetInt("_ZWrite", 0);
      // Force alpha blending; Internal-Colored exposes blend state via material params.
      if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
      if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
      material.renderQueue = (int)RenderQueue.Transparent;
      return material;
    }

    private void DrawPreviews()
    {
      if (_previewBlocks.Count == 0) return;

      foreach (BlockObject blockObject in _previewBlocks)
      {
        for (int i = 0; i < _previewTransputSpecs.Length; i++)
        {
          TransputSpec spec = _previewTransputSpecs[i];
          Vector3Int coordinates = blockObject.TransformCoordinates(spec.Coordinates);
          for (Direction3DEnumerator e = spec.Directions.GetEnumerator().GetEnumerator(); e.MoveNext();)
          {
            Direction3D direction = blockObject.TransformDirection(e.Current);
            if (direction == Direction3D.Bottom)
            {
              AddFloorMatrix(coordinates, direction, _previewGenerator);
            }
            else
            {
              AddMatrix(coordinates, direction, _previewGenerator);
            }
          }
        }
      }
    }

    private static bool IsGenerator(Transput transput)
    {
      MechanicalNode node = transput.ParentNode;
      return node != null && node.IsGenerator;
    }

    private void AddMatrix(Vector3Int coordinates, Direction3D direction, bool generator)
    {
      if (generator)
      {
        _outputMatrices.Add(MarkerMatrix(coordinates, direction));
      }
      else
      {
        _inputMatrices.Add(MarkerMatrix(coordinates, direction));
      }
    }

    private void AddFloorMatrix(Vector3Int coordinates, Direction3D direction, bool generator)
    {
      if (generator)
      {
        _floorOutputMatrices.Add(MarkerMatrix(coordinates, direction));
      }
      else
      {
        _floorInputMatrices.Add(MarkerMatrix(coordinates, direction));
      }
    }

    private static Matrix4x4 MarkerMatrix(Vector3Int coordinates, Direction3D direction)
    {
      Vector3Int offset = direction.ToOffset();
      Vector3 position = CoordinateSystem.GridToWorld(new Vector3(
          coordinates.x + offset.x * 0.5f + 0.5f,
          coordinates.y + offset.y * 0.5f + 0.5f,
          coordinates.z + offset.z * 0.5f + 0.5f));
      return Matrix4x4.TRS(position, direction.ToRotation(), Vector3.one);
    }

    private void ToggleConnectors()
    {
      switch (State)
      {
        case ConnectorDisplayState.Off:
          State = ConnectorDisplayState.Full;
          _notificationService.SendNotification(_loc.T("Calloatti.Grid.Connectors.NotificationOn"));
          break;
        case ConnectorDisplayState.Full:
          State = ConnectorDisplayState.PreviewOnly;
          _notificationService.SendNotification(_loc.T("Calloatti.Grid.Connectors.NotificationPreview"));
          break;
        default:
          State = ConnectorDisplayState.Off;
          _notificationService.SendNotification(_loc.T("Calloatti.Grid.Connectors.NotificationOff"));
          break;
      }
    }

    private void OnTransputAdded(object sender, Transput transput)
    {
      _transputs.Add(transput);
    }

    private void OnTransputRemoved(object sender, Transput transput)
    {
      _transputs.Remove(transput);
    }
  }
}