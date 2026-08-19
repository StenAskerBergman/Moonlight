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
        Debug.Log("StorageManager: HasReachedCapacity: "+!storage.HasReachedCapacity(quantity));
        return !storage.HasReachedCapacity(quantity);
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
    public virtual int GetCapacityLeft(ItemData itemData)
    {
        // Old Code:
            // return GetItemQuantity - storage.GetCapacityLimit(); // Method Groups Can't do this equation, and ai says it can throw a exception error
        

        // Note: If capacityLimit has a value, return it. Otherwise, return 0.
        int capacityLeft, itemQuant, itemCap;

        itemQuant = GetItemQuantity(itemData); 
        
        itemCap = storage.GetCapacityLimit();

        return capacityLeft = itemQuant - itemCap;
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

    // Wrong Way to do it? Not sure why
    //public virtual Dictionary<ItemData, int> GetAllItemsB()
    //{
    //    return storage.GetAllItems();
    //}

    // Additional management logic can go here.

}


