// IslandNoise.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandNoise : MonoBehaviour
{
    public int seed = 0;

    public int width = 256;
    public int height = 256;
    public int quantizationLevels = 10;  // Increase for more levels, decrease for fewer

    public float scale = 20.0f;
    public float mountainHeight = 10f;

    [Space(10)]
    public float centerFlatness = 0.2f;
    public float centerLimit = 0.3f; 
    public float edgeScale = 0.1f;
    public float OceanEdgeLength = 0.9f;
    public float seaLevel = 5.0f;

    [Space(10)]
    public float shoreFalloffStrength = 1.0f;
    public float oceanFalloffStrength = 2.0f;

    [Space(10)]
    public float shoreHeightFactor = 0.2f;
    public float shoreScale = 0.1f;

    [Space(10)]
    public float oceanHeightFactor = 0.2f;
    public float oceanScale = 0.1f;

    private float[,] heights;

    [HideInInspector] public int octaves = 4;
    [HideInInspector] public float persistence = 0.5f;
    [HideInInspector] public float lacunarity = 2.0f;

    public float[,] Heights
    {
        get { return heights; }
    }

    private void Awake()
    {
        heights = new float[width, height];
        GenerateNoise();
    }

    private float[,] GenerateFalloffMap()
    {
        float[,] map = new float[width, height];
        Vector2 center = new Vector2(width / 2, height / 2);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float distanceToCenter = Vector2.Distance(new Vector2(x, y), center) / (width / 2);

                // This value could be adjusted based on how much of the center you want to keep flat.

                if (distanceToCenter < centerFlatness)
                {
                    map[x, y] = 1; // No falloff at the center
                    continue;
                }

                // For shore
                float shoreValue = Mathf.Clamp01(1 - Mathf.Pow(distanceToCenter, shoreFalloffStrength));

                // For ocean
                float oceanValue = Mathf.Clamp01(1 - Mathf.Pow(distanceToCenter, oceanFalloffStrength));

                // Your existing square falloff function
                float xValue = x / (float)width * 2 - 1;
                float yValue = y / (float)height * 2 - 1;
                float squareValue = Evaluate(Mathf.Max(Mathf.Abs(xValue), Mathf.Abs(yValue)));

                // Combine them. This is a simple example; you can use more complex functions.
                map[x, y] = shoreValue * oceanValue * squareValue;
            }
        }

        return map;
    }


    static float Evaluate(float value)
    {
        float a = 3;
        float b = 2.2f;
        return Mathf.Pow(value, a) / (Mathf.Pow(value, a) + Mathf.Pow(b - b * value, a));
    }

    public float QuantizeHeight(float height, int levels)
{
    return Mathf.Round(height * levels) / levels;
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

        float[,] falloffMap = GenerateFalloffMap();
        float[,] roughnessMap = GenerateRoughnessMap();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // This value could be adjusted based on how much of the center you want to keep flat.
                float distanceToCenter = Vector2.Distance(new Vector2(x, y), new Vector2(width / 2, height / 2)) / (width / 2);
                float centerAmplitude = 1.0f - Mathf.Clamp01(distanceToCenter / centerFlatness); // closer to 1 at center

                float amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;

                for (int i = 0; i < octaves; i++)
                {
                    float xCoord = x / (float)width * scale * frequency + offsetX;
                    float yCoord = y / (float)height * scale * frequency + offsetY;

                    float perlinValue = Mathf.PerlinNoise(xCoord, yCoord) * 2 - 1;
                    noiseHeight += perlinValue * amplitude * centerAmplitude;  // Use centerAmplitude here

                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                // Normalize the generated noise value
                noiseHeight = Mathf.InverseLerp(-1, 1, noiseHeight); // Normalize to 0-1

                // For making the center of the island less uneven but above water
                if (distanceToCenter < centerLimit)
                {
                    noiseHeight *= 1.01f;  // Increase the noise effect at the center
                    noiseHeight += 0.5f;  // Increase the noise effect at the center
                }

                // Scale to mountain height
                heights[x, y] = noiseHeight * mountainHeight;

                // Control the roughness
                heights[x, y] += roughnessMap[x, y];

                //  If there only was a way to clamp roughness to the edge
                //  before the shores edge to stop it before it reached the
                //  middle of the islands core. And if you could scale the
                //  Parameters somehow.

                // For controlling the edges between shore and ocean
                if (distanceToCenter > OceanEdgeLength)  // This could be a configurable parameter
                {
                    // Apply some logic here to control the transition
                    float edgeControl = Mathf.PerlinNoise(x * edgeScale, y * edgeScale);
                    heights[x, y] *= edgeControl;
                }

                // Potential changes for the slope at the corners
                bool isCorner = (x < width * 0.2f && y < height * 0.2f) || // bottom-left corner
                                (x > width * 0.8f && y < height * 0.2f) || // bottom-right corner
                                (x < width * 0.2f && y > height * 0.8f) || // top-left corner
                                (x > width * 0.8f && y > height * 0.8f);   // top-right corner

                if (isCorner)
                {
                    // Apply some logic to make the slope steeper
                    heights[x, y] *= 0.7f;  // Reduce the height at the corners
                }

                // Apply the falloff map
                heights[x, y] *= falloffMap[x, y];

                // Add the shore and shore to ocean area
                float shoreNoise = Mathf.PerlinNoise(x / (float)width * shoreScale, y / (float)height * shoreScale);
                heights[x, y] *= 1 + shoreNoise * shoreHeightFactor;

                // Add the ocean and ocean to abyss area
                float oceanNoise = Mathf.PerlinNoise(x / (float)width * oceanScale, y / (float)height * oceanScale);
                heights[x, y] *= 1 + oceanNoise * oceanHeightFactor;

                // Quantize the height
                heights[x, y] = QuantizeHeight(heights[x, y], quantizationLevels);
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Raise the entire island above sea level
                heights[x, y] += seaLevel;
            }
        }
    }

    private float[,] GenerateRoughnessMap()
    {
        // Generate a Perlin noise map that will control the roughness
        // You can adjust the scale and other parameters
        float[,] map = new float[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float xCoord = x / (float)width * scale;
                float yCoord = y / (float)height * scale;
                map[x, y] = Mathf.PerlinNoise(xCoord, yCoord);
            }
        }
        return map;
    }
}


