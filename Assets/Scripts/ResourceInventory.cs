using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourceInventory
{
    // Define any private variables that you need to store the inventory data
    // Define any public methods to interact with the inventory data

    // Variables
    int stoneCount;
    
    // Methods
    public void AddWood(int amount)
    {
        // Code to add 'amount' of wood to the inventory
    }

    public bool HasStone(int amount)
    {
        // Code to check if there is at least 'amount' of stone in the inventory
        return (stoneCount >= amount);
        
    }


}
