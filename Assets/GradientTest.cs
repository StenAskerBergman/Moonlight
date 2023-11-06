using UnityEngine;

public class GradientTest : MonoBehaviour
{
    public Gradient gradient;

    void Start()
    {
        // Define the size of the preview texture
        int previewWidth = 100;
        int previewHeight = 100;

        // Create a texture for the preview
        Texture2D previewTexture = new Texture2D(previewWidth, previewHeight);
        previewTexture.wrapMode = TextureWrapMode.Clamp;
        previewTexture.filterMode = FilterMode.Point;

        // Create a vertical gradient
        for (int x = 0; x < previewWidth; x++)
        {
            for (int i = 0; i < previewHeight; i++)
            {
                float lerpValue = (float)i / (previewHeight - 1);
                Color gradientColor = gradient.Evaluate(lerpValue);
                previewTexture.SetPixel(x, i, gradientColor);
            }
        }

        previewTexture.Apply();

        // Apply the texture to a GameObject to visualize it
        GetComponent<Renderer>().material.mainTexture = previewTexture;
    } 
}
