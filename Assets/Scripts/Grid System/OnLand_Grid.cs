using UnityEngine;
using System.Collections.Generic;
using System.Collections;
public class OnLand_Grid : MonoBehaviour 
{
    public GameObject[] treePrefabs;    // Tree Prefabs Array
    public Material terrainMaterial;    // Terrain Material
    public Material edgeMaterial;       // Edge Material
    public MeshColliderCookingOptions cookingOptions;
    
    public float landLevel = .6f;      // Anything Below is land, Anything Above is Mountains 
    public float waterLevel = .4f;      // Anything Below is Water, Anything Above is Land 

    public float scale = .1f;
    public float treeNoiseScale = .05f;
    public float treeDensity = .5f;
    public int size = 100;

    Cell[,] grid;

    public bool _showLogs;


    #region Generation Function Explained

            /*  
                This is how the Generation Function Works
                
                This code block creates a 2D grid of Cell objects, where each cell is 
                assigned a "isWater" property based on the value of the noiseMap and 
                falloffMap. The value of the noiseMap at each point in the grid is first 
                subtracted by the corresponding value in the falloffMap to create a 
                new noise value, this new noise value is then compared to the waterLevel 
                variable, if the new noise value is less than the waterLevel, the cell is 
                considered water and the isWater property is set to true, otherwise it's 
                set to false.

                You can see this in this line bool isWater = noiseValue < waterLevel; 
                where the isWater variable is assigned true if the noiseValue is less 
                than the waterLevel variable, and false otherwise.
            */

        #endregion


