// NoiseStack.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NoiseStack", menuName = "Noise/NoiseStack", order = 1)]
public class NoiseStack : ScriptableObject, IPreviewable
{
    public List<NoiseLayer> layers;
    public List<float> layerWeights;

    [NonSerialized]
    private Texture2D _previewTexture;

    public Texture2D PreviewTexture
    {
        get
        {
            if (_previewTexture == null)
            {
                GeneratePreview();
            }
            return _previewTexture;
        }
    }

    public float[,] GenerateStack(int width, int height)
    {
        float[,] finalNoiseMap = new float[width, height];

        // Initialize with zeros
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                finalNoiseMap[x, y] = 0f;
            }
        }
        
        // Normalize the layer weights
        float totalWeight = 0;
        foreach (var weight in layerWeights)
        {
            totalWeight += weight;
        }

        // Combine the layers
        for (int i = 0; i < layers.Count; i++)
        {
            float[,] layerMap = layers[i].GenerateLayer(width, height);
            float weight = (i < layerWeights.Count) ? layerWeights[i] : 1f;

            // Combine the layer into the final map, taking into account the weight
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    finalNoiseMap[x, y] += (layerMap[x, y] * weight) / totalWeight;
                }
            }
        }

        return finalNoiseMap;
    }
    public void GeneratePreview()
    {
        // Logic to generate the preview texture for NoiseStack

        // Define the size of the preview texture
        int previewWidth = 100;
        int previewHeight = 100;

        // Generate the noise map for the stack
        float[,] noiseMap = GenerateStack(previewWidth, previewHeight);

        // Create a texture from the noise map
        _previewTexture = new Texture2D(previewWidth, previewHeight);
        _previewTexture.wrapMode = TextureWrapMode.Clamp;
        _previewTexture.filterMode = FilterMode.Point;

        for (int x = 0; x < previewWidth; x++)
        {
            for (int y = 0; y < previewHeight; y++)
            {
                float value = noiseMap[x, y];
                Color color = new Color(value, value, value, 1f);
                _previewTexture.SetPixel(x, y, color);
            }
        }

        _previewTexture.Apply();
    }

}