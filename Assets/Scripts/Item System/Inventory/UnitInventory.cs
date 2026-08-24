// UnitInventory.cs - Start
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;


// Notes: For Future Reference
// Remember that Items are projected onto the Userface
// They are not stored in the Userface, they are stored
// HERE in the UnitInventory.cs, Which doesn't "really" 
// Store them either! but rather just holds onto their
// references

// But Remember: this is as close to the actual unit u get
// if u want to do anything with them (user action wise).

// The Responsibility of this Class is to do the following:
// 1. Provide a clear interface to the item stacks
// That means, Creating, Changing, Updating, the ItemStacks
// That means, Initializing, Sending, and Getting ItemStacks // unsure 89% confidence

// Like What? > Provide Examples : List Them below 
//  

// The Inventory for the Unit
[RequireComponent(typeof(UnitStorageManager),typeof(UnitStorage), typeof(Unit))]
public class UnitInventory : MonoBehaviour, IUniqueIdentifier
{
    // Unique ID
    public string ID { get; private set; }

    // The controllers for the storage
    private UnitStorageManager unitStorageManager;

    // Drag and drop the prefab in the inspector
    public UnitStorageManager unitStorageManagerPrefab;

    // Creates New field for editor visibility
    public List<ItemEntry> itemListForEditor = new List<ItemEntry>();

    // Item Slot Array
    public ItemSlot[] uiSlots;      // UI slot References 
    public ItemSlot[] itemSlots;    // Item Slot References
    // public ItemSlot itemSlot;    // Seems to Serve no purpose?

    // Slot Max Quantity - Placeholder Number
    public int maxQuantity { get; private set; }

    // Note
    // issue is that we are not showing the empty stack > instead we are showing a empty
    // item currently hosted list, but since no items are hosted means nothing is shown.
    // but a empty list bc the stacks are empty

    // RE-WRITE THIS
    // TLDR: SHOWING CURRENT ITEMS -> SHOWING CURRENT SLOT

    bool debugMode = true; //= false;

    // Editor Visibility, Updates ItemList -> Ui API Readability
    private void UpdateItemListForEditor()
    {
        // NOTE: bc curr item is empty hence nothing will be shown here ever... fyi
        // (okay maybe if items actually get loaded it might but for the most parts
        // it won't so fucking fix this already u fucking retard!) (if u've forgtten
        // why read the UnitStorage "Important" Note)

        itemListForEditor.Clear();
        var allItems = unitStorageManager.GetAllItems; // Get the current state from the unitStorageManager
        if (allItems == null || allItems.Count == 0)
        {
            Debug.Log($"<color=red>No items found in UnitStorageManager or list is empty.</color>");
            return;
        }

        foreach (var kvp in allItems)
        {
            itemListForEditor.Add(new ItemEntry { key = kvp.Key, value = kvp.Value });
            Debug.Log($"Updating editor item list with item {kvp.Key.name}, Quantity: {kvp.Value}");
        }
    }

    private void PopulateItemSlots()
    {
        var allItems = unitStorageManager.GetAllItems;
        itemSlots = new ItemSlot[allItems.Count]; // Initialize itemSlots array

        int index = 0;
        foreach (var kvp in allItems)
        {
            if (itemSlots[index] == null)
            {
                GameObject slotGO = CreateSlotGameObject($"ItemSlot {index}");
                ItemSlot slot = slotGO.AddComponent<ItemSlot>();
                slot.unitInventory = this;
                slot.storageManager = unitStorageManager;
                slot.slotIndex = index;
                slot.cargoSlot = slot;
                itemSlots[index] = slot;

                ItemStack itemStack = slot.itemStack ?? slot.GetComponentInChildren<ItemStack>() ?? ItemStackFactory.CreateItemStack(slotGO.transform);
                itemStack.SetItemSlot(slot);
                itemStack.SetItemData(kvp.Key, kvp.Value);
                if (kvp.Key.maxStackSize > 0)
                {
                    itemStack.SetMaxQuantity(kvp.Key.maxStackSize);
                }

                slot.itemStack = itemStack;
            }
            index++;
        }

        UpdateUISlots(); // Ensure UI is updated after populating slots
    }

