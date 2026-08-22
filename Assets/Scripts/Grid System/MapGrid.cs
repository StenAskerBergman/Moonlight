using UnityEngine;
using System.Collections.Generic;
using static Cell;
using System.Linq;
using UnityEngine.AI;
using UnityEngine.UIElements;

public class MapGrid : MonoBehaviour
{

    #region Variables 


        // Edge Flags
        public bool hasMountainsGenerated = false; // Flag to track if mountains have been generated

        // Prefabs
        public GameObject[] treePrefabs;

        [Header("Base Materials")]

        [Header("Edge Materials")]
        // Materials
        public Material edgeMaterial;
        public Material terrainMaterial;
        public Material hillEdgeMaterial;
        public Material oceanEdgeMaterial;
        public Material coastEdgeMaterial;
        public Material beachEdgeMaterial;
        public Material riverEdgeMaterial;  
        public Material shallowEdgeMaterial;
        public Material deepSeaEdgeMaterial;
        public Material plateauEdgeMaterial;
        public Material abyssalEdgeMaterial;
        public Material mountainEdgeMaterial;

        // Nav Mesh
        [Header("Nav Mesh Related")]
        public MeshColliderCookingOptions cookingOptions;
        public MeshCollider meshCollider;
        public NavMeshBuilder ref_navMeshGameManager;
        [Space(10)]

        // Terrain & Noise Height thresholds
        [Header("Thresholds")]
        public float mountainThreshold = 0.8f; 
        public float landLevel = .6f;
        public float waterLevel = .4f;
        public float CoastThreshold = 0.35f;
        public float shallowWaterThreshold = 0.3f;
        public float plateauThreshold = 0.2f;
        public float deepSeaThreshold = 0.1f;
        public float abyssThreshold = 0.0f;
        [Space(10)]
            
        // More Terrain settings
        public float scale = .1f;
        public float treeNoiseScale = .05f;
        public float treeDensity = .5f;

        // Terrain Size
        public int size = 100;

        // Terrain Type 
        public TerrainType terrainType;

        // Cell Grid
        private Cell[,] grid;
        public Cell[,] Grid => grid;
        public int Size => size;

        public MeshCollider cellCollider;
    #endregion

    #region Awake + Start Method
    void Awake()
    {
            ref_navMeshGameManager = FindObjectOfType<NavMeshBuilder>();
            //GenerateTerrain();
    }

