// PlateauMesh.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlateauMesh : MonoBehaviour
{
    public PlateauNoise plateauNoise;
    private Vector3[] vertices;
    private int[] triangles;

    private void Start()
    {
        plateauNoise.GenerateNoise();

        vertices = new Vector3[plateauNoise.width * plateauNoise.height];
        triangles = new int[(plateauNoise.width - 1) * (plateauNoise.height - 1) * 6];

        GenerateMesh();
    }   

    private void GenerateMesh()
    {

        int vertexIndex = 0;
        int triangleIndex = 0;

        float offsetX = plateauNoise.width / 2.0f;
        float offsetY = plateauNoise.height / 2.0f;

        for (int x = 0; x < plateauNoise.width; x++)
        {
            for (int y = 0; y < plateauNoise.height; y++)
            {
                // Populate vertex array + Subtract the offsets to center the mesh
                vertices[vertexIndex] = new Vector3(x - offsetX, plateauNoise.Heights[x, y], y - offsetY);
                vertexIndex++;

                // Populate triangle array
                if (x < plateauNoise.width - 1 && y < plateauNoise.height - 1)
                {
                    int bottomLeft = x + y * plateauNoise.width;
                    int bottomRight = (x + 1) + y * plateauNoise.width;
                    int topLeft = x + (y + 1) * plateauNoise.width;
                    int topRight = (x + 1) + (y + 1) * plateauNoise.width;

                    // First triangle
                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = bottomRight;
                    triangles[triangleIndex++] = topLeft;

                    // Second triangle
                    triangles[triangleIndex++] = topRight;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = bottomRight;
                }
            }
        }

        Mesh mesh = new Mesh
        {
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateNormals();

        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;
    }
}
