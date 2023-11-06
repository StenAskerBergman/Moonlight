// Start - InitializeIslands.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Initialize Resources on the Storage Component
// When Starting a New game on a island
// When Building on a New Island 



[System.Serializable]
public class ItemInitializer
{
    public ItemStatus Activated;    // Starting Status
    public ItemData itemData;       // Direct References
    public int initialValue;        // Starting Quantity

}

public class InitializeIslands : MonoBehaviour
{
    public IslandConfiguration islandConfig;

    public List<ItemInitializer> initialSeeds;
    public List<ItemInitializer> initialResources;

    private Island island;
    private IslandStorage islandStorage;

    private SeedManager seedManager;

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

    private void Start()
    {
        seedManager = FindObjectOfType<SeedManager>();
        if (seedManager == null)
        {
            LogError("IslandInitializer: Could not find SeedManager component");
            return; // exit the method if there's no storage
        }


        island = GetComponent<Island>();
        if (island == null)
        {
            LogError("IslandInitializer: Could not find Island component");
            return; // exit the method if there's no storage
        }

        islandStorage = GetComponent<IslandStorage>();

        if (islandStorage == null)
        {
            LogError("IslandInitializer: Could not find IslandStorage component");
            return; // exit the method if there's no storage
        }

        if (island && island.islandConfig)
        {
            InitializeSeeds(island.islandConfig.seeds);
            InitializeResources(island.islandConfig.resources);
        }
        else
        {
            LogError("Island or IslandConfig not set!");
        }
        
    }

    private void InitializeSeeds(List<ItemInitializer> seeds)
    {
        int totalPowerfulSeeds = island.islandConfig.totalPowerfulSeeds;
        int totalSeedsToInitialize = island.islandConfig.totalSeedsToInitialize;

        // Shuffle the seeds list to randomize which seeds are selected (optional)
        System.Random rng = new System.Random();
        var randomizedSeeds = seeds.OrderBy(a => rng.Next()).ToList();

        foreach (ItemInitializer seed in randomizedSeeds)
        {
            Log("Checking seed: " + seed.itemData.itemName + " with value: " + seed.initialValue);

            if (totalSeedsToInitialize > 0)
            {
                if (seed.itemData.powerLevel > 7 && totalPowerfulSeeds > 0)
                {
                    island.ActiveSeeds.Add(seed.itemData);
                    totalPowerfulSeeds--;
                    totalSeedsToInitialize--;
                }
                else if (seed.itemData.powerLevel <= 7)
                {
                    island.ActiveSeeds.Add(seed.itemData);
                    totalSeedsToInitialize--;
                }
            }
            else
            {
                // Stop once we've added the desired number of seeds
                break;
            }

            // Assuming IslandStorage is where you track the quantity of each seed
            islandStorage.AddItem(seed.itemData, seed.initialValue);
        }

        // Log if there were not enough seeds to fill the desired starting seeds per island
        if (totalSeedsToInitialize > 0)
        {
            LogWarning("Not enough seeds to fill the desired starting seeds per island. Seeds to initialize left: " + totalSeedsToInitialize);
        }

        // Create a list of ItemData from ItemInitializer
        List<ItemData> seedsData = seeds.Select(s => s.itemData).ToList();

        // Call the new method in SeedManager
        seedManager.InitializeSeedsOnIsland(island, seedsData);
    }


    private void InitializeResources(List<ItemInitializer> resources)
    {

        foreach (ItemInitializer resource in resources)
        {
            Log("Initializing resource: " + resource.itemData.itemName + " with value: " + resource.initialValue);

            // Just adds all
            islandStorage.AddItem(resource.itemData, resource.initialValue);
        }
    }
}
// End - InitializeIslands.cs 



// Legacy Code Below: 
// Prior versions
//private void InitializeSeeds(List<ItemInitializer> seeds)
//{
//    int totalActiveSeeds = 0;  // Track active seeds count
//
//    foreach (ItemInitializer seed in seeds)
//    {
//        Debug.Log("Initializing seed: " + seed.itemData.itemName + " with value: " + seed.initialValue);
//
//        if (seed.Activated == ItemStatus.Activated)
//        {
//            // Limit the number of active seeds to 3
//            if (totalActiveSeeds < 2)
//            {
//                island.ActiveSeeds.Add(seed.itemData);
//                totalActiveSeeds++;
//            }
//            else
//            {
//                Debug.LogWarning("Exceeded active seed limit. Seed: " + seed.itemData.itemName + " will be set as potential seed.");
//                island.PotentialSeeds.Add(seed.itemData);
//            }
//        }
//        else if (seed.Activated == ItemStatus.Deactivate || seed.Activated == ItemStatus.None)
//        {
//            island.PotentialSeeds.Add(seed.itemData);
//        }
//
//        // Assuming IslandStorage is where you track the quantity of each seed
//        islandStorage.AddItem(seed.itemData, seed.initialValue);
//    }
//}