    void Start()
    {
        //ref_navMeshGameManager = FindObjectOfType<NavMeshBuilder>();
        // ref_navMeshGameManager = this.gameObject.GetComponentInChildren<NavMeshBuilder
        /* Getting it and Setting it
        meshCollider = this.transform.GetComponent<MeshCollider>();
        meshCollider.cookingOptions = cookingOptions;*/

        // Fuck that we tabbin n dabbin n adding raw

        #region Mesh Collider 1 - temp_local_Convex_Trigger_MeshCollider

        // Convex Trigger Mesh Collider - For detection
        MeshCollider temp_local_Convex_Trigger_MeshCollider = this.gameObject.AddComponent<MeshCollider>();
         
        // Convex 
            temp_local_Convex_Trigger_MeshCollider.convex = true;
        
        // Trigger 
            temp_local_Convex_Trigger_MeshCollider.isTrigger = true;

        #endregion

        #region Mesh Collider 2 - Actual Cell Mesh
        // Mesh Collider
        MeshCollider temp_local_cell_MeshCollider = this.gameObject.AddComponent<MeshCollider>(); /* Alias */ cellCollider = temp_local_cell_MeshCollider;
        
        // this is the mesh used for cells - we will need to figure out ways to render it more
        // effectivily the way I desire but for now this will do, reading this collider's mesh
        // data might be a nightmare to do later also. 

        #endregion

        #region NavMeshModifier
        
        UnityEngine.AI.NavMeshModifier modifier = this.gameObject.AddComponent<UnityEngine.AI.NavMeshModifier>();
        modifier.overrideArea = true;
        modifier.area = 1; // Not Walkable
        
        // Use reflection to set m_AffectedAgents (no public setter)
        System.Reflection.FieldInfo field = typeof(UnityEngine.AI.NavMeshModifier).GetField("m_AffectedAgents", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            // -1372625422 is the Agent Type ID for Ship
            field.SetValue(modifier, new System.Collections.Generic.List<int>() { -1372625422 });
        }

        #endregion
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
    public void InitializeTerrain()
    {
        GenerateTerrain();
    }

    // Grid Type 
    public GridType.Type currentGridType;
    private void GenerateIslandTerrain()
    {
        // Implementation of island terrain generation
    }

    private void GeneratePlateauTerrain()
    {
        // Implementation of plateau terrain generation
    }

    private void GenerateOceanTerrain()
    {
        // Implementation of ocean terrain generation
    }

    // For Generating the Terrain of a Island  -  incl. Falloff Edges 
    private void GenerateTerrain()
    {
        grid = new Cell[size, size];
        float[,] noiseMap = GenerateNoiseMap();
        float[,] falloffMap = GenerateFalloffMap();


        // Check the current grid type and generate terrain accordingly
        switch (this.currentGridType)
        {
            case GridType.Type.Island:
                GenerateIslandTerrain();
                break;
            case GridType.Type.Plateau:
                GeneratePlateauTerrain();
                break;
            case GridType.Type.Ocean:
                GenerateOceanTerrain();
                break;
            case GridType.Type.Empty:
                // Possibly clear the area or keep it untouched
                break;
        }




        // Debug.Log(currentGridType); // Prints as Plateau

        // Use the current grid type in the switch statement
        switch (currentGridType)
        {
            case GridType.Type.Island:
                // Code for generating island terrain
                // Populate grid
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float noiseValue = noiseMap[x, y] - falloffMap[x, y]; // Remember this is a Local Variable
                        TerrainType terrainType; // Remember this is also a Local Variable

                        // Check against new thresholds to determine terrain type
                        if (noiseValue < abyssThreshold) // 0
                        {
                            terrainType = TerrainType.Abyssal;
                        }
                        else if (noiseValue < deepSeaThreshold) // 0.1
                        {
                            terrainType = TerrainType.Deep;
                        }
                        else if (noiseValue < plateauThreshold) // 0.2 - Does not work for some reason
                        {
                            terrainType = TerrainType.Plateau;
                        }
                        else if (noiseValue < shallowWaterThreshold) // 0.3
                        {
                            terrainType = TerrainType.Shallow;
                        }
                        //else if (noiseValue < CoastThreshold) // 0.35 - Does not work for some reason
                        //{
                        //    terrainType = TerrainType.Coast; 
                        //}
                        else if (noiseValue < waterLevel) // 0.4
                        {
                            terrainType = TerrainType.Water;
                        }
                        else if (noiseValue > mountainThreshold) // 0.8
                        {
                            terrainType = DetermineMountainHeight(noiseValue);
                        }
                        else
                        {
                            terrainType = TerrainType.Land; // 0.6
                        }

                        float height = GetHeightForTerrainType(terrainType);
                        Vector3 position = new Vector3(x, height, y);
                        grid[x, y] = new Cell(position, null, terrainType);
                    }
                }
                break;

            case GridType.Type.Plateau:
                // Code for generating plateau terrain
                // Populate grid
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float noiseValue = noiseMap[x, y] - falloffMap[x, y];
                        TerrainType terrainType;

                        // Check against new thresholds to determine terrain type
                        if (noiseValue < abyssThreshold) // 0
                        {
                            terrainType = TerrainType.Abyssal;
                        }
                        else if (noiseValue < deepSeaThreshold) // 0.1
                        {
                            terrainType = TerrainType.Deep;
                        }
                        else
                        {
                            terrainType = TerrainType.Plateau; // 0.2
                        }

                        float height = GetHeightForTerrainType(terrainType);
                        Vector3 position = new Vector3(x, height, y);
                        grid[x, y] = new Cell(position, null, terrainType);
                    }
                }
                break;

            case GridType.Type.Ocean: // break; // Code for generating ocean 
            case GridType.Type.Empty:
            default:
                // Code for default case or ocean or empty terrain
                // Populate grid
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float noiseValue = noiseMap[x, y] - falloffMap[x, y];
                        TerrainType terrainType;

                        // Check against new thresholds to determine terrain type
                        if (noiseValue < abyssThreshold) // 0
                        {
                            terrainType = TerrainType.Abyssal;
                        }
                        else
                        {
                            terrainType = TerrainType.Deep; // 0.1
                        }

                        float height = GetHeightForTerrainType(terrainType);
                        Vector3 position = new Vector3(x, height, y);
                        grid[x, y] = new Cell(position, null, terrainType);
                    }
                }
                // IMPORTANT: Assumption, your a smart person who won't spawn anything empty :) rit?
                // MIGHT CAUSE UN EXPECTED RESULTS -> MIGHT CAUSE GLITCH / ERROR
                break;
        }

        // Universal Terrain Application Change

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
        int landCount = 0, waterCount = 0, oceanCount = 0, beachCount = 0;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (grid[x, y].currentTerrainType == TerrainType.Land) landCount++;
                if (grid[x, y].currentTerrainType == TerrainType.Beach) beachCount++;
                if (grid[x, y].currentTerrainType == TerrainType.Water) waterCount++;
                if (grid[x, y].currentTerrainType == TerrainType.Ocean) oceanCount++;
            }
        }

        Log($"Land cells: {landCount}, Water cells: {waterCount}, Beach cells: {beachCount} ");
        Log($"Total cells {landCount + waterCount + beachCount},  Size� {size * size}");


        // Populate neighbors
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Debug.Log("Populating neighbors for cell at (" + x + ", " + y + ")...");
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
        // Grid is Populated!

        // Further Grid Changes
        switch (currentGridType)
        {
            case GridType.Type.Island:
                // Code for generating island terrain

                // Time To Shape Edges
                ApplyBeachEdges();
                GenerateMountains();

                break;

            case GridType.Type.Plateau:
                // Code for generating plateau terrain
                ApplyBeachEdges();

                break;

            case GridType.Type.Ocean:
                // Code for generating ocean terrain
                break;

            case GridType.Type.Empty:
            default:
                // Code for default case or empty terrain
                break;
        }

        // Final Grid Application Procedure
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
        (float xOffset, float yOffset) = (UnityEngine.Random.Range(-10000f, 10000f), UnityEngine.Random.Range(-10000f, 10000f)); 
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
        int size = grid.GetLength(0);
        float[,] falloffMap = new float[size, size];
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                float x = i / (float)size * 2 - 1;
                float y = j / (float)size * 2 - 1;
                float value = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                falloffMap[i, j] = Evaluate(value);
            }
        }
        return falloffMap;
    }

    private float Evaluate(float value)
    {
        float a = 3;
        float b = 2.2f;
        return Mathf.Pow(value, a) / (Mathf.Pow(value, a) + Mathf.Pow(b - b * value, a));
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

                        // Check if the neighbor is water
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
                GenerateMountain(x, y, mountainHeight); // Empty Placeholder Method

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
                    GenerateMountain(x, y, mountainHeight); // Empty Placeholder Method
                    mountainCount++;
                }

                attempts++;
            }
        }
    }

    // For some reason this affects the neighbour algorithm
    // Values above +1 will cause the algorithm to not work
    // Values below -1 will cause the algorithm to not work
    // Why? I don't know - it just interferes directionally

    // Explainer:

    // BECAUSE THIS FUCKING METHOD IS A 2D SCALE IT WILL AFFECT THE DIRECTIONS
    // OF THE NEIGHBOURS ALONG THE X AND Y AXIS AND IT WILL CHANGE THE INDEXES
    // USED FOR ALL THE 2D DIRECTIONS OF THE NEIGHBOURS ALONG THE X AND Y AXIS. 
    
    // LATER ANOTHER HEIGHT METHOD WILL BE USED TO DETERMINE THE HEIGHT OF
    // THE EDGES, IT WILL BE USED FOR EVERY TYPE OF TERRAIN LIKE MOUNTAINS
    // BEACHES, WATER, SHORES, ETC.

    // I need to clarify this bc I keep forgetting it after a few months... :-(�_�

    // This method determines the specific mountain terrain type based on the noise value
    public float GetHeightForTerrainType(Cell.TerrainType type)
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
            case TerrainType.Shallow:
            case TerrainType.Plateau:
            case TerrainType.Abyssal:
                return -1f; // Water level, below land
                /*
                    return -4f; // Abyssal level
                    return -2f; // Shallow water level
                    return -3f; // Plateau level
                */
            default:
                return 0f;
        }
    }

    private void GenerateMountain(int x, int y, int height)
    {
        // Recursive function to generate a mountain
        // ... (mountain generation logic)

        // its fucking empty but somehow it generates mountains
        
        // Flag that the mountains have been generated
        hasMountainsGenerated = true;
    }

    #endregion

    #region Edge Section

    private void BuildMeshesAndTextures()
    {
        // Builds and assign Terrain
        TerrainMeshBuilder terrainMeshBuilder = new TerrainMeshBuilder(grid); // Does Works properly
        Mesh terrainMesh = terrainMeshBuilder.Build();
        ApplyTerrainMesh(terrainMesh);

        // Build and apply Texture
        TextureBuilder textureBuilder = new TextureBuilder(grid); // Does Work properly
        Texture2D texture = textureBuilder.Build();
        ApplyTexture(texture);

        // Build and add Edges
        EdgeMeshBuilder edgeMeshBuilder = new EdgeMeshBuilder(grid); // Does Work properly :D
        
        // Create Edge Object
        GameObject _edgeObj = new GameObject("Edge"); // islandGO.transform.position = island.bounds.center

        // Set MapGrid GO as the parent of _edgeObj by this GameObject's transform
        _edgeObj.transform.SetParent(transform);

        // Set the local position to zero to make it follow exactly the parent's position
        _edgeObj.transform.position = transform.position; // Vector3.zero;

        // Set a transform target
        Transform transform_target = _edgeObj.transform;

        // This is long string is the return type
        (Mesh coastMesh, Mesh oceanMesh, Mesh mountainMesh, Mesh beachMesh, Mesh shallowMesh, Mesh deepMesh, Mesh plateauMesh, Mesh abyssalMesh) = edgeMeshBuilder.Build(); // 2023: Receive "four" meshes - 2024: Now its "eight"

        // Apply the mountain edge mesh
        if (hasMountainsGenerated && HasGeometry(mountainMesh)) ApplyMountainEdgeMesh(mountainMesh, transform_target);

        // Apply the normal edge meshes ( Order of execution probably does not matter )
        if (HasGeometry(deepMesh)) ApplyDeepEdgeMesh(deepMesh, transform_target);            // Apply the deep edge mesh
        if (HasGeometry(coastMesh)) ApplyCoastEdgeMesh(coastMesh, transform_target);         // Apply the coast edge mesh
        if (HasGeometry(oceanMesh)) ApplyOceanEdgeMesh(oceanMesh, transform_target);         // Apply the ocean edge mesh
        if (HasGeometry(beachMesh)) ApplyBeachEdgeMesh(beachMesh, transform_target);         // Apply the beach edge mesh
        if (HasGeometry(shallowMesh)) ApplyShallowEdgeMesh(shallowMesh, transform_target);   // Apply the shallow edge mesh
        if (HasGeometry(plateauMesh)) ApplyPlateauEdgeMesh(plateauMesh, transform_target);   // Apply the plateau edge mesh
        if (HasGeometry(abyssalMesh)) ApplyAbyssalEdgeMesh(abyssalMesh, transform_target);   // Apply the abyssal edge mesh


        // Responsible for setting the final Edge position
        ResetChildPositions(_edgeObj);
    }

    #endregion

    /// <summary>
    /// True when the builder actually emitted geometry for this edge type.
    /// EdgeMeshBuilder always returns all eight meshes, but an island only has the
    /// edge types its terrain produced, so the rest come back with zero vertices.
    /// Handing one of those to a MeshFilter makes the NavMesh builder reject it with
    /// "Source mesh has invalid vertex data" - once per empty mesh, per island.
    /// </summary>
    private static bool HasGeometry(Mesh mesh) => mesh != null && mesh.vertexCount > 0;

    #region Set Child Position

    private void ResetChildPositions(GameObject edgeObject)
    {
        // This assumes that the Island GameObject is at the root level and 'Edge' is its direct child.
        Transform islandTransform = edgeObject.transform.parent;
        if (islandTransform == null)
        {
            Debug.LogError("Edge GameObject does not have a parent.");
            return;
        }

        // Get the world position of the Island GameObject
        Vector3 islandPosition = islandTransform.position; 
        
        // Calculate the offset
        Vector3 offset = islandTransform.TransformPoint(Vector3.zero) - edgeObject.transform.TransformPoint(Vector3.zero); 

        // Now loop through each child of the Edge GameObject and set their position
        foreach (Transform child in edgeObject.transform)
        {
            // Set the child's world position with offset
            child.position = islandPosition + offset;
        }
    }

    private void ResetChildOffsetPosition(GameObject edgeObject)
    {
        MapManager mapManager = FindObjectOfType<MapManager>();
        if (mapManager == null)
        {
            Debug.LogError("MapManager component not found in the scene.");
            return;
        }

        Vector3 offset = new Vector3(mapManager.xOffset, 0, mapManager.zOffset);

        // Assuming the Island GameObject is the parent of the Edge GameObject
        Transform islandTransform = edgeObject.transform.parent;
        if (islandTransform == null)
        {
            Debug.LogError("Edge GameObject does not have a parent.");
            return;
        }

        Vector3 globalPosition = islandTransform.position; // Global position of the Island
        edgeObject.transform.position = globalPosition; // Set Edge to the same global position

        foreach (Transform child in edgeObject.transform)
        {
            // Apply offset in local space, which will translate to a relative global position
            child.localPosition = offset;
        }
    }


    #endregion 

    #region Edge Methods

    // Beach Edge
    private void ApplyBeachEdgeMesh(Mesh beachMesh, Transform parentObject)
    {
        GameObject beachEdgeObj = new GameObject("BeachEdge");
        beachEdgeObj.transform.SetParent(parentObject);

        MeshFilter beachMeshFilter = beachEdgeObj.AddComponent<MeshFilter>() ?? beachEdgeObj.AddComponent<MeshFilter>();
        beachMeshFilter.mesh = beachMesh;

        MeshRenderer beachMeshRenderer = beachEdgeObj.AddComponent<MeshRenderer>() ?? beachEdgeObj.AddComponent<MeshRenderer>();
        beachMeshRenderer.material = beachEdgeMaterial;
    }

    // Mountain Edge
    private void ApplyMountainEdgeMesh(Mesh mountainMesh, Transform parentObject)
    {
        GameObject mountainEdgeObj = new GameObject("MountainEdge");
        mountainEdgeObj.transform.SetParent(parentObject);

        MeshFilter mountainMeshFilter = mountainEdgeObj.AddComponent<MeshFilter>() ?? mountainEdgeObj.AddComponent<MeshFilter>();
        mountainMeshFilter.mesh = mountainMesh;

        MeshRenderer mountainMeshRenderer = mountainEdgeObj.AddComponent<MeshRenderer>() ?? mountainEdgeObj.AddComponent<MeshRenderer>();
        mountainMeshRenderer.material = mountainEdgeMaterial; 
    }


    // Ocean Edge
    private void ApplyOceanEdgeMesh(Mesh oceanMesh, Transform parentObject)
    {
        GameObject oceanEdgeObj = new GameObject("OceanEdge");
        oceanEdgeObj.transform.SetParent(parentObject);

        // Setting up MeshFilter
        MeshFilter oceanMeshFilter = oceanEdgeObj.AddComponent<MeshFilter>() ?? oceanEdgeObj.AddComponent<MeshFilter>();
        oceanMeshFilter.mesh = oceanMesh;

        // Setting up MeshRenderer
        MeshRenderer oceanMeshRenderer = oceanEdgeObj.AddComponent<MeshRenderer>() ?? oceanEdgeObj.AddComponent<MeshRenderer>();
        oceanMeshRenderer.material = oceanEdgeMaterial;

        //// Adding and setting up MeshCollider
        //MeshCollider oceanMeshCollider = oceanEdgeObj.AddComponent<MeshCollider>() ?? oceanEdgeObj.AddComponent<MeshCollider>();
        //oceanMeshCollider.sharedMesh = oceanMesh;
        
        //// Apply the defined cooking options
        //oceanMeshCollider.cookingOptions = cookingOptions;

        //// Async Bake the Island onto the ocean
        //if (ref_navMeshGameManager != null) ref_navMeshGameManager.BakeNavMeshAsync(); 
        // else Debug.LogError("No NavMeshBuilder Ref"); 
    }

    // Coast Edge
    private void ApplyCoastEdgeMesh(Mesh coastMesh, Transform parentObject)
    {
        GameObject coastEdgeObj = new GameObject("CoastEdge");
        coastEdgeObj.transform.SetParent(parentObject);

        MeshFilter coastMeshFilter = coastEdgeObj.AddComponent<MeshFilter>() ?? coastEdgeObj.AddComponent<MeshFilter>();
        coastMeshFilter.mesh = coastMesh;

        MeshRenderer coastMeshRenderer = coastEdgeObj.AddComponent<MeshRenderer>() ?? coastEdgeObj.AddComponent<MeshRenderer>();
        coastMeshRenderer.material = beachEdgeMaterial; //coastEdgeMaterial;
    }

    // Shallow Edge
    private void ApplyShallowEdgeMesh(Mesh shallowMesh, Transform parentObject)
    {
        GameObject shallowEdgeObj = new GameObject("ShallowEdge");
        shallowEdgeObj.transform.SetParent(parentObject);

        MeshFilter shallowMeshFilter = shallowEdgeObj.AddComponent<MeshFilter>() ?? shallowEdgeObj.AddComponent<MeshFilter>();
        shallowMeshFilter.mesh = shallowMesh;

        MeshRenderer shallowMeshRenderer = shallowEdgeObj.AddComponent<MeshRenderer>() ?? shallowEdgeObj.AddComponent<MeshRenderer>();
        shallowMeshRenderer.material = shallowEdgeMaterial;

    }

    // Deep Edge
    private void ApplyDeepEdgeMesh(Mesh deepMesh, Transform parentObject)
    {
        GameObject deepEdgeObj = new GameObject("DeepEdge");
        deepEdgeObj.transform.SetParent(parentObject);

        MeshFilter deepMeshFilter = deepEdgeObj.AddComponent<MeshFilter>() ?? deepEdgeObj.AddComponent<MeshFilter>();
        deepMeshFilter.mesh = deepMesh;

        MeshRenderer deepMeshRenderer = deepEdgeObj.AddComponent<MeshRenderer>() ?? deepEdgeObj.AddComponent<MeshRenderer>();
        deepMeshRenderer.material = deepSeaEdgeMaterial;

    }

    // Plateau Edge
    private void ApplyPlateauEdgeMesh(Mesh plateauMesh, Transform parentObject)
    {
        GameObject plateauEdgeObj = new GameObject("PlateauEdge");
        plateauEdgeObj.transform.SetParent(parentObject);

        MeshFilter plateauMeshFilter = plateauEdgeObj.AddComponent<MeshFilter>() ?? plateauEdgeObj.AddComponent<MeshFilter>();
        plateauMeshFilter.mesh = plateauMesh;

        MeshRenderer plateauMeshRenderer = plateauEdgeObj.AddComponent<MeshRenderer>() ?? plateauEdgeObj.AddComponent<MeshRenderer>();
        plateauMeshRenderer.material = plateauEdgeMaterial;

    }

    // Abyssal Edge
    private void ApplyAbyssalEdgeMesh(Mesh abyssalMesh, Transform parentObject)
    {
        GameObject abyssalEdgeObj = new GameObject("AbyssalEdge");
        abyssalEdgeObj.transform.SetParent(parentObject);

        MeshFilter abyssalMeshFilter = abyssalEdgeObj.AddComponent<MeshFilter>() ?? abyssalEdgeObj.AddComponent<MeshFilter>();
        abyssalMeshFilter.mesh = abyssalMesh;

        MeshRenderer abyssalMeshRenderer = abyssalEdgeObj.AddComponent<MeshRenderer>() ?? abyssalEdgeObj.AddComponent<MeshRenderer>();
        abyssalMeshRenderer.material = abyssalEdgeMaterial;
    }


    #endregion

    #region Apply Refactors 
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

    private void ApplyEdgeMesh(Mesh edgeMesh, Material edgeMaterial)
    {
        // Default Material
        if (edgeMaterial != null) edgeMaterial = beachEdgeMaterial;

        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = edgeMesh;

        MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        if (meshRenderer != null) meshRenderer.material = edgeMaterial; // ApplyEdgeMaterial(determineEdgeMaterial(grid[x, y,]));
    }

    private Material determineEdgeMaterial(Cell.TerrainType terrainType)
    {
        // TODO: Determine edge material

        switch (terrainType)
        {
            case Cell.TerrainType.Mountain: 
                return mountainEdgeMaterial;

            default:
            case Cell.TerrainType.Beach:
                return beachEdgeMaterial;

        }
    }
    #endregion

}