    private void UpdateUISlots()
    {
        if (uiSlots == null) return;

        for (int i = 0; i < uiSlots.Length; i++)
        {
            if (uiSlots[i] == null) continue;

            if (itemSlots != null && i < itemSlots.Length && itemSlots[i] != null && itemSlots[i].itemStack != null && itemSlots[i].itemStack.HasItem() && itemSlots[i].itemStack.GetQuantity() > 0)
            {
                uiSlots[i].SetItemData(itemSlots[i].itemStack.GetItemData(), itemSlots[i].itemStack.GetQuantity());
            }
            else
            {
                uiSlots[i].ClearSlot();
            }
        }
    }

    private void Awake()
    {
        ID = Guid.NewGuid().ToString();
        // int maxQuantity = 40; maxQuantity = unitStorageManager.maxQuantity; // Added this in here to resolve bug, might be a real bad idea! - Error on 
        
        // Initialize or find the UnitStorageManager
        unitStorageManager = GetComponent<UnitStorageManager>();

        // if unitStorageManager not found
        if (unitStorageManager == null)
        {
            // if unitStorageManager is found on the parent for somereason

            unitStorageManager = GetComponentInParent<UnitStorageManager>();
            if (unitStorageManager != null) Debug.Log("<color=green>UnitStorageManager found in parent successfully.");

            if (unitStorageManager == null)
            {
                // if unitStorageManager is not found, then -> add it
                unitStorageManager = gameObject.AddComponent<UnitStorageManager>();

                if (unitStorageManager != null) Debug.Log("<color=green>UnitStorageManager was added successfully after not being found."); // might break something, Not sure
            }

            // Handle the case where UnitStorageManager is not found
            // might want to add a new UnitStorageManager component here
            // Or log an error

            Debug.Log("<color=yellow>UnitStorageManager component not found, trying to instantiate from prefab...");
            if (unitStorageManagerPrefab != null && unitStorageManager == null)
            {
                unitStorageManager = Instantiate(unitStorageManagerPrefab, transform).GetComponent<UnitStorageManager>();
                
                if (unitStorageManager != null)
                {
                    Debug.Log("<color=green>UnitStorageManager instantiated successfully.");

                }
                else
                {
                    Debug.LogError("Failed to instantiate UnitStorageManager from prefab.");
                }
            }
            else
            {
                Debug.LogError("UnitStorageManager prefab is not assigned.");
            }
        }
        else
        {
            Debug.Log("<color=green><b>SUCCESS:</b> UnitStorageManager component found.</color> by id: " + ID);
        }

        Debug.Assert(unitStorageManager != null, "UnitStorageManager should not be null at this point.");
        Debug.Assert(itemSlots != null, "No suitable slot found - this should not happen.");


        Debug.Log("<color=gray>UnitInventory: Initialized itemSlots count: </color><color=yellow>" + itemSlots.Count(s => s != null) + "</color> - <color=white> Before InitializeItemSlots && Before InitializeStorageManager() </color>");
        InitializeItemSlots(); // Awake - UnitInventory.cs
        Debug.Log("<color=gray>UnitInventory: Initialized itemSlots count: </color><color=yellow>" + itemSlots.Count(s => s != null) + "</color> - <color=white> After InitializeItemSlots && Before InitializeStorageManager() </color>");
        InitializeStorageManager();
        Debug.Log("<color=gray>UnitInventory: Initialized itemSlots count: </color><color=yellow>" + itemSlots.Count(s => s != null) + "</color> - <color=white> After InitializeItemSlots && After InitializeStorageManager() </color>");

    }



    private void InitializeStorageManager()
    {
        unitStorageManager = GetComponent<UnitStorageManager>();
        if (unitStorageManager == null && unitStorageManagerPrefab != null)
        {
            unitStorageManager = Instantiate(unitStorageManagerPrefab, transform).GetComponent<UnitStorageManager>();
        }

        if (unitStorageManager == null)
        {
            Debug.LogError("Failed to initialize UnitStorageManager.");
        }
    }



