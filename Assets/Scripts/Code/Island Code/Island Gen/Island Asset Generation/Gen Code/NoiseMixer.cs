// NoiseMixer.cs
using UnityEngine;
using System.Collections.Generic;

public class NoiseMixer
{
    // Mixes Noise Stacks
    public float[,] MixNoiseStacks(List<NoiseStack> noiseStacks, List<float> stackWeights, int width, int height)
    {
        float[,] mixedMap = new float[width, height];

        // Normalize stack weights
        float totalWeight = 0;
        foreach (var weight in stackWeights)
        {
            totalWeight += weight;
        }

        for (int i = 0; i < noiseStacks.Count; i++)
        {
            float[,] stackMap = noiseStacks[i].GenerateStack(width, height);
            float weight = (i < stackWeights.Count) ? stackWeights[i] : 1f;

            // Combine the stack into the final map
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    mixedMap[x, y] += (stackMap[x, y] * weight) / totalWeight;
                }
            }
        }

        if (mixedMap == null)
        {
            Debug.LogError("MixedMap is null");
            return null;
        }
        return mixedMap;
    }

    // Mixes Noise Layers
    public float[,] MixNoiseLayers(float[,] baseMap, List<NoiseLayer> noiseLayers, int width, int height)
    {
        if (noiseLayers != null)
        {
            foreach (var layer in noiseLayers)
            {
                float[,] layerHeights = layer.GenerateLayer(width, height);
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        baseMap[x, y] += layerHeights[x, y];
                    }
                }
            }
        }
        return baseMap;
    }

    // Mixes Noise Maps
    public float[,] MixNoiseMaps(List<float[,]> noiseMaps, List<float> stackWeights)
    {
        int width = noiseMaps[0].GetLength(0);
        int height = noiseMaps[0].GetLength(1);
        float[,] mixedMap = new float[width, height];

        // Normalize the stack weights
        float totalWeight = 0;
        foreach (var weight in stackWeights)
        {
            totalWeight += weight;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float mixedValue = 0;
                for (int i = 0; i < noiseMaps.Count; i++)
                {
                    mixedValue += (noiseMaps[i][x, y] * stackWeights[i]) / totalWeight;
                }
                mixedMap[x, y] = mixedValue;
            }
        }

        if (mixedMap == null)
        {
            Debug.LogError("MixedMap is null");
            return null;
        }
        return mixedMap;
    }
}
