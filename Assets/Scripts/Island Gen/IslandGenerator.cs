using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandGenerator : MonoBehaviour
{
    public int width = 256;
    public int height = 256;
    public float scale = 20.0f;
    public int seed = 0;

    private float[,] heights;

    private void Awake()
    {
        heights = new float[width, height];

        Random.InitState(seed);
        (float offsetX, float offsetY) = (Random.Range(-10000f, 10000f), Random.Range(-10000f, 10000f)); 

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float xCoord = (float)x / width * scale + offsetX;
                float yCoord = (float)y / height * scale + offsetY;

                heights[x, y] = Mathf.PerlinNoise(xCoord, yCoord);
            }
        }
        
        Mesh mesh = new Mesh();

        Vector3[] vertices = new Vector3[width * height];
        int[] triangles = new int[(width - 1) * (height - 1) * 6];

        int triangleIndex = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                vertices[x + y * width] = new Vector3(x, heights[x, y], y);

                if (x < width - 1 && y < height - 1)
                {
                    int bottomLeft = x + y * width;
                    int bottomRight = (x + 1) + y * width;
                    int topLeft = x + (y + 1) * width;
                    int topRight = (x + 1) + (y + 1) * width;

                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = bottomRight;

                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = bottomRight;
                    triangles[triangleIndex++] = topRight;
                }
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
    }
}