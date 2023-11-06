using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StorageManager : MonoBehaviour
{
    [SerializeField]
    protected Storage storage;

    // C# Constructor 
    public StorageManager(Storage storage)
    {
        this.storage = storage;
    }

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
        storage.AddItem(itemData, quantity);
    }

    public virtual bool RemoveItem(ItemData itemData, int quantity)
    {
        return storage.RemoveItem(itemData, quantity);
    }

    // Validation methods
    public virtual bool CanAddItem(ItemData itemData, int quantity)
    {
        // Maybe check against storage's capacity
        return !storage.HasReachedCapacity(quantity);

        /* Same As:
        if (storage.HasReachedCapacity(quantity))
        {
            return false;
        }
        return true;
        */
    }

    public virtual bool CanRemoveItem(ItemData itemData, int quantity)
    {
        return storage.GetItemQuantity(itemData) >= quantity;
    }

    // Capacity - Limit methods
    public virtual int GetCapacityLimit()
    {
        return storage.GetCapacityLimit();
    }

    public virtual bool HasReachedCapacity(int quantityToAdd)
    {
        return storage.HasReachedCapacity(quantityToAdd);
    }

    // Get all items in storage Method
    public virtual Dictionary<ItemData, int> GetAllItemsA
    {
        get
        {
            return storage.GetAllItems();
        }
    }

    // Wrong Way to do it? Not sure why
    public virtual Dictionary<ItemData, int> GetAllItemsB()
    {
        return storage.GetAllItems();
    }

    // Additional management logic can go here.
}


