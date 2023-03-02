using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerResourceManager : ResourceManager
{
    // Add any additional player-specific resources here
    public int money = 1000;
    public int resource1 = 100;
    public int resource2 = 100;
    public int resource3 = 100;

    public int GetResourceCount(int islandID, Enums.Resource resource, int newCount)
    {
        // Find the IslandResourceManager for the specified islandID
        IslandResourceManager islandResourceManager = GameManager.instance.GetIslandResourceManager(islandID);
        if (islandResourceManager != null)
        {
            // Get the resource dictionary for the island
            Dictionary<Enums.Resource, int> resourceDict = islandResourceManager.island.Resource;
            
            // Check if the specified resource type exists in the dictionary
            if (resourceDict.ContainsKey(resource))
            {
                // Update the resource count for the specified resource type
                resourceDict[resource] = newCount;
            }
        }

        return newCount;
    }

    #region Money interaction

        // Subtract the cost of a building from the player's money
        public bool SpendMoney(int cost)
        {
            if (money >= cost)
            {
                money -= cost;
                return true;
            }
            else
            {
                return false;
            }
        }
        // Subtract the cost of a building from the player's Storage 
        public bool SubtractResource(int cost)
        {
            if (resource1 >= cost)
            {
                resource1 -= cost;
                return true;
            }
            else
            {
                return false;
            }
        }

    #endregion


}