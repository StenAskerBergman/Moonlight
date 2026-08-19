// UnitStorageManager.cs - Start
using System.Collections.Generic;
using UnityEngine;

public class UnitStorageManager : StorageManager
{
    //[HideInInspector]
    //[SerializeField]
    protected new Storage storage; // This hides the inherited field


    // Unit Storage + Stack Stats
    //[SerializeField]
    protected UnitStorage unitStorage;

    // Constructor
    public UnitStorageManager(Storage storage) : base(storage) { }

    // Unity Constructor
    private void Awake()
    {
        if (!unitStorage)
        {
            unitStorage = GetComponent<UnitStorage>();
            if (!unitStorage)
            {
                unitStorage = new UnitStorage(); // Or some other way to initialize it properly
            }

            // Updates Slot Max Quantity
            UpdateMaxQuantity();
        }


        // Initialize usedSlots dictionary here if it's not already initialized
        usedSlots = new Dictionary<ItemType, int>()
        {
            { ItemType.Normal, 0 },
            { ItemType.Consumable, 0 }
        };
    }


    // Stack Stat - "Dynamic" / Placeholder Number
    public int maxQuantity { get; private set; }
    public int BonusQuantity { get; set; }

    // Stack Stat - Constants - These Number should be derive from the UnitStorage.cs file
    private const int MAX_STACK_SIZE = 40; // Max Stack Quantity Allowed
    private const int NORMAL_SLOTS = 4;    // Max Normal Item Slots - THIS IS ITEM SLOTS
    private const int CONSUME_SLOT = 1;    // Max Consumable Item Slots
    private const int ABILITY_SLOT = 1;    // Max Ability Item Slots

    #region Const Getters

        public int GetMaxStackSize()
        {
            return MAX_STACK_SIZE;
        }

        public int GetNormalSlots()
        {
            return NORMAL_SLOTS;
        }

        public int GetConsumeSlot()
        {
            return CONSUME_SLOT;
        }

        public int GetAbilitySlot()
        {
            return ABILITY_SLOT;
        }

    #endregion

    // Used Slots
    private int totalStacks = 0; // Current Stack Count
    private Dictionary<ItemType, int> usedSlots = new Dictionary<ItemType, int>
    {
        // Type - Stack Count
        { ItemType.Normal, 0 },
        { ItemType.Consumable, 0 }
    };

    // Used Slot Getter
    public int GetUsedSlots
    {
        get
        {
            return usedSlots[ItemType.Normal] + usedSlots[ItemType.Consumable];
        }
    }

    public override void AddItem(ItemData itemData, int quantity)
    {
        if (CanAddItem(itemData, quantity))
        {
            unitStorage.AddItem(itemData, quantity);

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

    // Update Max Quantity
    private void UpdateMaxQuantity()
    {
        maxQuantity = MAX_STACK_SIZE + BonusQuantity;
    }

    // Dedicated Method to determine if Can Add Item 
    public override bool CanAddItem(ItemData itemData, int quantity)
    {
        // First, call the base method to check general storage capacity
        if (!base.CanAddItem(itemData, quantity))
        {
            Debug.LogWarning("General storage capacity reached, cannot add item.");
            return false;
        }

        // Check if adding this quantity exceeds the total allowed stacks
        if (totalStacks + quantity > maxQuantity)
        {
            Debug.LogWarning($"Total stacks exceeded: {totalStacks + quantity} / {maxQuantity}");
            return false;
        }

        // Determine the number of slots available for the item type using the new method
        int slotsAvailable = CalculateAvailableSlots(itemData);

        // Check if there are no slots available for the item type
        if (slotsAvailable <= 0)
        {
            Debug.LogWarning($"No slots available for type {itemData.type}");
            return false;
        }

        // If all checks pass, return true indicating the item can be added
        return true;
    }


    // Dedicated method to calculate available slots
    private int CalculateAvailableSlots(ItemData itemData)
    {
        switch (itemData.type)
        {
            case ItemType.Normal:
                return NORMAL_SLOTS - usedSlots[ItemType.Normal];
            case ItemType.Consumable:
                return CONSUME_SLOT - usedSlots[ItemType.Consumable];
            default:
                Debug.LogError("Unhandled item type: " + itemData.type);
                return 0; // Handle other item types or throw an error
        }
    }


    // More version of this exists in a file to read in documents called
    // CanAddItemMethods.cs

    //public override bool RemoveItem(ItemData itemData, int quantity)
    //{
    //    if (base.RemoveItem(itemData, quantity))
    //    {
    //        switch (itemData.type)
    //        {
    //            case ItemType.Normal:
    //                usedSlots[ItemType.Normal] -= quantity;
    //                break;
    //            case ItemType.Consumable:
    //                usedSlots[ItemType.Consumable] -= quantity;
    //                break;
    //        }
    //    }
    //}

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
                    // TODO: in future please check
                    // if item in use or not check! 
                    break;
            }

            totalStacks -= quantity;
            return true;
        }

        return false;
    }

}
// UnitStorageManager.cs - End