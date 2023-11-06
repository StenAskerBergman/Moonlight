using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnitInventoryUI : InventoryUIBase
{
    public int ConsumableSlots = 1;
    private int ConsumableSlotsUsed = 0;

    private int currentTradeQuantity = 1;  // By default, trade 1 item

    public void SetTradeQuantityToOne() => currentTradeQuantity = 1;
    public void SetTradeQuantityToTen() => currentTradeQuantity = 10;
    public void SetTradeQuantityToMax() => currentTradeQuantity = 40;

    public List<ItemData> tradeableItems = new List<ItemData>();
    public List<ItemData> displayItems = new List<ItemData>();
    public void UpdateInventoryDisplay(Unit selectedUnit)
    {
        // Use the selectedUnit's inventory to update this UI.
        // Depending on the unit type, you can have specific variations in displaying items.
    }

    protected override void Start()
    {
        // Adjust the slots based on the unit's upgrades, ships, or other factors.
        // maxUISlots can be adjusted here or elsewhere as needed.

        base.Start();
    }

    // DONE: Add a button or Option to the UI to allow the player to throw items at sea by dragging item into ocean and letting it go. 
    // DONE: Add a button to allow player to select amount of items per click to trade with other units; Settings: 3 btns (1 - 10 - 40) 

    // TODO: ADD a button Slot System to this UI to allow the player to select what items to trade with other units.
    // TODO: ADD a button that adds a Option to switch end target to trade with, within units trading range. 
    // TODO: ADD a button Slot System tells the UI what items to show in the ui on every unit your inspecting the inventory on.

    // TODO: ADD a Indicator to show the player what unit they are trading with.
    // TODO: ADD a Indicator to show the player trade range their Unit can trading with.


    public override void RefreshInventoryDisplay()
    {
        ConsumableSlotsUsed = 0;

        Dictionary<ItemData, int> items = inventory.GetAllItems();

        foreach (var item in items)
        {
            // Find a suitable slot
            ItemSlotUI slot = FindSlotForItem(item.Key);

            if (slot != null)
            {
                slot.UpdateSlot(item.Key, item.Value);
            }
        }
    }

    private ItemSlotUI FindSlotForItem(ItemData itemData)
    {
        // This searches for an available slot that can hold the given item
        foreach (var slot in itemSlotContainer.GetComponentsInChildren<ItemSlotUI>())
        {
            if (slot.CanHold(itemData))
            {
                return slot;
            }
        }
        return null;
    }


    protected override void CreateItemSlot(KeyValuePair<ItemData, int> item)
    {
        switch (item.Key.type)
        {
            case ItemType.Consumable:
                if (ConsumableSlotsUsed >= ConsumableSlots)
                    return;  // No more equipment slots available
                ConsumableSlotsUsed++;
                break;
        }

        base.CreateItemSlot(item);
    }

    protected override void OnItemSlotClicked(ItemData clickedItem)
    {
        // Unit-specific item interactions can be placed here.
        // E.g., showing options for trading, throwing items, selling, buying, etc.
    }
}
