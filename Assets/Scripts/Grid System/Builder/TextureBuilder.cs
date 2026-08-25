using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextureBuilder
{
    private readonly Cell[,] grid;
    private readonly IslandTerrainProvider terrainSource;
    private readonly int visualSamplesPerCell;
    private readonly ClimateProfile climate;

    public TextureBuilder(Cell[,] grid, ClimateProfile climate = null)
    {
        this.grid = grid;
        this.visualSamplesPerCell = 1;
        this.climate = climate != null ? climate : ScriptableObject.CreateInstance<ClimateProfile>();
    }

    public TextureBuilder(
        Cell[,] grid,
        IslandTerrainProvider terrainSource,
        int visualSamplesPerCell,
        ClimateProfile climate = null)
    {
        this.grid = grid;
        this.terrainSource = terrainSource;
        this.visualSamplesPerCell = Mathf.Max(1, visualSamplesPerCell);
        this.climate = climate != null ? climate : ScriptableObject.CreateInstance<ClimateProfile>();
    }

    private float FractalNoise(float x, float y, float scale)
    {
        float noise = 0f;
        float frequency = scale;
        float amplitude = 1f;
        float maxValue = 0f;
        for (int i = 0; i < 3; i++)
        {
            noise += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }
        return noise / maxValue;
    }

    public Texture2D Build()
    {
        int gridSize = grid.GetLength(0);
        bool useFractionalSampling = terrainSource != null && visualSamplesPerCell > 1;
        int textureSize = useFractionalSampling ? gridSize * visualSamplesPerCell : gridSize;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, true);

        Color[] colorMap = new Color[textureSize * textureSize];
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                Color finalColor = Color.black;
                if (useFractionalSampling)
                {
                    float localX = -0.5f + (x + 0.5f) / visualSamplesPerCell;
                    float localZ = -0.5f + (y + 0.5f) / visualSamplesPerCell;
                    
                    TerrainSample sample = terrainSource.SampleVisual(localX, localZ);
                    TerrainGenerationSettings settings = terrainSource.Settings;

                    float height = sample.Height;
                    
                    // Generate fractal noise to perturb the boundaries organically
                    float boundaryNoise = (FractalNoise(x, y, climate.splatNoiseFrequency) - 0.5f) * climate.splatSandNoiseAmplitude;

                    // Apply noise to the THRESHOLDS. 
                    float rockThreshold = settings.cliffHeight + boundaryNoise;
                    
                    // Grass boundary noise is usually restricted so it doesn't cause puddles on flatland
                    float grassBoundaryNoise = (FractalNoise(x, y, climate.splatNoiseFrequency) - 0.5f) * climate.splatGrassNoiseRestriction;
                    float grassThreshold = (settings.surfaceFlatlandHeight + climate.splatGrassThresholdOffset) + grassBoundaryNoise; 
                    
                    // Sand gets the full wild noise because it's safely on the slope/underwater
                    float sandThreshold = (settings.waterHeight + climate.splatSandThresholdOffset) + boundaryNoise;

                    // Calculate local slope to detect steep dropoff cliffs (both above and below water)
                    float sampleStep = 0.25f;
                    float hL = terrainSource.SampleVisual(localX - sampleStep, localZ).Height;
                    float hR = terrainSource.SampleVisual(localX + sampleStep, localZ).Height;
                    float hD = terrainSource.SampleVisual(localX, localZ - sampleStep).Height;
                    float hU = terrainSource.SampleVisual(localX, localZ + sampleStep).Height;
                    float slope = Mathf.Sqrt((hR - hL) * (hR - hL) + (hU - hD) * (hU - hD)) / (2f * sampleStep);

                    if (sample.TerrainType == Cell.TerrainType.River)
                    {
                        finalColor = new Color(0f, 0f, 0f, 1f); // River / Water
                    }
                    else if (sample.TerrainType == Cell.TerrainType.Mountain
                        || sample.TerrainType == Cell.TerrainType.MountainPeak
                        || sample.TerrainType == Cell.TerrainType.Cliff
                        || height >= rockThreshold
                        || slope > 0.6f) 
                    {
                        finalColor = new Color(0f, 0f, 1f, 0f); // B channel = Rock
                    }
                    else if (sample.TerrainType == Cell.TerrainType.Beach
                        || sample.TerrainType == Cell.TerrainType.Shore)
                    {
                        finalColor = new Color(0f, 1f, 0f, 0f); // G channel = Sand
                    }
                    else if (height >= grassThreshold) 
                    {
                        finalColor = new Color(1f, 0f, 0f, 0f); // R channel = Grass
                    }
                    else if (height >= sandThreshold) 
                    {
                        finalColor = new Color(0f, 1f, 0f, 0f); // G channel = Sand
                    }
                    else 
                    {
                        finalColor = new Color(0f, 0f, 0f, 1f); // A channel = Deep Water / Seafloor
                    }

                    int cellX = Mathf.Clamp(Mathf.FloorToInt(localX + 0.5f), 0, gridSize - 1);
                    int cellZ = Mathf.Clamp(Mathf.FloorToInt(localZ + 0.5f), 0, gridSize - 1);
                    if (grid[cellX, cellZ].currentTerrainType == Cell.TerrainType.River)
                    {
                        finalColor = new Color(0f, 0f, 0f, 1f);
                    }
                }                else
                {
                    Cell.TerrainType tType = grid[x, y].currentTerrainType;
                    if (tType == Cell.TerrainType.Land || tType == Cell.TerrainType.Plain || tType == Cell.TerrainType.Hill || tType == Cell.TerrainType.Forest)
                        finalColor = new Color(1f, 0f, 0f, 0f);
                    else if (tType == Cell.TerrainType.Beach || tType == Cell.TerrainType.Shore || tType == Cell.TerrainType.Coast || tType == Cell.TerrainType.Desert)
                        finalColor = new Color(0f, 1f, 0f, 0f);
                    else if (tType == Cell.TerrainType.Mountain || tType == Cell.TerrainType.Cliff || tType == Cell.TerrainType.Rocky)
                        finalColor = new Color(0f, 0f, 1f, 0f);
                    else
                        finalColor = new Color(0f, 0f, 0f, 1f);
                }

                colorMap[y * textureSize + x] = finalColor;
            }
        }

        texture.SetPixels(colorMap);
        texture.Apply(true);
        return texture;
    }
}