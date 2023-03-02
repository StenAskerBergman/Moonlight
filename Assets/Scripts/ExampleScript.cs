using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleScript : MonoBehaviour
{
    private ResourceManager resourceManager;
    private IslandStorage islandStorage;

    private void Awake(){
        //int resource1 = FindObjectOfType<ResourceManager>().GetResourceCount(Enums.Island.Island1, Enums.Resource.resource1);
        //Debug.Log("Resource1 count on Island1: " + resource1);

        //IslandManager.AddIsland(islandData);

    }
    private void Start()
    {   
        //Island tutorialIsland = IslandManager.Instance.GetIslandByID(1);
        //Island someIsland = IslandManager.Instance.GetIslandByName("Some Island");


        /*
        ResourceInventoryManager inventoryManager = new ResourceInventoryManager();
        IslandStorage islandStorage = new IslandStorage();
        inventoryManager.AddIslandStorage(Enums.Island.Island1, islandStorage);
        resourceManager.AddIsland(Enums.Island.Island1);

        IslandStorage storedIslandStorage;
        if (inventoryManager.TryGetIslandStorage(Enums.Island.Island1, out storedIslandStorage))
        {
            // The IslandStorage object for Island1 exists and was returned in the out parameter
            // Do something with storedIslandStorage
        }
        else
        {
            // The IslandStorage object for Island1 does not exist
        }
        
        
        // Get a reference to the ResourceManager component
        resourceManager = FindObjectOfType<ResourceManager>();

        // Create a new island
        Island myIsland = new Island();

        // Add the island to the IslandStorage
        IslandStorage.AddIsland(myIsland);

        // Add some resources to the island
        ResourceManager.Instance.AddResourceToIsland(myIsland, Enums.Resource.Wood, 10);
        ResourceManager.Instance.AddResourceToIsland(myIsland, Enums.Resource.Stone, 5);
        ResourceManager.Instance.AddResourceToIsland(myIsland, Enums.Resource.Money, 100);
        
        Island myIsland = new Island(Enums.IslandType.Forest);
        IslandStorage.AddIsland(myIsland);

        IslandStorage storage = IslandStorage.GetIslandStorage(myIsland);
        storage.AddResource(Enums.Resource.Wood, 100);
        storage.AddResource(Enums.Resource.Stone, 50);
        storage.AddResource(Enums.Resource.Money, 200);
        

        // Create a new island and add it to the island resources dictionary
        Island myIsland = Enums.Island.MyIsland;
        IslandStorage.AddIsland(myIsland);

        // Get the island storage for the newly created island
        IslandStorage islandStorage = IslandStorage.GetIslandStorage(myIsland);

        // Add resources to the island storage
        islandStorage.AddResource(Enums.Resource.Wood, 100);
        islandStorage.AddResource(Enums.Resource.Stone, 50);
        islandStorage.AddResource(Enums.Resource.Money, 200);
        */

    }

    private void Update()
    {
        // Call the TransferResource method with the desired arguments
        //resourceManager.TransferResource(Enums.Island.Island1, Enums.Island.Island2, Enums.TransportType.Boat, Enums.Resource.Wood, 10);
    }
}

