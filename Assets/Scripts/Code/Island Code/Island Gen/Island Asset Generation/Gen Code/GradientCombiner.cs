using UnityEngine;

[CreateAssetMenu(fileName = "GradientCombiner", menuName = "Gradient/GradientCombiner", order = 1)]
public class GradientCombiner : ScriptableObject, IPreviewable
{
    public NoiseGradient[] gradients;
    public float[] weights;
    public Texture2D _previewTexture { get; private set; }

    // Create a texture for the preview
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

    public void GeneratePreview()
    {
        if (gradients.Length == 0)
        {
            Debug.LogError("No gradients to combine.");
            return;
        }

        _previewTexture = Combine();
    }

    /// <summary>
    /// Combine the gradients based on the given weights.
    /// </summary>
    /// <returns>The combined texture.</returns>
    public Texture2D Combine()
    {
        if (gradients.Length == 0)
        {
            Debug.LogError("No gradients to combine.");
            return null;
        }

        if (gradients.Length != weights.Length)
        {
            Debug.LogError("The length of weights should be the same as the length of gradients.");
            return null;
        }

        Texture2D[] textures = new Texture2D[gradients.Length];

        for (int i = 0; i < gradients.Length; i++)
        {
            if (gradients[i].PreviewTexture == null)
            {
                Debug.LogError($"Gradient {i}'s preview texture is null.");
            }
            else
            {
                Debug.Log($"Gradient {i}'s preview texture is valid.");
            }

            textures[i] = gradients[i].PreviewTexture;
        }

        _previewTexture = Combine(textures, weights);

        if (PreviewTexture == null)
        {
            Debug.LogError("Failed to create the combined texture.");
        }
        else
        {
            Debug.Log("Combined texture created successfully.");
        }

        return PreviewTexture;
    }


    private Texture2D Combine(Texture2D[] textures, float[] weights)
    {
        int width = textures[0].width;
        int height = textures[0].height;
        Texture2D combinedTexture = new Texture2D(width, height);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Color combinedColor = Color.black;

                for (int i = 0; i < textures.Length; i++)
                {
                    Color color = textures[i].GetPixel(x, y);
                    combinedColor += color * weights[i];
                }

                combinedTexture.SetPixel(x, y, combinedColor);
            }
        }

        combinedTexture.Apply();
        return combinedTexture;
    }
}
