using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System;

public class UnitInventoryUI : InventoryUserface
{
    // For Editing Unit Names post Creation
    public string newName;
    public Button setNameButton;
    [Space(10)]

    public Text unitDisplayText;
    public Inventory inventory;
    public GameObject Inspected;
    [Space(10)]

    // [SerializeField] - Randomly Causes Errors? Whatever
    public string CurrentDisplayText;
    
    // Current Selection Displayed
    public UnitInventory unitInventory;
    public ItemSlot[] itemSlots;
    public Unit unit;

    // Current Stack Positions
    protected Dictionary<ItemStack, Vector2> stacksPos = new Dictionary<ItemStack, Vector2>();

    // Current Slot Positions
    protected Dictionary<ItemSlot, Vector2> slotPos = new Dictionary<ItemSlot, Vector2>();

    private int currentTradeQuantity = 1;
    public int CurrentTradeQuantity { get; private set; } = 1;

    public void UpdateBasedOnSelection(Unit selected_unit)
    {
        if (UnitSelections.Instance.unitsSelected.Count > 0 || selected_unit != null)
        {
            if (selected_unit != null)
            {
                SetUnitInventory(selected_unit.unitInventory ?? selected_unit.GetComponent<UnitInventory>());
                SetInventory(selected_unit.inventory ?? selected_unit.GetComponent<Inventory>());
            } 
            else
            {
                Unit selectedUnit = UnitSelections.Instance.unitsSelected[0]; // or however you determine the relevant unit - Unit Selection Priority
                if (selectedUnit != null)
                {
                    // If you also need to set the general 
                    SetUnitInventory(selectedUnit.unitInventory ?? selectedUnit.GetComponent<UnitInventory>());
                }
                else
                {
                    Debug.LogError("No UnitInventory found");
                    ClearSlots();
                }
            }
        }
        else
        {
            ClearSlots(); // No units are selected
        }
    }

    public override void SetUnitInventory(UnitInventory newUnitInventory)
    {
        this.unitInventory = newUnitInventory;

        // Update the slot's UnitInventory data
        foreach (var slot in inventorySlots)
        {
            slot.unitInventory = newUnitInventory;

            Debug.Log("<color=white>UnitInventoryUI:</color> <color=green>Succesful Set unitInventory! unitInventory: </color>" + unitInventory.name + " unitInventory.ID: " + unitInventory.ID);
        }
    }

    public override void SetInventory(Inventory newInventory)
    {
        this.inventory = newInventory;
    }

    public void SetUnit(Unit newUnit)
    {
        this.unit = newUnit;
        Debug.Log("<color=green>UnitInventoryUI: Succesful Set Unit! Unit: </color>" + unit.name + " Unit.ID: " + unit.ID);
    }


    public void EditName()
    {
        String key = "";
        bool edit = true;

        if (edit)
        {
            // Check if User is Done
            if (Input.GetKeyDown(KeyCode.Return)) edit = false;

            // Get the current keyboard input + adds it to Key String
            key += Input.anyKeyDown;

            
        } else if (!edit)
        {

            // Log Name
            Debug.Log($"Requesting Name Change: {key}");

            // Set Name
            UpdateDisplayName(key);
        }
    }

    public void SetInspection(GameObject gameObject)
    {
        this.Inspected = gameObject;
        Debug.Log("Inspected GameObject Updated: " + Inspected.name);

    }

    protected override void Start()
    {
        base.Start();
        // Additional initialization if needed
    }

    public void UpdateDisplayName(string unitName)
    {
        if (unitDisplayText != null)
        {
            unitDisplayText.text = unitName;
            CurrentDisplayText = unitName;
            Debug.Log("Unit name updated: " + CurrentDisplayText);
        }
        else
        {
            Debug.LogError("Unit name text UI element not set!");
        }
    }

    public void SetTradeQuantityToOne() => UpdateTradeQuantity(1);
    public void SetTradeQuantityToTen() => UpdateTradeQuantity(10);
    public void SetTradeQuantityToMax() => UpdateTradeQuantity(40); // Assuming 40 is the max

    private void UpdateTradeQuantity(int quantity)
    {
        CurrentTradeQuantity = quantity;
    }

