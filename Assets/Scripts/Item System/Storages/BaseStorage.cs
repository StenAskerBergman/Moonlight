using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ItemEnums;

public class BaseStorage : Storage
{
    // TODO: Add a reference to the player and owner
    // public Player Player { get; set; }
    // public Owner owner { get; set; }

    public int baseCapacity;  // Default storage size for the player's base
    private int bonusCapacityFromStructures;
    private int otherEnhancementsSize; // Other enhancements or upgrades

    public int TotalCapacity
    {
        get { return baseCapacity + bonusCapacityFromStructures + otherEnhancementsSize; }
    }

    public delegate void OnFullCapacity();
    public event OnFullCapacity onFullCapacityEvent;

    public BaseStorage()
    {
        baseCapacity = 50;  // Default size for player's base
        bonusCapacityFromStructures = 0; // No constructed buildings at the start
        otherEnhancementsSize = 0; // Any other enhancements at the start (e.g., due to quests or milestones)
    }

    public void AddBonusCapacityFromStructure(int bonusCapacity)
    {
        bonusCapacityFromStructures += bonusCapacity;
    }

    public void RemoveBonusCapacityFromStructure(int bonusCapacity)
    {
        bonusCapacityFromStructures -= bonusCapacity;
    }

    // Used for any other type of storage enhancements, like milestones or quests
    public void AddOtherEnhancementsSize(int enhancementSize)
    {
        otherEnhancementsSize += enhancementSize;
    }

    // Overriding the AddItem method to add specific behaviors for Players BaseStorage
    public override void AddItem(ItemData itemData, int quantity)
    {
        if (HasCapacityForItems(quantity))
        {
            base.AddItem(itemData, quantity);
        }
        else
        {
            // Handle the scenario where the storage is full
            onFullCapacityEvent?.Invoke();
        }
    }

    public bool HasCapacityForItems(int quantity)
    {
        return (GetCurrentCapacity() + quantity) <= TotalCapacity;
    }


    public bool AddMultipleItems(Dictionary<ItemData, int> itemsToAdd)
    {
        int totalQuantityToAdd = itemsToAdd.Values.Sum();
        if (HasReachedCapacity(totalQuantityToAdd))
        {
            return false; // Not enough space to add all items
        }

        foreach (var item in itemsToAdd)
        {
            AddItem(item.Key, item.Value);
        }
        return true; // Successfully added all items
    }

    private int GetCurrentCapacity()
    {
        int currentCapacity = 0;
        foreach (var kvp in items)
        {
            currentCapacity += kvp.Value; // Assuming kvp.Value represents quantity
        }
        return currentCapacity;
    }

    // Inside BaseStorage class
    public Dictionary<ItemType, int> GetCurrentItems()
    {
        Dictionary<ItemType, int> currentItems = new Dictionary<ItemType, int>();

        foreach (var kvp in items)
        {
            currentItems[kvp.Key.type] = kvp.Value;
        }

        return currentItems;
    }

    public new void RemoveItem(ItemData itemData, int amount)
    {
        if (items.ContainsKey(itemData))
        {
            items[itemData] -= amount;
            if (items[itemData] <= 0)
            {
                items.Remove(itemData);
            }
        }
    }

}