    private void Start()
    {
        if (unitStorageManager == null)
        {
            Debug.Log($"UnitInventory.cs from {this.transform.name} - <color=red><b><b>MISSING:</b>:</b></color> unitStorageManager");
            // Just Incase the unit StorageManager is not found...
            unitStorageManager = GetComponent<UnitStorageManager>();
        }
    }

    #region Events & Delegates

    public delegate void ItemCountChangedHandler(ItemData itemData, int count);
    public event ItemCountChangedHandler OnItemCountChanged;

    public delegate void UnitInventoryChange();
    public event UnitInventoryChange OnUnitInventoryChanged;

    #endregion

    #region Item Management - Public Interaction Methods

    /// <summary>
    /// Returns all items of a specific inventory.
    /// </summary>
    /// <param name="itemData"></param>
    /// <returns>Dictionary<ItemData, int></returns>
    public Dictionary<ItemData, int> GetAllItems()
    {
        if (unitStorageManager == null)
        {
            Debug.LogWarning("unitStorageManager is being lazily initialized.");
            unitStorageManager = GetComponent<UnitStorageManager>();
            if (unitStorageManager == null && unitStorageManagerPrefab != null)
            {
                unitStorageManager = Instantiate(unitStorageManagerPrefab, transform).GetComponent<UnitStorageManager>();
            }
        }

        if (unitStorageManager != null)
        {
            UpdateItemListForEditor(); // Ensure the editor list is updated
            return unitStorageManager.GetAllItems;
        }
        else
        {
            Debug.LogError("unitStorageManager is still not initialized.");
            return new Dictionary<ItemData, int>();
        }
    }

    // Need a method to set the current units slot 

    // UnitInventory.cs - Main Operators
    private void InitializeItemSlots()
    {
        Debug.Log($"<color=yellow>UnitInventory:</color><color=white> Initializing Slots</color>");
        if (itemSlots == null || itemSlots.Length == 0)
        {
            // Set the desired initial slot count here.
            // int initialSlotCount = 4;
            int defaultSlotCount = 4; 

            itemSlots = new ItemSlot[defaultSlotCount]; 

            for (int i = 0; i < defaultSlotCount; i++)
            {
                Debug.Log($"<color=yellow>UnitInventory:</color><color=white> Slot Order #{i}</color>");
                itemSlots[i] = CreateNewItemSlot(false);
                itemSlots[i].slotIndex = i;
                itemSlots[i].cargoSlot = itemSlots[i];
            }

            Debug.Log($"<color=green> Initialized {defaultSlotCount} item slots.</color>");
        }
        else
        {
            for (int i = 0; i < itemSlots.Length; i++)
            {
                if (itemSlots[i] != null)
                {
                    itemSlots[i].slotIndex = i;
                    itemSlots[i].cargoSlot = itemSlots[i];
                }
            }
        }
    }

    // UnitInventory.cs - Returns a GO for a Unit's Slot
    private GameObject CreateSlotGameObject(string slotName)
    {
        // > Creates the GO for the Slots
        // > Sets Slots GO to Unit as Parent
        
        // Create SlotGO with spesific Naming Convention
        GameObject slotGO = new GameObject(slotName);

        // Set the SlotGO to the Transform of the Unit (aka. Transform)s
        slotGO.transform.SetParent(transform, false);

        // return a GameObject With Slot Component Assigned But Empty
        return slotGO;
    }


