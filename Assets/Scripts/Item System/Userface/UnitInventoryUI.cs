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
        if (this.unitInventory != null)
        {
            this.unitInventory.OnUnitInventoryChanged -= RefreshInventoryDisplay;
        }

        this.unitInventory = newUnitInventory;

        if (this.unitInventory != null)
        {
            this.unitInventory.OnUnitInventoryChanged += RefreshInventoryDisplay;
        }

        SyncSlots();

        // Update the slot's UnitInventory data
        var slots = (itemSlots != null && itemSlots.Length > 0) ? itemSlots : inventorySlots?.ToArray();
        if (slots != null)
        {
            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    slot.unitInventory = newUnitInventory;
                    slot.unitInventoryUI = this;
                    if (newUnitInventory != null)
                    {
                        slot.storageManager = newUnitInventory.GetComponent<UnitStorageManager>();
                    }
                    Debug.Log("<color=white>UnitInventoryUI:</color> <color=green>Succesful Set unitInventory! unitInventory: </color>" + (unitInventory != null ? unitInventory.name : "null") + " unitInventory.ID: " + (unitInventory != null ? unitInventory.ID : "null"));
                }
            }
        }
    }

    public override void SetInventory(Inventory newInventory)
    {
        if (this.inventory != null)
        {
            this.inventory.OnInventoryChanged -= RefreshInventoryDisplay;
        }

        this.inventory = newInventory;

        if (this.inventory != null)
        {
            this.inventory.OnInventoryChanged += RefreshInventoryDisplay;
        }
    }

    protected virtual void OnDestroy()
    {
        if (unitInventory != null)
        {
            unitInventory.OnUnitInventoryChanged -= RefreshInventoryDisplay;
        }
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= RefreshInventoryDisplay;
        }
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

    protected virtual void Awake()
    {
        SyncSlots();
    }

    protected override void Start()
    {
        base.Start();
        SyncSlots();
    }

    public void SyncSlots()
    {
        if ((inventorySlots == null || inventorySlots.Count == 0) && itemSlots != null && itemSlots.Length > 0)
        {
            inventorySlots = new List<ItemSlot>(itemSlots);
        }
        else if ((itemSlots == null || itemSlots.Length == 0) && inventorySlots != null && inventorySlots.Count > 0)
        {
            itemSlots = inventorySlots.ToArray();
        }
        else if ((itemSlots == null || itemSlots.Length == 0) && (inventorySlots == null || inventorySlots.Count == 0) && itemSlotContainer != null)
        {
            itemSlots = itemSlotContainer.GetComponentsInChildren<ItemSlot>(true);
            inventorySlots = new List<ItemSlot>(itemSlots);
        }
    }

    protected override void ClearSlots()
    {
        SyncSlots();
        var slots = (itemSlots != null && itemSlots.Length > 0) ? itemSlots : inventorySlots?.ToArray();
        if (slots != null)
        {
            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    slot.ClearSlot();
                }
            }
        }
        base.ClearSlots();
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
            // Refresh display based on UnitInventory's physical cargo slots to preserve slot identity
            UpdateSlotsWithItems(unitInventory.itemSlots);
        }
        else if (inventory != null)
        {
            // Fallback to aggregate Inventory if UnitInventory is not available.
            var items = inventory.GetAllItems();
            UpdateSlotsWithItems(items);
        }
        else
        {
            Debug.LogError("<color=red>UnitInventoryUI: No inventory or unit inventory available</color>");
        }
    }

    public void RefreshSlots()
    {
        SyncSlots();
        ClearSlots(); // Ensure all slots are cleared initially.

        if (unitInventory == null && inventory == null)
        {
            Debug.LogError("No inventory found.");
            return;
        }

        if (unitInventory != null)
        {
            UpdateSlotsWithItems(unitInventory.itemSlots);
        }
        else if (inventory != null)
        {
            var items = inventory.GetAllItems();
            if (items == null)
            {
                Debug.LogError("No items to display.");
                return;
            }
            UpdateSlotsWithItems(items);
        }
    }

    // UnitInventoryUI.cs - Resets all its ItemSlots & then copies the UnitInventory.cs onto its
    // own list or array of ItemSlots. Its own slots are the ones that display the unitInventory
    // content. Think of ItemSlots as windows which reflects the Unit Inventory content. 
    public void SetItemSlots()
    {
        if (unitInventory == null)
        {
            Debug.LogError("No UnitInventory set.");
            return;
        }

        SyncSlots();
        UpdateSlotsWithItems(unitInventory.itemSlots);

        Debug.Log("Slots have been set based on UnitInventory.");
    }

    public void DestroyInventory()
    {
        // Example method called when the inventory gets destroyed
        SyncSlots();
        var slots = (itemSlots != null && itemSlots.Length > 0) ? itemSlots : inventorySlots?.ToArray();
        if (slots != null)
        {
            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    slot.CheckAndClearSlotIfEmpty();
                }
            }
        }
    }

    public void UpdateSlotsWithItems(ItemSlot[] physicalSlots)
    {
        SyncSlots();
        var slots = (itemSlots != null && itemSlots.Length > 0) ? itemSlots : inventorySlots?.ToArray();
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            slots[i].unitInventory = unitInventory;
            slots[i].unitInventoryUI = this;
            if (unitInventory != null)
            {
                slots[i].storageManager = unitInventory.GetComponent<UnitStorageManager>();
            }

            if (physicalSlots != null && i < physicalSlots.Length && physicalSlots[i] != null && physicalSlots[i].itemStack != null && physicalSlots[i].itemStack.HasItem() && physicalSlots[i].itemStack.GetQuantity() > 0)
            {
                var stack = physicalSlots[i].itemStack;
                slots[i].InitializeSlot(stack.GetItemData(), stack.GetQuantity());
            }
            else
            {
                slots[i].ClearSlot();
            }
        }
    }

    public void UpdateSlotsWithItems(Dictionary<ItemData, int> items)
    {
        SyncSlots();
        var slots = (itemSlots != null && itemSlots.Length > 0) ? itemSlots : inventorySlots?.ToArray();
        if (slots == null || items == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            slots[i].unitInventory = unitInventory;
            slots[i].unitInventoryUI = this;
            if (unitInventory != null)
            {
                slots[i].storageManager = unitInventory.GetComponent<UnitStorageManager>();
            }

            if (i < items.Count)
            {
                var item = items.ElementAt(i);
                slots[i].InitializeSlot(item.Key, item.Value);
            }
            else
            {
                slots[i].ClearSlot();
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

