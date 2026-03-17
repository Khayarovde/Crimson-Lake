using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ReCalcCubeTexture : MonoBehaviour {

  [SerializeField]
  private Vector3 currentScale;

  [SerializeField]
  private string generatedMeshName = "Generated UV mesh";

  [SerializeField]
  private float tiling = 1f;


  public void Calculate() {
    if (currentScale == transform.localScale) return;
    if (CheckForDefaultSize()) return;

    currentScale = transform.localScale;
    var mesh = GetMesh();

    if (mesh == null) {
      Debug.LogWarning("No mesh found. Try again later.");
      return;
    }

    mesh.uv = SetupUvMap(mesh.uv);
    mesh.name = generatedMeshName;

    var meshFilter = GetComponent<MeshFilter>();
    meshFilter.mesh = mesh;
  }

  private Mesh GetMesh() {
    var meshFilter = GetComponent<MeshFilter>();

    if (meshFilter.sharedMesh == null) {
      return null;
    }

    var mesh = Instantiate(meshFilter.sharedMesh);
    return mesh;
  }

  private Vector2[] SetupUvMap(Vector2[] meshUVs) {

    var width = currentScale.x * tiling;
    var depth = currentScale.z * tiling;
    var height = currentScale.y * tiling;

    //Front
    meshUVs[2] = new Vector2(0, height);
    meshUVs[3] = new Vector2(width, height);
    meshUVs[0] = new Vector2(0, 0);
    meshUVs[1] = new Vector2(width, 0);

    //Back
    meshUVs[7] = new Vector2(0, 0);
    meshUVs[6] = new Vector2(width, 0);
    meshUVs[11] = new Vector2(0, height);
    meshUVs[10] = new Vector2(width, height);

    //Left
    meshUVs[19] = new Vector2(depth, 0);
    meshUVs[17] = new Vector2(0, height);
    meshUVs[16] = new Vector2(0, 0);
    meshUVs[18] = new Vector2(depth, height);

    //Right
    meshUVs[23] = new Vector2(depth, 0);
    meshUVs[21] = new Vector2(0, height);
    meshUVs[20] = new Vector2(0, 0);
    meshUVs[22] = new Vector2(depth, height);

    //Top
    meshUVs[4] = new Vector2(width, 0);
    meshUVs[5] = new Vector2(0, 0);
    meshUVs[8] = new Vector2(width, depth);
    meshUVs[9] = new Vector2(0, depth);

    //Bottom
    meshUVs[13] = new Vector2(width, 0);
    meshUVs[14] = new Vector2(0, 0);
    meshUVs[12] = new Vector2(width, depth);
    meshUVs[15] = new Vector2(0, depth);

    return meshUVs;
  }

  private bool CheckForDefaultSize() {
    if (currentScale != Vector3.one / tiling) return false;

    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

    DestroyImmediate(GetComponent<MeshFilter>());
    gameObject.AddComponent<MeshFilter>();
    GetComponent<MeshFilter>().sharedMesh = cube.GetComponent<MeshFilter>().sharedMesh;

    DestroyImmediate(cube);

    return true;
  }
}

