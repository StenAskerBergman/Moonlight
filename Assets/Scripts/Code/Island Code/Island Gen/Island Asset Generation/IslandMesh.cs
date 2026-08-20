// IslandMesh.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class IslandMesh : MonoBehaviour, IPreviewable
{
    [Header("Map Size")]
    public int baseWidth = 100;
    public int baseHeight = 100;
    [Space(10)]

    [Header("Elevation Control")]
    public float minHeight = 0f;
    public float maxHeight = 10f;
    public float blackToWhiteSlope = 1f;
    public float blackSlope = 1f;
    public float whiteSlope = 1f;

    #region Calculate MinMax Elevation
    private void CalculateMinMaxElevation()
    {
        if (vertices == null || vertices.Length == 0) return;

        float minVertexHeight = float.MaxValue;
        float maxVertexHeight = float.MinValue;

        // Find min and max vertex heights
        for (int i = 0; i < vertices.Length; i++)
        {
            minVertexHeight = Mathf.Min(minVertexHeight, vertices[i].y);
            maxVertexHeight = Mathf.Max(maxVertexHeight, vertices[i].y);
        }

        float heightRange = maxVertexHeight - minVertexHeight;

        for (int i = 0; i < vertices.Length; i++)
        {
            float normalizedHeight = (vertices[i].y - minVertexHeight) / heightRange;

            // Apply the slope controls
            if (normalizedHeight < 0.5f)
            {
                normalizedHeight = Mathf.Pow(normalizedHeight * 2f, blackSlope) / 2f;
            }
            else
            {
                normalizedHeight = 1f - Mathf.Pow((1f - normalizedHeight) * 2f, whiteSlope) / 2f;
            }

            // Apply the height difference control
            vertices[i].y = Mathf.Lerp(minHeight, maxHeight, normalizedHeight);
        }
    }

    #endregion

    [Space(10)]
    [Header("Noise Control")]
    public List<NoiseStack> noiseStacks;
    public List<NoiseLayer> noiseLayers;
    public List<float> stackWeights;        // Used to control the influence of each stack
    public List<float> stackBias;           // Used to control whether a stack affects the final height map

    public NoiseMixer noiseMixer;



    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    private bool needsRegeneration = true;

    /* Weights Explanation 

        Weights in the noise stack are used to controls 
        the influence of each stack on the final height 
        map. Here's how it works:

        If you have a weight of 1 for a noise stack, it 
        means that stack fully influences the height map.

        If you have a weight of 0 for a noise stack, it 
        means that stack has no height map impact.

        Any weight between 0 and 1 proportionally affects 
        the height map. For instance, 0.5 weight implies 
        half the influence of weight 1.

        The final point value in the height map is found by 
        multiplying stack value by its weight, summing these 
        values, and dividing by total stack weight.

     */

    #region Initalization + Awake
    void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// Mirrors the generated mesh onto the MeshCollider.
    /// Without this the collider keeps whatever mesh the prefab shipped with (a flat
    /// built-in Plane), so raycasts and NavMesh baking would see a flat square while
    /// the island renders as terrain. Reassigning sharedMesh also forces PhysX to
    /// re-cook, which is required after the vertices change.
    /// </summary>
    private void PushMeshToCollider()
    {
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null) return;

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }

    public void Initialize()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        if (noiseMixer == null)
        {
            noiseMixer = new NoiseMixer();
        }

        if (noiseMixer != null && noiseStacks != null && stackWeights != null)
        {
            CreateMesh();
        }
        else
        {
            Debug.LogError("Initialization failed. Make sure all required fields are set.");
        }

        // Clamp vertex heights based on MinHeight and MaxHeight from the inspector
        CalculateMinMaxElevation();
    }
    #endregion

    #region Mesh Generation
    public void CreateMesh()
    {
        Flat = false;
        GeneratePreview();  // Add this line to update the preview texture immediately

        if (!needsRegeneration)
        {
            return;
        }

        Debug.Log($"noiseStacks is null: {noiseStacks == null}");
        Debug.Log($"stackWeights is null: {stackWeights == null}");

        // Pass baseWidth and baseHeight as parameters to MixNoiseStacks
        float[,] heights = noiseMixer.MixNoiseStacks(noiseStacks, stackWeights, baseWidth, baseHeight);

        // Pass baseWidth and baseHeight as parameters to MixNoiseLayers
        heights = noiseMixer.MixNoiseLayers(heights, noiseLayers, baseWidth, baseHeight);

        // Null Checks
        if (heights == null) Debug.Log($"heights is null: {heights == null}");
        if (heights.Length == 0) Debug.Log($"heights is Empty: {heights.Length == 0}");

        if (mesh == null)
        {
            mesh = new Mesh();
            GetComponent<MeshFilter>().mesh = mesh;
        }

        // Use baseWidth and baseHeight as default values if heights is null or empty
        int width = (heights != null && heights.Length > 0) ? heights.GetLength(0) : baseWidth;
        int height = (heights != null && heights.Length > 0) ? heights.GetLength(1) : baseHeight;

        // Calc. & Apply the Center Offset
        float offsetX = width / 2.0f;
        float offsetY = height / 2.0f;

        Debug.Log($"heights dimensions: width = {width}, height = {height}");

        vertices = new Vector3[(width + 1) * (height + 1)];

        ApplyHeightMap(heights, width, height, offsetX, offsetY);

        triangles = new int[width * height * 6];
        for (int ti = 0, vi = 0, y = 0; y < height; y++, vi++)
        {
            for (int x = 0; x < width; x++, ti += 6, vi++)
            {
                triangles[ti] = vi;
                triangles[ti + 1] = vi + width + 1;
                triangles[ti + 2] = vi + 1;
                triangles[ti + 3] = vi + 1;
                triangles[ti + 4] = vi + width + 1;
                triangles[ti + 5] = vi + width + 2;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        PushMeshToCollider();

        Debug.Log($"Vertices length: {vertices.Length}");
        Debug.Log($"Triangles length: {triangles.Length}");
        Debug.Log($"First vertex: {vertices[0]}");

#if UNITY_EDITOR
        UnityEditor.SceneView.RepaintAll();
#endif

        needsRegeneration = false;
    }
    #endregion

    #region Apply HeightMap Method
    public void ApplyHeightMap(float[,] heights, int width, int height, float offsetX, float offsetY)
    {
        for (int i = 0, y = 0; y <= height; y++)
        {
            for (int x = 0; x <= width; x++)
            {
                // This line is where the height map value is being applied to the y-coordinate of the vertex.
                // If you want to see the mesh without the height map applied, you can replace this line with:
                // float vertexHeight = 0;

                // This line is where the height map value is being applied to the x-coordinate of the vertex.
                float vertexHeight = y < height && x < width && heights != null ? heights[x, y] : 0;
                vertices[i] = new Vector3(x - offsetX, vertexHeight, y - offsetY);
                i++;
            }
        }
    }
    #endregion

    #region "Mark for Regeneration" Method
    public void MarkForRegeneration()
    {
        needsRegeneration = true;
    }

    #endregion

    #region Flatten Mesh
    private bool Flat;
    public void FlattenMesh()
    {
        // Ensure the mesh is initialized and vertices are fetched
        InitializeMeshData();

        if (vertices == null || vertices.Length == 0)
        {
            Debug.LogError("Vertices array is null or empty. Cannot flatten mesh.");
            return;
        }

        // Set the y-coordinate of each vertex to 0 to flatten the mesh
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].y = 0f;
        }
        Flat = true;
        GeneratePreview();  // Add this line to update the preview texture immediately

        // Update the mesh
        mesh.vertices = vertices;
        mesh.RecalculateNormals();

        // Mark the mesh for regeneration
        MarkForRegeneration();
    }

    #endregion

    #region Reshape Mesh

    // IslandMesh.cs
    public void ReshapeMesh()
    {
        Flat = false;
        GeneratePreview();  // Add this line to update the preview texture immediately

        // Ensure the mesh is initialized and vertices are fetched
        InitializeMeshData();

        // Pass baseWidth and baseHeight as parameters to MixNoiseStacks
        float[,] heights = noiseMixer.MixNoiseStacks(noiseStacks, stackWeights, baseWidth, baseHeight);
      
        // Mix the noise layers
        heights = noiseMixer.MixNoiseLayers(heights, noiseLayers, baseWidth, baseHeight);

        if (heights == null || heights.Length == 0)
        {
            Debug.LogError("Heights array is null or empty. Cannot reshape mesh.");
            return;
        }


        // Apply the height map values to the y-coordinate of each vertex
        for (int i = 0, y = 0; y <= baseHeight; y++)
        {
            for (int x = 0; x <= baseWidth; x++)
            {
                if (y < heights.GetLength(1) && x < heights.GetLength(0))
                {
                    vertices[i].y = heights[x, y];
                }
                i++;
            }
        }

        // Update the mesh
        mesh.vertices = vertices;
        mesh.RecalculateNormals();

        // Mark the mesh for regeneration
        MarkForRegeneration();
    }


    #endregion

    #region Initialize Mesh Data

    private void InitializeMeshData()
    {
        if (mesh == null)
        {
            mesh = GetComponent<MeshFilter>().sharedMesh;
        }

        if (vertices == null || vertices.Length == 0)
        {
            if (mesh != null)
            {
                vertices = mesh.vertices;
            }
            else
            {
                Debug.LogError("Mesh is null. Cannot fetch vertices.");
            }
        }

        if (noiseMixer == null)
        {
            noiseMixer = new NoiseMixer();
        }
    }

    #endregion

    #region Save Mesh

    public void SaveHeightMap()
    {
        // Ensure the mesh is initialized and vertices are fetched
        InitializeMeshData();

        if (vertices == null || vertices.Length == 0)
        {
            Debug.LogError("Vertices array is null or empty. Cannot save height map.");
            return;
        }

        // Create a texture and set its pixels based on the height values of the vertices
        Texture2D heightMap = new Texture2D(baseWidth + 1, baseHeight + 1);
        for (int y = 0; y <= baseHeight; y++)
        {
            for (int x = 0; x <= baseWidth; x++)
            {
                float height = vertices[y * (baseWidth + 1) + x].y;
                Color color = new Color(height, height, height);
                heightMap.SetPixel(x, y, color);
            }
        }
        heightMap.Apply();

        // Save the texture as a PNG file
        byte[] bytes = heightMap.EncodeToPNG();
        System.IO.File.WriteAllBytes("Assets/Resources/HeightMaps/heightMap.png", bytes);

        // Refresh the asset database to make the new file appear in the editor
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif

        Debug.Log("Height map saved successfully!");
    }

    #endregion

    #region Preview Generation

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

    // IslandMesh.cs
    public virtual void GeneratePreview()
    {
        // Logic to generate the preview texture for IslandMesh

        // Define the size of the preview texture
        int previewWidth = 100;
        int previewHeight = 100;

        // Create a texture for the preview
        _previewTexture = new Texture2D(previewWidth, previewHeight);
        _previewTexture.wrapMode = TextureWrapMode.Clamp;
        _previewTexture.filterMode = FilterMode.Point;

        // Null Checks
        if (noiseMixer == null)
        {
            noiseMixer = new NoiseMixer();
            Debug.LogWarning("noiseMixer was null and a new instance was created.");
        }

        if (noiseStacks == null || stackWeights == null)
        {
            Debug.LogError("noiseStacks or stackWeights is null");
            return;
        }

        float[,] heights = null;

        // Check the Flat value and set the height accordingly
        if (!Flat)
        {
            // Mix the noise stacks
            heights = noiseMixer.MixNoiseStacks(noiseStacks, stackWeights, previewWidth, previewHeight);

            // Mix the noise layers
            heights = noiseMixer.MixNoiseLayers(heights, noiseLayers, previewWidth, previewHeight);

            if (heights == null || heights.Length == 0)
            {
                Debug.LogError("Heights array is null or empty. Cannot create preview.");
                return;
            }
        }
        else
        {
            // If Flat is true, create a flat height map
            heights = new float[previewWidth, previewHeight];
            for (int x = 0; x < previewWidth; x++)
            {
                for (int y = 0; y < previewHeight; y++)
                {
                    heights[x, y] = 0;
                }
            }
        }

        // Set the pixel color based on the height map
        for (int x = 0; x < previewWidth; x++)
        {
            for (int y = 0; y < previewHeight; y++)
            {
                float heightValue = heights[x, y];
                Color color = new Color(heightValue, heightValue, heightValue);
                _previewTexture.SetPixel(x, y, color);
            }
        }

        _previewTexture.Apply();
    }
    #endregion

    #region Apply Island Shape
    public void ApplyIslandShape()
    {
        // Ensure the mesh is initialized and vertices are fetched
        InitializeMeshData();

        if (vertices == null || vertices.Length == 0)
        {
            Debug.LogError("Vertices array is null or empty. Cannot apply island shape.");
            return;
        }

        // Create a radial gradient mask
        float[,] mask = CreateRadialGradientMask(baseWidth, baseHeight);

        // Apply the mask to the height map
        for (int i = 0, y = 0; y <= baseHeight; y++)
        {
            for (int x = 0; x <= baseWidth; x++)
            {
                if (y < mask.GetLength(1) && x < mask.GetLength(0))
                {
                    vertices[i].y *= mask[x, y];
                }
                i++;
            }
        }

        // Update the mesh
        mesh.vertices = vertices;
        mesh.RecalculateNormals();

        // Mark the mesh for regeneration
        MarkForRegeneration();
    }

    public float[,] CreateRadialGradientMask(int width, int height)
    {
        float[,] mask = new float[width, height];

        float centerX = width / 2.0f;
        float centerY = height / 2.0f;
        float maxDistance = Mathf.Min(centerX, centerY);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                float value = 1.0f - Mathf.Clamp01(distance / maxDistance);
                mask[x, y] = value;
            }
        }

        return mask;
    }
    #endregion

}


