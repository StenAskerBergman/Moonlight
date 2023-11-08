using CodeMonkey;
using System.Collections;
using System.Collections.Generic;
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

// tldr: responsible for spawning in the islands

public class MapManager : MonoBehaviour
{
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
        Square,
        Normal,
    }
    
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
    [Space]
    [SerializeField] private bool WaterOnStart;
    [SerializeField] private float waterHeight = 0f; // Replace with the correct height of your water
    
    [Space]
    
    
    [Space]
    [Header("Square Patterns Only")][Tooltip("Square Patterns Only")]
    [SerializeField] private float islandSpacing = 20f;
    [SerializeField] private float islandSize = 10f;
    [SerializeField] private int IslandHeight;

    private float xOffset, zOffset; // Calculate the offset needed to start the islands in the center of the scene

    [Header("Island Selection")]
    public bool invertSelection;

    [Tooltip("Max Island Amount: 49")]
    public List<int> currentIslandSelection; // Current Selected Islands

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

        switch (selectedSpawnPattern)
        {
            case SpawnPattern.Singular: // DEV ONLY
                // Singular Spawn

                float halfSize = islandSize / 2f;
                float QuarterSize = (halfSize / 2);
                float Offset = QuarterSize * -1; 
               
                for (int i = 0; i < 1; i++)
                {
                    // Singular Spawn

                    // Generate a Single new island
                    invertSelection = false;
                    IslandData islandData = new IslandData();

                    // Set the position and size of the island's bounds
                    Vector3 islandPosition = new Vector3(Offset, 0, Offset);
                    Bounds islandBounds = new Bounds(islandPosition, new Vector3(islandSize, islandSize, islandSize));
                    islandData.bounds = islandBounds;

                    islandData.islandType = IslandType.None;
                    islandData.id = i + 1;
                    islandData.name = "Island " + 1;

                    AddIsland(islandData); // Singular
                }
                break;

            //case SpawnPattern.Linear: // DEV ONLY
            //    // Linear Spawn
            //    for (int i = 0; i < numberOfIslands; i++)
            //    {
            //        // Linear Spawn
            //        // Generate a islands in a row
            //        IslandData islandData = new IslandData();
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
            //        IslandData islandData = new IslandData();
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
                        IslandData islandData = new IslandData();
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
                        IslandData islandData = new IslandData();

                        // Set the position
                        Vector3 islandPosition = new Vector3(i * islandSpacing - xOffset, 0, j * islandSpacing - zOffset);

                        // Set Size 
                        // Yet to be a Fully Written, "Game Option / Player Setting

                        // Set island type
                        islandData.islandType = IslandType.None; 

                        // Set buildings
                        islandData.buildings = new List<Building>();

                        // Set items
                        islandData.items = new Dictionary<ItemData, int>();

                        // Set the bounds
                        Bounds islandBounds = new Bounds(islandPosition, new Vector3(islandSize, islandSize, islandSize));

                        // Set the remaining data for the island
                        islandData.bounds = islandBounds;
                        islandData.id = currentIsland + 1;
                        islandData.name = "Island " + (currentIsland + 1);

                        // Generate Island Data

                        // Add the island to the game world
                        AddIsland(islandData); // Normal

                        currentIsland++;

                    }
                }

                // Creates Map Game Objects
                // > Complete

                // Creates Island Game Object
                // > Complete

                // Add Water Operation
                // Start - Complete

                // Add Plataeu Operation
                // Start - Complete

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
    }
    #endregion

    #region Island +/- Methods 

    // IMPORTANT: USES SPAWN PATTERNS 
    public void AddIsland(IslandData data)
    {
        if (currentIslandSelection.Contains(nextIslandID) && !invertSelection)
        {
            nextIslandID++;
            return;
        }
        
        if (!currentIslandSelection.Contains(nextIslandID) && invertSelection)
        {
            nextIslandID++;
            return;
        }
        
        Island island = new Island(data.islandType);
        
        island.islandConfig = islandConfig;  // Assign the appropriate IslandConfiguration here - Doesn't work, not sure why

        // Add Buildings at the games start to the island
        island.buildings = data.buildings;

        // Add Raw Resources & Seed items to the island
        island.IslandItems = data.items; 

        island.bounds = data.bounds;
        island.id = GetNextIslandID(); // set the id of the island
        
        // Create new game object for the island
        GameObject islandGO = Instantiate(islandPrefab);
        islandGO.transform.position = island.bounds.center;
        islandGO.name = data.name;

        // islandGO.transform.parent = this.transform; // island = transform.root.gameObject.GetComponent<Island>();

        // Set the GameObject reference in the Island class
        island.islandObject = islandGO;

        islands.Add(island); // adds the island to the MapManager private list after the ID has been assigned

        // Create the grid for the island
        GridSystem gridSystem = islandGO.GetComponent<GridSystem>();
    }

    public void RemoveIsland(Island island)
    {
        islands.Remove(island);
    }
    
    #endregion

    #region Remove Selection Methods
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
        //Debug.Log("nextIslandID = " + nextIslandID);
        return nextIslandID++;
    }
    #endregion

    #region Get Island Methods

    // Unique Id 
    public Island GetIslandUID(string ID)
    {
        return islands.Find(island => island.ID == ID);
    }
    // Spawn Id
    public Island GetIslandSID(int id)
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


