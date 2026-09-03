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
        if (!storage)
        {
            storage = GetComponent<Storage>(); 
        }
    }

    public virtual int GetItemQuantity(ItemData itemData)
    {
        return storage.GetItemQuantity(itemData);
    }

    // Add & Remove methods
    public virtual void AddItem(ItemData itemData, int quantity)
    {
        TryAddItem(itemData, quantity);
    }

    public virtual bool TryAddItem(ItemData itemData, int quantity)
    {
        return storage != null && storage.TryAddItem(itemData, quantity);
    }

    public virtual bool RemoveItem(ItemData itemData, int quantity)
    {
        return storage.RemoveItem(itemData, quantity);
    }


    // Validation methods
    public virtual bool CanAddItem(ItemData itemData, int quantity)
    {
        return storage != null && storage.CanAddItem(itemData, quantity);
    }

    public virtual bool CanRemoveItem(ItemData itemData, int quantity)
    {
        return storage != null && itemData != null && quantity > 0 && storage.GetItemQuantity(itemData) >= quantity;
    }

    // Capacity - Limit methods
    public virtual int GetCapacityLimit()
    {
        return storage.GetCapacityLimit();
    }
    public virtual int GetCapacityLeft(ItemData itemData)
    {
        // Old Code:
            // return GetItemQuantity - storage.GetCapacityLimit(); // Method Groups Can't do this equation, and ai says it can throw a exception error
        

        // Note: If capacityLimit has a value, return it. Otherwise, return 0.
        if (storage == null) return 0;
        int itemCap = storage.GetCapacityLimit();
        if (itemCap <= 0) return int.MaxValue;
        return Mathf.Max(0, itemCap - storage.GetCurrentQuantity());
    }

    public virtual bool HasReachedCapacity(int quantityToAdd)
    {
        return storage.HasReachedCapacity(quantityToAdd);
    }

    // StorageManager.cs
    // Get all items in storage Method
    public virtual Dictionary<ItemData, int> GetAllItems
    {
        get
        {
            return storage.GetAllItems();
        }
    }

    public virtual bool TryAddItems(IReadOnlyDictionary<ItemData, int> itemsToAdd)
    {
        return storage != null && storage.TryAddItems(itemsToAdd);
    }

    public virtual bool TryRemoveItems(IReadOnlyDictionary<ItemData, int> itemsToRemove)
    {
        return storage != null && storage.TryRemoveItems(itemsToRemove);
    }

    // Wrong Way to do it? Not sure why
    //public virtual Dictionary<ItemData, int> GetAllItemsB()
    //{
    //    return storage.GetAllItems();
    //}

    // Additional management logic can go here.

}

