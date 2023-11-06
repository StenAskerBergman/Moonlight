using UnityEngine;

[CreateAssetMenu(fileName = "NoiseGradient", menuName = "Noise/NoiseGradient", order = 3)]
public class NoiseGradient : NoiseCurve
{
    [GradientUsage(true)]
    public Gradient gradient;

    private Texture2D final_previewTexture;

    public enum GradientMethod
    {
        HorizontalGradient,
        VerticalGradient,
        LineGradient,
        NewGradientMethod,
        NewTransGradientMethod,
        HorizontalBasedOnLineYPosition
    }

    public GradientMethod gradientMethod;

    public Color lineColor = Color.red;

    public override void GeneratePreview()
    {
        Debug.Log("NoiseGradient GeneratePreview");

        if (curve == null)
        {
            Debug.LogError("Curve is null. Please assign a valid AnimationCurve object.");
            return;
        }

        // Define the size of the preview texture
        int previewWidth = 100;
        int previewHeight = 100;

        // Create a texture for the preview
        _previewTexture = new Texture2D(previewWidth, previewHeight);
        _previewTexture.wrapMode = TextureWrapMode.Clamp;
        _previewTexture.filterMode = FilterMode.Point;

        // Evaluate the curve at several points and plot the results on the texture
        for (int x = 0; x < previewWidth; x++)
        {
            float t = (float)x / (previewWidth - 1);
            float value = curve.Evaluate(t);
            int y = Mathf.Clamp((int)(value * (previewHeight - 1)), 0, previewHeight - 1);

            switch (gradientMethod)
            {
                case GradientMethod.HorizontalGradient:
                    // Create a horizontal gradient
                    for (int i = 0; i < previewHeight; i++)
                    {
                        float lerpValue = (float)x / (previewWidth - 1);
                        Color gradientColor = gradient.Evaluate(lerpValue);
                        _previewTexture.SetPixel(x, i, gradientColor);
                    }
                    break;

                case GradientMethod.VerticalGradient:
                    // Create a vertical gradient
                    for (int i = 0; i < previewHeight; i++)
                    {
                        float lerpValue = (float)i / (previewHeight - 1);
                        Color gradientColor = gradient.Evaluate(lerpValue);
                        _previewTexture.SetPixel(x, i, gradientColor);
                    }
                    break;

                case GradientMethod.LineGradient:
                    // Create a gradient based on the line's y-position
                    for (int i = 0; i < previewHeight; i++)
                    {
                        float lerpValue = (float)i / (previewHeight - 1);
                        Color gradientColor = gradient.Evaluate(lerpValue);
                        if (i == y)
                        {
                            _previewTexture.SetPixel(x, i, lineColor);
                        }
                        else
                        {
                            _previewTexture.SetPixel(x, i, gradientColor);
                        }
                    }
                    break;

                case GradientMethod.NewGradientMethod:
                    // Add your new gradient method logic here
                    for (int i = 0; i < previewHeight; i++)
                    {
                        float lerpValue = (float)i / (previewHeight - 1);
                        Color gradientColor = gradient.Evaluate(lerpValue);
                        if (i <= y)
                        {
                            _previewTexture.SetPixel(x, i, gradientColor);
                        }
                        else
                        {
                            _previewTexture.SetPixel(x, i, Color.black);
                        }
                    }
                    break;

                case GradientMethod.NewTransGradientMethod:
                    // Add your new gradient method logic here
                    for (int i = 0; i < previewHeight; i++)
                    {
                        float lerpValue = (float)i / (previewHeight - 1);
                        Color gradientColor = gradient.Evaluate(lerpValue);
                        if (i <= y)
                        {
                            _previewTexture.SetPixel(x, i, gradientColor);
                        }
                        else
                        {
                            _previewTexture.SetPixel(x, i, Color.clear);
                        }
                    }
                    break;

                case GradientMethod.HorizontalBasedOnLineYPosition:
                    // Create a horizontal gradient based on the line's y-position
                    for (int i = 0; i < previewHeight; i++)
                    {
                        float distance = Mathf.Abs(i - y);
                        float lerpValue = 1 - (distance / (previewHeight - 1));
                        Color gradientColor = gradient.Evaluate(lerpValue);
                        _previewTexture.SetPixel(x, i, gradientColor);
                    }
                    break;
            }

            // Set the pixel color at the evaluated point
            _previewTexture.SetPixel(x, y, lineColor);
        }

        // Apply the texture preview
        _previewTexture.Apply();

        // Assign the preview texture to the final texture
        final_previewTexture = _previewTexture;

        // Apply the final texture
        final_previewTexture.Apply();
    }
}