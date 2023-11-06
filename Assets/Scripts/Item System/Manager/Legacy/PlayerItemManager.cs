using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*  
    File Role: managing all player items interactions on a Singular Island 

    Author: Sten

    The Island Item Manager script is responsible for  
    managing the items and buildings on an individual 
    island. It gets the Island object from the Game Manager 
    and initializes the resources and buildings based on 
    the Island's data.

*/


public class PlayerItemManager : MonoBehaviour
{
    // Add any additional player-specific resources here
    public int money = 1000;

    public int material1 = 10;
    public int material2 = 10;
    public int material3 = 10;

    //public int resource1 = 100;
    //public int resource2 = 100;
    //public int resource3 = 100;



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
        public bool SubtractMaterial(int cost)
        {
            if (material1 >= cost)
            {
                material1 -= cost;
                return true;
            }
            else
            {
                return false;
            }
        }

    #endregion


    #region Material interaction

        // Add a material to the player's Storage
        public bool AddMaterial(int amount)
        {
            material1 += amount;
            return true;
        }

        // Remove a material from the player's Storage
        public bool RemoveMaterial(int amount)
        {
            if (material1 >= amount)
        {
                material1 -= amount;
                return true;
            }
            else
            {
                return false;
            }
        }

        // New methods to interface with the IslandStorage for transactions:
        public bool AddResource(ItemEnums.ResourceType resource, int amount)
        {
            // Add the resource to player's inventory. You'll need to define the storage system for the player.
            return true;
        }

        public bool RemoveResource(ItemEnums.ResourceType resource, int amount)
        {
            // Remove the resource from the player's inventory.
            return true;
        }

    #endregion
}