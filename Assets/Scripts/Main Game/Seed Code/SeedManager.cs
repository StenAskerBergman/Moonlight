// Start - Seed Manager

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SeedManager : MonoBehaviour
{

    /* File Purpose:
     * SeedManager is responsible for managing the placement 
     * and permanence of seeds on islands. It uses a dictionary 
     * to associate each island with a list of seeds placed on it.
     */


    private Dictionary<Island, List<ItemData>> islandSeeds = new Dictionary<Island, List<ItemData>>();
    private const int MAX_SEEDS_PER_ISLAND = 3;

    public void InitializeSeedsOnIsland(Island island, List<ItemData> seeds)
    {
        if (!islandSeeds.ContainsKey(island))
        {
            islandSeeds[island] = new List<ItemData>();
        }

        foreach (var seed in seeds)
        {
            AddSeedToIsland(seed, island);
        }
    }

    public bool AddSeedToIsland(ItemData seed, Island island)
    {
        if (!islandSeeds.TryGetValue(island, out var seedsOnIsland))
        {
            seedsOnIsland = new List<ItemData>();
            islandSeeds[island] = seedsOnIsland;
        }

        if (seedsOnIsland.Count < MAX_SEEDS_PER_ISLAND && !seedsOnIsland.Contains(seed) && CanPlaceSeedOnIsland(seed, island))
        {
            seedsOnIsland.Add(seed);
            return true;
        }
        return false;
    }

    public bool CanProduceResource(Island island, ResourceProductionInfo productionInfo)
    {
        if (islandSeeds.TryGetValue(island, out var seedsOnIsland) && seedsOnIsland.Contains(productionInfo.requiredSeed))
        {
            return CheckIslandConditions(island, productionInfo);
        }
        return false;
    }

    private bool CheckIslandConditions(Island island, ResourceProductionInfo productionInfo)
    {
        // Logic to check if the island’s condition (temperature, ecology, etc.) allows for the production.
        // Example: 
        // return island.Temperature >= productionInfo.minTemperature && island.Temperature <= productionInfo.maxTemperature;

        return true; // Placeholder logic
    }

    public bool RemoveSeedFromIsland(ItemData seed, Island island)
    {
        if (islandSeeds.TryGetValue(island, out var seedsOnIsland) && seedsOnIsland.Contains(seed))
        {
            seedsOnIsland.Remove(seed);
            return true;
        }
        return false;
    }

    public List<ItemData> GetSeedsOnIsland(Island island)
    {
        // Make sure this method returns a valid list of ItemData objects.
        // Initialize an empty list rather than returning null.
        
        if (islandSeeds.TryGetValue(island, out var seedsOnIsland))
        {
            return seedsOnIsland;
        }
        else
        {
            return new List<ItemData>();  // Return an empty list if the island has no seeds
        }
    }

    public bool CanAddMoreSeeds(Island island)
    {
        return !islandSeeds.ContainsKey(island) || islandSeeds[island].Count < MAX_SEEDS_PER_ISLAND;
    }

    public int GetSeedCountOnIsland(Island island)
    {
        return islandSeeds.TryGetValue(island, out var seedsOnIsland) ? seedsOnIsland.Count : 0;
    }

    public void ClearSeedsOnIsland(Island island)
    {
        if (islandSeeds.ContainsKey(island))
        {
            islandSeeds[island].Clear();
        }
    }

    public bool CanPlaceSeedOnIsland(ItemData seed, Island island)
    {
        return true; // Placeholder logic
    }
}

[Serializable]
public class ResourceProductionInfo
{
    public ItemData requiredSeed; // The seed (key) needed for production.
    // Additional fields for required conditions...
    // public float minTemperature;
    // public float maxTemperature;
}

// End - Seed Manager
