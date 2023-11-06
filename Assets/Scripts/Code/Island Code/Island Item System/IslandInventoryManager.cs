using System.Collections.Generic;
using UnityEngine;

/*  
    File Role: Managing all Base Inventories on a singular island  
    
    Author: Sten

    The Island Player Manager script handles player inventories on a single island.
    It communicates with other systems about events on a specific island.

    For example, it informs another island item manager when any player constructs 
    a building on the island or when a player purchases shares of a base on the island.

    This script is attached to the Island prefab.
*/

public class IslandInventoryManager
{
    private Dictionary<string, IslandStorage> inventories = new Dictionary<string, IslandStorage>();

    public void AddIslandStorage(string islandID, IslandStorage inventory)
    {
        inventories[islandID] = inventory;
    }

    public IslandStorage GetIslandStorage(string islandID)
    {
        inventories.TryGetValue(islandID, out var storage);
        return storage;
    }

    public bool TryGetIslandStorage(string islandID, out IslandStorage islandStorage)
    {
        return inventories.TryGetValue(islandID, out islandStorage);
    }

}