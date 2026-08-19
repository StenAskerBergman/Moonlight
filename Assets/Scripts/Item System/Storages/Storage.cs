using System.Collections.Generic;
using UnityEngine;

/* 
    Encapsulation Principle: 
 
        No Direct Contact with Storage without Manager

 */

public abstract class Storage : MonoBehaviour
{
    // Key thing to pay attention to here, is that we are creating a dictionary
    // Using itemData instead of item, since this class doesnt need to stack the
    // Same item over and over. Hence why we added UnitStorage.

    protected Dictionary<ItemData, int> items = new Dictionary<ItemData, int>();
    protected int? capacityLimit = null; // By default, there's no capacity limit.

    // Sets Limit - When needed
    public void SetCapacityLimit(int limit)
    {
        capacityLimit = limit;
    }

    // Checks if the storage has reached its capacity limit
    public bool HasReachedCapacity(int quantityToAdd)
    {
        // No Value > Not Full > Return false
        if (!capacityLimit.HasValue)
            return false;

        int currentTotalQuantity = 0;

        foreach (var entry in items)
        {
            currentTotalQuantity += entry.Value;
        }

        return currentTotalQuantity + quantityToAdd > capacityLimit;
    }

    // Add & Remove methods
    public virtual void AddItem(ItemData itemData, int quantity)
    {
        if (items.ContainsKey(itemData))
        {
            items[itemData] += quantity;
        }
        else
        {
            items.Add(itemData, quantity);
        }
    }

    public virtual bool RemoveItem(ItemData itemData, int quantity)
    {
        if (items.ContainsKey(itemData) && items[itemData] >= quantity)
        {
            items[itemData] -= quantity;

            if (items[itemData] <= 0)
            {
                items.Remove(itemData);
            }

            return true;
        }

        return false;
    }

    // Get Methods
    public int GetItemQuantity(ItemData itemData)
    {
        if (itemData == null)
        {
            // Debug.LogWarning("Trying to get item quantity with null ItemData.");
            return 0;
        }

        if (items.ContainsKey(itemData))
        {
            return items[itemData];
        }
        return 0;
    }

    public Dictionary<ItemData, int> GetAllItems()
    {
        return items;
    }

    // Get Capacity Limit
    public int GetCapacityLimit()
    {
        // If capacityLimit has a value, return it. Otherwise, return 0.
        return capacityLimit.HasValue ? capacityLimit.Value : 0; 
    }

    // Additional methods and properties common to all storages can go here...
}
