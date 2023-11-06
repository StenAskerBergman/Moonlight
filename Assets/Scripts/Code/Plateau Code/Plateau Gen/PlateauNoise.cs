// PlateauNoise.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlateauNoise : MonoBehaviour
{
    public int width = 256;
    public int height = 256;
    public float scale = 20.0f;
    public float peakHeight = 10f;
    public int seed = 0;
    private float[,] heights;

    public float[,] Heights
    {
        get { return heights; }
    }

    private void Awake()
    {
        heights = new float[width, height];
        GenerateNoise();
    }

    public void GenerateNoise()
    {
        if (heights == null)
        {
            heights = new float[width, height];
        }

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
                heights[x, y] = perlinValue * peakHeight;
            }
        }
    }
}

