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
        capacityLimit = limit > 0 ? limit : (int?)null;
    }

    // Checks if the storage has reached its capacity limit
    public bool HasReachedCapacity(int quantityToAdd)
    {
        if (quantityToAdd <= 0) return false;

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
    public virtual bool CanAddItem(ItemData itemData, int quantity)
    {
        return itemData != null && quantity > 0 && !HasReachedCapacity(quantity);
    }

    public virtual bool TryAddItem(ItemData itemData, int quantity)
    {
        if (!CanAddItem(itemData, quantity)) return false;

        if (items.ContainsKey(itemData))
        {
            items[itemData] += quantity;
        }
        else
        {
            items.Add(itemData, quantity);
        }

        return true;
    }

    public virtual void AddItem(ItemData itemData, int quantity)
    {
        TryAddItem(itemData, quantity);
    }

    public virtual bool RemoveItem(ItemData itemData, int quantity)
    {
        if (itemData == null || quantity <= 0) return false;

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

    public bool TryAddItems(IReadOnlyDictionary<ItemData, int> itemsToAdd)
    {
        if (itemsToAdd == null || itemsToAdd.Count == 0) return true;

        int total = 0;
        foreach (KeyValuePair<ItemData, int> entry in itemsToAdd)
        {
            if (entry.Key == null || entry.Value <= 0) return false;
            total += entry.Value;
        }

        if (HasReachedCapacity(total)) return false;

        foreach (KeyValuePair<ItemData, int> entry in itemsToAdd)
        {
            if (!TryAddItem(entry.Key, entry.Value))
            {
                foreach (KeyValuePair<ItemData, int> rollback in itemsToAdd)
                {
                    if (rollback.Key == entry.Key) break;
                    RemoveItem(rollback.Key, rollback.Value);
                }
                return false;
            }
        }

        return true;
    }

    public bool TryRemoveItems(IReadOnlyDictionary<ItemData, int> itemsToRemove)
    {
        if (itemsToRemove == null || itemsToRemove.Count == 0) return true;

        foreach (KeyValuePair<ItemData, int> entry in itemsToRemove)
        {
            if (entry.Key == null || entry.Value <= 0 || GetItemQuantity(entry.Key) < entry.Value)
            {
                return false;
            }
        }

        foreach (KeyValuePair<ItemData, int> entry in itemsToRemove)
        {
            RemoveItem(entry.Key, entry.Value);
        }

        return true;
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
        return new Dictionary<ItemData, int>(items);
    }

    public int GetCurrentQuantity()
    {
        int total = 0;
        foreach (KeyValuePair<ItemData, int> entry in items) total += entry.Value;
        return total;
    }

    // Get Capacity Limit
    public virtual int GetCapacityLimit()
    {
        // If capacityLimit has a value, return it. Otherwise, return 0.
        return capacityLimit.HasValue ? capacityLimit.Value : 0; 
    }

    // Additional methods and properties common to all storages can go here...
}
