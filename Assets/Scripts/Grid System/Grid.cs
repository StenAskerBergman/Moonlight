using UnityEngine;
using System.Collections.Generic;
using static Cell;
using System.Linq;

public class Grid : MonoBehaviour
{

    #region Variables + Awake

        // Prefabs
        public GameObject[] treePrefabs;
        public Material terrainMaterial;

        // Materials
        public Material edgeMaterial;
        public Material oceanEdgeMaterial;
        public Material coastEdgeMaterial;
        public Material beachEdgeMaterial;
        public Material mountainEdgeMaterial;

        public MeshColliderCookingOptions cookingOptions;

        // Height thresholds
        public float mountainThreshold = 0.8f; 
        public float deepSeaThreshold = 0.2f; 

        // Terrain settings
        public float landLevel = .6f;
        public float waterLevel = .4f;
        public float scale = .1f;
        public float treeNoiseScale = .05f;
        public float treeDensity = .5f;
        public int size = 100;

        public TerrainType terrainType;

        private Cell[,] grid;

        void Awake()
        {
            GenerateTerrain();
        }

    #endregion

    #region Console Log Method
    [SerializeField] private bool _showLogs;
    void Log(object message)
    {
        if (_showLogs)
        {
            Debug.Log(message);
        }
    }
    void LogError(object message)
    {
        if (_showLogs)
        {
            Debug.LogError(message);
        }
    }
    void LogWarning(object message)
    {
        if (_showLogs)
        {
            Debug.LogWarning(message);
        }
    }
    #endregion