    // UnitInventory.cs - Returns a ItemSlot 
    private ItemSlot CreateNewItemSlot(bool resizeArray = true)
    {
        // Create a Name for the Slot
        int slotIndex = itemSlots != null ? itemSlots.Length : 0;
        string slotName = $"ItemSlot {slotIndex}";
        Debug.Log($"<color=orange>Creating New ItemSlot: </color>{slotName}");

        // Pass the Slot Name On & GetSlotGameObjects
        GameObject slotGO = CreateSlotGameObject(slotName);

        // Declare Slot Variable into adding the Slot to SlotGO
        ItemSlot slot = slotGO.AddComponent<ItemSlot>(); 

        // Take Prior Slot -> Assign reference to "this" UnitInventory
        slot.unitInventory = this;
        
        // Assign storageManager
        slot.storageManager = unitStorageManager; 
        slot.slotIndex = slotIndex;
        slot.cargoSlot = slot;

        // Ensure single persistent ItemStack without duplicating
        ItemStack stack = slot.itemStack;
        if (stack == null)
        {
            stack = slot.GetComponentInChildren<ItemStack>();
        }
        if (stack == null)
        {
            stack = ItemStackFactory.CreateItemStack(slotGO.transform);
        }

        if (stack != null)
        {
            Debug.Log($"<color=green>{stack.name} -> ItemStack successfully configured for {stack} in {slotName}</color>");
            stack.SetItemSlot(slot);
            stack.SetItemData(null, 0); // Initialize with no item and quantity zero.
            slot.itemStack = stack;
        }
        else
        {
            Debug.LogError($"Failed to create ItemStack for {slotName}");
        }

        if (resizeArray)
        {
            if (itemSlots == null)
            {
                itemSlots = new ItemSlot[] { slot };
            }
            else
            {
                // Resize itemSlots array to accommodate the new slot
                Array.Resize(ref itemSlots, itemSlots.Length + 1);
                itemSlots[itemSlots.Length - 1] = slot;
                slot.slotIndex = itemSlots.Length - 1;
            }
        }
        return slot;
    }


    // Logging Bools
    public bool log_from;

    // Main method to add items
    /// <returns>True if the amount was actually added; false if it was rejected.</returns>
    public bool AddItem(ItemData itemData, int amount, string? from, bool _log_from = false)
    {
        // Set _log_from to false if 'from' is not provided
        if (string.IsNullOrEmpty(from))
        {
            _log_from = false;
        }

        // Logging based on _log_from or the class-level log_from
        if (_log_from || log_from)
        {
            Debug.Log(
                $"<color=white>{from}</color>" +
                "<color=orange> UnitInventory: </color>" +
                $"<color=white>{this.name}</color>" +
                "<color=yellow> <b>ATTEMPT:</b> to add </color>" +
                $"<color=white>{itemData.name}</color>" +
                "<color=yellow> with amount </color>" +
                $"<color=white>{amount}</color>" +
                "<color=yellow> to </color>" +
                $"<color=white>{this.gameObject.name}</color>"
            );
        }
        else
        {
            Debug.Log(
                $"<color=orange>UnitInventory: </color>" +
                $"<color=white>{this.name}</color>" +
                "<color=yellow> <b>ATTEMPT:</b> to add </color>" +
                $"<color=white>{itemData.name}</color>" +
                "<color=yellow> with amount </color>" +
                $"<color=white>{amount}</color>" +
                "<color=yellow> to </color>" +
                $"<color=white>{this.gameObject.name}</color>"
            );
        }

        // Ensure itemSlots array is initialized
        if (itemSlots == null || itemSlots.Length == 0)
        {
            Debug.Log("Ensure itemSlots array is initialized bc -> itemSlots == null || itemSlots.Length == 0 is True");
            InitializeItemSlots(); // Method: Additems - UnitInventory.cs
        }

        // Find or create an appropriate slot
        ItemSlot slot = GetSlot(itemData, amount);

        // Check if Null
        if (slot == null)
        {
            Debug.Log($"<color=orange>UnitInventory: </color><color=red><b>MISSING:</b> No Suitable Slots!</color> -> <color=white>No suitable slot found.</color> -> <color=orange>Creating a new slot.</color>");
            slot = CreateNewItemSlot(); // Check if this is even possible
        }
        //else if (slot == null) 
        //{
        //    // Does this cause a loop?
        //    // And,
        //    // Why is this here to begin with?
        //
        //    Debug.LogError("No suitable slot found.");
        //    return;
        //}

        // Check if Initialize / Update the Item Stack
        if (!InitializeOrUpdateItemStack(slot, itemData))
        {
            Debug.LogError("Stop if we cannot initialize or update the ItemStacks");
            return false;  // Stop if we cannot initialize or update the ItemStacks
        }

        if (ValidateAndAddItem(slot, itemData, amount))
        {
            Debug.Log($"<color=green><b>Successfully</b></color> added {amount} of {itemData.name} to {slot.name} in {this.name} Unit Inventory.");
            OnUnitInventoryChanged?.Invoke();
            UpdateItemListForEditor();
            UpdateUISlots();
            return true;
        }

        // Rejected by ValidateAndAddItem (capacity, slot budget, ...). It has
        // already logged why; callers must not report this as a success.
        return false;
    }

