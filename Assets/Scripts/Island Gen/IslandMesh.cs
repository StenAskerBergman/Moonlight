using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandMesh : MonoBehaviour
{
    public IslandNoise islandNoise;

    private void Start()
    {
        float[,] heights = islandNoise.GenerateNoise();

        Mesh mesh = new Mesh();

        Vector3[] vertices = new Vector3[islandNoise.width * islandNoise.height];
        int[] triangles = new int[(islandNoise.width - 1) * (islandNoise.height - 1) * 6];

        int triangleIndex = 0;
        for (int x = 0; x < islandNoise.width; x++)
        {
            for (int y = 0; y < islandNoise.height; y++)
            {
                vertices[x + y * islandNoise.width] = new Vector3(x, heights[x, y], y);

                if (x < islandNoise.width - 1 && y < islandNoise.height - 1)
                {
                    int bottomLeft = x + y * islandNoise.width;
                    int bottomRight = (x + 1) + y * islandNoise.width;
                    int topLeft = x + (y + 1) * islandNoise.width;
                    int topRight = (x + 1) + (y + 1) * islandNoise.width;

                    // Mesh Corrected
                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = bottomRight;

                    triangles[triangleIndex++] = topRight;
                    triangles[triangleIndex++] = bottomRight;
                    triangles[triangleIndex++] = topLeft;

                }
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;
    }
}