    void Awake()
    {
        // Creates a new Noise Map
        float[,] noiseMap = new float[size, size]; 
        (float xOffset, float yOffset) = (Random.Range(-10000f, 10000f), Random.Range(-10000f, 10000f));
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noiseValue = Mathf.PerlinNoise(x * scale + xOffset, y * scale + yOffset);
                noiseMap[x, y] = noiseValue;
            }
        }

        // Creates a new Falloff Map
        float[,] falloffMap = new float[size, size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float xv = x / (float)size * 2 - 1;
                float yv = y / (float)size * 2 - 1;
                float v = Mathf.Max(Mathf.Abs(xv), Mathf.Abs(yv));
                falloffMap[x, y] = Mathf.Pow(v, 3f) / (Mathf.Pow(v, 3f) + Mathf.Pow(2.2f - 2.2f * v, 3f));
            }
        }
        
        //Generation Function
        grid = new Cell[size, size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noiseValue = noiseMap[x, y];
                noiseValue -= falloffMap[x, y];
                bool isWater = noiseValue < waterLevel;
                Cell cell = new Cell(isWater);
                grid[x, y] = cell;
            }
        }

        DrawTerrainMesh(grid);
        DrawEdgeMesh(grid);
        DrawTexture(grid);
        //GenerateTree(grid);
    }
    Material material;

    void Start() {
        material = GetComponent<Renderer>().material;
        material.mainTexture = Resources.Load("Terrain") as Texture2D;

        //gameObject.GetComponent<MeshRenderer>();
        MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        //meshRenderer = GetComponent<MeshRenderer>();
        //meshFilter = GetComponent<MeshFilter>();
    }

    void DrawTerrainMesh(Cell[,] grid)
    {
        Log("Drawing Terrain Mesh");
        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Cell cell = grid[x, y];
                if (!cell.isWater)
                {
                    Vector3 a = new Vector3(x - .5f, 0, y + .5f);
                    Vector3 b = new Vector3(x + .5f, 0, y + .5f);
                    Vector3 c = new Vector3(x - .5f, 0, y - .5f);
                    Vector3 d = new Vector3(x + .5f, 0, y - .5f);
                    Vector2 uvA = new Vector2(x / (float)size, y / (float)size);
                    Vector2 uvB = new Vector2((x + 1) / (float)size, y / (float)size);
                    Vector2 uvC = new Vector2(x / (float)size, (y + 1) / (float)size);
                    Vector2 uvD = new Vector2((x + 1) / (float)size, (y + 1) / (float)size);
                    Vector3[] V = new Vector3[] { a, b, c, b, d, c };
                    Vector2[] uv = new Vector2[] { uvA, uvB, uvC, uvB, uvD, uvC };
                    for (int k = 0; k < 6; k++)
                    {
                        vertices.Add(V[k]);
                        triangles.Add(triangles.Count);
                        uvs.Add(uv[k]);
                    }
                }
            }
        
        //terrainMaterial.mainTexture = texture;

        }


        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();

        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.material = terrainMaterial;

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf && mf.sharedMesh)
        {
            Bounds bounds = mf.sharedMesh.bounds;
            BoxCollider collider = mf.gameObject.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
        }
        
    }

    void DrawTexture(Cell[,] grid)
    {   
        // Create a new texture with size x size dimensions
        Texture2D texture = new Texture2D(size, size);

        // Create a color map with size x size number of colors
        Color[] colorMap = new Color[size * size];

        // Dark Green
        Color darkGreen = new Vector4(0.2f, 0.6f, 0.2f, 1f);

        // Set the texture's filter mode to Point
        texture.filterMode = FilterMode.Point;

        // Set the texture's pixels to the color map
        texture.SetPixels(colorMap);

        // Apply the changes to the texture
        texture.Apply();

        // Get the MeshRenderer component of the current game object
        MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();

        // Assign the terrainMaterial to the MeshRenderer's material
        meshRenderer.material = terrainMaterial;

        // Iterate through the cells in the grid
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Get the current cell
                Cell cell = grid[x, y];

                // If the cell is designated as water, set the corresponding color in the color map to blue
                if (cell.isWater)
                    colorMap[y * size + x] = Color.blue;
                // If the cell is designated as plain, set the corresponding color in the color map to green
                else if (cell.isPlain)
                    colorMap[y * size + x] = darkGreen;
                // If the cell is designated as desert, set the corresponding color in the color map to yellow
                else if (cell.isDesert)
                    colorMap[y * size + x] = Color.yellow;
                // If the cell is designated as forest, set the corresponding color in the color map to green
                else if (cell.isForest)
                    colorMap[y * size + x] = Color.green;
            }
        }

        // Assign the texture to the MeshRenderer's mainTexture
        meshRenderer.material.mainTexture = texture;
        
        // Checks Textures
        string[] textureNames = new string[] { "PlainTexture", "DesertTexture", "ForestTexture" };

        // Checks Folder Path
        Debug.Log(Application.dataPath + "/Resources/");

        // Checks What Texture is missing / null
        foreach (string textureName in textureNames)
        {
            Texture2D myTexture = Resources.Load(textureName) as Texture2D;
            if (myTexture == null)
            {
                Debug.Log(textureName + " not found in Resources folder");
            }
            else
            {
                Debug.Log(textureName + " found in Resources folder");
            }
        }

    }

    /*
    void DrawTexture(Cell[,] grid)
    {   

        // Create a new texture with size x size dimensions
        Texture2D texture = new Texture2D(size, size);

        // Create a color map with size x size number of colors
        Color[] colorMap = new Color[size * size];

        // Create a list of materials
        List<Material> materials = new List<Material>();

        // Dark Green
        Color darkGreen = new Vector4(0.2f, 0.6f, 0.2f, 1f);
        
        // Create a list of textures
        List<Texture2D> textures = new List<Texture2D>();

        // Add plain texture to the list of textures
        Texture2D plainTexture = Resources.Load("PlainTexture") as Texture2D;
        textures.Add(plainTexture);

        // Add desert texture to the list of textures
        Texture2D desertTexture = Resources.Load("DesertTexture") as Texture2D;
        textures.Add(desertTexture);

        // Add forest texture to the list of textures
        Texture2D forestTexture = Resources.Load("ForestTexture") as Texture2D;
        textures.Add(forestTexture);

        // Set the texture's filter mode to Point
        texture.filterMode = FilterMode.Point;

        // Set the texture's pixels to the color map
        texture.SetPixels(colorMap);

        // Apply the changes to the texture
        texture.Apply();

        // Get the MeshRenderer component of the current game object
        MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();

        // Assign the terrainMaterial to the MeshRenderer's material
        meshRenderer.material = terrainMaterial;

        // Assign the texture to the MeshRenderer's mainTexture
        meshRenderer.material.mainTexture = texture;

        // Iterate through the cells in the grid
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Get the current cell
                Cell cell = grid[x, y];

                // If the cell is designated as water, set the corresponding color in the color map to blue
                if (cell.isWater)
                    colorMap[y * size + x] = Color.blue;
                // If the cell is designated as plain, set the corresponding texture in the material
                else if (cell.isPlain)
                {
                    texture = textures[0];
                    meshRenderer.material.mainTexture = texture;
                }
                // If the cell is designated as desert, set the corresponding texture in the material
                else if (cell.isDesert)
                {
                    texture = textures[1];
                    meshRenderer.material.mainTexture = texture;
                }
                // If the cell is designated as forest, set the corresponding texture in the material
                else if (cell.isForest)
                {
                    texture = textures[2];
                    meshRenderer.material.mainTexture = texture;
                }
            }
        }
    }
    */


    void DrawEdgeMesh(Cell[,] grid)
    {
        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Cell cell = grid[x, y];
                if (!cell.isWater)
                {
                    if (x > 0)
                    {
                        Cell left = grid[x - 1, y];
                        if (left.isWater)
                        {
                            Vector3 a = new Vector3(x - .5f, 0, y + .5f);
                            Vector3 b = new Vector3(x - .5f, 0, y - .5f);
                            Vector3 c = new Vector3(x - .5f, -1, y + .5f);
                            Vector3 d = new Vector3(x - .5f, -1, y - .5f);
                            Vector3[] v = new Vector3[] { a, b, c, b, d, c };
                            for (int k = 0; k < 6; k++)
                            {
                                vertices.Add(v[k]);
                                triangles.Add(triangles.Count);
                            }
                        }
                    }
                    if (x < size - 1)
                    {
                        Cell right = grid[x + 1, y];
                        if (right.isWater)
                        {
                            Vector3 a = new Vector3(x + .5f, 0, y - .5f);
                            Vector3 b = new Vector3(x + .5f, 0, y + .5f);
                            Vector3 c = new Vector3(x + .5f, -1, y - .5f);
                            Vector3 d = new Vector3(x + .5f, -1, y + .5f);
                            Vector3[] v = new Vector3[] { a, b, c, b, d, c };
                            for (int k = 0; k < 6; k++)
                            {
                                vertices.Add(v[k]);
                                triangles.Add(triangles.Count);
                            }
                        }
                    }
                    if (y > 0)
                    {
                        Cell down = grid[x, y - 1];
                        if (down.isWater)
                        {
                            Vector3 a = new Vector3(x - .5f, 0, y - .5f);
                            Vector3 b = new Vector3(x + .5f, 0, y - .5f);
                            Vector3 c = new Vector3(x - .5f, -1, y - .5f);
                            Vector3 d = new Vector3(x + .5f, -1, y - .5f);
                            Vector3[] v = new Vector3[] { a, b, c, b, d, c };
                            for (int k = 0; k < 6; k++)
                            {
                                vertices.Add(v[k]);
                                triangles.Add(triangles.Count);
                            }
                        }
                    }
                    if (y < size - 1)
                    {
                        Cell up = grid[x, y + 1];
                        if (up.isWater)
                        {
                            Vector3 a = new Vector3(x + .5f, 0, y + .5f);
                            Vector3 b = new Vector3(x - .5f, 0, y + .5f);
                            Vector3 c = new Vector3(x + .5f, -1, y + .5f);
                            Vector3 d = new Vector3(x - .5f, -1, y + .5f);
                            Vector3[] v = new Vector3[] { a, b, c, b, d, c };
                            for (int k = 0; k < 6; k++)
                            {
                                vertices.Add(v[k]);
                                triangles.Add(triangles.Count);
                            }
                        }
                    }
                }
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        GameObject edgeObj = new GameObject("Edge");
        edgeObj.transform.SetParent(transform);

        MeshFilter meshFilter = edgeObj.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        MeshRenderer meshRenderer = edgeObj.AddComponent<MeshRenderer>();
        meshRenderer.material = edgeMaterial;

    }

    // Tree Generation Function
    void GenerateTree(Cell[,] grid)
    {
        Log("Initiating Tree Generation");
        float[,] noiseMap = new float[size, size];
        (float xOffset, float yOffset) = (Random.Range(-10000f, 10000f), Random.Range(-10000f, 10000f));
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noiseValue = Mathf.PerlinNoise(x * treeNoiseScale + xOffset, y * treeNoiseScale + yOffset);
                noiseMap[x, y] = noiseValue;
            }
        }
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Cell cell = grid[x, y];
                if (!cell.isWater)
                {
                    //MeshCollider sc = GetComponent<MeshCollider>();
                    float v = Random.Range(0f, treeDensity);
                    if (noiseMap[x, y] < v)
                    {
                        GameObject prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
                        GameObject tree = Instantiate(prefab, transform);
                        tree.transform.position = new Vector3(x, 0, y);
                        //tree.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                        //tree.transform.localScale = Vector3.one * Random.Range(.8f, 1.2f);
                    }
                }
            }
        }
    }

    /*//Gizmo Illustration
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Cell cell = grid[x, y];
                if (cell.isWater)
                    Gizmos.color = Color.blue;
                else
                    Gizmos.color = Color.green;
                Vector3 pos = new Vector3(x, 0, y);
                Gizmos.DrawCube(pos, Vector3.one);
            }
        }
    }*/

    #region Old DrawTexture Code

    // Segment 0
            // Old
            // // Iterate through the cells in the grid
            // for (int y = 0; y < size; y++)
            // {
            //     for (int x = 0; x < size; x++)
            //     {
            //         // Get the current cell
            //         Cell cell = grid[x, y];
            //
            //         // If the cell is designated as water, set the corresponding color in the color map to blue
            //         if (cell.isWater)
            //             colorMap[y * size + x] = Color.blue;
            //         // Otherwise, set it to green
            //         else if (cell.isPlain)
            //             colorMap[y * size + x] = Color.green;
            //         // Otherwise, set it to yellow
            //         else if (cell.isDesert)
            //             colorMap[y * size + x] = Color.yellow;
            //         // Otherwise, set it to dark green
            //         else if (cell.isForest)
            //             colorMap[y * size + x] = darkGreen;
            //       
            //     }
            // }
    // Segment 1 
            // void DrawTexture(Cell[,] grid)
            // {
            //     Texture2D texture = new Texture2D(size, size);
            //     Color[] colorMap = new Color[size * size];
            //     for (int y = 0; y < size; y++)
            //     {
            //         for (int x = 0; x < size; x++)
            //         {
            //             Cell cell = grid[x, y];
            //             if (cell.isWater)
            //                 colorMap[y * size + x] = Color.blue;
            //             else
            //                 colorMap[y * size + x] = Color.green;
            //         }
            //     }
            //     texture.filterMode = FilterMode.Point;
            //     texture.SetPixels(colorMap);
            //     texture.Apply();

            //     MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
            //     meshRenderer.material = terrainMaterial;
            //     meshRenderer.material.mainTexture = texture;
            // } 
            
            // // Map Materials
            // materials.Add(desertMaterial);
            // materials.Add(forestMaterial);

            // // Material index 
            // int materialIndex = 0;
    #endregion


    void Log(object message)
    {
        if (_showLogs)
        {
            Debug.Log(message);
        }
    }

}