    // Create ItemStack component
    private bool CreateItemStackInSlot(ItemSlot slot, ItemData itemData)
    {
        Debug.Log($"<color=orange>UnitInventory: </color><color=lightblue>Creating ItemStack in slot </color><color=white>{slot.name}</color><color=lightblue> for item </color><color=white>{itemData?.name ?? "undefined"}</color>");

        if (itemData == null)
        {
            Debug.LogError("<color=red><b>MISSING:</b> UnitInventory: CreateItemStackInSlot: ItemData is null.</color>");
            return false;
        }

        ItemStack itemStack = slot.itemStack ?? slot.GetComponentInChildren<ItemStack>();
        if (itemStack == null)
        {
            itemStack = ItemStackFactory.CreateItemStack(slot.transform);
        }

        if (itemStack == null)
        {
            Debug.LogError("<color=red><b>FAILED:</b> UnitInventory: Failed to create ItemStack.</color>");
            return false;
        }

        itemStack.SetItemSlot(slot);
        itemStack.SetItemData(itemData, 0);
        slot.itemStack = itemStack;
        Debug.Log($"<color=green>Created and assigned new ItemStack for {itemData.name} under slot {slot.name}.</color>");
        return true;
    }


    // Utility method to ensure the ItemStack is correctly initialized or updated
    private bool InitializeOrUpdateItemStack(ItemSlot slot, ItemData itemData)
    {
        Debug.Log($"<color=lightblue>Initializing or updating ItemStack in slot {slot.name} for item {itemData.name}</color>");

        if (slot.itemStack == null)
        {
            Debug.Log($"<color=red><b>NULL DETECTED:</b> {slot.name} ItemStack was <b>null!</b></color><color=yellow> Creating New ItemStack in Slot</color>");
            return CreateItemStackInSlot(slot, itemData);
        }
        else
        {
            return UpdateItemData(slot.itemStack, itemData);
        }
    }


    // Update the ItemStack with new data
    private bool UpdateItemData(ItemStack stack, ItemData newData)
    {
        Debug.Log($"<color=orange>UnitInventory: </color><color=lightblue>Updating ItemStack with new item data for </color><color=white>{newData?.name ?? "undefined"}</color>");

        if (newData == null)
        {
            Debug.LogError("<color=red><b>MISSING:</b> UnitInventory: UpdateItemData: New ItemData is null.</color>");
            return false;
        }

        if (stack.GetItemData() != newData)
        {
            stack.SetItemData(newData);
            Item itemComponent = stack.GetComponent<Item>();
            if (itemComponent != null)
            {
                itemComponent.itemData = newData;
            }
        }
        return true;
    }

    // Validate addition of item and add it
    private bool ValidateAndAddItem(ItemSlot slot, ItemData itemData, int amount)
    {
        Debug.Log($"<color=orange>UnitInventory: </color><color=yellow>Validating and adding item </color><color=white>{itemData.name}</color><color=yellow> with amount </color><color=white>{amount}</color><color=yellow> to slot </color><color=white>{slot.name}</color>");

        if (unitStorageManager != null && !unitStorageManager.CanAddItem(itemData, amount))
        {
            // A full hold is normal gameplay, not a fault - the caller decides what to
            // do with the refusal. LogError here made every legitimate "cargo is full"
            // look like a crash in the console.
            Debug.LogWarning($"<color=orange><b>REJECTED:</b> UnitInventory: Cannot add </color><color=white>{itemData.name}</color><color=orange> with amount </color><color=white>{amount}</color><color=orange> to slot: </color><color=white>{slot.name}</color>");
            return false;
        }

        if (unitStorageManager != null)
        {
            unitStorageManager.AddItem(itemData, amount);
        }

        slot.itemStack.AddQuantity(amount);
        return true;
    }

