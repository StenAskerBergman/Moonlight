using UnityEngine;
using System.Collections.Generic;
using static Cell;
using System.Linq;
using UnityEngine.AI;
using UnityEngine.UIElements;

[System.Serializable]
public sealed class TerrainGenerationProfile
{
    public string chunkName;
    public string gridType;
    public int gridSize;
    public int visualSamplesPerCell;
    public bool completed;
    public long totalMs;
    public long featureReservationsMs;
    public long samplingCacheMs;
    public long gameplayGridAndMetricsMs;
    public long meshBuildMs;
    public long meshUploadMs;
    public long textureSplatMs;
    public long textureUploadMs;
    public long foliageMs;
}

public class MapGrid : MonoBehaviour
{
    private static readonly int TerrainBaseMapProperty = Shader.PropertyToID("_BaseMap");
    private const string GeneratedPlateauGeometryRootName = "Generated Plateau Geometry";


    #region Variables 


        // Edge Flags
        public bool hasMountainsGenerated = false; // Flag to track if mountains have been generated

        // Prefabs
        public GameObject[] treePrefabs;
        public ClimateProfile climateProfile;

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

        [Header("Semantic Terrain Generation")]
        public TerrainGenerationSettings generationSettings = new TerrainGenerationSettings();

        [Header("Diagnostics & Heatmap")]
        public TerrainDebugViewMode debugViewMode = TerrainDebugViewMode.Normal;

        /// <summary>
        /// Shared procedural source. The gameplay grid samples it at integer cell
        /// coordinates; a future denser visual mesh can sample the same source at
        /// fractional coordinates without replacing Cell[,] as gameplay authority.
        /// </summary>
        public IslandTerrainProvider TerrainSource { get; private set; }
        public TerrainGenerationProfile LastGenerationProfile { get; private set; }
        private int activeGenerationSeed;
            
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

        /// <summary>
        /// The generated cells, or null until InitializeTerrain() has run.
        /// Exposed so WaterNavMeshCarver can read the land/water footprint straight
        /// from the source data instead of re-deriving it from the built mesh.
        /// </summary>
        public Cell[,] Grid => grid;
        public int Size => size;

        public MeshCollider cellCollider;
        [System.NonSerialized] private HashSet<Object> generatedVisualResources;
        [System.NonSerialized] private bool isReleasingGeneratedVisuals;
    #endregion

    #region Start Method
    void Start()
    {
        #region Island Detection Trigger Collider
        // Lightweight BoxCollider for chunk boundary detection. Prevents PhysX from attempting
        // convex hull computation on a 2.56M-vertex mesh, which locks Unity in EnterPlayMode.
        BoxCollider detectionBox = gameObject.AddComponent<BoxCollider>();
        detectionBox.isTrigger = true;
        detectionBox.center = new Vector3(size * 0.5f, 0f, size * 0.5f);
        detectionBox.size = new Vector3(size, 40f, size);
        #endregion

        #region Mesh Collider 2 - Actual Cell Mesh
        // Standard non-convex MeshCollider for cursor raycasts
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            MeshCollider temp_local_cell_MeshCollider = gameObject.AddComponent<MeshCollider>();
            temp_local_cell_MeshCollider.convex = false;
            cellCollider = temp_local_cell_MeshCollider;
        }
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

    // Populates the grid for an island: full threshold ladder from abyssal water up
    // through mountains, so islands get the full range of land/water terrain types.
    private void GenerateIslandTerrain(float[,] noiseMap, float[,] falloffMap)
    {
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
    }

    // Populates the grid for open ocean/empty grids: nothing but deep and abyssal
    // water, since there's no land tier to generate.
    private void GenerateOceanTerrain(float[,] noiseMap, float[,] falloffMap)
    {
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
    }

    private long lastReservationsMs;
    private long lastSamplingMs;

