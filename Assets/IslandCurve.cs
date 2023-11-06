using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandCurve : MonoBehaviour
{
    public int seed = 0;
    public int width = 256;
    public int height = 256;

    public float scale = 20.0f;
    public float mountainHeight = 10f;

    [HideInInspector] public int octaves = 4;
    [HideInInspector] public float persistence = 0.5f;
    [HideInInspector] public float lacunarity = 2.0f;

    public AnimationCurve islandProfile;

    [Space(10)]
    public float seaLevel = 5.0f;

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
                float distanceToCenter = Vector2.Distance(new Vector2(x, y), new Vector2(width / 2, height / 2)) / (width / 2);

                float amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;

                for (int i = 0; i < octaves; i++)  // 4 octaves, you can adjust
                {
                    float xCoord = x / (float)width * scale * frequency + offsetX;
                    float yCoord = y / (float)height * scale * frequency + offsetY;

                    float perlinValue = Mathf.PerlinNoise(xCoord, yCoord) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistence;  // 0.5f persistence
                    frequency *= lacunarity;  // 2.0f lacunarity
                }

                noiseHeight = Mathf.InverseLerp(-1, 1, noiseHeight);  // Normalize to 0-1

                // Apply curve based on distance to center
                float curveValue = islandProfile.Evaluate(distanceToCenter);
                noiseHeight *= curveValue;

                // Scale to mountain height and add sea level
                heights[x, y] = noiseHeight * mountainHeight + seaLevel;
            }
        }
    }
}