    public void NotifyInventoryChanged()
    {
        OnUnitInventoryChanged?.Invoke();
        UpdateItemListForEditor();
        UpdateUISlots();
    }

    public bool AddItemToSlot(ItemSlot targetSlot, ItemData itemData, int amount)
    {
        if (targetSlot == null || itemData == null || amount <= 0) return false;

        ItemSlot cargoSlot = targetSlot.cargoSlot != null ? targetSlot.cargoSlot : targetSlot;

        if (!cargoSlot.CanHoldItemType(itemData.type))
        {
            Debug.LogWarning($"<color=orange><b>REJECTED:</b> UnitInventory: Slot {cargoSlot.name} cannot hold item type {itemData.type}</color>");
            return false;
        }

        if (cargoSlot.IsOccupied() && cargoSlot.itemStack.GetItemData() != itemData)
        {
            Debug.LogWarning($"<color=orange><b>REJECTED:</b> UnitInventory: Slot {cargoSlot.name} is occupied by {cargoSlot.itemStack.GetItemData().name}, cannot add {itemData.name}</color>");
            return false;
        }

        if (cargoSlot.IsOccupied() && cargoSlot.itemStack.IsFull())
        {
            Debug.LogWarning($"<color=orange><b>REJECTED:</b> UnitInventory: Slot {cargoSlot.name} is full.</color>");
            return false;
        }

        if (unitStorageManager != null && !unitStorageManager.CanAddItem(itemData, amount))
        {
            Debug.LogWarning($"<color=orange><b>REJECTED:</b> UnitInventory: UnitStorageManager cannot add {amount} of {itemData.name}</color>");
            return false;
        }

        if (unitStorageManager != null)
        {
            unitStorageManager.AddItem(itemData, amount);
        }

        if (cargoSlot.itemStack == null)
        {
            CreateItemStackInSlot(cargoSlot, itemData);
        }

        if (cargoSlot.itemStack.HasItem())
        {
            cargoSlot.itemStack.AddQuantity(amount);
        }
        else
        {
            cargoSlot.itemStack.SetItemData(itemData, amount);
        }

        NotifyInventoryChanged();
        return true;
    }

    public bool AddItemToSlot(int slotIndex, ItemData itemData, int amount)
    {
        if (slotIndex < 0 || itemSlots == null || slotIndex >= itemSlots.Length) return false;
        return AddItemToSlot(itemSlots[slotIndex], itemData, amount);
    }

    public bool RemoveItemFromSlot(ItemSlot targetSlot, int amount)
    {
        if (targetSlot == null || amount <= 0) return false;

        ItemSlot cargoSlot = targetSlot.cargoSlot != null ? targetSlot.cargoSlot : targetSlot;

        if (cargoSlot.itemStack == null || !cargoSlot.itemStack.HasItem() || cargoSlot.itemStack.GetQuantity() <= 0)
        {
            Debug.LogWarning($"<color=orange><b>REJECTED:</b> UnitInventory: Slot {cargoSlot.name} is empty, cannot remove items.</color>");
            return false;
        }

        ItemData itemData = cargoSlot.itemStack.GetItemData();
        int currentQty = cargoSlot.itemStack.GetQuantity();
        int toRemove = Mathf.Min(currentQty, amount);

        if (unitStorageManager != null && !unitStorageManager.CanRemoveItem(itemData, toRemove))
        {
            Debug.LogWarning($"<color=orange><b>REJECTED:</b> UnitInventory: UnitStorageManager cannot remove {toRemove} of {itemData.name}</color>");
            return false;
        }

        if (unitStorageManager != null)
        {
            unitStorageManager.RemoveItem(itemData, toRemove);
        }

        cargoSlot.itemStack.SubtractQuantity(toRemove);
        if (cargoSlot.itemStack.GetQuantity() <= 0)
        {
            cargoSlot.itemStack.ClearStack();
        }

        NotifyInventoryChanged();
        return true;
    }

