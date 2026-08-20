using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemInteraction : MonoBehaviour, IItemManagement
{
    public Inventory inventory;

    // Define a delegate and event for interaction occurrences
    public delegate void ItemInteractionHandler(string message);
    public event ItemInteractionHandler OnInteractionOccurred;

    private void Awake()
    {
        inventory = GetComponent<Inventory>();
        if (inventory == null) Debug.LogError("<color=red>ItemInteraction: Unit Inventory not found</color>");
    }

    public void AddItem(ItemData item, int quantity)
    {
        // TODO: Determine Inventory Type
        inventory.AddItem(item, quantity);
        OnInteractionOccurred?.Invoke($"Added {quantity} {item.displayName} to inventory");
    }

    public void RemoveItem(ItemData item, int quantity)
    {
        if (inventory.RemoveItem(item, quantity))
        {
            OnInteractionOccurred?.Invoke($"Removed {quantity} {item.displayName} from inventory");
        }
        else
        {
            OnInteractionOccurred?.Invoke($"Failed to remove {quantity} {item.displayName}");
        }
    }

    public void UseItem(ItemData item, int quantity)
    {
        if (inventory.RemoveItem(item, quantity))
        {
            // Implement item usage logic
            OnInteractionOccurred?.Invoke($"Used {quantity} {item.displayName}");
        }
        else
        {
            OnInteractionOccurred?.Invoke($"Failed to use {quantity} {item.displayName}");
        }
    }

    public void ThrowItemAtSea(ItemData item, int quantity)
    {
        if (inventory.RemoveItem(item, quantity))
        {
            // TODO: Add visual representation and logic
            OnInteractionOccurred?.Invoke($"Threw {quantity} {item.displayName} into the sea");
        }
        else
        {
            OnInteractionOccurred?.Invoke($"Failed to throw {quantity} {item.displayName}");
        }
    }

    // Additional methods as needed...
}
