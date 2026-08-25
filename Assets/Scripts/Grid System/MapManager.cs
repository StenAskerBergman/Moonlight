using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/*  File Role: Creating the map of the game 

    Author: Sten

    The MapManager script is responsible for creating 
    the map of the game. It creates the islands, sets 
    their positions, and creates the borders around them. 
    It also handles clicking on an island to select it 
    and displaying the name and resources of the island 
    in the UI.

*/

// MapManager
// tldr: responsible for spawning in empty prefab islands objects

// MapGrid
// tldr: responsible for actual generation of the island on awake

public class MapManager : MonoBehaviour
{
    public static MapManager instance { get; private set; }
    private const string GeneratedMapRootName = "Generated Map";

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;

        if (FindObjectOfType<PlayerSpawnManager>() == null)
        {
            gameObject.AddComponent<PlayerSpawnManager>();
        }
    }

    #region Map Variables

    // Spawn Patterns
    [System.Serializable]
    public class PatternData
    {
        [SerializeField]
        internal string displayName = "My Custom Name";

        public SpawnPattern spawnPattern;
        public string patternName;
        public string patternDescription;
    }


    public enum SpawnPattern
    {
        Singular,
        Circlar,
        Square,
        Normal,
    }

    public static event System.Action OnMapGenerated;

    [Header("Spawn Patterns")]
    public List<PatternData> patternDataList;
    [Space]
    public SpawnPattern selectedSpawnPattern;


    // Prefabs ... (and below the rest of your variables)
    [SerializeField] private GameObject islandPrefab; // The Current Island Object 
    [SerializeField] private GameObject waterObject; // Assuming that waterObject is a reference to the water game object
    [SerializeField] private IslandConfiguration islandConfig; // Assuming that IslandConfig is a reference to the IslandConfiguration scriptable object

    //[Range(0, 7)] // Add Later
    public int numberOfIslands;
    public List<Island> islands { get; private set; }
    private int nextIslandID;
    private GameManager gameManager;
    [SerializeField, HideInInspector] private Transform generatedMapRoot;
    [Space]
    [SerializeField] private bool WaterOnStart;
    [SerializeField] private float waterHeight = 0f; // Replace with the correct height of your water

    [Space]


    [Space]
    [Header("Square Patterns Only")]
    // The LATTICE SLOT pitch, not the chunk width. See LatticeSlotSpacing.
    [SerializeField] private float islandSpacing = 20f;
    [Tooltip("VESTIGIAL. Chunk bounds are now sized from ChunkWorldSize so they match " +
             "the generated mesh exactly. Nothing reads this.")]
    [SerializeField] private float islandSize = 10f;
    [SerializeField] private int IslandHeight;

    // ---------------------------------------------------------------------------
    // SPATIAL CONTRACT - the numbers here are not interchangeable, and conflating
    // them is what produced both the "overlapping chunks" and the "60-unit gaps".
    //
    //   gridResolution          = 60 cells per axis        (MapGrid.size)
    //   cellWorldSize           = 1 world unit             (GridSystem.cellSize)
    //   chunkWorldSize          = 60 world units           = resolution * cellWorldSize
    //   latticeSlotSpacing      = 30 world units           (islandSpacing)
    //   occupied-slot stride    = 2                        (selection fills every other slot)
    //   generated-neighbour pitch = 60                     = stride * latticeSlotSpacing
    //
    // Chunks therefore tile exactly because
    //     occupied-slot stride * latticeSlotSpacing == chunkWorldSize
    // NOT because the slot spacing equals the chunk width. A spawn pattern that fills
    // adjacent slots would overlap by half a chunk - ValidateGeneratedChunkSeparation
    // below exists to catch exactly that.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Pitch between adjacent lattice SLOTS. Half a chunk with the current sparse
    /// selection - it is not the chunk's world width.
    /// </summary>
    private float LatticeSlotSpacing => islandSpacing;

    /// <summary>
    /// World-space width of one generated chunk: gridResolution * cellWorldSize, read
    /// from the island prefab's MapGrid.size (cells are 1 world unit, per
    /// GridSystem.cellSize). This is the extent of the terrain mesh and of
    /// Island.bounds - it is deliberately independent of LatticeSlotSpacing.
    /// </summary>
    private float ChunkWorldSize
    {
        get
        {
            if (islandPrefab != null)
            {
                MapGrid prefabGrid = islandPrefab.GetComponent<MapGrid>();
                if (prefabGrid != null && prefabGrid.Size > 0) return prefabGrid.Size;
            }

            return islandSpacing;
        }
    }

    /// <summary>
    /// Distance subtracted from every lattice slot position so the lattice's SWEPT MESH
    /// EXTENT is centred on the world origin.
    ///
    /// The chunk transform is the mesh's MINIMUM corner, so before offsetting the
    /// lattice sweeps [0, (slots-1)*latticeSlotSpacing + chunkWorldSize] - the extra
    /// chunkWorldSize being the last chunk's own body. Centring the slot POSITIONS
    /// instead of that swept extent left the terrain half a chunk positive: the map
    /// read [-90, 150] rather than [-120, 120].
    ///
    /// This generalises what the Singular pattern already hardcodes - substituting
    /// slots = 1 yields exactly its chunkWorldSize/2.
    /// </summary>
    private float LatticeCenteringOffset
    {
        get
        {
            int slots = Mathf.Max(1, numberOfIslands);
            return ((slots - 1) * LatticeSlotSpacing + ChunkWorldSize) * 0.5f;
        }
    }

    /// <summary>
    /// Bounds for a chunk whose MINIMUM CORNER is at minCorner. The MapGrid transform
    /// sits on that corner and its mesh spans local 0..ChunkWorldSize, so the bounds
    /// centre is half a chunk further along X and Z. Keeping this in one place is what
    /// makes Island.bounds actually enclose the terrain it claims to.
    /// </summary>
    private Bounds MakeChunkBounds(Vector3 minCorner)
    {
        float width = ChunkWorldSize;
        return new Bounds(
            minCorner + new Vector3(width * 0.5f, 0f, width * 0.5f),
            new Vector3(width, width, width));
    }

    // Calculate the offset needed to start the islands in the center of the scene
    public float xOffset { get; private set; }
    public float zOffset { get; private set; }

    [Header("Island Selection")]
    public bool invertSelection;

    [Tooltip("Max Island Amount: 49")] // Write down why this number is here to begin with
    public List<int> currentIslandSelection; // Current Selected Islands

    [Tooltip("Max Ocean Amount: XX")]
    public List<int> currentOceanSelection; // Current Selected Oceans

    #endregion

    #region Match Configuration

    /// <summary>
    /// Overrides this map's generation inputs with the lobby's choices.
    ///
    /// Must be called before Start(), which is where generation actually happens -
    /// MatchBootstrapper does this from Awake(). islandPrefab is deliberately not
    /// overridable: it is scene wiring, not a match setting.
    /// </summary>
    public void ApplyConfig(MatchConfig config)
    {
        if (config == null)
        {
            return;
        }

        if (islands != null)
        {
            Debug.LogWarning("MapManager: ApplyConfig ran after generation - the map " +
                             "is already built and these values will not take effect.");
            return;
        }

        selectedSpawnPattern = config.spawnPattern;
        numberOfIslands = config.numberOfIslands;

        // A null islandConfig means "the lobby had no opinion", so keep the
        // Inspector's asset rather than blanking a working reference.
        if (config.islandConfig != null)
        {
            islandConfig = config.islandConfig;
        }

        Debug.Log($"<color=lightblue>MapManager:</color> applied {config}");
    }

    #endregion

    #region Start Methods

    // Set Island Spawning Settings - Spawn Patterns 

    void Start()
    {
        RegenerateMap();
    }

    public void GenerateMap()
    {
        ResolveGeneratedMapRoot();
        if (generatedMapRoot != null)
        {
            Debug.LogWarning("MapManager: a generated map already exists. Use Regenerate Map to replace it.", this);
            return;
        }

        // If a lobby handed over a config and nothing consumed it, the match is
        // about to generate with Inspector values and silently ignore the player's
        // choices. Almost always means MatchBootstrapper is missing from the scene.
        if (Application.isPlaying && GameSession.HasPending)
        {
            Debug.LogError("MapManager: a MatchConfig is still pending at generation " +
                           "time - no MatchBootstrapper consumed it. The lobby's " +
                           "settings are being ignored.");
        }

        if (waterObject != null)
        {
            waterObject.SetActive(WaterOnStart);
            // waterObject.transform.localPosition = new Vector3(0f, waterHeight, 0f); // Removed so user can position it manually
        }

        islands = new List<Island>();
        nextIslandID = 1;
        gameManager = FindObjectOfType<GameManager>();
        generatedMapRoot = new GameObject(GeneratedMapRootName).transform;
        generatedMapRoot.SetParent(transform, false);

        // Creates Island Game Object
        // > Start 
        // Debug.Log("Pattern: " + selectedSpawnPattern); // Pattern Tracker + Verifier
        switch (selectedSpawnPattern)
        {
            case SpawnPattern.Singular: // DEV ONLY
                // Singular Spawn
                int singularIslandID = nextIslandID++;
                // Minimum corner placed half a chunk negative on both axes, so the one
                // generated chunk is centred on the world origin.
                float Offset = ChunkWorldSize * -0.5f;
               
                for (int i = 0; i < 1; i++)
                {
                    // Singular Spawn

                    // Generate a Single new island
                    invertSelection = true;
                    IslandData islandData = new IslandData(GridType.Type.Island);

                    // Set the position and size of the island's bounds
                    Vector3 islandPosition = new Vector3(Offset, 0, Offset);
                    islandData.bounds = MakeChunkBounds(islandPosition);

                    islandData.islandType = IslandType.None;
                    islandData.id = singularIslandID;  // Use the reserved ID
                    islandData.name = "Island " + singularIslandID;

                    AddIsland(islandData); // Singular
                }
                break;

            //case SpawnPattern.Linear: // DEV ONLY
            //    // Linear Spawn
            //    for (int i = 0; i < numberOfIslands; i++)
            //    {
            //        // Linear Spawn
            //        // Generate a islands in a row
            //        IslandData islandData = new IslandData(GridType.Type.Island);
            //        islandData.islandType = IslandType.None;
            //        islandData.buildings = new List<Building>();
            //        islandData.items = new Dictionary<ItemData, int>();
            //        islandData.bounds = new Bounds(new Vector3(i * 20, 0, 0), new Vector3(10, 10, 10));
            //        islandData.id = i + 1;
            //        islandData.name = "Island " + (i + 1);
            //        AddIsland(islandData);
            //    }
            //    break;

            //case SpawnPattern.Circular: // DEV ONLY
            //    // Circular Spawn
            //    float worldLimit = 100f; // Define your world limit here
            //    float angleIncrement = 360f / numberOfIslands;
            //    for (int i = 0; i < numberOfIslands; i++)
            //    {
            //        // Calculate the island's position in a circular pattern
            //        float angle = i * angleIncrement * Mathf.Deg2Rad;
            //        Vector3 islandPosition = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (worldLimit / 2);
            //        // 1. Generate a new island
            //        IslandData islandData = new IslandData(GridType.Type.Island);
            //        // 2. Set the new island's data
            //        islandData.islandType = IslandType.None;
            //        islandData.buildings = new List<Building>(); 
            //        islandData.items = new Dictionary<ItemData, int>();
            //        islandData.bounds = new Bounds(islandPosition, new Vector3(10, 10, 10));
            //        islandData.id = i + 1;
            //        islandData.name = "Island " + (i + 1);
            //        AddIsland(islandData);
            //    }
            //    break;

            case SpawnPattern.Square: // DEV ONLY
                // Square Spawn
                int currentIsland = 0;
                // Centres the lattice's swept MESH extent on the world origin, not the
                // slot positions - the transform is each chunk's minimum corner.
                xOffset = LatticeCenteringOffset;
                zOffset = LatticeCenteringOffset;

                for (int i = 0; i < numberOfIslands; i++)
                {
                    for (int j = 0; j < numberOfIslands; j++)
                    {
                        // Generate a new island
                        IslandData islandData = new IslandData(GridType.Type.Island);
                        islandData.islandType = IslandType.None;
                        //islandData.buildings = new List<Building>();

                        // Chunk minimum corner, stepped by the LATTICE SLOT pitch.
                        Vector3 islandPosition = new Vector3(i * LatticeSlotSpacing - xOffset, 0, j * LatticeSlotSpacing - zOffset);

                        // Set the remaining data for the island
                        islandData.bounds = MakeChunkBounds(islandPosition);
                        islandData.id = currentIsland + 1;
                        islandData.name = "Island " + (currentIsland + 1);

                        // Add the island to the game world
                        AddIsland(islandData); // Square

                        currentIsland++;
                    }
                }
                break;
            
            // CURRENT FINAL DEFAULT:
            case SpawnPattern.Normal: // FINAL VERSION
                // Square Spawn + Normal Sized Orbitor Islands

                currentIsland = 0;
                // Centres the lattice's swept MESH extent on the world origin, not the
                // slot positions - the transform is each chunk's minimum corner.
                xOffset = LatticeCenteringOffset;
                zOffset = LatticeCenteringOffset;
                // Add Islands Loop
                for (int i = 0; i < numberOfIslands; i++)
                {
                    for (int j = 0; j < numberOfIslands; j++)
                    {
                        // Create New Data
                        IslandData islandData = new IslandData(GridType.Type.Island);

                        // Set the position
                        // Chunk minimum corner - the MapGrid transform goes here and its
                        // mesh spans local 0..ChunkWorldSize from it. Stepping by the slot
                        // pitch tiles only because the selection fills every other slot.
                        Vector3 islandPosition = new Vector3(i * LatticeSlotSpacing - xOffset, 0, j * LatticeSlotSpacing - zOffset);

                        // Set Size - Not looking good bro :NAHH:
                        // Yet to be a Fully Written, Use Pre-game "Biome Setting" / Player Setting"


                        // Set island type
                        islandData.islandType = IslandType.None;

                        // Set island type
                        // Setting island type using system
                        // Yet to be a Fully Written, Using:
                        // -> location equation 
                        // -> pre-game settings

                        // Set islands buildings
                        islandData.buildings = new List<Building>();

                        // Set islands items
                        islandData.items = new Dictionary<ItemData, int>();

                        // Set the bounds
                        // Set the remaining data for the island
                        islandData.bounds = MakeChunkBounds(islandPosition);
                        islandData.id = currentIsland + 1;
                        islandData.name = "Island " + (currentIsland + 1);

                        // Generate Island Data

                        // Generate island data logic...
                        if (currentOceanSelection.Contains(currentIsland + 1))
                        {
                            //Debug.Log($"Generating ocean terrain for Island ID {currentIsland + 1} at position {islandPosition}");
                        }

                        // Add the island to the game world
                        AddIsland(islandData); // Normal

                        currentIsland++;

                    }
                }

                // Map Generation Cycle

                // Creates Map Game Objects
                // > Complete

                // Creates Island Game Object
                // > Complete

                // Add Water Operation
                // Start - Complete

                // Add Plataeu Operation
                // Start - Incomplete - Not Working Atm! 

                // Initialize Start Game Session
                // gameManager.StartGameSession(string Name,int GameSpeed, Bool WinCon);

                // If WinCon exists 
                // false: gameManager.LoseGameSession();
                // true: gameManager.WinGameSession();

                // And then End Game Session
                // gameManager.EndGameSession();


               break;

            default:
                Debug.LogError("Incomplete or Invalid Spawn Pattern Selected!");
                Debug.LogWarning("Select New a Valid or Complete Pattern next time!");
                break;
        }

        if (selectedSpawnPattern == SpawnPattern.Singular)
        {
            currentIslandSelection.Add(1);
            currentOceanSelection.Remove(1); // Ensure ID 1 is not considered for oceans
        }

        ValidateGeneratedChunkSeparation();

        OnMapGenerated?.Invoke();
    }

    /// <summary>
    /// Guards the spatial contract: no two GENERATED chunks may overlap.
    ///
    /// Chunks tile only because the current selection fills every other lattice slot,
    /// so occupied-slot stride (2) * latticeSlotSpacing (30) happens to equal
    /// chunkWorldSize (60). Nothing structurally enforces that - a spawn pattern or
    /// selection list that puts two chunks in adjacent slots would silently overlap
    /// them by half a chunk, which is exactly the failure that produced one chunk's
    /// abyssal rim being drawn through its neighbour's centre. This reports it loudly
    /// instead of leaving it to be discovered from a screenshot.
    ///
    /// Footprints come from transform + ChunkWorldSize rather than renderer bounds so
    /// the check is about the spatial contract only, independent of terrain height.
    /// </summary>
    private void ValidateGeneratedChunkSeparation()
    {
        if (generatedMapRoot == null) return;

        MapGrid[] chunks = generatedMapRoot.GetComponentsInChildren<MapGrid>(true);
        float width = ChunkWorldSize;
        int overlaps = 0;

        for (int a = 0; a < chunks.Length; a++)
        {
            for (int b = a + 1; b < chunks.Length; b++)
            {
                Vector3 pa = chunks[a].transform.position;
                Vector3 pb = chunks[b].transform.position;

                // Axis-aligned footprints, so an overlap on BOTH axes is a real
                // intersection. Touching exactly (delta == width) is correct tiling.
                float overlapX = width - Mathf.Abs(pa.x - pb.x);
                float overlapZ = width - Mathf.Abs(pa.z - pb.z);
                if (overlapX <= 0f || overlapZ <= 0f) continue;

                overlaps++;
                Debug.LogError(
                    $"MapManager: generated chunks '{chunks[a].name}' and '{chunks[b].name}' OVERLAP by " +
                    $"{overlapX:F1} x {overlapZ:F1} world units. chunkWorldSize={width}, " +
                    $"latticeSlotSpacing={LatticeSlotSpacing}. Two selected slots are closer than one " +
                    $"chunk width apart - the selection must keep an occupied-slot stride of at least " +
                    $"{Mathf.CeilToInt(width / Mathf.Max(0.0001f, LatticeSlotSpacing))}.", chunks[a]);
            }
        }

        if (overlaps > 0)
        {
            Debug.LogError($"MapManager: {overlaps} overlapping chunk pair(s) - the map's spatial contract is violated.", this);
        }
    }

    public void RegenerateMap()
    {
        ClearMap();
        GenerateMap();
    }

    public void ClearMap()
    {
        ResolveGeneratedMapRoot();
        if (generatedMapRoot != null)
        {
            GameObject rootObject = generatedMapRoot.gameObject;
            generatedMapRoot = null;

            foreach (MapGrid mapGrid in rootObject.GetComponentsInChildren<MapGrid>(true))
            {
                mapGrid.ReleaseGeneratedVisualResources();
            }

            if (Application.isPlaying) Destroy(rootObject);
            else DestroyImmediate(rootObject);
        }

        islands ??= new List<Island>();
        islands.Clear();
        oceanGizmoPositions.Clear();
        nextIslandID = 1;

        if (!Application.isPlaying && waterObject != null)
        {
            waterObject.SetActive(false);
        }
    }

    private void ResolveGeneratedMapRoot()
    {
        if (generatedMapRoot == null && !Application.isPlaying)
        {
            generatedMapRoot = transform.Find(GeneratedMapRootName);
        }
    }
    
    #endregion

    #region Island +/- Methods 
    public List<Vector3> oceanGizmoPositions = new List<Vector3>();

    // IMPORTANT: USES SPAWN PATTERNS 
    public void AddIsland(IslandData data)
    {
        bool shouldAddIsland = invertSelection ? !currentIslandSelection.Contains(data.id) : currentIslandSelection.Contains(data.id);
        bool shouldAddOcean = !invertSelection && currentOceanSelection.Contains(data.id) && selectedSpawnPattern != SpawnPattern.Singular;

        // If we should add this island (or ocean, depending on the list and invertSelection)
        if (shouldAddIsland || shouldAddOcean)
        {

            // Instantiate first: Island is a MonoBehaviour, so the prefab's own component
            // is the island. Building one with 'new' created a second, orphaned instance
            // that took all the data, while the component the rest of the game reaches
            // through GetComponent - raycasts, GridSystem, BuildingPlacer - kept defaults.
            GameObject islandGO = Instantiate(islandPrefab);
            islandGO.transform.SetParent(generatedMapRoot, true);
            // The MapGrid transform is the chunk's MINIMUM CORNER, not its centre: the
            // terrain mesh spans local 0..ChunkWorldSize from the transform. MakeChunkBounds
            // put the bounds centre half a chunk past that corner, so undo it here.
            float halfChunk = ChunkWorldSize * 0.5f;
            islandGO.transform.position = new Vector3(
                data.bounds.center.x - halfChunk,
                data.bounds.center.y,
                data.bounds.center.z - halfChunk);

            // Ocean macro-grids are built from the same prefab and pipeline as islands,
            // but calling them "Island N" makes the hierarchy unreadable when half the
            // map is open water. Only the GameObject is renamed - IslandData.name and
            // the island id are left alone so nothing that looks them up breaks.
            islandGO.name = shouldAddOcean ? $"Ocean {data.id}" : data.name;

            Island island = islandGO.GetComponent<Island>();
            if (island == null)
            {
                Debug.LogError($"MapManager: '{islandPrefab.name}' has no Island component - cannot add island '{data.name}'.");
                Destroy(islandGO);
                nextIslandID++;
                return;
            }

            island.Initialize(data.islandType);
            island.islandConfig = islandConfig;
            island.buildings = data.buildings;
            island.IslandItems = data.items;
            island.bounds = data.bounds;
            island.id = GetNextIslandID();
            island.islandObject = islandGO;

            islands.Add(island);

            GridSystem gridSystem = islandGO.GetComponent<GridSystem>();
            MapGrid mapGrid = islandGO.GetComponent<MapGrid>();
            InfluenceManager influenceManager = islandGO.AddComponent<InfluenceManager>();

            // If the island is actually an ocean, then set the grid type accordingly
            if (shouldAddOcean)
            {
                mapGrid.currentGridType = GridType.Type.Ocean; // or GridType.Type.Plateau if that's what you meant by "ocean/plateaus"
                                                               // Add ocean gizmo position, log message, etc.
                                                               // ... What about -> shouldBePlateau(mapGrid.currentGridType); - Undecided for now - TODO!
            }
            else 
            {
                // If it's a regular island, then proceed as normal
                mapGrid.currentGridType = data.gridType;
            }

            mapGrid.InitializeTerrain();


            // If this ID is marked for ocean and we're not inverting, or it's an island and we are inverting
            if (shouldAddOcean)
            {
                // Add the position for gizmo drawing
                oceanGizmoPositions.Add(island.bounds.center + Vector3.up * 5); // Raise the Gizmo above the terrain for visibility
                // Debug.Log($"Ocean placed at {island.bounds.center} with ID {island.id}");
            }
        }
        else
        {
            // Debug.Log($"Skipped adding terrain with ID {data.id}");
        }

        // Always increment the ID to ensure unique IDs
        nextIslandID++;
    }

    // At the class level
    [SerializeField] private bool showGizmos = true; // Default to true to show gizmos initially

    // Within your existing OnDrawGizmos method
    void OnDrawGizmos()
    {
        if (showGizmos)
        {
            Gizmos.color = Color.red; // Set the color of the gizmos to red
            foreach (var pos in oceanGizmoPositions)
            {
                Gizmos.DrawSphere(pos, 2.5f); // Draw a sphere at each position with a radius of 2.5 units
            }
        }
    }


    private bool shouldBePlateau(IslandData islandData)
    {
        // Define logic here to determine if this should be a plateau
        // if it should be then return the type we desire
        return islandData.gridType == GridType.Type.Plateau;
    }

    public void RemoveIsland(Island island)
    {
        islands.Remove(island);
    }

    #endregion

    #region Remove Selection Methods
    [SerializeField] private GameObject flatLandPrefab; // Reference to the flat land prefab
    [SerializeField] private bool showOceanLocations = false; // Toggle to visualize ocean placements

    // Version 2
    public void RemoveSelectedIslandsAndOceans(bool invertSelection)
    {
        List<int> mergedSelection = new List<int>(currentIslandSelection);
        mergedSelection.AddRange(currentOceanSelection.Except(currentIslandSelection)); // Combine and avoid duplicates

        List<Island> toRemove = new List<Island>();

        // Add the islands and oceans to remove based on the merged selection
        foreach (Island island in islands)
        {
            bool isSelected = mergedSelection.Contains(island.id);
            if ((isSelected && !invertSelection) || (!isSelected && invertSelection))
            {
                toRemove.Add(island);
            }
        }

        // Remove the selected islands and oceans
        foreach (Island island in toRemove)
        {
            // Here you could instantiate a flat land or ocean prefab as needed
            // Example for replacing with flat land:
            GameObject flatLandGO = Instantiate(flatLandPrefab, island.islandObject.transform.position, Quaternion.identity);
            flatLandGO.transform.localScale = new Vector3(island.bounds.size.x, 0.1f, island.bounds.size.z);
            flatLandGO.name = island.islandObject.name + " (Flat Land)";

            // Remove island or ocean from the list and destroy the game object
            RemoveIsland(island);
            Destroy(island.islandObject);
        }
    }


    // Version 1
    public void RemoveSelectedIslands(bool invertSelection)
    {
        List<Island> islandsToRemove = new List<Island>();

        // Add the islands to remove based on the current selection
        foreach (Island island in islands)
        {
            if (currentIslandSelection.Contains(island.id) && !invertSelection)
            {
                islandsToRemove.Add(island);
            }
            else if (!currentIslandSelection.Contains(island.id) && invertSelection)
            {
                islandsToRemove.Add(island);
            }
        }

        // Remove the selected islands
        foreach (Island island in islandsToRemove)
        {
            RemoveIsland(island);
        }
    }


    #endregion

    #region Set Island Id
    private int GetNextIslandID()
    {
        // Debug.Log("nextIslandID = " + nextIslandID);
        return nextIslandID++;
    }
    #endregion

    #region Get Island Methods

    // Unique Id 
    public Island GetIslandUID(string ID) // UID: Unique Id
    {
        return islands.Find(island => island.ID == ID);
    }
    // Spawn Id
    public Island GetIslandSID(int id) // SID: Spawn Id
    {
        return islands.Find(island => island.id == id);
    }

    public Island GetIslandByName(string name)
    {
        return islands.Find(island => island.islandName == name);
    }

    public IslandType GetCurrentIslandType(Vector3 playerPosition)
    {
        foreach (Island island in islands)
        {
            if (island.bounds.Contains(playerPosition))
            {
                return island.islandType;
            }
        }
        
        return IslandType.None;
    }
    #endregion
}

