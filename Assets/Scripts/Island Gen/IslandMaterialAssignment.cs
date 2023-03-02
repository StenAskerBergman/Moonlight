using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandMaterialAssignment : MonoBehaviour
{
    public Material material1;
    public Material material2;
    public float heightThreshold = 0.5f;

    private Mesh mesh;

    private void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;

        int[] materials1 = new int[mesh.triangles.Length / 3];
        int[] materials2 = new int[mesh.triangles.Length / 3];

        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            Vector3 vertex1 = mesh.vertices[mesh.triangles[i]];
            Vector3 vertex2 = mesh.vertices[mesh.triangles[i + 1]];
            Vector3 vertex3 = mesh.vertices[mesh.triangles[i + 2]];

            float avgY = (vertex1.y + vertex2.y + vertex3.y) / 3.0f;

            if (avgY < heightThreshold)
            {
                materials1[i / 3] = mesh.triangles[i];
                materials2[i / 3] = mesh.triangles[i + 1];
                materials1[i / 3 + 1] = mesh.triangles[i + 1];
                materials2[i / 3 + 1] = mesh.triangles[i + 2];
                materials1[i / 3 + 2] = mesh.triangles[i + 2];
                materials2[i / 3 + 2] = mesh.triangles[i];
            }
            else
            {
                materials1[i / 3] = mesh.triangles[i];
                materials2[i / 3] = mesh.triangles[i + 1];
                materials1[i / 3 + 1] = mesh.triangles[i + 1];
                materials2[i / 3 + 1] = mesh.triangles[i + 2];
                materials1[i / 3 + 2] = mesh.triangles[i + 2];
                materials2[i / 3 + 2] = mesh.triangles[i];
            }
        }

        mesh.subMeshCount = 2;
        mesh.SetTriangles(materials1, 0);
        mesh.SetTriangles(materials2, 1);

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        Material[] rendererMaterials = new Material[2];
        rendererMaterials[0] = material1;
        rendererMaterials[1] = material2;
        meshRenderer.materials = rendererMaterials;
    }
}
