using Timberborn.AssetSystem;
using UnityEngine;

namespace Calloatti.Grid
{
  public class MeasuringTapeRenderer
  {
    private readonly IAssetLoader _assetLoader;
    private Texture2D _tapeTexture;
    private Material _tapeMaterial;
    private readonly GameObject[] _arms = new GameObject[4];

    public MeasuringTapeRenderer(IAssetLoader assetLoader)
    {
      _assetLoader = assetLoader;
      LoadTexture();
    }

    private void LoadTexture()
    {
      // CARGA SEGURA: Timberborn procesa el PNG. 
      // Ruta: /Sprites/MeasuringTape.png dentro de tu mod.
      _tapeTexture = _assetLoader.Load<Texture2D>("Sprites/MeasuringTape");

      _tapeTexture.wrapMode = TextureWrapMode.Clamp;
      _tapeTexture.filterMode = FilterMode.Bilinear;

      _tapeMaterial = new Material(Shader.Find("Sprites/Default"));
      _tapeMaterial.mainTexture = _tapeTexture;
    }

    public void DrawTape(Vector3 center)
    {
      ClearTape();
      Vector3 origin = new Vector3(center.x, center.y + 0.15f, center.z);

      _arms[0] = CreateArm(origin, 0);
      _arms[1] = CreateArm(origin, 90);
      _arms[2] = CreateArm(origin, 180);
      _arms[3] = CreateArm(origin, 270);
    }

    private GameObject CreateArm(Vector3 origin, float rotationY)
    {
      GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Quad);
      Object.Destroy(arm.GetComponent<Collider>());

      arm.GetComponent<MeshRenderer>().material = _tapeMaterial;
      arm.transform.localScale = new Vector3(1, 50, 1);
      arm.transform.rotation = Quaternion.Euler(90, rotationY, 0);

      Vector3 forward = Quaternion.Euler(0, rotationY, 0) * Vector3.forward;
      arm.transform.position = origin + (forward * 25f);

      return arm;
    }

    public void ClearTape()
    {
      for (int i = 0; i < 4; i++)
      {
        if (_arms[i] != null) Object.Destroy(_arms[i]);
      }
    }
  }
}