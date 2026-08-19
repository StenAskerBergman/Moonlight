using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* 
    Encapsulation Principle: 
 
        No Direct Contact with Storage without Manager
        
        Or rather...
        
        No Direct Contact with UnitStorage.cs without UnitStorageManager.cs

 */

public class UnitStorage : Storage
{

    #region Important Notes

    // STACKS ARE WHAT SLOTS SETS ITEMS INTO
    // THE OPERATE EXTREMLY SIMILAR TO A BAG

    // STACKS HAVE A MAX CAPACITY && CURRENT
    // CAPACITY. SLOTS SET MAX CAPACITY OF A
    // STACK! AND DECIDE WHAT ITEM THE STACK
    // GETS TO HOLD INSIDE IT. 

    // STACK CAN ONLY BE OF ONE TYPE OF ITEM
    // CONSUMABLE ITEMS CANT STACK BEYOND 1.

    // NORMAL ITEMS CAN STACK TO THE ALLOWED
    // STACK MAX SIZE / QUANTITY.

    // Edit:
    /*   THANK GOD FOR THIS FUCKING NOTE  */
    /*   AGAIN THANK GOD FOR THIS F NOTE  */
    /*   HOLY GOD FOR THIS NOTE           */

    // EACH TIME U MUST RE-READ THIS ADD 
    // HOURS WASTED HERE: 10h

    // Inheritade Methods
    // public int GetItemQuantity(ItemData itemData)
    // public int GetCapacityLimit()
    // public bool HasReachedCapacity(int quantityToAdd)
    // public void SetCapacityLimit(int limit)

    // Virtual - Overridable Methods
    // public virtual bool RemoveItem(ItemData itemData, int quantity)
    // public virtual void AddItem(ItemData itemData, int quantity)

    #endregion

    // Current Full Stacks 
    protected Dictionary<ItemStack, bool> FullStacks = new Dictionary<ItemStack, bool>();

    // Current Stack Quantities
    protected Dictionary<ItemStack, int> StackSize = new Dictionary<ItemStack, int>();

    // Current Assigned Stacks Slots
    protected Dictionary<ItemStack, ItemSlot> slots = new Dictionary<ItemStack, ItemSlot>();

    // Current Slots
    private List<ItemSlot> itemSlots = new List<ItemSlot>();

    // Stack Stats
    private const int MAX_STACK_SIZE = 40; // Max Stack Quantity Allowed
    private const int NORMAL_SLOTS = 4; // Max Normal Item Slots
    private const int CONSUME_SLOT = 1; // Max Consumable Item Slots
    private const int ABILITY_SLOT = 1; // Max Ability Item Slots

    private void Awake()
    {
        // Sets This UnitStorage Total Item Quantity Capacity Limit
        this.SetCapacityLimit((NORMAL_SLOTS * MAX_STACK_SIZE) + CONSUME_SLOT + ABILITY_SLOT);
    }


    private Dictionary<ItemType, int> occupiedSlots = new Dictionary<ItemType, int>
    {
        { ItemType.Normal, 0 },
        { ItemType.Consumable, 0 }
    };

    // Used When:
    // Adding Items to Existing Item Stack on Slot          ( if possible )
    // Creating New Stack for Getting New Items             ( if possible )
    // Assigning Empty Slot New Stack & mark as Occupied    ( if possible )

    // Checking if any Occupied slots match Added Item
        // N: return false: NO MATCHES!
        // Check if Any Empty slots exists
            // N: If not, return false => "Full Inventory!"
            // Y: Creating New Stack for Getting New Items  ( if possible )
                // Assigning Empty Slot, New Stack & mark as Occupied.
            
        // Y: return true: MATCH FOUND!
                // Calculate Stat Add Difference
                // If more items added than there is space for
                // Hold the ones that fit and send back the ones
                // That don't fit. 

    public override void AddItem(ItemData itemData, int quantity)
    {
        if (itemData == null)
        {
            Debug.LogWarning("Can't add null itemData to UnitStorage.");
            return;
        }

        if (CanAddSpecificItem(itemData, quantity))
        {
            bool isNewSlot = !items.ContainsKey(itemData) || items[itemData] == 0;

            base.AddItem(itemData, quantity);

            if (isNewSlot)
            {
                switch (itemData.type)
                {
                    case ItemType.Normal:
                        if (!occupiedSlots.ContainsKey(ItemType.Normal)) occupiedSlots[ItemType.Normal] = 0;
                        occupiedSlots[ItemType.Normal]++;
                        break;
                    case ItemType.Consumable:
                        if (!occupiedSlots.ContainsKey(ItemType.Consumable)) occupiedSlots[ItemType.Consumable] = 0;
                        occupiedSlots[ItemType.Consumable]++;
                        break;
                }
            }
        }
        else
        {
            Debug.LogWarning("Can't add item due to storage constraints: " + itemData.displayName);
        }
    }

    public bool CanAddSpecificItem(ItemData itemData, int quantity)
    {
        if (itemData == null)
        {
            return false;
        }

        if (quantity > MAX_STACK_SIZE)
        {
            return false;
        }

        bool isNewSlot = !items.ContainsKey(itemData) || items[itemData] == 0;

        if (isNewSlot)
        {
            int currentOccupied = occupiedSlots.ContainsKey(itemData.type) ? occupiedSlots[itemData.type] : 0;

            // Check for slot availability based on item type.
            switch (itemData.type)
            {
                case ItemType.Normal:
                    if (currentOccupied + 1 > NORMAL_SLOTS) // +1 because we are checking if we can add an item to an empty slot
                    {
                        return false;
                    }
                    break;
                case ItemType.Consumable:
                    if (currentOccupied + 1 > CONSUME_SLOT)
                    {
                        return false;
                    }
                    break;
            }
        }
        else
        {
            if (items[itemData] + quantity > MAX_STACK_SIZE)
            {
                return false;
            }
        }

        return true;
    }
    // Check for slots availability based on item type.
    public virtual void CheckSlots()
    {
        // Check for slot availability based on item type.
    }

    // Mark slots based on availability & SpaceLeft.
    public virtual void MarkSlots()
    {

    }

    public override bool RemoveItem(ItemData itemData, int quantity)
    {
        if (itemData == null)
        {
            return false;
        }

        bool wasInStorage = items.ContainsKey(itemData) && items[itemData] > 0;

        if (base.RemoveItem(itemData, quantity))
        {
            bool isStillInStorage = items.ContainsKey(itemData) && items[itemData] > 0;

            if (wasInStorage && !isStillInStorage)
            {
                switch (itemData.type)
                {
                    case ItemType.Normal:
                        if (occupiedSlots.ContainsKey(ItemType.Normal) && occupiedSlots[ItemType.Normal] > 0)
                        {
                            occupiedSlots[ItemType.Normal]--;
                        }
                        break;
                    case ItemType.Consumable:
                        if (occupiedSlots.ContainsKey(ItemType.Consumable) && occupiedSlots[ItemType.Consumable] > 0)
                        {
                            occupiedSlots[ItemType.Consumable]--;
                        }
                        break;
                }
            }

            return true;
        }

        return false;
    }


    public List<string> GetInventoryItemList()
    {
        List<string> itemList = new List<string>();
        foreach (var slotEntry in slots)
        {
            ItemStack stack = slotEntry.Key;
            ItemSlot slot = slotEntry.Value;

            if (stack != null && stack.itemData != null)
            {
                string itemDescription = $"{stack.itemData.displayName} - Quantity: {StackSize[stack]}";
                itemList.Add(itemDescription);
            }
        }
        return itemList;
    }
}
// UnitStorage.cs
