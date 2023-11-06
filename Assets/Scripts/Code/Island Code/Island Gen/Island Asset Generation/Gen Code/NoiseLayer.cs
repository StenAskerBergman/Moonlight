// NoiseLayer.cs
using System;
using UnityEngine;
using static NoiseCurve;

[CreateAssetMenu(fileName = "NoiseLayer", menuName = "Noise/NoiseLayer", order = 2)]
public class NoiseLayer : ScriptableObject, IPreviewable
{
    #region Noise

    // Image Size
    public int width; 
    public int height; 

    public float seed;
    public float frequency;
    public float amplitude;
    public Vector2 offset;
    public NoiseCurve noiseCurve;

    [NonSerialized]
    protected Texture2D _previewTexture; // edit: protected
    
    private float[,] _noiseMap;
    #endregion

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

    public enum CurveEvaluationMethod
    {
        CurveEvaluate,      // Default  
        MaxAxisDistance,    // Cubic DropOff - Square Shape
        DistanceFromCenter, // Round DropOff - Circular Shape
        // Add other methods as needed
    }

    public CurveEvaluationMethod curveEvaluationMethod;

    public float[,] GenerateLayer(int width, int height)
    {
        // Use the width and height passed as parameters, or fall back to the properties of this class
        int finalWidth = width > 0 ? width : this.width;
        int finalHeight = height > 0 ? height : this.height;

        // Final Size of the noise map
        float[,] noiseMap = new float[finalWidth, finalHeight];
        // Noise generation logic
        for (int x = 0; x < finalWidth; x++)
        {
            for (int y = 0; y < finalHeight; y++)
            {
                float sampleX = (x + offset.x) / (float)finalWidth * frequency;
                float sampleY = (y + offset.y) / (float)finalHeight * frequency;
                float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * amplitude;
                noiseMap[x, y] = perlinValue;
            }
        }

        // Cache Map
        _noiseMap = noiseMap;

        // Curve logic
        if (noiseCurve != null)
        {
            // Apply the curve to modulate the noise map
            for (int x = 0; x < finalWidth; x++)
            {
                for (int y = 0; y < finalHeight; y++)
                {
                    float value = EvaluateCurve(x, y, finalWidth, finalHeight);
                    noiseMap[x, y] = noiseCurve.Evaluate(value);
                }
            }
        }
        return noiseMap;
    }

    private float EvaluateCurve(int x, int y, int width, int height)
    {
        switch (curveEvaluationMethod)
        {
            case CurveEvaluationMethod.DistanceFromCenter:
                return Mathf.Sqrt(Mathf.Pow((float)x / width - 0.5f, 2) + Mathf.Pow((float)y / height - 0.5f, 2));

            case CurveEvaluationMethod.MaxAxisDistance:
                return Mathf.Max(Mathf.Abs((float)x / width - 0.5f), Mathf.Abs((float)y / height - 0.5f));

            default:
                return _noiseMap[x, y]; 
                // throw new ArgumentOutOfRangeException();
        }
    }

    public void GeneratePreview()
    {
        int previewWidth = 100;
        int previewHeight = 100;

        float[,] noiseMap = GenerateLayer(previewWidth, previewHeight);

        // Find min and max values in the noise map
        float minNoiseValue = float.MaxValue;
        float maxNoiseValue = float.MinValue;
        for (int x = 0; x < previewWidth; x++)
        {
            for (int y = 0; y < previewHeight; y++)
            {
                minNoiseValue = Mathf.Min(minNoiseValue, noiseMap[x, y]);
                maxNoiseValue = Mathf.Max(maxNoiseValue, noiseMap[x, y]);
            }
        }

        // Normalize the noise values to the range [0, 1]
        for (int x = 0; x < previewWidth; x++)
        {
            for (int y = 0; y < previewHeight; y++)
            {
                if (maxNoiseValue - minNoiseValue != 0)
                {
                    noiseMap[x, y] = (noiseMap[x, y] - minNoiseValue) / (maxNoiseValue - minNoiseValue);
                    //Debug.Log("After normalization: " + noiseMap[x, y]);  // Log the normalized values
                }
            }
        }

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


    void OnValidate()
    {
        GeneratePreview();
    }
}
