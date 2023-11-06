using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NoiseCurve", menuName = "Noise/NoiseCurve", order = 1)]
public class NoiseCurve : ScriptableObject, IPreviewable
{
    public AnimationCurve curve;

    [NonSerialized]
    protected Texture2D _previewTexture;

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


    /// <summary>
    /// Evaluate the curve at a specific point.
    /// </summary>
    /// <param name="t">Value between 0 and 1</param>
    /// <returns>Modified value based on curve</returns>
    public float Evaluate(float t)
    {
        return curve.Evaluate(t);
    }

    public bool PreviewType;

    public virtual void GeneratePreview()
    {
        // Logic to generate the preview texture for NoiseCurve

        // Define the size of the preview texture
        int previewWidth = 100;
        int previewHeight = 100;

        // Create a texture for the preview
        _previewTexture = new Texture2D(previewWidth, previewHeight);
        _previewTexture.wrapMode = TextureWrapMode.Clamp;
        _previewTexture.filterMode = FilterMode.Point;

        if (PreviewType == true)
        {
            // See Thru
            // Evaluate the curve at several points and plot the results on the texture
            for (int x = 0; x < previewWidth; x++)
            {
                float t = (float)x / (previewWidth - 1);
                float value = curve.Evaluate(t);
                int y = Mathf.Clamp((int)(value * (previewHeight - 1)), 0, previewHeight - 1);

                // Set the pixel color at the evaluated point
                _previewTexture.SetPixel(x, y, Color.white);

                // Clear the other pixels in the column
                for (int i = 0; i < previewHeight; i++)
                {
                    if (i != y)
                    {
                        _previewTexture.SetPixel(x, i, Color.black);
                    }
                }
            }
        } 
        else 
        {
            // Not See Thru
            for (int x = 0; x < previewWidth; x++)
            {
                float t = (float)x / (previewWidth - 1);
                float value = curve.Evaluate(t);
                int y = Mathf.Clamp((int)(value * (previewHeight - 1)), 0, previewHeight - 1);

                _previewTexture.SetPixel(x, y, Color.white);
            }

        }
        
        _previewTexture.Apply();
    }
}