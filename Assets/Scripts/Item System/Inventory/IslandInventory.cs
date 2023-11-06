using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

/*  
    File Role: Managing Inventory of Items on an Island  

    Author: Sten 

    The IslandInventory script is responsible for managing 
    the inventory of items on a specific island. It provides 
    functionality to add and remove items from the inventory, 
    and each island is assigned a unique identifier.

    This script should be attached to objects representing 
    individual islands in the game world.

*/

public class IslandInventory : MonoBehaviour, IUniqueIdentifier
{
    public string ID { get; private set; }

    private void Awake()
    {
        ID = Guid.NewGuid().ToString();
    }

    public List<Item> items = new List<Item>();

    public void AddItem(ItemData itemData, int amount)
    {
        // Logic to add the item to the "items" list
    }

    public void RemoveItem(ItemData itemData, int amount)
    {
        // Logic to remove the item from the "items" list
    }
}
