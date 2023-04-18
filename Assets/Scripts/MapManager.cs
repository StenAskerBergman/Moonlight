using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        Linear,
        Circular,
        Square,
        Normal,
        Medium,
        Large,
    }
    
    [Header("Spawn Patterns")]
    public List<PatternData> patternDataList;
    [Space]
    public SpawnPattern selectedSpawnPattern;
    

    // Prefabs ... (and below the rest of your variables)
    [SerializeField] private GameObject islandPrefab; // The Current Island Object 
    [SerializeField] private GameObject waterObject; // Assuming that waterObject is a reference to the water game object
    
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
    void Start()
    {
        
        this.waterObject.SetActive(WaterOnStart); 
        waterObject.transform.localPosition = new Vector3(0f, waterHeight, 0f);

        islands = new List<Island>();
        nextIslandID = 1;
        gameManager = FindObjectOfType<GameManager>();
        

        switch (selectedSpawnPattern)
        {
            case SpawnPattern.Singular:
                // Singular Spawn
                for (int i = 0; i < 1; i++)
                {
                    // Singular Spawn
                    // Generate a Single new island
                    invertSelection = false;
                    IslandData islandData = new IslandData();
                    islandData.islandType = Enums.IslandType.None;
                    //islandData.buildings = new List<Building>();
                    //islandData.resources = new List<Enums.Resource>() { Enums.Resource.Resource1, Enums.Resource.Resource2 };
                    islandData.bounds = new Bounds(new Vector3(i * 0, 0, 0), new Vector3(10, 10, 10));
                    islandData.id = i + 1;
                    islandData.name = "Island " + 1;

                    AddIsland(islandData);
                }
                break;

            case SpawnPattern.Linear:
                // Linear Spawn
                for (int i = 0; i < numberOfIslands; i++)
                {
                    // Linear Spawn
                    // Generate a new island
                    IslandData islandData = new IslandData();
                    islandData.islandType = Enums.IslandType.None;
                    //islandData.buildings = new List<Building>();
                    //islandData.resources = new List<Enums.Resource>() { Enums.Resource.Resource1, Enums.Resource.Resource2 };
                    islandData.bounds = new Bounds(new Vector3(i * 20, 0, 0), new Vector3(10, 10, 10));
                    islandData.id = i + 1;
                    islandData.name = "Island " + (i + 1);

                    AddIsland(islandData);
                }
                break;

            case SpawnPattern.Circular:
                // Circular Spawn
                float worldLimit = 100f; // Define your world limit here
                float angleIncrement = 360f / numberOfIslands;

                for (int i = 0; i < numberOfIslands; i++)
                {
                    // Calculate the island's position in a circular pattern
                    float angle = i * angleIncrement * Mathf.Deg2Rad;
                    Vector3 islandPosition = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (worldLimit / 2);

                    // Generate a new island
                    IslandData islandData = new IslandData();
                    islandData.islandType = Enums.IslandType.None;
                    //islandData.buildings = new List<Building>();
                    //islandData.resources = new List<Enums.Resource>() { Enums.Resource.Resource1, Enums.Resource.Resource2 };
                    islandData.bounds = new Bounds(islandPosition, new Vector3(10, 10, 10));
                    islandData.id = i + 1;
                    islandData.name = "Island " + (i + 1);

                    AddIsland(islandData);
                }
                break;
                
            case SpawnPattern.Square:
                // Square Spawn
                int currentIsland = 0;
                float halfIslands = numberOfIslands / 2f;
                float xOffset = (halfIslands - 0.5f) * islandSpacing;
                float zOffset = (halfIslands - 0.5f) * islandSpacing;
                
                for (int i = 0; i < numberOfIslands; i++)
                {
                    for (int j = 0; j < numberOfIslands; j++)
                    {
                        // Generate a new island
                        IslandData islandData = new IslandData();
                        islandData.islandType = Enums.IslandType.None;
                        //islandData.buildings = new List<Building>();
                        //islandData.resources = new List<Enums.Resource>() { Enums.Resource.Resource1, Enums.Resource.Resource2 };

                        // Set the position and size of the island's bounds
                        Vector3 islandPosition = new Vector3(i * islandSpacing - xOffset, 0, j * islandSpacing - zOffset);
                        Bounds islandBounds = new Bounds(islandPosition, new Vector3(islandSize, islandSize, islandSize));

                        // Set the remaining data for the island
                        islandData.bounds = islandBounds;
                        islandData.id = currentIsland + 1;
                        islandData.name = "Island " + (currentIsland + 1);

                        // Add the island to the game world
                        AddIsland(islandData);

                        currentIsland++;
                    }
                }
                break;

            case SpawnPattern.Normal:
                // Square Spawn + Normal Sized Orbitor Islands
                currentIsland = 0;
                halfIslands = numberOfIslands / 2f;
                xOffset = (halfIslands - 0.5f) * islandSpacing;
                zOffset = (halfIslands - 0.5f) * islandSpacing;
                
                for (int i = 0; i < numberOfIslands; i++)
                {
                    for (int j = 0; j < numberOfIslands; j++)
                    {
                        // Generate a new island
                        IslandData islandData = new IslandData();
                        islandData.islandType = Enums.IslandType.None;
                        //islandData.buildings = new List<Building>();
                        //islandData.resources = new List<Enums.Resource>() { Enums.Resource.Resource1, Enums.Resource.Resource2 };

                        // Set the position and size of the island's bounds
                        Vector3 islandPosition = new Vector3(i * islandSpacing - xOffset, 0, j * islandSpacing - zOffset);
                        Bounds islandBounds = new Bounds(islandPosition, new Vector3(islandSize, islandSize, islandSize));

                        // Set the remaining data for the island
                        islandData.bounds = islandBounds;
                        islandData.id = currentIsland + 1;
                        islandData.name = "Island " + (currentIsland + 1);

                        // Add the island to the game world
                        AddIsland(islandData);

                        currentIsland++;
                    }
                }
                break;
                
            /*
            case SpawnPattern.Medium:
                // Circular Spawn
                // Your existing code for Circular Spawn
                break;

            case SpawnPattern.Large:
                // Custom1 Spawn
                // Your code for Custom1 Spawn
                break;
            */

            default:
                Debug.LogError("Incomplete or Invalid Spawn Pattern Selected!");
                Debug.LogWarning("Select New a Valid or Complete Pattern next time!");
                break;
        }
    }

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
        //island.buildings = data.buildings;

        // Convert the list of resources to a dictionary
        //Dictionary<Enums.Resource, int> resourceDictionary = new Dictionary<Enums.Resource, int>();
        /*foreach (Enums.Resource resource in data.resources)
        {
            if (resourceDictionary.ContainsKey(resource))
            {
                resourceDictionary[resource]++;
            }
            else
            {
                resourceDictionary.Add(resource, 1);
            }
        }*/
        //island.Resource = resourceDictionary;
        island.bounds = data.bounds;
        island.id = GetNextIslandID(); // set the id of the island
        
        // Create new game object for the island
        GameObject islandGO = Instantiate(islandPrefab);
        islandGO.transform.position = island.bounds.center;
        islandGO.name = data.name;
        // islandGO.transform.parent = this.transform; // island = transform.root.gameObject.GetComponent<Island>();

        // Set the GameObject reference in the Island class
        island.islandObject = islandGO;

        // Get the IslandResourceManager component and set its island field
        IslandResourceManager islandResourceManager = islandGO.GetComponent<IslandResourceManager>();
        if (islandResourceManager != null) 
        {
            islandResourceManager.SetIsland(island);
            islandResourceManager.island.id = data.id; // Sets ID
            
        }
        else 
        {
            Debug.LogError("IslandResourceManager component not found on islandPrefab.");
        }
        
        islands.Add(island); // add the island to the list after the ID has been assigned
    }
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

    private int GetNextIslandID()
    {
        //Debug.Log("nextIslandID = " + nextIslandID);
        return nextIslandID++;
    }

    // Other methods...

    public void RemoveIsland(Island island)
    {
        islands.Remove(island);
    }

    public Island GetIslandByName(string name)
    {
        return islands.Find(island => island.islandName == name);
    }

    public Enums.IslandType GetCurrentIslandType(Vector3 playerPosition)
    {
        foreach (Island island in islands)
        {
            if (island.bounds.Contains(playerPosition))
            {
                return island.islandType;
            }
        }
        
        return Enums.IslandType.None;
    }

    // Incase you need to copy or add a field value to each element in mass
    
    // void Update(){
    //     // Incase you need to copy or add a field value to each element in mass
    //     for (int i = 0; i < patternDataList.Count; i++)
    //     {
    //         patternDataList[i].displayName = patternDataList[i].patternName;
    //     }
    // }
}
