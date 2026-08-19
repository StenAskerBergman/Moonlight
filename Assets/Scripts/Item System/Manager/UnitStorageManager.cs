// UnitStorageManager.cs - Start
using System.Collections.Generic;
using UnityEngine;

public class UnitStorageManager : StorageManager
{
    // Unit Storage + Stack Stats
    //[SerializeField]
    protected UnitStorage unitStorage;

    // Constructor
    public UnitStorageManager(Storage storage) : base(storage) { }

    // Stack Stat - Constants - These Number should be derive from the UnitStorage.cs file
    public const int MAX_STACK_SIZE = 40; // Max Stack Quantity Allowed
    private const int NORMAL_SLOTS = 4;    // Max Normal Item Slots - THIS IS ITEM SLOTS
    private const int CONSUME_SLOT = 1;    // Max Consumable Item Slots
    private const int ABILITY_SLOT = 1;    // Max Ability Item Slots

    // Stack Stat - "Dynamic" / Placeholder Number
    public int maxQuantity { get; private set; } = MAX_STACK_SIZE;
    public int BonusQuantity { get; set; }

    // Unity Constructor
    private void Awake()
    {
        if (!unitStorage)
        {
            unitStorage = GetComponent<UnitStorage>();
            if (!unitStorage)
            {
                unitStorage = gameObject.AddComponent<UnitStorage>();
            }
        }

        if (!storage)
        {
            storage = unitStorage;
        }

        // Updates Slot Max Quantity
        UpdateMaxQuantity();


        // Initialize usedSlots dictionary here if it's not already initialized
        usedSlots = new Dictionary<ItemType, int>()
        {
            { ItemType.Normal, 0 },
            { ItemType.Consumable, 0 }
        };
    }

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
            int normalUsed = usedSlots.ContainsKey(ItemType.Normal) ? usedSlots[ItemType.Normal] : 0;
            int consumableUsed = usedSlots.ContainsKey(ItemType.Consumable) ? usedSlots[ItemType.Consumable] : 0;
            return normalUsed + consumableUsed;
        }
    }

    public override void AddItem(ItemData itemData, int quantity)
    {
        if (itemData == null)
        {
            return;
        }

        if (CanAddItem(itemData, quantity))
        {
            bool isNewSlot = GetItemQuantity(itemData) == 0;

            unitStorage.AddItem(itemData, quantity);

            if (isNewSlot)
            {
                switch (itemData.type)
                {
                    case ItemType.Normal:
                        if (!usedSlots.ContainsKey(ItemType.Normal)) usedSlots[ItemType.Normal] = 0;
                        usedSlots[ItemType.Normal]++;  
                        break;
                    case ItemType.Consumable:
                        if (!usedSlots.ContainsKey(ItemType.Consumable)) usedSlots[ItemType.Consumable] = 0;
                        usedSlots[ItemType.Consumable]++;
                        break;
                }
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
        if (itemData == null)
        {
            return false;
        }

        // First, call the base method to check general storage capacity
        if (!base.CanAddItem(itemData, quantity))
        {
            Debug.LogWarning("General storage capacity reached, cannot add item.");
            return false;
        }

        // Check if adding this quantity exceeds the per-stack maximum
        if (quantity > maxQuantity)
        {
            Debug.LogWarning($"Item quantity exceeds stack maximum: {quantity} / {maxQuantity}");
            return false;
        }

        bool isNewSlot = GetItemQuantity(itemData) == 0;
        if (isNewSlot)
        {
            // Determine the number of slots available for the item type using the new method
            int slotsAvailable = CalculateAvailableSlots(itemData);

            // Check if there are no slots available for the item type
            if (slotsAvailable <= 0)
            {
                Debug.LogWarning($"No slots available for type {itemData.type}");
                return false;
            }
        }
        else
        {
            if (GetItemQuantity(itemData) + quantity > maxQuantity)
            {
                Debug.LogWarning($"Total stack quantity exceeded for {itemData.displayName}: {GetItemQuantity(itemData) + quantity} / {maxQuantity}");
                return false;
            }
        }

        // If all checks pass, return true indicating the item can be added
        return true;
    }


    // Dedicated method to calculate available slots
    private int CalculateAvailableSlots(ItemData itemData)
    {
        if (itemData == null)
        {
            return 0;
        }

        int currentUsed = usedSlots.ContainsKey(itemData.type) ? usedSlots[itemData.type] : 0;
        switch (itemData.type)
        {
            case ItemType.Normal:
                return NORMAL_SLOTS - currentUsed;
            case ItemType.Consumable:
                return CONSUME_SLOT - currentUsed;
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
        if (itemData == null)
        {
            return false;
        }

        bool wasInStorage = GetItemQuantity(itemData) > 0;

        if (base.RemoveItem(itemData, quantity))
        {
            bool isStillInStorage = GetItemQuantity(itemData) > 0;

            if (wasInStorage && !isStillInStorage)
            {
                switch (itemData.type)
                {
                    case ItemType.Normal:
                        if (usedSlots.ContainsKey(ItemType.Normal) && usedSlots[ItemType.Normal] > 0)
                        {
                            usedSlots[ItemType.Normal]--;
                        }
                        break;
                    case ItemType.Consumable:
                        if (usedSlots.ContainsKey(ItemType.Consumable) && usedSlots[ItemType.Consumable] > 0)
                        {
                            usedSlots[ItemType.Consumable]--;
                        }
                        // TODO: in future please check
                        // if item in use or not check! 
                        break;
                }
            }

            totalStacks -= quantity;
            if (totalStacks < 0) totalStacks = 0;
            return true;
        }

        return false;
    }

}
// UnitStorageManager.cs - End