    // UnitInventoryUI.cs
    public override void RefreshInventoryDisplay()
    {
        // Make sure to assign the inventories before refreshing.
        // AssignInventoriesIfNeeded();

        // Now you can safely call the base refresh logic.
        base.RefreshInventoryDisplay();

        // Clear previous slots
        ClearSlots();

        if (inventory == null && unitInventory == null)
        {
            // This shouldn't be called if nothing has been selected...
            Debug.Log("<color=yellow>UnitInventoryUI: RefreshInventoryDisplay():No inventory to display Yet!</color>"); 
            return; // Exit the method if both inventories are null.
        }

        // Check if UnitInventory or Inventory is available.
        if (unitInventory != null)
        {
            // Set the inspected object && unit - Good Idea but Wrong Place
            //SetInspection(unitInventory.gameObject);
            //SetUnit(unitInventory.gameObject.GetComponent<Unit>());

            // Debug.Log("<color=green>UnitInventoryUI: Succesful Set Units Display Name! Unit.displayName: </color>" + unit.displayName);

            // Refresh display based on UnitInventory
            var items = unitInventory.GetAllItems();
            // Fetch items from inventory and update UI slots.
            UpdateSlotsWithItems(items);
        }
        else if (inventory != null)
        {
            // Fallback to Inventory if UnitInventory is not available.
            var items = inventory.GetAllItems();
            UpdateSlotsWithItems(items);
        }
        else
        {
            Debug.LogError("<color=red>UnitInventoryUI: No inventory or unit inventory available</color>");
        }
    }
    public void RefreshSlots() {
    ClearSlots(); // Ensure all slots are cleared initially.

    if (unitInventory == null && inventory == null) {
        Debug.LogError("No inventory found.");
        return;
    }

    var items = unitInventory?.GetAllItems() ?? inventory?.GetAllItems();
    if (items == null) {
        Debug.LogError("No items to display.");
        return;
    }

    int index = 0;
    foreach (var item in items) {
        if (index >= itemSlots.Length) break; // Avoid exceeding slot array.
        itemSlots[index].InitializeSlot(item.Key, item.Value);
        index++;
    }

    for (int i = index; i < itemSlots.Length; i++) {
        itemSlots[i].ClearSlot(); // Clear any remaining slots.
    }
}

    // UnitInventoryUI.cs - Resets all its ItemSlots & then copies the UnitInventory.cs onto its
    // own list or array of ItemSlots. Its own slots are the ones that display the unitInventory
    // content. Think of ItemSlots as windows which reflects the Unit Inventory content. 

    // UnitInventoryUI.cs - Resets ItemSlots with content from its UnitInventory.cs Reference
    //public void SetItemSlots()
    //{
    //    if (unitInventory == null)
    //    {
    //        Debug.LogError("No UnitInventory set.");
    //        return;
    //    }

    //    // Only assign the inventories if they are not already set prior to this method in this class.
    //    if (unitInventory == null) { unitInventory = UnitSelections.Instance.GetSelectedComponent<UnitInventory>(); }
    //    if (inventory == null) { inventory = UnitSelections.Instance.GetSelectedComponent<Inventory>(); }

    //    // If both are still null, log an error or handle appropriately.
    //    if (unitInventory == null && inventory == null) { Debug.LogError("<color=red>No inventory available to display</color>"); }

    //    // Clear All Slots from prior Inventory
    //    foreach (var slot in inventorySlots)
    //    {
    //        // Check Update & Clear each Slot 
    //        slot.itemStack.ClearStack();
    //    }

    //    // Retrieve Slots from Unit Inventory
    //    foreach (var slot in inventorySlots)
    //    {
    //        // Check Update each Slots Value 
    //        unitInventory.ViewItemSlots();
    //    }
    //}

    // UnitInventoryUI.cs 
    public void SetItemSlots()
    {
        if (unitInventory == null)
        {
            Debug.LogError("No UnitInventory set.");
            return;
        }

        // Assuming inventorySlots is an array or list of ItemSlot UI components
        int index = 0;
        foreach (var item in unitInventory.GetAllItems())
        {
            if (index < inventorySlots.Count)
            {
                inventorySlots[index].InitializeSlot(item.Key, item.Value);
            }
            index++;
        }

        // Clear remaining slots if any
        for (int i = index; i < inventorySlots.Count; i++)
        {
            if (inventorySlots[i] != null)
            {
                inventorySlots[i].ClearSlot(); // Ensure this method exists to clear the slot visually and logically
            }
        }

        Debug.Log("Slots have been set based on UnitInventory.");
    }

    public void DestroyInventory()
    {
        // Example method called when the inventory gets destroyed
        foreach (var slot in inventorySlots)
        {
            slot.CheckAndClearSlotIfEmpty();
            // Additional logic for handling destroyed inventories.
        }
    }

    private void UpdateSlotsWithItems(Dictionary<ItemData, int> items)
    {
        for (int i = 0; i < itemSlots.Length; i++)
        {
            if (i < items.Count)
            {
                var item = items.ElementAt(i);
                itemSlots[i].InitializeSlot(item.Key, item.Value);
            }
            else
            {
                // Clear slots if no item to display
                //itemSlots[i].ClearSlot(); // ClearSlot in Slots yet to be written
                itemSlots[i].CheckAndClearSlotIfEmpty(); // Checks and clears empty Slots 
                // itemSlots[i].itemStack.ClearStack(); // ClearStack in ItemStack is written
            }
        }
    }

    protected override void OnItemSlotClicked(ItemData clickedItem)
    {
        Debug.Log($"Clicked on item: {clickedItem.displayName}, Quantity: {currentTradeQuantity}");
        // Logic for handling item slot clicks based on the selected trade quantity
    }

    // Additional methods for trading, throwing items, selling, buying, etc. can be implemented here
}

