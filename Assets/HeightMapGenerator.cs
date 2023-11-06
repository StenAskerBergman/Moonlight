using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class HeightMapGenerator : MonoBehaviour
{
    public int width = 256;
    public int height = 256;
    public float scale = 20;
    [HideInInspector]
    public INoiseGenerator noiseGenerator;

    private void OnEnable()
    {
        // Dependency Injection
        noiseGenerator = new PerlinNoiseGenerator();
    }

    // Add a Generate button in the Inspector
    [ContextMenu("Generate")]
    public void GenerateHeightMap()
    {
        Texture2D heightMapTexture = new Texture2D(width, height);
        heightMapTexture.wrapMode = TextureWrapMode.Clamp;
        heightMapTexture.filterMode = FilterMode.Point;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float pX = (float)x / width * scale;
                float pY = (float)y / height * scale;
                float value = noiseGenerator.Generate(pX, pY, 1.0f); // scale already applied to pX and pY
                Debug.Log("Noise value: " + value); // Debugging line
                Color color = new Color(value, value, value);
                heightMapTexture.SetPixel(x, y, color);
            }
        }

        heightMapTexture.Apply();

        if (Application.isPlaying)
        {
            GetComponent<Renderer>().material.mainTexture = heightMapTexture;
        }
        else
        {
            GetComponent<Renderer>().sharedMaterial.mainTexture = heightMapTexture;
        }
    }
}