    #region GenerateTerrain Method
    // For Generating the Terrain of a Island
    private void GenerateTerrain()
    {
        grid = new Cell[size, size];
        float[,] noiseMap = GenerateNoiseMap();
        float[,] falloffMap = GenerateFalloffMap();

        // Populate grid
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noiseValue = noiseMap[x, y] - falloffMap[x, y];
                TerrainType terrainType;

                if (noiseValue < deepSeaThreshold)
                {
                    terrainType = TerrainType.Deep;
                }
                else if (noiseValue < waterLevel)
                {
                    terrainType = TerrainType.Water;
                }
                else if (noiseValue > mountainThreshold)
                {
                    terrainType = DetermineMountainHeight(noiseValue);
                }
                else
                {
                    terrainType = TerrainType.Land;
                }

                float height = GetHeightForTerrainType(terrainType);
                Vector3 position = new Vector3(x, height, y);
                grid[x, y] = new Cell(position, null, terrainType);
            }
        }

        // Log noise and falloff values for a sample of cells
        Log("Logging noise and falloff values for a sample of cells...");
        for (int y = 0; y < size; y += size / 10) // Log for every 10th row for sampling
        {
            for (int x = 0; x < size; x += size / 10) // Log for every 10th column for sampling
            {
                var noiseValue = noiseMap[x, y];
                var falloffValue = falloffMap[x, y];
                Log($"Cell at ({x}, {y}) has noise value {noiseValue} and falloff value {falloffValue}");
            }
        }


        // After terrain types have been assigned and before UpdateNeighbors is called
        int landCount = 0, waterCount = 0, beachCount = 0;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (grid[x, y].currentTerrainType == TerrainType.Land) landCount++;
                if (grid[x, y].currentTerrainType == TerrainType.Beach) beachCount++;
                if (grid[x, y].currentTerrainType == TerrainType.Water) waterCount++;
            }
        }
        Log($"Land cells: {landCount}, Water cells: {waterCount}, Beach cells: {beachCount}");

        // Populate neighbors
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                grid[x, y].UpdateNeighbors(grid, size);
            }
        }

        // After updating neighbors
        Log("Logging neighbors for a sample of cells...");
        for (int y = 0; y < size; y += size / 10) // Log for every 10th row for sampling
        {
            for (int x = 0; x < size; x += size / 10) // Log for every 10th column for sampling
            {
                var cell = grid[x, y];
                Log($"Cell at ({x}, {y}) of type {cell.currentTerrainType} has {cell.neighbors.Count} neighbors");
            }
        }

        // Inserted snippet to log out neighbor information for debugging
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (grid[x, y].currentTerrainType == TerrainType.Land)
                {
                    int waterNeighborCount = grid[x, y].neighbors.Count(n => n.currentTerrainType == TerrainType.Water);
                    if (waterNeighborCount > 0)
                    {
                        Log($"Land cell at ({x}, {y}) has {waterNeighborCount} water neighbors");
                    }
                }
            }
        }

        ApplyBeachEdges();
        GenerateMountains();

        BuildMeshesAndTextures();
    }

    #endregion

    #region DetermineMountainHeight Method

    // Note: Might be overriding the terrain type for beaches

    // This method determines the specific mountain terrain type based on the noise value
    private TerrainType DetermineMountainHeight(float noiseValue)
    {
        // Adjust thresholds as necessary
        if (noiseValue > 1.1f) 
            return TerrainType.MountainPeak;
        else if (noiseValue > 0.5f)
            return TerrainType.Mountain;
        else
            return TerrainType.Land;
    }

    #endregion

    #region Noise & Falloff Methods

    private float[,] GenerateNoiseMap()
        {
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
            return noiseMap;
        }

        private float[,] GenerateFalloffMap()
        {
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
            return falloffMap;
        }

    #endregion

    #region Beach Creation Methods

    // Sand Edge
    private void ApplyBeachEdges()
    {
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Cell cell = grid[x, y];
                if (cell.currentTerrainType == TerrainType.Land)
                {
                    // Check the neighbors of the cell
                    foreach (Vector2Int direction in new Vector2Int[] { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) })
                    {
                        int newX = x + direction.x;
                        int newY = y + direction.y;
                        if (newX >= 0 && newX < size && newY >= 0 && newY < size)
                        {
                            Cell neighbor = grid[newX, newY];
                            if (neighbor.currentTerrainType == TerrainType.Water)
                            {
                                // If any neighbor is water, change the current cell to beach
                                cell.ChangeTerrainType(TerrainType.Beach); 
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region Mountain Section

    // Mountain Related
    [Space(10)]
    public int minMountains = 1; // Minimum number of mountains
    public int maxMountains = 3; // Maximum number of mountains
    public int minMountainHeight = 3; // Minimum height of mountains
    public int maxMountainHeight = 10; // Maximum height of mountains

    private void GenerateMountains()
    {
        // Ensure at least one mountain is generated
        bool atLeastOneMountainGenerated = false;

        // Try to generate the mountains up to a reasonable number of attempts
        int attempts = 0;
        int maxAttempts = size * size; // You can adjust this for efficiency

        while (!atLeastOneMountainGenerated && attempts < maxAttempts)
        {
            int x = Random.Range(0, size);
            int y = Random.Range(0, size);
            Cell startCell = grid[x, y];

            // Ensure the start cell is land and not already part of a beach or shore
            if (startCell.currentTerrainType == TerrainType.Land)
            {
                // Determine mountain height
                int mountainHeight = Random.Range(minMountainHeight, maxMountainHeight);

                // Generate mountain
                GenerateMountain(x, y, mountainHeight);

                atLeastOneMountainGenerated = true;
            }

            attempts++;
        }

        // Generate additional mountains if needed and if the first mountain was generated
        if (atLeastOneMountainGenerated)
        {
            int mountainCount = 1; // We've already generated one mountain
            while (mountainCount < maxMountains && attempts < maxAttempts)
            {
                int x = Random.Range(0, size);
                int y = Random.Range(0, size);
                Cell startCell = grid[x, y];

                if (startCell.currentTerrainType == TerrainType.Land)
                {
                    int mountainHeight = Random.Range(minMountainHeight, maxMountainHeight);
                    GenerateMountain(x, y, mountainHeight);
                    mountainCount++;
                }

                attempts++;
            }
        }
    }

    private float GetHeightForTerrainType(TerrainType type)
    {
        switch (type)
        {
            case TerrainType.MountainPeak:
                return 1f; // Example value, adjust as needed
            case TerrainType.Mountain:
                return 1f; // Example value, adjust as needed
            case TerrainType.Land:
                return 0f; // Land level
            case TerrainType.Beach:
            case TerrainType.Shore:
                return -0.5f; // Beach level, slightly below land
            case TerrainType.Water:
            case TerrainType.Deep:
                return -1f; // Water level, below land
            default:
                return 0f;
        }
    }

    private void GenerateMountain(int x, int y, int height)
    {
        // Recursive function to generate a mountain
        // ... (mountain generation logic)
    }

    #endregion

    private void BuildMeshesAndTextures()
    {
        // Builds Terrain
        TerrainMeshBuilder terrainMeshBuilder = new TerrainMeshBuilder(grid);
        Mesh terrainMesh = terrainMeshBuilder.Build();
        ApplyTerrainMesh(terrainMesh);

        // Build edges
        EdgeMeshBuilder edgeMeshBuilder = new EdgeMeshBuilder(grid);
        (Mesh coastMesh, Mesh oceanMesh, Mesh mountainMesh, Mesh beachMesh) = edgeMeshBuilder.Build(); // Receive "four" meshes
        ApplyCoastEdgeMesh(coastMesh);          // Apply the coast edge mesh
        ApplyOceanEdgeMesh(oceanMesh);          // Apply the ocean edge mesh
        ApplyMountainEdgeMesh(mountainMesh);    // Apply the mountain edge mesh
        ApplyBeachEdgeMesh(beachMesh);

        // Build and apply texture
        TextureBuilder textureBuilder = new TextureBuilder(grid);
        Texture2D texture = textureBuilder.Build();
        ApplyTexture(texture);
    }

    #region Edge Methods
    private void ApplyBeachEdgeMesh(Mesh beachMesh)
    {
        GameObject beachEdgeObj = new GameObject("beachEdge");
        beachEdgeObj.transform.SetParent(transform);

        MeshFilter beachMeshFilter = beachEdgeObj.AddComponent<MeshFilter>() ?? beachEdgeObj.AddComponent<MeshFilter>();
        beachMeshFilter.mesh = beachMesh;

        MeshRenderer beachMeshRenderer = beachEdgeObj.AddComponent<MeshRenderer>() ?? beachEdgeObj.AddComponent<MeshRenderer>();
        beachMeshRenderer.material = beachEdgeMaterial;
    }

    private void ApplyMountainEdgeMesh(Mesh mountainMesh)
    {
        GameObject mountainEdgeObj = new GameObject("MountainEdge");
        mountainEdgeObj.transform.SetParent(transform);

        MeshFilter mountainMeshFilter = mountainEdgeObj.AddComponent<MeshFilter>() ?? mountainEdgeObj.AddComponent<MeshFilter>();
        mountainMeshFilter.mesh = mountainMesh;

        MeshRenderer mountainMeshRenderer = mountainEdgeObj.AddComponent<MeshRenderer>() ?? mountainEdgeObj.AddComponent<MeshRenderer>();
        mountainMeshRenderer.material = mountainEdgeMaterial; 
    }

    private void ApplyCoastEdgeMesh(Mesh coastMesh)
    {
        GameObject coastEdgeObj = new GameObject("CoastEdge");
        coastEdgeObj.transform.SetParent(transform);

        MeshFilter coastMeshFilter = coastEdgeObj.AddComponent<MeshFilter>() ?? coastEdgeObj.AddComponent<MeshFilter>();
        coastMeshFilter.mesh = coastMesh;

        MeshRenderer coastMeshRenderer = coastEdgeObj.AddComponent<MeshRenderer>() ?? coastEdgeObj.AddComponent<MeshRenderer>();
        coastMeshRenderer.material = coastEdgeMaterial; 
    }

    private void ApplyOceanEdgeMesh(Mesh oceanMesh)
    {
        GameObject oceanEdgeObj = new GameObject("OceanEdge");
        oceanEdgeObj.transform.SetParent(transform);

        MeshFilter oceanMeshFilter = oceanEdgeObj.AddComponent<MeshFilter>() ?? oceanEdgeObj.AddComponent<MeshFilter>();
        oceanMeshFilter.mesh = oceanMesh;

        MeshRenderer oceanMeshRenderer = oceanEdgeObj.AddComponent<MeshRenderer>() ?? oceanEdgeObj.AddComponent<MeshRenderer>();
        oceanMeshRenderer.material = oceanEdgeMaterial; 
    }
    #endregion

    private void ApplyTerrainMesh(Mesh terrainMesh)
    {
        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = terrainMesh;

        MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        meshRenderer.material = terrainMaterial;
    }

    private void ApplyTexture(Texture2D texture)
    {
        MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.material.mainTexture = texture;
        }
    }
}
