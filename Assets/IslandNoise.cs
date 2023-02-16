using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class IslandNoise : MonoBehaviour
{
    public int width = 256;
    public int height = 256;
    public float scale = 20.0f;
    [Space]
    public float mountainHeight = 10f;
    [Space]
    public int seed = 0;

    public float[,] GenerateNoise()
    {
        float[,] heights = new float[width, height];

        Random.InitState(seed);
        float offsetX = Random.Range(0.0f, 999.0f);
        float offsetY = Random.Range(0.0f, 999.0f);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float xCoord = (float)x / width * scale + offsetX;
                float yCoord = (float)y / height * scale + offsetY;

                float perlinValue = Mathf.PerlinNoise(xCoord, yCoord);
                heights[x, y] = perlinValue * mountainHeight;
            }
        }

        return heights;
    }
}
