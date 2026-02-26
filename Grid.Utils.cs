using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace Calloatti.Grid
{
  public partial class GridRenderer
  {
    private void TurnOffTerrainGrid()
    {
      if (_terrainGridRoot != null) _terrainGridRoot.SetActive(false);
    }

    private MeshRenderer CreateGridMesh(string name, List<Vector3> vertices, List<int> indices, out GameObject obj)
    {
      obj = new GameObject(name);
      MeshFilter mf = obj.AddComponent<MeshFilter>();
      MeshRenderer mr = obj.AddComponent<MeshRenderer>();
      Mesh mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
      mesh.SetVertices(vertices);
      mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
      mf.mesh = mesh;

      Material mat = new Material(Shader.Find("Hidden/Internal-Colored"));
      mat.color = new Color(0, 0, 0, 0.4f);
      mat.SetInt("_ZTest", (int)CompareFunction.LessEqual);
      mr.material = mat;
      return mr;
    }
  }
}