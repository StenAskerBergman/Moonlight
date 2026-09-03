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

    [Tooltip("Slots that present the inspected unit's abilities. Left empty, they are " +
             "discovered from any child ItemSlot flagged AbilitySlot.")]
    public ItemSlot[] abilitySlots;

    // Read-only windows for shared base behaviour. These expose the fields above;
    // they are not additional state. This class owns its display context, while
    // UnitInventory / UnitStorageManager remain the authority over item movement.
    protected override Inventory DisplayedInventory => inventory;

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
                Unit selectedUnit = UnitSelections.Instance.FocusedUnit;
                if (selectedUnit != null)
                {
                    // If you also need to set the general 
                    SetUnitInventory(selectedUnit.unitInventory ?? selectedUnit.GetComponent<UnitInventory>());
                }
                else
                {
                    Debug.LogError("<color=red>UnitInventoryUI: No UnitInventory found</color>");
                    ClearSlots();
                }
            }
        }
        else
        {
            ClearSlots(); // No units are selected
        }
    }

    // Not an override - SetUnitInventory is specific to this class, not part of the
    // shared presentation contract. Every caller already holds a UnitInventoryUI.
    public void SetUnitInventory(UnitInventory newUnitInventory)
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
        RefreshInventoryDisplay();

        // Update the slot's UnitInventory data
        var slots = (itemSlots != null && itemSlots.Length > 0) ? itemSlots : inventorySlots?.ToArray();
        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot != null)
                {
                    slot.unitInventory = newUnitInventory;
                    slot.unitInventoryUI = this;
                    slot.slotIndex = i;
                    if (newUnitInventory != null)
                    {
                        slot.storageManager = newUnitInventory.GetComponent<UnitStorageManager>();
                        if (newUnitInventory.itemSlots != null && i < newUnitInventory.itemSlots.Length)
                        {
                            slot.cargoSlot = newUnitInventory.itemSlots[i];
                        }
                        else
                        {
                            slot.cargoSlot = null;
                        }
                    }
                    else
                    {
                        slot.cargoSlot = null;
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
        
        RefreshInventoryDisplay();
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
        RefreshAbilityDisplay();
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
            Debug.Log($"<color=white>UnitInventoryUI:</color> <color=yellow>Requesting Name Change: </color><color=white>{key}</color>");

            // Set Name
            UpdateDisplayName(key);
        }
    }

    public void SetInspection(GameObject gameObject)
    {
        this.Inspected = gameObject;
        Debug.Log($"<color=white>UnitInventoryUI:</color> <color=green>Inspected GameObject Updated: </color><color=white>{Inspected.name}</color>");

    }

    protected virtual void Awake()
    {
        // Suppress legacy visual background so modern Anno 2070 UI takes visual precedence
        var rootImg = GetComponent<Image>();
        if (rootImg != null && (rootImg.sprite == null || rootImg.sprite.name.Contains("UISprite")))
        {
            rootImg.color = new Color(0, 0, 0, 0);
            rootImg.raycastTarget = false;
        }

        SyncSlots();
    }

    protected override void Start()
    {
        base.Start();
        SyncSlots();
    }

    // Ability slots are not part of itemSlots: they hold no cargo and are skipped by
    // the inventory projection entirely, so nothing was ever written into them.
    private void SyncAbilitySlots()
    {
        if (abilitySlots != null && abilitySlots.Length > 0) return;

        var found = new List<ItemSlot>();
        foreach (ItemSlot slot in GetComponentsInChildren<ItemSlot>(true))
        {
            if (slot != null && slot.AbilitySlot) found.Add(slot);
        }
        abilitySlots = found.ToArray();
    }

    /// <summary>
    /// Paints the inspected unit's abilities into the ability slots. A unit with no
    /// UnitAbilities component, or fewer abilities than slots, leaves the remaining
    /// slots reading "(empty)" and greyed out rather than showing prefab placeholders.
    /// </summary>
    public void RefreshAbilityDisplay()
    {
        SyncAbilitySlots();
        if (abilitySlots == null || abilitySlots.Length == 0) return;

        // The live selection wins over the cached unit so a deselect cannot leave the
        // previous unit's abilities on screen.
        Unit displayedUnit = (UnitSelections.Instance != null ? UnitSelections.Instance.FocusedUnit : null) ?? unit;
        UnitAbilities unitAbilities = displayedUnit != null ? displayedUnit.GetComponent<UnitAbilities>() : null;
        int abilityCount = unitAbilities != null ? unitAbilities.Abilities.Count : 0;

        for (int i = 0; i < abilitySlots.Length; i++)
        {
            if (abilitySlots[i] == null) continue;

            AbilityDefinition definition = i < abilityCount ? unitAbilities.Abilities[i].definition : null;
            if (definition != null)
            {
                abilitySlots[i].ShowAbility(definition.icon, definition.displayName);
            }
            else
            {
                abilitySlots[i].ShowEmptyAbility();
            }
        }
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
        RefreshAbilityDisplay();
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
            Debug.Log($"<color=white>UnitInventoryUI:</color> <color=green>Unit name updated: </color><color=white>{CurrentDisplayText}</color>");
        }
        else
        {
            Debug.LogError("<color=red>UnitInventoryUI: Unit name text UI element not set!</color>");
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

        // Clear previous slots (this also repaints the ability slots)
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
            Debug.LogError("<color=red>UnitInventoryUI: No inventory found.</color>");
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
                Debug.LogError("<color=red>UnitInventoryUI: No items to display.</color>");
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
            Debug.LogError("<color=red>UnitInventoryUI: No UnitInventory set.</color>");
            return;
        }

        SyncSlots();
        UpdateSlotsWithItems(unitInventory.itemSlots);

        Debug.Log("<color=white>UnitInventoryUI:</color> <color=green>Slots have been set based on UnitInventory.</color>");
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
            slots[i].slotIndex = i;
            if (unitInventory != null)
            {
                slots[i].storageManager = unitInventory.GetComponent<UnitStorageManager>();
            }

            if (physicalSlots != null && i < physicalSlots.Length && physicalSlots[i] != null)
            {
                slots[i].cargoSlot = physicalSlots[i];

                if (physicalSlots[i].itemStack != null && physicalSlots[i].itemStack.HasItem() && physicalSlots[i].itemStack.GetQuantity() > 0)
                {
                    var stack = physicalSlots[i].itemStack;
                    slots[i].InitializeSlot(stack.GetItemData(), stack.GetQuantity());
                }
                else
                {
                    slots[i].ClearSlot();
                }
            }
            else
            {
                slots[i].cargoSlot = null;
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
            slots[i].slotIndex = i;
            slots[i].cargoSlot = null;
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
        Debug.Log($"<color=white>UnitInventoryUI:</color> <color=yellow>Clicked on item: </color><color=white>{clickedItem.displayName}</color><color=yellow>, Quantity: </color><color=white>{currentTradeQuantity}</color>");
        // Logic for handling item slot clicks based on the selected trade quantity
    }

    // Additional methods for trading, throwing items, selling, buying, etc. can be implemented here
}

