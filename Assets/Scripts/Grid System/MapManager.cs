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
    #region Map Variables

    // Ocean Nav Mesh
    [SerializeField] private NavMeshBuilder navMeshBuilder;

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
        Square,
        Normal,
    }

    [Header("Spawn Patterns")]
    public static event System.Action OnMapGenerated;

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
    [Space]
    [SerializeField] private bool WaterOnStart;
    [SerializeField] private float waterHeight = 0f; // Replace with the correct height of your water

    [Space]


    [Space]
    [Header("Square Patterns Only")][Tooltip("Square Patterns Only")]
    [SerializeField] private float islandSpacing = 20f;
    [SerializeField] private float islandSize = 10f;
    [SerializeField] private int IslandHeight;

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

    #region Start Methods

    // Set Island Spawning Settings - Spawn Patterns 

    void Start()
    {
        this.waterObject.SetActive(WaterOnStart); 
        waterObject.transform.localPosition = new Vector3(0f, waterHeight, 0f);

        islands = new List<Island>();
        nextIslandID = 1;
        gameManager = FindObjectOfType<GameManager>();

        // Creates Island Game Object
        // > Start 
        // Debug.Log("Pattern: " + selectedSpawnPattern); // Pattern Tracker + Verifier
        switch (selectedSpawnPattern)
        {
            case SpawnPattern.Singular: // DEV ONLY
                // Singular Spawn
                int singularIslandID = nextIslandID++;
                float halfSize = islandSize / 2f;
                float QuarterSize = (halfSize / 2);
                float Offset = QuarterSize * -1; 
               
                for (int i = 0; i < 1; i++)
                {
                    // Singular Spawn

                    // Generate a Single new island
                    invertSelection = true;
                    IslandData islandData = new IslandData(GridType.Type.Island);

                    // Set the position and size of the island's bounds
                    Vector3 islandPosition = new Vector3(Offset, 0, Offset);
                    Bounds islandBounds = new Bounds(islandPosition, new Vector3(islandSize, islandSize, islandSize));
                    islandData.bounds = islandBounds;

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
                float halfIslands = numberOfIslands / 2f;
                xOffset = (halfIslands - 0.5f) * islandSpacing;
                zOffset = (halfIslands - 0.5f) * islandSpacing;

                for (int i = 0; i < numberOfIslands; i++)
                {
                    for (int j = 0; j < numberOfIslands; j++)
                    {
                        // Generate a new island
                        IslandData islandData = new IslandData(GridType.Type.Island);
                        islandData.islandType = IslandType.None;
                        //islandData.buildings = new List<Building>();

                        // Set the position and size of the island's bounds
                        Vector3 islandPosition = new Vector3(i * islandSpacing - xOffset - 30, 0, j * islandSpacing - zOffset - 30);
                        Bounds islandBounds = new Bounds(islandPosition, new Vector3(islandSize, islandSize, islandSize));

                        // Set the remaining data for the island
                        islandData.bounds = islandBounds;
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
                halfIslands = numberOfIslands / 2f;
                xOffset = (halfIslands - 0.5f) * islandSpacing;
                zOffset = (halfIslands - 0.5f) * islandSpacing;
                // Add Islands Loop
                for (int i = 0; i < numberOfIslands; i++)
                {
                    for (int j = 0; j < numberOfIslands; j++)
                    {
                        // Create New Data
                        IslandData islandData = new IslandData(GridType.Type.Island);

                        // Set the position
                        Vector3 islandPosition = new Vector3(i * islandSpacing - xOffset, 0, j * islandSpacing - zOffset);

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
                        Bounds islandBounds = new Bounds(islandPosition, new Vector3(islandSize, islandSize, islandSize));

                        // Set the remaining data for the island
                        islandData.bounds = islandBounds;
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

        OnMapGenerated?.Invoke();
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

            // Proceed with adding the island as it's not being skipped
            Island island = new Island(data.islandType);
            island.islandConfig = islandConfig;
            island.buildings = data.buildings;
            island.IslandItems = data.items;
            island.bounds = data.bounds;
            island.id = GetNextIslandID();

            GameObject islandGO = Instantiate(islandPrefab);
            islandGO.transform.position = island.bounds.center;
            islandGO.name = data.name;
            island.islandObject = islandGO;

            islands.Add(island);

            GridSystem gridSystem = islandGO.GetComponent<GridSystem>();
            MapGrid mapGrid = islandGO.GetComponent<MapGrid>();

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