    public bool RemoveItemFromSlot(int slotIndex, int amount)
    {
        if (slotIndex < 0 || itemSlots == null || slotIndex >= itemSlots.Length) return false;
        return RemoveItemFromSlot(itemSlots[slotIndex], amount);
    }

    public bool RemoveItem(ItemData itemData, int amount)
    {
        if (unitStorageManager != null && unitStorageManager.CanRemoveItem(itemData, amount))
        {
            bool removed = unitStorageManager.RemoveItem(itemData, amount);
            if (removed)
            {
                // Deduct from physical stacks (itemSlots)
                int remainingToRemove = amount;
                if (itemSlots != null)
                {
                    for (int i = 0; i < itemSlots.Length && remainingToRemove > 0; i++)
                    {
                        ItemSlot slot = itemSlots[i];
                        if (slot != null && slot.itemStack != null && slot.itemStack.GetItemData() == itemData)
                        {
                            int currentQty = slot.itemStack.GetQuantity();
                            int toSubtract = Mathf.Min(currentQty, remainingToRemove);
                            slot.itemStack.SubtractQuantity(toSubtract);
                            remainingToRemove -= toSubtract;

                            if (slot.itemStack.GetQuantity() <= 0)
                            {
                                slot.itemStack.ClearStack();
                            }
                        }
                    }
                }

                NotifyInventoryChanged();
                return true;
            }
        }

        // Handle the case where the item cannot be removed or removal failed
        return false;
    }

    // Op: This seems Unfinished
    #region UnitInventory - CanAddToUnit, AddToUnit 
    public bool CanAddToUnit(Unit unit, ItemData item, int quantity) //, out string reason
    {
        // Implement logic to check if the item can be added
        // For example, check if there's enough space in the inventory
        return true; // Placeholder - replace with actual logic
    }
    
    public bool AddToUnit(Unit unit, ItemData itemData, int amount)
    {
        // Implement logic to add the item to unit's inventory
        return true; // Placeholder - replace with actual logic
    }
    #endregion



    public int GetItemQuantity(ItemData itemData)
    {
        // Return the quantity of the given item
        return unitStorageManager.GetItemQuantity(itemData);
    }
    #endregion

    #region ItemSlot Assignment & Related - UnitInventory.cs

    // Notes:
    // > TODO: "Somehow" Needs to be able to determine if slot is within range

    // WATADO: Returns the "Current Fit" Item Slot 

    //  Get Slot based on Item && quant
    private ItemSlot GetSlot(ItemData itemData, int quantity)
    {
        if (itemData == null || itemSlots == null) return null;

        ItemSlot firstEmptySlot = null;

        foreach (var slot in itemSlots)
        {
            if (slot == null || !slot.CanHoldItemType(itemData.type))
            {
                continue;
            }

            if (slot.IsOccupied())
            {
                if (slot.itemStack.GetItemData() == itemData && !slot.IsSlotFull())
                {
                    return slot;
                }
            }
            else if (firstEmptySlot == null)
            {
                firstEmptySlot = slot;
            }
        }

        return firstEmptySlot;
    }

    // Get Best Slot Available - Don't even know if this works anymore
    public ItemSlot GetSlot() 
    {
        // Placeholder logic for finding the best slot
        // You'll need to implement logic based on your game's needs
        
        // Implement logic to check if the item can be added
        // For example, check if there's enough space in the inventory
        
        foreach (var slot in itemSlots)
        {
            if (slot != null && !slot.IsOccupied()) // Assuming IsOccupied() is a method in ItemSlot
            {
                return slot;
            }
        }
        return null; // No available slot found
    }

