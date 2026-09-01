using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

[System.Serializable]
public struct ItemEntry
{
    public ItemData key;
    public int value;
}

// Default Inventory System
[RequireComponent(typeof(StorageManager), typeof(Storage))]
public class Inventory : MonoBehaviour, IUniqueIdentifier
{
    // Unique ID
    public string ID { get; private set; }

    // The controllers for the storage
    private StorageManager storageManager;

    // Drag and drop the prefab in the inspector
    public StorageManager storageManagerPrefab;

    // New field for editor visibility
    public List<ItemEntry> itemListForEditor = new List<ItemEntry>();

    private void UpdateItemListForEditor()
    {
        itemListForEditor.Clear();
        var allItems = storageManager.GetAllItems; // Get the current state from the StorageManager
        foreach (var kvp in allItems)
        {
            itemListForEditor.Add(new ItemEntry { key = kvp.Key, value = kvp.Value });
        }
    }

    private void Awake()
    {
        ID = Guid.NewGuid().ToString();
        storageManager = GetComponent<StorageManager>();

        if (storageManager == null)
        {
            Debug.Log("<color=orange>Inventory: </color><color=yellow>StorageManager component not found, trying to instantiate from prefab.</color>");
            if (storageManagerPrefab != null)
            {
                storageManager = Instantiate(storageManagerPrefab, transform).GetComponent<StorageManager>();
                if (storageManager != null)
                {
                  //Debug.Log("StorageManager instantiated successfully.");
                    
                }
                else
                {
                  //Debug.LogError("Failed to instantiate StorageManager from prefab.");
                }
            }
            else
            {
                //Debug.LogError("StorageManager prefab is not assigned.");
            }
        }
        else
        {
            //Debug.Log("StorageManager component found.");
        }
    }


    private void Start()
    {
        if (storageManager == null)
        {
            // Not sure why we are randomly getting this but not assigning it, ok?
            storageManager = GetComponent<StorageManager>();
        }
    }

    #region Events & Delegates

    public delegate void ItemCountChangedHandler(ItemData itemData, int count);
    public event ItemCountChangedHandler OnItemCountChanged;

    public delegate void InventoryChange();
    public event InventoryChange OnInventoryChanged;

    #endregion

    #region Item Management - Public Interaction Methods

    //  Print all items in the inventory rather than Get all items
    public void PrintAllItems()
    {
        foreach (KeyValuePair<ItemData, int> itemEntry in storageManager.GetAllItems)
        {
            Debug.Log($"<color=orange>Inventory: </color><color=white>{itemEntry.Key.name}</color>");
        }
    }

    /// <summary>
    /// Returns all items of a specific inventory.
    /// </summary>
    /// <param name="itemData"></param>
    /// <returns>Dictionary<ItemData, int></returns>
    public Dictionary<ItemData, int> GetAllItems()
    {
        if (storageManager == null)
        {
            Debug.LogWarning("<color=orange>Inventory: StorageManager is being lazily initialized.</color>");
            storageManager = GetComponent<StorageManager>();
            if (storageManager == null && storageManagerPrefab != null)
            {
                storageManager = Instantiate(storageManagerPrefab, transform).GetComponent<StorageManager>();
            }
        }

        if (storageManager != null)
        {
            UpdateItemListForEditor(); // Ensure the editor list is updated
            return storageManager.GetAllItems;
        }
        else
        {
            Debug.LogError("<color=red>Inventory: StorageManager is still not initialized.</color>");
            return new Dictionary<ItemData, int>();
        }
    }

    /// <summary>
    /// Returns the amount of items of a specific type in the inventory.
    /// </summary>
    /// <param name="itemData"></param>
    /// <returns>Int</returns>
    public int GetItemAmount(ItemData itemData)
    {
        return storageManager.GetItemQuantity(itemData);
    }
    // idea: add boolean to check if item / amount is more than 0 or not
    
    /// <summary>
    /// Returns true if the inventory contains the item and the amount specified.
    /// </summary>
    /// <param name="itemData"></param>
    /// <param name="amount"></param>
    /// <returns>Bools: True: if has amount - False: if has nothing </returns>
    public bool HasItem(ItemData itemData, int amount)
    {
        if (itemData == null) { Debug.Log("<color=orange>Inventory: </color><color=red>ItemData is null</color>"); return false; } 
        return GetItemAmount(itemData) >= amount;
    }

    public bool CanRemove(ItemData item, int quantity)
    {
        // Implement logic to check if the item can be removed
        // For example, check if the inventory contains enough of the item
        return GetItemAmount(item) >= quantity;
    }

    /// <summary>
    /// Per-item stock ceiling, for UI fill bars and "max" labels. 0 means the storage
    /// manager isn't up yet or reports no limit; callers should treat that as unbounded
    /// and skip the bar rather than dividing by it.
    /// </summary>
    public int GetCapacityLimit() => storageManager != null ? storageManager.GetCapacityLimit() : 0;

    public bool CanAdd(ItemData item, int quantity)
    {
        // Was a copy of CanRemove (GetItemAmount(item) >= quantity), so it asked
        // "do I already hold this much?" and therefore refused every add into an
        // inventory that did not already contain the item - which made
        // TradeInteraction.ExecuteTrade fail against any empty inventory.
        // Capacity is the storage manager's business, so ask it.
        if (item == null || quantity <= 0) return false;
        return storageManager != null && storageManager.CanAddItem(item, quantity);
    }

    public void AddItem(ItemData itemData, int amount)
    {
        storageManager.AddItem(itemData, amount);
        OnInventoryChanged?.Invoke(); // Notify change

        // After adding the item, update the editor list
        UpdateItemListForEditor();

        Debug.Log($"<color=orange>Inventory: </color><color=green>Added </color><color=yellow>{amount}</color><color=green> of </color><color=white>{itemData.name}</color><color=green> to the Inventory of </color><color=white>{this.transform.name}</color><color=green>.</color>");
    }

    public bool RemoveItem(ItemData itemData, int amount)
    {
        bool removedSuccessfully = storageManager.RemoveItem(itemData, amount);
        if (removedSuccessfully)
        {
            OnInventoryChanged?.Invoke(); // Notify change
        }
        return removedSuccessfully;
    }

    #endregion
}
