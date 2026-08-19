// Start - TradeMenu.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TradeMenu : MonoBehaviour
{
    // Reference to the TradeInteraction component of the trading unit
    private TradeInteraction tradeInteraction;

    // UI elements, such as item slots, buttons, etc.
    public GameObject tradeMenuUI; // The trade menu panel
    public ItemSlot[] playerItemSlots; // Item slots for player's items
    public ItemSlot[] npcItemSlots; // Item slots for NPC's items
    public Button finalizeTradeButton; // Button to finalize the trade
    private UnitInventoryUI UnitInventory; // Current Selected Unit

    void Awake()
    {
        // Initialize references to UI elements here, if needed
        UnitInventory = FindObjectOfType<UnitInventoryUI>(); // Find the UnitInventoryUI in the scene
        // Not good solution considering that several different are used so there will be a problems, here...

        // Need a independant System to indicate which unit is selected, maybe run a even6t or something...

        // No sure How this will work?                                                                           
        // Maybe a run a Event from the
        // UnitInventoryUI.cs to change                                  
        // the amount everywhere for one
        // single player and nobody else

    }

    // TradeMenu.cs
    public void ConfirmTrade()
    {
        // How can I trade when we yet to implemented different owners? ... Lets await Owner Adjustment
        //ItemData selectedItem = GetSelectedItem();
        //int quantity = UnitInventoryUI.CurrentTradeQuantity;

        //if (ShowConfirmationDialog($"Trade {quantity} {selectedItem.displayName}?"))
        //{
        //    tradeInteraction.ExecuteTrade(targetInventory, selectedItem, quantity);
        //}
    }

    public int GetSelectedQuantity()
    {
        return UnitInventory.CurrentTradeQuantity;
    }

    private bool ShowConfirmationDialog(string message)
    {
        // Implement a confirmation dialog and return the user's choice
        return true; // Placeholder
    }

    public void Open()
    {
    //     Enable and display the trade menu UI
    //     tradeMenuUI.SetActive(true);

    //     Populate the item slots with items from both player and NPC inventories
    //     PopulateItemSlots();

    //     Add listeners to UI elements, such as item slots and buttons
    //     finalizeTradeButton.onClick.AddListener(FinalizeTrade);

    //     tradeMenuUI.SetActive(true);

    //     There is no way to tell what the current Unit is...
    //     PopulateItemSlots(UnitInventoryUI.CurrentUnitInventory, null); // Populate with current unit's inventory

    //     Add additional initialization...
    }

    public void Close()
    {
    //     Disable and hide the trade menu UI
    //     tradeMenuUI.SetActive(false);

    //     Clear item slots and remove listeners
    //     ClearItemSlots();
    //     finalizeTradeButton.onClick.RemoveListener(FinalizeTrade);

    //     tradeMenuUI.SetActive(false);
    //     Clear slots and additional cleanup...
    }

    public void PopulateItemSlots(Inventory playerInventory, Inventory npcInventory = null)
    {
        // Populate slots for player inventory
        PopulateSlots(playerItemSlots, playerInventory.GetAllItems());

        // Optionally, populate for NPC inventory
        if (npcInventory != null)
        {
            PopulateSlots(npcItemSlots, npcInventory.GetAllItems());
        }
    }

    private void PopulateSlots(ItemSlot[] slots, Dictionary<ItemData, int> items)
    {
        int index = 0;
        foreach (var item in items)
        {
            if (index < slots.Length)
            {
                slots[index].InitializeSlot(item.Key, item.Value);
                index++;
            }
        }

        for (int i = index; i < slots.Length; i++)
        {
            // Clear slots somehow...
            // slots[i].ClearSlot();
        }
    }

    private void ClearItemSlots()
    {
        // Logic to clear item slots

        foreach (var slot in playerItemSlots)
        {
            // Clear slots somehow...
            // slot.ClearSlot();
        }
    }

    public void ExecuteTrade()
    {
        // Gather selected items from player and NPC
        var selectedPlayerItems = GetSelectedItems(playerItemSlots);
        var selectedNpcItems = GetSelectedItems(npcItemSlots);

        // Perform the trade
        foreach (var item in selectedPlayerItems)
        {
            // There are NO OWNER CLASSES YET SO WE CANT TRADE AT THE MOST BASIC FUNDAMENTAL LEVEL!
            // tradeInteraction.TradeItem(targetNpcUnit, item.Key, item.Value);
        }

        foreach (var item in selectedNpcItems)
        {
            // Assuming a method to handle NPC's part of the trade
            // npcUnit.TradeItem(playerUnit, item.Key, item.Value);
        }

        Close(); // Close the trade menu after trade
    }

    private Dictionary<ItemData, int> GetSelectedItems(ItemSlot[] slots)
    {
        var selectedItems = new Dictionary<ItemData, int>();
        foreach (var slot in slots)
        {
            //if (slot.IsSelectedForTrade())
            //{
            //    // Since quantity is now managed at the UnitStorage level, we retrieve it from there.
            //    int quantity = unitStorageManager.GetItemQuantity(slot.GetItemData());
            //    selectedItems.Add(slot.GetItemData(), quantity);
            //}
        }
        return selectedItems;
    }


    public void FinalizeTrade()
    {
        // Call the FinalizeTrade method in TradeInteraction
        if (tradeInteraction != null)
        {
            tradeInteraction.FinalizeTrade();
        }

        // Close the trade menu after finalizing the trade
        Close();
    }

    // Additional methods for handling trade menu interactions can be added here

    // Method to assign the TradeInteraction component
    public void AssignTradeInteraction(TradeInteraction interaction)
    {
        tradeInteraction = interaction;
    }
}
// End - TradeMenu.cs
