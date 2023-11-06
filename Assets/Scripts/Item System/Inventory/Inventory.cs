using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class Inventory : MonoBehaviour, IUniqueIdentifier
{
    // Unique ID
    public string ID { get; private set; }

    private StorageManager storageManager; // The controller

    public StorageManager storageManagerPrefab;  // Drag and drop the prefab in the inspector

    private void Awake()
    {
        ID = Guid.NewGuid().ToString();
        storageManager = GetComponent<StorageManager>();

        if (storageManager == null)
        {
            // Instantiate the prefab if it's not already on the game object
            Debug.Log("No manager found, creating one!");
            storageManager = Instantiate(storageManagerPrefab, transform);
        }
    }

    private void Start()
    {
        if (storageManager == null)
        {
            GetComponent<StorageManager>();
        }
    }

    #region Events & Delegates

    public delegate void ItemCountChangedHandler(ItemData itemData, int count);
    public event ItemCountChangedHandler OnItemCountChanged;

    public delegate void InventoryChange();
    public event InventoryChange OnInventoryChanged;

    #endregion

    #region Public Interaction Methods

    //  Print all items in the inventory rather than Get all items
    public void PrintAllItems()
    {
        foreach (KeyValuePair<ItemData, int> itemEntry in storageManager.GetAllItemsA)
        {
            Debug.Log(itemEntry.Key.name);
        }
    }

    public Dictionary<ItemData, int> GetAllItems()
    {
        return storageManager.GetAllItemsA;
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
        if (itemData == null) return false; Debug.Log("ItemData is null"); 
        return GetItemAmount(itemData) >= amount;
    }

    public void AddItem(ItemData itemData, int amount)
    {
        storageManager.AddItem(itemData, amount);
        OnInventoryChanged?.Invoke(); // Notify change
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