    // Get Specific Slot - Using Index
    public ItemSlot GetSlot(int Nr)
    {
        if (Nr >= 0 && Nr < itemSlots.Length)
        {
            return itemSlots[Nr];
        }
        return null; // Invalid index
    }

    // Get Specific Slot's ItemData - Using Index
    public ItemData GetSlotItemData(int Nr)
    {
        ItemSlot slot = GetSlot(Nr);
        return slot != null ? slot.GetItemData() : null;
    }

    // What Slot am I? - Inside UnitInventory.cs Called from ItemSlot.cs 
    public int GetSlotNumber(ItemSlot _itemSlot)
    {
        if (_itemSlot == null || itemSlots == null) return -1;

        ItemSlot targetCargo = _itemSlot.cargoSlot != null ? _itemSlot.cargoSlot : _itemSlot;

        for (int i = 0; i < itemSlots.Length; i++)
        {
            if (itemSlots[i] == targetCargo || itemSlots[i] == _itemSlot)
            {
                return i; // Return the index where the slot was found
            }
        }
        return _itemSlot.slotIndex >= 0 ? _itemSlot.slotIndex : -1;
    }
    public int SetSlotNumber(ItemSlot _itemSlot)
    {
        return GetSlotNumber(_itemSlot);
    }

    // Sets the Existing Slots Numbers
    public void SetSlotNumbers(UnitInventoryUI unitInventoryUI)
    {
        foreach (var slot in unitInventoryUI.itemSlots)
        {
            if (slot != null || !slot.IsSlotFull())
            {
                // Log slot details - replace Debug.Log with your logic
                Debug.Log($"<color=orange>UnitInventory: </color><color=white>Slot: {slot.name}</color>");
                Debug.Log($"<color=orange>UnitInventory: </color><color=yellow>Index: {SetSlotNumber(slot)}</color>");
                return;
            }
        }
    }

    // Gets the Existing Slot Numbers
    public void GetSlotNumbers()
    {
        foreach (var slot in itemSlots)
        {
            if (slot != null || !slot.IsSlotFull())
            {
                // Log slot details - replace Debug.Log with your logic
                Debug.Log($"<color=orange>UnitInventory: </color><color=white>Slot: {slot.name}</color>");
                Debug.Log($"<color=orange>UnitInventory: </color><color=yellow>Index: {GetSlotNumber(slot)}</color>");
                return;

            }
        }

    }


    // Purpose: Populate another class's ItemSlot List on Call
    public void ViewItemSlots()
    {
        int trackerInt = 0;
        
        foreach (var slot in itemSlots)
        {
            trackerInt++;
            Debug.Log($"<color=white>Tracker ID: {trackerInt}</color>");

            if (slot != null && slot.itemStack != null)
            {

                // Not sure I want to rename this 
                bool localTestBool = true;
                if (localTestBool)
                {
                    try
                    {
                        slot.Rename("UnitInventory"); // Causes bug if on start for some weird reason ... more investigation required, not to sure why
                    }
                    catch (NullReferenceException nre)
                    {
                        Debug.Log($"<color=orange>UnitInventory: </color><color=red>Unable to rename slot due to null reference: {nre.Message}. Investigating further may be required.</color>");
                        localTestBool = false;
                    }
                }

                // Log slot details - replace Debug.Log with your logic
                Debug.Log(

                    $"<color=white>Slot: </color><color=orange>" + slot.name + // Slot Name
                    $"{slot.itemStack.GetStackSpaceLeft()}" + // Space Left
                    $"/{slot.itemStack.GetMaxQuantity()}" + // Max Quanity
                    $"</color>");
            }
            else if (slot.itemStack == null) // TRUE: slot.stack == null
            {
                // Scenario: slot.stack == null
                Debug.Log($"<color=red>itemStack is null!</color>");

                if (slot != null)
                {
                    // Scenario: slot != null 
                    Debug.Log($"<color=white>Slot wasn't null!</color>");
                }
            }
        }
    }
    #endregion

    // Additional methods as needed, such as methods to get all items,
    // check if the inventory contains a specific item, etc.

}

// UnitInventory.cs - end