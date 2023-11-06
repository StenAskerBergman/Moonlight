using System.Collections.Generic;
using UnityEngine;

public class UnitStorageManager : StorageManager
{
    private const int MAX_STACKS = 40;
    private const int MAX_NORMAL_SLOTS = 4;
    private const int MAX_CONSUMABLE_SLOTS = 2;

    private int totalStacks = 0;
    private Dictionary<ItemType, int> usedSlots = new Dictionary<ItemType, int>
    {
        { ItemType.Normal, 0 },
        { ItemType.Consumable, 0 }
    };

    // Constructor
    public UnitStorageManager(Storage storage) : base(storage) {}

    public override void AddItem(ItemData itemData, int quantity)
    {
        if (CanAddItem(itemData, quantity))
        {
            storage.AddItem(itemData, quantity);

            switch (itemData.type)
            {
                case ItemType.Normal:
                    usedSlots[ItemType.Normal]++;
                    break;
                case ItemType.Consumable:
                    usedSlots[ItemType.Consumable]++;
                    break;
            }

            totalStacks += quantity;
        }
        else
        {
            Debug.LogWarning("Can't add item: " + itemData.displayName);
        }
    }

    public override bool CanAddItem(ItemData itemData, int quantity)
    {
        // Check for total stacks
        if (totalStacks + quantity > MAX_STACKS)
        {
            return false;
        }

        // Check for item slot type availability
        switch (itemData.type)
        {
            case ItemType.Normal:
                if (usedSlots[ItemType.Normal] >= MAX_NORMAL_SLOTS)
                {
                    return false;
                }
                break;
            case ItemType.Consumable:
                if (usedSlots[ItemType.Consumable] >= MAX_CONSUMABLE_SLOTS)
                {
                    return false;
                }
                break;
        }

        return base.CanAddItem(itemData, quantity);
    }

    public override bool RemoveItem(ItemData itemData, int quantity)
    {
        if (base.RemoveItem(itemData, quantity))
        {
            switch (itemData.type)
            {
                case ItemType.Normal:
                    usedSlots[ItemType.Normal]--;
                    break;
                case ItemType.Consumable:
                    usedSlots[ItemType.Consumable]--;
                    break;
            }

            totalStacks -= quantity;
            return true;
        }

        return false;
    }
}
