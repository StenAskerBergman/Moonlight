using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public abstract class InventoryUserface : MonoBehaviour
{
    // This base owns reusable PRESENTATION behaviour only. It deliberately holds no
    // inventory reference of its own: the display context (which unit/inventory is
    // currently inspected) belongs to the concrete subclass, and item authority
    // belongs below the UI layer entirely, in UnitInventory / UnitStorageManager.
    //
    // These are read-only windows onto the subclass's own fields, not backing state,
    // so there is exactly one source of truth per reference and no serialized field
    // name is declared twice in the hierarchy.
    // Inventory only. UnitInventory is deliberately absent: nothing in this base needs
    // it, and requiring it would force non-unit subclasses such as BuildingInventoryUI
    // to implement unit members they have no use for. A subclass that displays a unit
    // declares that itself - see UnitInventoryUI.
    protected abstract Inventory DisplayedInventory { get; }

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

        // Deliberately does NOT subscribe here. The inspected inventory changes long
        // after Start(), and SetInventory/SetUnitInventory already unsubscribe the old
        // reference and subscribe the new one on every change. Subscribing here as well
        // would add a second handler whenever the scene ships a non-null reference
        // (Match.unity does), making RefreshInventoryDisplay fire twice per change.
        // Subscription lifecycle lives in exactly one place: the setters.

        //if (unitInventory == null && inventory == null)
        //{
        //    Debug.LogError("InventoryUserface: UnitInventory component not found");
        //    Debug.LogError("InventoryUserface: Inventory component not found");
        //}
    }

    // Abstract, not virtual with an empty body: the subclass owns the reference, so it
    // must also own assigning it and moving the change-event subscription with it. An
    // inherited no-op would silently swallow the call.
    //
    // There is no SetUnitInventory here. Every caller holds a UnitInventoryUI-typed
    // reference, so routing it through this base bought nothing and only forced
    // non-unit subclasses to implement it.
    public abstract void SetInventory(Inventory newInventory);

    protected bool HasInventory()
    {
        return DisplayedInventory != null;
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

        Dictionary<ItemData, int> items = DisplayedInventory.GetAllItems();
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
