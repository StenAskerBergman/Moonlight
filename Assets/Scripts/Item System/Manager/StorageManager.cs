using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StorageManager : MonoBehaviour
{
    // [SerializeField]
    protected Storage storage;

    // C# Constructor 
    public StorageManager(Storage storage)
    {
        this.storage = storage;
    }

    // Unity never calls a constructor with arguments. Declaring only the one above
    // removed the implicit parameterless constructor, so Unity could not run field
    // initializers on any StorageManager and every serialized default came up 0.
    public StorageManager() { }

    // Unity Constructor
    private void Awake()
    {
        EnsureStorage();
    }

    private void EnsureStorage()
    {
        if (storage == null)
        {
            storage = GetComponent<Storage>();
        }
    }

    public virtual int GetItemQuantity(ItemData itemData)
    {
        EnsureStorage();
        return storage != null ? storage.GetItemQuantity(itemData) : 0;
    }

    // Add & Remove methods
    public virtual void AddItem(ItemData itemData, int quantity)
    {
        TryAddItem(itemData, quantity);
    }

    public virtual bool TryAddItem(ItemData itemData, int quantity)
    {
        EnsureStorage();
        return storage != null && storage.TryAddItem(itemData, quantity);
    }

    public virtual bool RemoveItem(ItemData itemData, int quantity)
    {
        EnsureStorage();
        return storage != null && storage.RemoveItem(itemData, quantity);
    }


    // Validation methods
    public virtual bool CanAddItem(ItemData itemData, int quantity)
    {
        EnsureStorage();
        return storage != null && storage.CanAddItem(itemData, quantity);
    }

    public virtual bool CanRemoveItem(ItemData itemData, int quantity)
    {
        EnsureStorage();
        return storage != null && itemData != null && quantity > 0 && storage.GetItemQuantity(itemData) >= quantity;
    }

    // Capacity - Limit methods
    public virtual int GetCapacityLimit()
    {
        EnsureStorage();
        return storage != null ? storage.GetCapacityLimit() : 0;
    }
    public virtual int GetCapacityLeft(ItemData itemData)
    {
        EnsureStorage();
        // Note: If capacityLimit has a value, return it. Otherwise, return 0.
        if (storage == null) return 0;
        int itemCap = storage.GetCapacityLimit();
        if (itemCap <= 0) return int.MaxValue;
        return Mathf.Max(0, itemCap - storage.GetCurrentQuantity());
    }

    public virtual bool HasReachedCapacity(int quantityToAdd)
    {
        EnsureStorage();
        return storage != null && storage.HasReachedCapacity(quantityToAdd);
    }

    // StorageManager.cs
    // Get all items in storage Method
    public virtual Dictionary<ItemData, int> GetAllItems
    {
        get
        {
            EnsureStorage();
            return storage != null ? storage.GetAllItems() : new Dictionary<ItemData, int>();
        }
    }

    public virtual bool TryAddItems(IReadOnlyDictionary<ItemData, int> itemsToAdd)
    {
        EnsureStorage();
        return storage != null && storage.TryAddItems(itemsToAdd);
    }

    public virtual bool TryRemoveItems(IReadOnlyDictionary<ItemData, int> itemsToRemove)
    {
        EnsureStorage();
        return storage != null && storage.TryRemoveItems(itemsToRemove);
    }

    // Wrong Way to do it? Not sure why
    //public virtual Dictionary<ItemData, int> GetAllItemsB()
    //{
    //    return storage.GetAllItems();
    //}

    // Additional management logic can go here.

}

