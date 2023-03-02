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
    public GameObject islandPrefab;
    public int numberOfIslands = 5;
    public List<Island> islands { get; private set; }
    private int nextIslandID;
    private GameManager gameManager;

    void Start()
    {
        islands = new List<Island>();
        nextIslandID = 1;
        gameManager = FindObjectOfType<GameManager>();

        for (int i = 0; i < numberOfIslands; i++)
        {
            // Generate a new island
            IslandData islandData = new IslandData();
            islandData.islandType = Enums.IslandType.None;
            islandData.buildings = new List<Building>();
            islandData.resources = new List<Enums.Resource>() { Enums.Resource.Resource1, Enums.Resource.Resource2 };
            islandData.bounds = new Bounds(new Vector3(i * 20, 0, 0), new Vector3(10, 10, 10));
            islandData.id = i + 1;
            islandData.name = "Island " + (i + 1);

            AddIsland(islandData);
        }
    }

    public void AddIsland(IslandData data)
    {
        Island island = new Island(data.islandType);
        island.buildings = data.buildings;

        // Convert the list of resources to a dictionary
        Dictionary<Enums.Resource, int> resourceDictionary = new Dictionary<Enums.Resource, int>();
        foreach (Enums.Resource resource in data.resources)
        {
            if (resourceDictionary.ContainsKey(resource))
            {
                resourceDictionary[resource]++;
            }
            else
            {
                resourceDictionary.Add(resource, 1);
            }
        }
        island.Resource = resourceDictionary;

        island.bounds = data.bounds;
        island.id = GetNextIslandID(); // set the id of the island

        // Create new game object for the island
        GameObject islandGO = Instantiate(islandPrefab);
        islandGO.transform.position = island.bounds.center;
        islandGO.name = data.name;

        // Set the GameObject reference in the Island class
        island.islandObject = islandGO;

        // Get the IslandResourceManager component and set its island field
        IslandResourceManager islandResourceManager = islandGO.GetComponent<IslandResourceManager>();
        if (islandResourceManager != null) 
        {
            islandResourceManager.SetIsland(island);
        }
        else 
        {
            Debug.LogError("IslandResourceManager component not found on islandPrefab.");
        }

        islands.Add(island); // add the island to the list after the ID has been assigned
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
}