    // Semantic generation is intentionally a small orchestration seam: the source
    // class owns procedural composition/classification, while MapGrid owns Cell[,].
    private void GenerateTerrain()
    {
        System.Diagnostics.Stopwatch totalSw = System.Diagnostics.Stopwatch.StartNew();
        System.Diagnostics.Stopwatch stageSw = System.Diagnostics.Stopwatch.StartNew();

        LastGenerationProfile = new TerrainGenerationProfile
        {
            chunkName = gameObject.name,
            gridType = currentGridType.ToString(),
            gridSize = size,
            visualSamplesPerCell = generationSettings != null ? generationSettings.visualSamplesPerCell : 0,
            completed = false,
        };

        generationSettings ??= new TerrainGenerationSettings();
        generationSettings.EnforceAuthoritativeHeights();
        generationSettings.ApplyLegacyIslandTuning(
            scale,
            abyssThreshold,
            deepSeaThreshold,
            plateauThreshold,
            shallowWaterThreshold,
            waterLevel,
            mountainThreshold);
        activeGenerationSeed = ResolveGenerationSeed();
        int worldSeed = generationSettings.seed;
        Vector2 chunkWorldOrigin = new Vector2(transform.position.x, transform.position.z);
        
        GenerationWatchdog.SetPhase(gameObject.name, "Feature Reservations");
        TerrainSource = new IslandTerrainProvider(generationSettings, currentGridType, size, activeGenerationSeed, worldSeed, chunkWorldOrigin);
        lastReservationsMs = stageSw.ElapsedMilliseconds;
        LastGenerationProfile.featureReservationsMs = lastReservationsMs;

        stageSw.Restart();
        GenerationWatchdog.SetPhase(gameObject.name, "Terrain Sampling Cache");
        TerrainSample[,] samples = TerrainSource.GenerateGameplaySamples();
        lastSamplingMs = stageSw.ElapsedMilliseconds;
        LastGenerationProfile.samplingCacheMs = lastSamplingMs;

        GenerationWatchdog.SetPhase(gameObject.name, "Gameplay Grid & Metrics");
        stageSw.Restart();
        grid = new Cell[size, size];
        hasMountainsGenerated = false;

        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                TerrainSample sample = samples[x, z];
                grid[x, z] = new Cell(new Vector3(x, sample.Height, z), null, sample.TerrainType);
                float plateauBuildability = sample.PlateauData.IsDefined
                    ? sample.PlateauData.BuildableWeight
                    : sample.PlateauInfluence;
                grid[x, z].SetDeliberatePlateauBuildability(plateauBuildability);

                if (sample.TerrainType == TerrainType.Hill
                    || sample.TerrainType == TerrainType.Cliff
                    || sample.TerrainType == TerrainType.Mountain
                    || sample.TerrainType == TerrainType.MountainPeak)
                {
                    hasMountainsGenerated = true;
                }
            }
        }

        PopulateNeighborsAndTerrainMetrics();

        if (currentGridType == GridType.Type.Island || currentGridType == GridType.Type.Plateau)
        {
            MarkDepositCells();
        }

        LastGenerationProfile.gameplayGridAndMetricsMs = stageSw.ElapsedMilliseconds;

        BuildMeshesAndTextures();

        totalSw.Stop();
        LastGenerationProfile.totalMs = totalSw.ElapsedMilliseconds;
        LastGenerationProfile.completed = true;
    }

    private int ResolveGenerationSeed()
    {
        int seed = generationSettings.seed;
        Island island = GetComponent<Island>();

        if (island != null)
        {
            seed = unchecked(seed * 397 ^ island.id);
        }

        return seed;
    }

    private void PopulateNeighborsAndTerrainMetrics()
    {
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                grid[x, z].UpdateNeighbors(grid, size);
            }
        }

        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                Cell cell = grid[x, z];
                float maxVariance = 0f;

                foreach (Cell neighbor in cell.neighbors)
                {
                    maxVariance = Mathf.Max(maxVariance, Mathf.Abs(cell.height - neighbor.height));
                }

                cell.SetTerrainMetrics(maxVariance, generationSettings.maxBuildableHeightVariance);
            }
        }
    }

    // Retained temporarily for prefab-data migration/reference. Runtime generation
    // now goes through IslandTerrainProvider above.
    private void GenerateLegacyTerrain()
    {
        grid = new Cell[size, size];
        float[,] noiseMap = GenerateNoiseMap();
        float[,] falloffMap = GenerateFalloffMap();

        // Check the current grid type and generate terrain accordingly
        switch (this.currentGridType)
        {
            case GridType.Type.Island:
                GenerateIslandTerrain(noiseMap, falloffMap);
                break;
            case GridType.Type.Plateau:
                throw new System.NotSupportedException(
                    "Legacy plateau generation was removed; use IslandTerrainProvider.");
            case GridType.Type.Ocean:
            case GridType.Type.Empty:
            default:
                // Empty grids get the same deep/abyssal fill as ocean - there's no
                // "clear" terrain type, and every other system expects a populated grid.
                GenerateOceanTerrain(noiseMap, falloffMap);
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
        Log($"Total cells {landCount + waterCount + beachCount},  Size {size * size}");


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
                ApplyBeachEdges();
                GenerateMountains();
                MarkDepositCells();
                break;

            case GridType.Type.Plateau:
                ApplyBeachEdges();
                MarkDepositCells();
                break;

            case GridType.Type.Ocean:
            case GridType.Type.Empty:
            default:
                break;
        }

        // Final Grid Application Procedure
        BuildMeshesAndTextures();
    }

    #endregion

    #region DetermineMountainHeight Method

    private TerrainType DetermineMountainHeight(float noiseValue)
    {
        if (noiseValue > 1.1f) 
            return TerrainType.MountainPeak;
        else if (noiseValue > 0.5f)
            return TerrainType.Mountain;
        else
            return TerrainType.Land;
    }

    public float GetHeightForTerrainType(Cell.TerrainType type)
    {
        switch (type)
        {
            case TerrainType.MountainPeak:
                return 1f;
            case TerrainType.Mountain:
                return 1f;
            case TerrainType.Land:
                return 0f;
            case TerrainType.Beach:
            case TerrainType.Shore:
                return -0.5f;
            case TerrainType.Water:
            case TerrainType.Deep:
            case TerrainType.Shallow:
            case TerrainType.Plateau:
            case TerrainType.Abyssal:
                return -1f;
            default:
                return 0f;
        }
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

    private void ApplyBeachEdges()
    {
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Cell cell = grid[x, y];
                if (cell.currentTerrainType == TerrainType.Land)
                {
                    foreach (Vector2Int direction in new Vector2Int[] { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) })
                    {
                        int newX = x + direction.x;
                        int newY = y + direction.y;

                        if (newX >= 0 && newX < size && newY >= 0 && newY < size)
                        {
                            Cell neighbor = grid[newX, newY];
                            if (neighbor.currentTerrainType == TerrainType.Water)
                            {
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

    #region Deposit Marking Methods

    private void MarkDepositCells()
    {
        FeatureReservationMap reservations = TerrainSource?.Reservations;
        new RiverArea().GenerateRiver(grid, reservations, new System.Random(unchecked(activeGenerationSeed ^ 0x5F3759DF)));

        // 1. Mines: Discrete mountain nodes on mountain/mainland boundary with usable flat ground access
        List<Vector2Int> mineSpots = new List<Vector2Int>();
        if (reservations != null && reservations.MineAnchors.Count > 0)
        {
            foreach (var anchor in reservations.MineAnchors)
            {
                int ax = Mathf.Clamp(Mathf.RoundToInt(anchor.Position.x), 1, size - 2);
                int az = Mathf.Clamp(Mathf.RoundToInt(anchor.Position.y), 1, size - 2);
                for (int dx = -1; dx <= 1 && mineSpots.Count < 3; dx++)
                {
                    for (int dz = -1; dz <= 1 && mineSpots.Count < 3; dz++)
                    {
                        int cx = ax + dx;
                        int cz = az + dz;
                        if (cx < 1 || cx >= size - 1 || cz < 1 || cz >= size - 1) continue;
                        Cell c = grid[cx, cz];
                        if (c.currentTerrainType == TerrainType.Mountain || c.currentTerrainType == TerrainType.MountainPeak || c.currentTerrainType == TerrainType.Cliff)
                        {
                            bool tooClose = false;
                            foreach (var s in mineSpots)
                            {
                                if (Vector2Int.Distance(new Vector2Int(cx, cz), s) < 8f) { tooClose = true; break; }
                            }
                            if (!tooClose)
                            {
                                mineSpots.Add(new Vector2Int(cx, cz));
                                c.SetDeposit(ResourceNodeType.Mine);
                                break;
                            }
                        }
                    }
                }
            }
        }

        if (mineSpots.Count < 2)
        {
            for (int y = 2; y < size - 2 && mineSpots.Count < 3; y++)
            {
                for (int x = 2; x < size - 2 && mineSpots.Count < 3; x++)
                {
                    Cell cell = grid[x, y];
                    if (cell.currentTerrainType == TerrainType.Mountain || cell.currentTerrainType == TerrainType.MountainPeak || cell.currentTerrainType == TerrainType.Cliff)
                    {
                        bool hasLandNeighbor = false;
                        foreach (var dir in new Vector2Int[] { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) })
                        {
                            Cell n = grid[x + dir.x, y + dir.y];
                            if (n.currentTerrainType == TerrainType.Land)
                            {
                                hasLandNeighbor = true;
                                break;
                            }
                        }

                        if (hasLandNeighbor)
                        {
                            bool tooClose = false;
                            foreach (var s in mineSpots)
                            {
                                if (Vector2Int.Distance(new Vector2Int(x, y), s) < 8f) { tooClose = true; break; }
                            }
                            if (!tooClose)
                            {
                                mineSpots.Add(new Vector2Int(x, y));
                                cell.SetDeposit(ResourceNodeType.Mine);
                            }
                        }
                    }
                }
            }
        }

        // 2. Coastal Fishery: 3-5 discrete spaced fishing grounds along suitable coast
        List<Vector2Int> fishSpots = new List<Vector2Int>();
        System.Random rng = new System.Random(unchecked(activeGenerationSeed ^ 0x27D4EB2D));
        List<Vector2Int> beachCandidates = new List<Vector2Int>();
        for (int y = 1; y < size - 1; y++)
        {
            for (int x = 1; x < size - 1; x++)
            {
                Cell cell = grid[x, y];
                if (cell.currentTerrainType == TerrainType.Beach && IsAdjacentToWater(x, y))
                {
                    beachCandidates.Add(new Vector2Int(x, y));
                }
            }
        }

        for (int i = beachCandidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var temp = beachCandidates[i];
            beachCandidates[i] = beachCandidates[j];
            beachCandidates[j] = temp;
        }

        for (int i = 0; i < beachCandidates.Count && fishSpots.Count < 4; i++)
        {
            Vector2Int pt = beachCandidates[i];
            bool tooClose = false;
            foreach (var f in fishSpots)
            {
                if (Vector2Int.Distance(pt, f) < 8f) { tooClose = true; break; }
            }
            if (!tooClose)
            {
                fishSpots.Add(pt);
                grid[pt.x, pt.y].SetDeposit(ResourceNodeType.CoastalFishery);
            }
        }

        // 3. Underwater Plateaus ONLY: Hydrothermal Vents and Seabed Ore
        List<Vector2Int> ventSpots = new List<Vector2Int>();
        List<Vector2Int> oreSpots = new List<Vector2Int>();

        for (int y = 2; y < size - 2; y++)
        {
            for (int x = 2; x < size - 2; x++)
            {
                Cell cell = grid[x, y];
                if (cell.IsBuildableUnderwaterPlateau)
                {
                    if (ventSpots.Count < 2 && IsSparseDepositCell(x, y))
                    {
                        bool tooClose = false;
                        foreach (var v in ventSpots)
                        {
                            if (Vector2Int.Distance(new Vector2Int(x, y), v) < 10f) { tooClose = true; break; }
                        }
                        if (!tooClose)
                        {
                            ventSpots.Add(new Vector2Int(x, y));
                            cell.SetDeposit(ResourceNodeType.HydrothermalVent);
                            continue;
                        }
                    }

                    if (oreSpots.Count < 3 && IsSparseDepositCell(x + 13, y + 7))
                    {
                        bool tooClose = false;
                        foreach (var o in oreSpots)
                        {
                            if (Vector2Int.Distance(new Vector2Int(x, y), o) < 8f) { tooClose = true; break; }
                        }
                        if (!tooClose)
                        {
                            oreSpots.Add(new Vector2Int(x, y));
                            cell.SetDeposit(ResourceNodeType.OreSeabed);
                        }
                    }
                }
            }
        }

        // 5. Forest Grove: 1-2 discrete interior fertile groves
        List<Vector2Int> forestSpots = new List<Vector2Int>();
        for (int y = 5; y < size - 5 && forestSpots.Count < 2; y += 3)
        {
            for (int x = 5; x < size - 5 && forestSpots.Count < 2; x += 3)
            {
                Cell cell = grid[x, y];
                if (cell.currentTerrainType == TerrainType.Land && cell.IsSlopeSuitableForBuilding && !cell.isDeposit && cell.riverStatus == Cell.RiverStatus.None)
                {
                    bool tooClose = false;
                    foreach (var f in forestSpots)
                    {
                        if (Vector2Int.Distance(new Vector2Int(x, y), f) < 12f) { tooClose = true; break; }
                    }
                    if (!tooClose)
                    {
                        forestSpots.Add(new Vector2Int(x, y));
                        cell.SetDeposit(ResourceNodeType.ForestGrove);
                    }
                }
            }
        }
    }

    private bool IsAdjacentToWater(int x, int y)
    {
        foreach (Vector2Int direction in new Vector2Int[] { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) })
        {
            int nx = x + direction.x;
            int ny = y + direction.y;
            if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
            Cell n = grid[nx, ny];
            if (n.IsUnderwater || n.currentTerrainType == TerrainType.Water || n.currentTerrainType == TerrainType.Shallow) return true;
        }
        return false;
    }

    private bool IsSparseDepositCell(int x, int y)
    {
        unchecked
        {
            int hash = x * 73856093 ^ y * 19349663;
            hash ^= activeGenerationSeed;
            hash ^= hash >> 13;
            return Mathf.Abs(hash % 100) < 15;
        }
    }

    #endregion

    #region Mountain Section

    [Space(10)]
    public int minMountains = 1;
    public int maxMountains = 3;
    public int minMountainHeight = 3;
    public int maxMountainHeight = 10;

    private void GenerateMountains()
    {
        bool atLeastOneMountainGenerated = false;
        int attempts = 0;
        int maxAttempts = size * size;

        while (!atLeastOneMountainGenerated && attempts < maxAttempts)
        {
            int x = Random.Range(0, size);
            int y = Random.Range(0, size);
            Cell startCell = grid[x, y];

            if (startCell.currentTerrainType == TerrainType.Land)
            {
                int mountainHeight = Random.Range(minMountainHeight, maxMountainHeight);
                GenerateMountain(x, y, mountainHeight);
                atLeastOneMountainGenerated = true;
            }

            attempts++;
        }

        if (atLeastOneMountainGenerated)
        {
            int mountainCount = 1;
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

    private void GenerateMountain(int x, int y, int height)
    {
        int radius = Mathf.Clamp(height / 2, 2, 6);
        float peakRadius = radius * 0.4f;

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;

                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                if (distance > radius) continue;

                Cell cell = grid[nx, ny];
                if (cell.currentTerrainType != TerrainType.Land) continue;

                cell.ChangeTerrainType(distance <= peakRadius ? TerrainType.MountainPeak : TerrainType.Mountain);
            }
        }

        hasMountainsGenerated = true;
    }

    #endregion

    #region Edge Section

    private void BuildMeshesAndTextures()
    {
        bool isOceanChunk = currentGridType == GridType.Type.Ocean || currentGridType == GridType.Type.Empty;
        int effectiveVisualSamples = isOceanChunk ? 1 : generationSettings.visualSamplesPerCell;
        bool useContinuousMesh = TerrainSource != null;
        TerrainGenerationProfile profile = LastGenerationProfile;

        // Builds and assign Terrain
        System.Diagnostics.Stopwatch stageSw = System.Diagnostics.Stopwatch.StartNew();
        GenerationWatchdog.SetPhase(gameObject.name, "Mesh Building");
        PlateauGeometryResult plateauGeometry = currentGridType == GridType.Type.Plateau
            && useContinuousMesh
            && generationSettings.standalonePlateau.generateVolumetricRockGeometry
                ? PlateauGeometryGenerator.Generate(
                    TerrainSource.GetOrCreateSampleCache(effectiveVisualSamples),
                    generationSettings.standalonePlateau,
                    activeGenerationSeed,
                    generationSettings.seed)
                : null;
        TerrainMeshBuilder terrainMeshBuilder = useContinuousMesh
            ? new TerrainMeshBuilder(grid, TerrainSource, effectiveVisualSamples)
            : new TerrainMeshBuilder(grid);
        Mesh terrainMesh = terrainMeshBuilder.Build();
        long meshBuildMs = stageSw.ElapsedMilliseconds;
        if (profile != null) profile.meshBuildMs = meshBuildMs;

        stageSw.Restart();
        GenerationWatchdog.SetPhase(gameObject.name, "Mesh Upload");
        TrackGeneratedVisualResource(terrainMesh);
        ApplyTerrainMesh(terrainMesh);
        ApplyPlateauGeometry(plateauGeometry);
        long meshUploadMs = stageSw.ElapsedMilliseconds;
        if (profile != null) profile.meshUploadMs = meshUploadMs;

        // Build and apply Texture
        stageSw.Restart();
        GenerationWatchdog.SetPhase(gameObject.name, "Texture Splat Generation");
        TextureBuilder textureBuilder = useContinuousMesh
            ? new TextureBuilder(grid, TerrainSource, effectiveVisualSamples, climateProfile, debugViewMode)
            : new TextureBuilder(grid, climateProfile, debugViewMode);
        Texture2D texture = textureBuilder.Build();
        texture.name = $"Generated Terrain Texture ({debugViewMode})";
        long textureBuildMs = stageSw.ElapsedMilliseconds;
        if (profile != null) profile.textureSplatMs = textureBuildMs;

        stageSw.Restart();
        GenerationWatchdog.SetPhase(gameObject.name, "Texture Upload");
        TrackGeneratedVisualResource(texture);
        ApplyTexture(texture);
        long textureUploadMs = stageSw.ElapsedMilliseconds;
        if (profile != null) profile.textureUploadMs = textureUploadMs;

        // Foliage Placer (Islands only)
        long foliageMs = 0;
        if (!isOceanChunk)
        {
            stageSw.Restart();
            GenerationWatchdog.SetPhase(gameObject.name, "Foliage Scattering");
            IslandFoliagePlacer foliagePlacer = GetComponent<IslandFoliagePlacer>();
            if (foliagePlacer != null) {
                foliagePlacer.climateProfile = climateProfile;
                foliagePlacer.ScatterFoliage(grid);
            }
            foliageMs = stageSw.ElapsedMilliseconds;
        }
        if (profile != null) profile.foliageMs = foliageMs;

        long gameplayMs = profile != null ? profile.gameplayGridAndMetricsMs : 0L;
        long totalTime = lastReservationsMs + lastSamplingMs + gameplayMs + meshBuildMs + meshUploadMs + textureBuildMs + textureUploadMs + foliageMs;
        Debug.Log($"<color=cyan>[Terrain Regeneration Profile - {gameObject.name}]</color> Total: <b>{totalTime} ms</b> | " +
            $"Reservations: {lastReservationsMs} ms | " +
            $"Sampling Cache: {lastSamplingMs} ms | " +
            $"Gameplay Grid & Metrics: {gameplayMs} ms | " +
            $"Mesh Vertices & Topology: {meshBuildMs} ms | " +
            $"Mesh Upload: {meshUploadMs} ms | " +
            $"Texture Splatting: {textureBuildMs} ms | " +
            $"Texture Upload: {textureUploadMs} ms | " +
            $"Foliage: {foliageMs} ms");

        if (useContinuousMesh) return;

        // Build and add Edges (Legacy/Debug path only)
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
        TrackGeneratedVisualResource(coastMesh);
        TrackGeneratedVisualResource(oceanMesh);
        TrackGeneratedVisualResource(mountainMesh);
        TrackGeneratedVisualResource(beachMesh);
        TrackGeneratedVisualResource(shallowMesh);
        TrackGeneratedVisualResource(deepMesh);
        TrackGeneratedVisualResource(plateauMesh);
        TrackGeneratedVisualResource(abyssalMesh);

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
        beachMeshFilter.sharedMesh = beachMesh;

        MeshRenderer beachMeshRenderer = beachEdgeObj.AddComponent<MeshRenderer>() ?? beachEdgeObj.AddComponent<MeshRenderer>();
        beachMeshRenderer.sharedMaterial = beachEdgeMaterial;
    }

    // Mountain Edge
    private void ApplyMountainEdgeMesh(Mesh mountainMesh, Transform parentObject)
    {
        GameObject mountainEdgeObj = new GameObject("MountainEdge");
        mountainEdgeObj.transform.SetParent(parentObject);

        MeshFilter mountainMeshFilter = mountainEdgeObj.AddComponent<MeshFilter>() ?? mountainEdgeObj.AddComponent<MeshFilter>();
        mountainMeshFilter.sharedMesh = mountainMesh;

        MeshRenderer mountainMeshRenderer = mountainEdgeObj.AddComponent<MeshRenderer>() ?? mountainEdgeObj.AddComponent<MeshRenderer>();
        mountainMeshRenderer.sharedMaterial = mountainEdgeMaterial;
    }


    // Ocean Edge
    private void ApplyOceanEdgeMesh(Mesh oceanMesh, Transform parentObject)
    {
        GameObject oceanEdgeObj = new GameObject("OceanEdge");
        oceanEdgeObj.transform.SetParent(parentObject);

        // Setting up MeshFilter
        MeshFilter oceanMeshFilter = oceanEdgeObj.AddComponent<MeshFilter>() ?? oceanEdgeObj.AddComponent<MeshFilter>();
        oceanMeshFilter.sharedMesh = oceanMesh;

        // Setting up MeshRenderer
        MeshRenderer oceanMeshRenderer = oceanEdgeObj.AddComponent<MeshRenderer>() ?? oceanEdgeObj.AddComponent<MeshRenderer>();
        oceanMeshRenderer.sharedMaterial = oceanEdgeMaterial;

        //// Adding and setting up MeshCollider
        //MeshCollider oceanMeshCollider = oceanEdgeObj.AddComponent<MeshCollider>() ?? oceanEdgeObj.AddComponent<MeshCollider>();
        //oceanMeshCollider.sharedMesh = oceanMesh;
        
        //// Apply the defined cooking options
        //oceanMeshCollider.cookingOptions = cookingOptions;

    }

    // Coast Edge
    private void ApplyCoastEdgeMesh(Mesh coastMesh, Transform parentObject)
    {
        GameObject coastEdgeObj = new GameObject("CoastEdge");
        coastEdgeObj.transform.SetParent(parentObject);

        MeshFilter coastMeshFilter = coastEdgeObj.AddComponent<MeshFilter>() ?? coastEdgeObj.AddComponent<MeshFilter>();
        coastMeshFilter.sharedMesh = coastMesh;

        MeshRenderer coastMeshRenderer = coastEdgeObj.AddComponent<MeshRenderer>() ?? coastEdgeObj.AddComponent<MeshRenderer>();
        coastMeshRenderer.sharedMaterial = beachEdgeMaterial; //coastEdgeMaterial;
    }

    // Shallow Edge
    private void ApplyShallowEdgeMesh(Mesh shallowMesh, Transform parentObject)
    {
        GameObject shallowEdgeObj = new GameObject("ShallowEdge");
        shallowEdgeObj.transform.SetParent(parentObject);

        MeshFilter shallowMeshFilter = shallowEdgeObj.AddComponent<MeshFilter>() ?? shallowEdgeObj.AddComponent<MeshFilter>();
        shallowMeshFilter.sharedMesh = shallowMesh;

        MeshRenderer shallowMeshRenderer = shallowEdgeObj.AddComponent<MeshRenderer>() ?? shallowEdgeObj.AddComponent<MeshRenderer>();
        shallowMeshRenderer.sharedMaterial = shallowEdgeMaterial;

    }

    // Deep Edge
    private void ApplyDeepEdgeMesh(Mesh deepMesh, Transform parentObject)
    {
        GameObject deepEdgeObj = new GameObject("DeepEdge");
        deepEdgeObj.transform.SetParent(parentObject);

        MeshFilter deepMeshFilter = deepEdgeObj.AddComponent<MeshFilter>() ?? deepEdgeObj.AddComponent<MeshFilter>();
        deepMeshFilter.sharedMesh = deepMesh;

        MeshRenderer deepMeshRenderer = deepEdgeObj.AddComponent<MeshRenderer>() ?? deepEdgeObj.AddComponent<MeshRenderer>();
        deepMeshRenderer.sharedMaterial = deepSeaEdgeMaterial;

    }

    // Plateau Edge
    private void ApplyPlateauEdgeMesh(Mesh plateauMesh, Transform parentObject)
    {
        GameObject plateauEdgeObj = new GameObject("PlateauEdge");
        plateauEdgeObj.transform.SetParent(parentObject);

        MeshFilter plateauMeshFilter = plateauEdgeObj.AddComponent<MeshFilter>() ?? plateauEdgeObj.AddComponent<MeshFilter>();
        plateauMeshFilter.sharedMesh = plateauMesh;

        MeshRenderer plateauMeshRenderer = plateauEdgeObj.AddComponent<MeshRenderer>() ?? plateauEdgeObj.AddComponent<MeshRenderer>();
        plateauMeshRenderer.sharedMaterial = plateauEdgeMaterial;

    }

    // Abyssal Edge
    private void ApplyAbyssalEdgeMesh(Mesh abyssalMesh, Transform parentObject)
    {
        GameObject abyssalEdgeObj = new GameObject("AbyssalEdge");
        abyssalEdgeObj.transform.SetParent(parentObject);

        MeshFilter abyssalMeshFilter = abyssalEdgeObj.AddComponent<MeshFilter>() ?? abyssalEdgeObj.AddComponent<MeshFilter>();
        abyssalMeshFilter.sharedMesh = abyssalMesh;

        MeshRenderer abyssalMeshRenderer = abyssalEdgeObj.AddComponent<MeshRenderer>() ?? abyssalEdgeObj.AddComponent<MeshRenderer>();
        abyssalMeshRenderer.sharedMaterial = abyssalEdgeMaterial;
    }


    #endregion

    #region Apply Refactors 
    private void ApplyTerrainMesh(Mesh terrainMesh)
    {
        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = terrainMesh;

        MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = terrainMaterial;
    }

    private void ApplyPlateauGeometry(PlateauGeometryResult geometry)
    {
        Transform existingRoot = transform.Find(GeneratedPlateauGeometryRootName);
        if (existingRoot != null)
        {
            if (Application.isPlaying) Destroy(existingRoot.gameObject);
            else DestroyImmediate(existingRoot.gameObject);
        }

        if (geometry == null || !geometry.HasGeometry) return;

        GameObject root = new GameObject(GeneratedPlateauGeometryRootName);
        root.layer = gameObject.layer;
        root.transform.SetParent(transform, false);

        Material rockMaterial = plateauEdgeMaterial != null ? plateauEdgeMaterial : terrainMaterial;
        ApplyPlateauGeometryLayer(root.transform, "Procedural Escarpment", geometry.Escarpment, rockMaterial);
        ApplyPlateauGeometryLayer(root.transform, "Rock Formations", geometry.Formations, rockMaterial);
    }

    private void ApplyPlateauGeometryLayer(
        Transform parent,
        string layerName,
        PlateauGeneratedMeshData meshData,
        Material material)
    {
        if (meshData == null || !meshData.HasGeometry) return;

        Mesh mesh = meshData.CreateMesh($"Generated Plateau {layerName}");
        if (mesh == null) return;
        TrackGeneratedVisualResource(mesh);

        GameObject layer = new GameObject(layerName);
        layer.layer = gameObject.layer;
        layer.transform.SetParent(parent, false);
        MeshFilter filter = layer.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = layer.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
    }

    private void ApplyTexture(Texture2D texture)
    {
        MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetTexture(TerrainBaseMapProperty, texture);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    /// <summary>
    /// Rebuilds and applies the terrain texture splat without regenerating or reuploading the 3D mesh.
    /// Useful for instant inspector-driven diagnostic / heatmap inspection.
    /// </summary>
    public void UpdateTerrainTexture()
    {
        if (grid == null) return;
        bool isOceanChunk = currentGridType == GridType.Type.Ocean || currentGridType == GridType.Type.Empty;
        int effectiveVisualSamples = isOceanChunk ? 1 : generationSettings.visualSamplesPerCell;
        bool useContinuousMesh = TerrainSource != null;

        TextureBuilder textureBuilder = useContinuousMesh
            ? new TextureBuilder(grid, TerrainSource, effectiveVisualSamples, climateProfile, debugViewMode)
            : new TextureBuilder(grid, climateProfile, debugViewMode);
        Texture2D texture = textureBuilder.Build();
        texture.name = $"Generated Terrain Texture ({debugViewMode})";
        TrackGeneratedVisualResource(texture);
        ApplyTexture(texture);
    }

    public void ReleaseGeneratedVisualResources()
    {
        if (isReleasingGeneratedVisuals) return;
        isReleasingGeneratedVisuals = true;

        try
        {
            HashSet<Object> ownedResources = new HashSet<Object>();
            if (generatedVisualResources != null)
            {
                ownedResources.UnionWith(generatedVisualResources);
                generatedVisualResources.Clear();
            }

            MeshRenderer terrainRenderer = GetComponent<MeshRenderer>();
            bool hasGeneratedPropertyTexture = false;
            bool hasLegacyMaterialInstance = false;
            if (terrainRenderer != null)
            {
                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                terrainRenderer.GetPropertyBlock(propertyBlock);
                Texture propertyTexture = propertyBlock.GetTexture(TerrainBaseMapProperty);
                if (propertyTexture != null)
                {
                    hasGeneratedPropertyTexture = true;
                    ownedResources.Add(propertyTexture);
                }
                terrainRenderer.SetPropertyBlock(null);

                // Cleans previews generated before the property-block path existed.
                Material currentMaterial = terrainRenderer.sharedMaterial;
                if (currentMaterial != null
                    && currentMaterial != terrainMaterial
                    && IsDestroyableGeneratedResource(currentMaterial))
                {
                    hasLegacyMaterialInstance = true;
                    if (currentMaterial.HasProperty(TerrainBaseMapProperty))
                    {
                        Texture legacyTexture = currentMaterial.GetTexture(TerrainBaseMapProperty);
                        if (legacyTexture != null) ownedResources.Add(legacyTexture);
                    }

                    terrainRenderer.sharedMaterial = terrainMaterial;
                    ownedResources.Add(currentMaterial);
                }
            }

            MeshFilter terrainFilter = GetComponent<MeshFilter>();
            Mesh currentTerrainMesh = terrainFilter != null ? terrainFilter.sharedMesh : null;
            bool ownsTerrainMesh = currentTerrainMesh != null
                && (ownedResources.Contains(currentTerrainMesh)
                    || hasLegacyMaterialInstance
                    || (hasGeneratedPropertyTexture && IsNamedGeneratedTerrainMesh(currentTerrainMesh)));
            if (ownsTerrainMesh)
            {
                ownedResources.Add(currentTerrainMesh);
                terrainFilter.sharedMesh = null;
            }

            Transform edgeRoot = transform.Find("Edge");
            if (edgeRoot != null)
            {
                foreach (MeshFilter edgeFilter in edgeRoot.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (edgeFilter.sharedMesh == null) continue;
                    ownedResources.Add(edgeFilter.sharedMesh);
                    edgeFilter.sharedMesh = null;
                }

                foreach (MeshRenderer edgeRenderer in edgeRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Material edgeMaterialInstance = edgeRenderer.sharedMaterial;
                    if (edgeMaterialInstance != null && !IsConfiguredEdgeMaterial(edgeMaterialInstance))
                    {
                        ownedResources.Add(edgeMaterialInstance);
                    }
                    edgeRenderer.sharedMaterial = null;
                }
            }

            Transform plateauGeometryRoot = transform.Find(GeneratedPlateauGeometryRootName);
            if (plateauGeometryRoot != null)
            {
                foreach (MeshFilter geometryFilter in plateauGeometryRoot.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (geometryFilter.sharedMesh == null) continue;
                    ownedResources.Add(geometryFilter.sharedMesh);
                    geometryFilter.sharedMesh = null;
                }

                foreach (MeshRenderer geometryRenderer in plateauGeometryRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    geometryRenderer.sharedMaterial = null;
                }

                if (Application.isPlaying) Destroy(plateauGeometryRoot.gameObject);
                else DestroyImmediate(plateauGeometryRoot.gameObject);
            }

            foreach (Object resource in ownedResources)
            {
                if (!IsDestroyableGeneratedResource(resource)) continue;
                if (Application.isPlaying) Destroy(resource);
                else DestroyImmediate(resource);
            }
        }
        finally
        {
            isReleasingGeneratedVisuals = false;
        }
    }

    private void TrackGeneratedVisualResource(Object resource)
    {
        if (resource == null) return;
        generatedVisualResources ??= new HashSet<Object>();
        generatedVisualResources.Add(resource);
    }

    private static bool IsNamedGeneratedTerrainMesh(Mesh mesh)
    {
        if (mesh == null) return false;
        return mesh.name.StartsWith("Generated Terrain")
            || mesh.name.StartsWith("Fractional Terrain");
    }

    private static bool IsDestroyableGeneratedResource(Object resource)
    {
        if (resource == null) return false;

#if UNITY_EDITOR
        // Generated preview resources are scene-owned and non-persistent. Never
        // destroy a material, texture, or mesh that belongs to the project/built-ins.
        return !UnityEditor.EditorUtility.IsPersistent(resource);
#else
        return true;
#endif
    }

    private bool IsConfiguredEdgeMaterial(Material material)
    {
        return material == edgeMaterial
            || material == hillEdgeMaterial
            || material == oceanEdgeMaterial
            || material == coastEdgeMaterial
            || material == beachEdgeMaterial
            || material == riverEdgeMaterial
            || material == shallowEdgeMaterial
            || material == deepSeaEdgeMaterial
            || material == plateauEdgeMaterial
            || material == abyssalEdgeMaterial
            || material == mountainEdgeMaterial;
    }

    private void OnDestroy()
    {
        ReleaseGeneratedVisualResources();
    }

    private void ApplyEdgeMesh(Mesh edgeMesh, Material edgeMaterial)
    {
        // Default Material
        if (edgeMaterial != null) edgeMaterial = beachEdgeMaterial;

        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = edgeMesh;

        MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        if (meshRenderer != null) meshRenderer.sharedMaterial = edgeMaterial; // ApplyEdgeMaterial(determineEdgeMaterial(grid[x, y,]));
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
