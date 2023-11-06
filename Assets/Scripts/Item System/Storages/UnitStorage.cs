using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitStorage : Storage
{
    private const int MAX_STACK_SIZE = 40;
    private const int MAX_NORMAL_SLOTS = 4;
    private const int MAX_CONSUMABLE_SLOTS = 2;

    private Dictionary<ItemType, int> occupiedSlots = new Dictionary<ItemType, int>
    {
        { ItemType.Normal, 0 },
        { ItemType.Consumable, 0 }
    };

    public override void AddItem(ItemData itemData, int quantity)
    {
        if (CanAddSpecificItem(itemData, quantity))
        {
            base.AddItem(itemData, quantity);

            switch (itemData.type)
            {
                case ItemType.Normal:
                    occupiedSlots[ItemType.Normal]++;
                    break;
                case ItemType.Consumable:
                    occupiedSlots[ItemType.Consumable]++;
                    break;
            }
        }
        else
        {
            Debug.LogWarning("Can't add item due to storage constraints: " + itemData.displayName);
        }
    }

    public bool CanAddSpecificItem(ItemData itemData, int quantity)
    {
        if (quantity > MAX_STACK_SIZE)
        {
            return false;
        }

        // Check for slot availability based on item type.
        switch (itemData.type)
        {
            case ItemType.Normal:
                if (occupiedSlots[ItemType.Normal] + 1 > MAX_NORMAL_SLOTS) // +1 because we are checking if we can add an item to an empty slot
                {
                    return false;
                }
                break;
            case ItemType.Consumable:
                if (occupiedSlots[ItemType.Consumable] + 1 > MAX_CONSUMABLE_SLOTS)
                {
                    return false;
                }
                break;
        }

        return true;
    }

    public override bool RemoveItem(ItemData itemData, int quantity)
    {
        if (base.RemoveItem(itemData, quantity))
        {
            switch (itemData.type)
            {
                case ItemType.Normal:
                    occupiedSlots[ItemType.Normal]--;
                    break;
                case ItemType.Consumable:
                    occupiedSlots[ItemType.Consumable]--;
                    break;
            }

            return true;
        }

        return false;
    }
}
