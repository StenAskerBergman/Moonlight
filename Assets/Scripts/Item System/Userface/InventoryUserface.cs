using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public abstract class InventoryUserface : MonoBehaviour
{
    // Inventories
    // Declared once, here. UnitInventoryUI used to re-declare both as public fields,
    // which shadowed these and made Unity refuse to serialize either ("the same field
    // name is serialized multiple times"). It also meant the derived setters wrote the
    // derived copies while this base class kept reading its own always-null ones, so
    // Start() never subscribed to inventory change events and HasInventory() was
    // always false.
    //
    // Public rather than protected: they were public on UnitInventoryUI before being
    // pulled up, and StarterUnit and ItemSlot read unitInventory from outside the
    // hierarchy, so public preserves the contract that already existed.
    public Inventory inventory;
    public UnitInventory unitInventory;
    protected UnitSelections unitSelections;

    public Transform itemSlotContainer; // Parent Obj. to all item Slots 
    public List<ItemSlot> inventorySlots; // List of all item Slots in the inventory userface. <ItemSlot>

    // InventoryUserface.cs
    protected virtual void Start()
    {
        // Initialize or assign your inventory here
        //unitInventory = UnitSelections.Instance.GetSelectedComponent<UnitInventory>();
        //inventory = UnitSelections.Instance.GetSelectedComponent<Inventory>();

        //if (unitInventory == null)
        //{
        //    unitInventory = UnitSelections.Instance.GetSelectedUnitInventory();
        //}

        //if (inventory == null)
        //{
        //    inventory = UnitSelections.Instance.GetSelectedInventory();
        //}

        if (inventory != null)
        {
            inventory.OnInventoryChanged += RefreshInventoryDisplay;
        }
        
        if (unitInventory != null)
        {
            unitInventory.OnUnitInventoryChanged += RefreshInventoryDisplay;
        }

        //if (unitInventory == null && inventory == null)
        //{
        //    Debug.LogError("InventoryUserface: UnitInventory component not found");
        //    Debug.LogError("InventoryUserface: Inventory component not found");
        //}
    }

    public virtual void SetUnitInventory(UnitInventory newUnitInventory)
    {
        this.unitInventory = newUnitInventory;
    }

    public virtual void SetInventory(Inventory newInventory)
    {
        this.inventory = newInventory;
    }

    private bool HasInventory()
    {
        return inventory != null || unitInventory != null;
    }

    protected virtual void OnEnable()
    {

    }

    public virtual void NewStack(Item item, ItemData itemData)
    {
        // When there is empty space and a item is recieved to the inventory
        // create stack of that item in the inventory and assign it to fitting
        // slot which is open / empty. 
    }

    public virtual void RemoveStack(Item item, ItemData itemData)
    {
        // When there is item slot which has reached the item stack size of 0
        // When a stack reaches 0 it seizes to exist and is removed. We start
        // by removing the item from it's own inventory item slot
    }


    public virtual void RefreshInventoryDisplay()
    {
        // Used inside both Classes using Inventory & UnitInventory

        // Implementation to update the predefined slots with the items in the inventory
        // This will be unique to your implementation and will not instantiate new slots
        
        //// Initialize or assign your inventory here
        //unitInventory = UnitSelections.Instance.GetSelectedComponent<UnitInventory>();

        //// Don't see valid use case for this, yet though...
        //inventory = UnitSelections.Instance.GetSelectedComponent<Inventory>();

        //if (inventory == null && unitInventory == null)
        //{
        //    Debug.LogError("No inventory available to display");
        //    return;
        //}

        // Your existing logic for refreshing the inventory display

        /*
        // Clear previous slots
        ClearSlots();

        Dictionary<ItemData, int> items = inventory.GetAllItems();
        int slotsUsed = 0;

        foreach (var item in items)
        {
            if (slotsUsed >= maxUISlots) break; // Ensure we don't exceed our UI slots

            CreateItemSlot(item);

            slotsUsed++;
        }*/
    }

    protected virtual void ClearSlots()
    {
        // Implementation to clear/reset predefined slots
        // This might involve setting the slots to some 'empty' state

        if (inventorySlots != null)
        {
            foreach (ItemSlot slot in inventorySlots)
            {
                if (slot != null)
                {
                    slot.CheckAndClearSlotIfEmpty(); 
                    // Old: Destroy(child.gameObject);
                }
            }
        }
    }
    
    // Keep for future use in create a universal method for stacks
    //protected virtual void CreateItemSlot(KeyValuePair<ItemData, int> item)
    //{
    //    GameObject newItemSlot = Instantiate(itemSlotPrefab, itemSlotContainer);
    //    Text itemText = newItemSlot.GetComponentInChildren<Text>();

    //    if (itemText)
    //    {
    //        itemText.text = FormatItemDisplay(item.Key, item.Value);
    //    }

    //    Button itemButton = newItemSlot.GetComponent<Button>();
    //    if (itemButton)
    //    {
    //        itemButton.onClick.AddListener(() => OnItemSlotClicked(item.Key));
    //    }
    //}

    protected virtual string FormatItemDisplay(ItemData item, int quantity)
    {
        return $"{item.displayName} x{quantity}";
    }

    protected virtual void OnItemSlotClicked(ItemData clickedItem)
    {
        Debug.Log($"<color=white>InventoryUserface:</color> <color=yellow>Clicked on item: </color><color=white>{clickedItem.displayName}</color>");
        // Implementation for what should happen when a slot is clicked
        // This might involve showing item details, selecting the item, etc.
    }
}
