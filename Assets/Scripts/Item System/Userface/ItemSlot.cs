// ItemSlot.cs - Start
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/*

    ItemSlot

    > Most Important Role:
    Correctly Initialize All Slots!

    > Main Role: Track All Players
    Specific Interactions for it's
    drops & buttons. 

    > Major Role: Handle All Stack
    And Item related drag + drop's
    for all player interactions.

    > DO NOT FORGET STACKS ARE IGN
    OBJECT AND A COMPONENT, BUT IG
    THEY ARE PREFAB OBJECTS!

 */

// Reduce Dependancy On Other Classes
public class ItemStackFactory
{
    // Ref ItemStack.cs
    public static ItemStack CreateItemStack(Transform parent)
    {
        GameObject stackGO = new GameObject("ItemStack");
        stackGO.transform.SetParent(parent, false);

        ItemStack itemStack = stackGO.AddComponent<ItemStack>();

        // AddComponent<ItemStack>() above already ran Awake -> InitializeUIComponents(),
        // which owns Image/Text creation. Adding them again here is refused by Unity
        // (both derive from Graphic, which is [DisallowMultipleComponent]) and returns null.
        itemStack.InitializeUIComponents();

        // Set up other necessary components
        ItemSlot slot = parent != null ? parent.GetComponent<ItemSlot>() : null;
        if (slot != null)
        {
            itemStack.SetItemSlot(slot);
            slot.itemStack = itemStack;
        }

        return itemStack;
    }
}


public class ItemSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public bool debugSlot;

    public ItemType? restrictedType;  // null means any type -> allowed? -> unclear answear
    public ItemStack itemStack;       // Reference to the ItemStack component
    public GameObject itemSlotPrefab; // Prefab for the content of the slot, if needed

    [SerializeField]
    public bool AbilitySlot;

    public GameObject itemStackObject;      // Reference to the ItemStack Object -> Realization
    public GameObject itemStackPrefab;      // Reference to the ItemStack Prefab -> Abstractation (should this even exist? + isn't assigned)
    public UnitInventoryUI unitInventoryUI; // Reference to the UnitInventoryUI 
    public UnitInventory unitInventory;     // Reference to the UnitInventory -> Assigned on Creation by creator

    public Sprite icon;
    public int quantity;

    public ItemSlot cargoSlot; // The authoritative physical cargo slot this UI slot represents (or itself if it is a cargo slot)
    public int slotIndex = -1;  // The index of this slot in the inventory

    public bool canTrade, canTransfer, InRange, InTradeRange, hasItem;

    public UnitStorageManager storageManager; // Storage Manager reference - field is declared but not assigned?

    private bool isSelectedForTrade = false;
    private bool isSelectedForTransfer = false;

    private string realName, debugName;

    public ItemSlot GetCargoSlot()
    {
        if (cargoSlot != null) return cargoSlot;
        if (unitInventory != null && slotIndex >= 0 && unitInventory.itemSlots != null && slotIndex < unitInventory.itemSlots.Length)
        {
            return unitInventory.itemSlots[slotIndex];
        }
        return this;
    }

    public int GetSlotIndex()
    {
        if (slotIndex >= 0) return slotIndex;
        if (unitInventory != null)
        {
            return unitInventory.GetSlotNumber(this);
        }
        return -1;
    }

    #region Slot Initialization - Awake + InitializeSlot()

    // ItemSlot.cs 
    private void Awake()
    {
        // Assign storageManager if available on parent
        if (storageManager == null)
        {
            storageManager = GetComponentInParent<UnitStorageManager>();
        }

        // Set up names
        realName = gameObject.name;
        debugName = gameObject.name + " / (Debug Object)";
        gameObject.name = debugSlot ? debugName : realName;

        // Setup references
        if (unitInventoryUI == null)
        {
            unitInventoryUI = GetComponentInParent<UnitInventoryUI>();
        }

        // Not Ability? Initialize ItemStack
        if (!AbilitySlot)
        {
            // Initialize => ItemStack
            IsItemStackSetup(name);
        }
    }

    // Method to set item data
    public void SetItemData(ItemData itemData, int quantity)
    {
        if (itemStack == null)
        {
            itemStack = GetComponentInChildren<ItemStack>() ?? ItemStackFactory.CreateItemStack(transform);
        }
        
        if (itemStack != null)
        {
            if (itemStack.itemSlot == null)
            {
                itemStack.SetItemSlot(this);
            }
            itemStack.SetItemData(itemData, quantity);
        }
        UpdateSlotName();
    }

    private bool empty = false;
    private void UpdateSlotName()
    {
        if (this.itemStack != null && this.itemStack.itemData != null)
        {
            var ItemText = GetComponentInChildren<Text>();

            empty = false;
            int max = this.itemStack.GetMaxQuantity();
            this.gameObject.name = $"{this.realName} - {this.itemStack.itemData.itemName} ({this.itemStack.GetQuantity()}/{max})";

            if (ItemText != null)
            {
                ItemText.text = this.itemStack.GetQuantity() + "/" + max;
            }
        }
        else
        {
            empty = true;
            this.gameObject.name = $"{this.realName} - Empty";
        }

        if (debugSlot)
        {
            Debug.Log($"<color=lightblue>ItemSlot: </color><color=green>Slot updated: </color><color=white>{this.gameObject.name}</color>");
        }
    }

    /// <summary>
    /// Checks  if name is equal too the name is has then returns true 
    /// Otherwise it returns negative meaning name change is required
    /// </summary>
    /// <param name="callerName"></param>
    /// <returns>True / False </returns>
    public bool IsSlotNameGood(string callerName)
    {
        Debug.Log($"<color=lightblue>ItemSlot: </color><color=yellow>NameCheck by </color><color=white>{callerName}</color>");
        // if name is equal too the name is has then returns true otherwise returns negative meaning name change required
        if (!this.empty)
        {
            int max = this.itemStack != null ? this.itemStack.GetMaxQuantity() : 0;
            if (this.gameObject.name == $"{this.realName} - {this.itemStack.itemData.itemName} ({this.itemStack.GetQuantity()}/{max})") return true; 
            else return false;
        }
        else if (this.empty && this.gameObject.name == $"{this.realName} - Empty") return true;
        else return false;
    }

    /// <summary>
    /// Checks name then renames if need be to update and make sure name is accurate before returning the name
    /// </summary>
    /// <returns></returns>
    public string Rename(string callerName = "")
    {
        if (callerName == "") callerName = "itemSlot ";
        // If name is good ...
        if (IsSlotNameGood(callerName)) return this.gameObject.name;      // True: if its good return it
        else UpdateSlotName(); return this.gameObject.name; // False: returns after rename 
    }

    /// <summary>
    /// Checks name then renames if need be to update and make sure name is accurate before returning the name
    /// </summary>
    /// <returns></returns>
    public string RenameSlot()
    {
        // If name is good ...
        if (IsSlotNameGood("itemSlot ")) return this.gameObject.name;      // True: if its good return it
        else UpdateSlotName(); return this.gameObject.name; // False: returns after rename 
    }

    // Prior: public ItemStack itemStack;       // Reference to the ItemStack component

    // ItemSlot.cs
    private bool IsItemStackSetup(string from)
    {
        Debug.Log($"<color=lightblue>ItemSlot: </color><color=yellow>Initializing ItemSlot: </color><color=white>{this.name}</color>");

        // Try to get ItemStack from children
        itemStack = GetComponentInChildren<ItemStack>();
        if (itemStack == null)
        {
            if (itemStackPrefab != null)
            {
                // Instantiate the prefab as itemStackObject
                itemStackObject = Instantiate(itemStackPrefab, this.transform);

                // Get the ItemStack component
                itemStack = itemStackObject.GetComponent<ItemStack>();

                if (itemStack == null)
                {
                    Debug.LogError("<color=red><b>MISSING:</b> ItemSlot: ItemStack component not found on instantiated prefab.</color>");
                    return false;
                }
            }
            else
            {
                // No prefab assigned, use ItemStackFactory to create the ItemStack
                itemStack = ItemStackFactory.CreateItemStack(transform);

                if (itemStack == null)
                {
                    Debug.LogError("<color=red><b>FAILED:</b> ItemSlot: Failed to create ItemStack using ItemStackFactory.</color>");
                    return false;
                }
            }
        }

        if (itemStack != null && itemStack.itemSlot == null)
        {
            itemStack.SetItemSlot(this);
        }

        // Continue initializing references
        if (unitInventoryUI == null)
        {
            unitInventoryUI = GetComponentInParent<UnitInventoryUI>();
        }
        if (unitInventory == null)
        {
            unitInventory = unitInventoryUI != null ? unitInventoryUI.unitInventory : GetComponentInParent<UnitInventory>();
        }
        if (storageManager == null && unitInventory != null)
        {
            storageManager = unitInventory.GetComponent<UnitStorageManager>();
        }

        UpdateSlotName();
        return true;
    }

    // ItemSlot.cs 
    public void InitializeSlot(ItemData itemData, int quantity)
    {
        if (itemStack == null)
        {
            itemStack = GetComponentInChildren<ItemStack>() ?? ItemStackFactory.CreateItemStack(transform);
        }

        if (itemStack != null)
        {
            if (itemStack.itemSlot == null)
            {
                itemStack.SetItemSlot(this);
            }

            itemStack.SetItemData(itemData, quantity);

            // Add button interaction if needed
            Button button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnItemSlotClicked());
            }
        }

        UpdateSlotName();
    }
    #endregion

    #region Find Available Slot - Used By UnitInventory.cs 

    // used by the FindNextAvailableSlot Method in UnitInventoryUI.cs
    public bool CanHoldItemType(ItemType itemType)
    {
        // Determine if the slot can hold/retain the given item type
        return !restrictedType.HasValue || restrictedType.Value == itemType;
    }
    private bool CanReceiveItem(ItemData itemData)
    {
        // Determine if the slot can receive/get the given item 
        return itemData != null && (!restrictedType.HasValue || restrictedType.Value == itemData.type);
    }

    /// <summary>
    /// Does Both Method Operations:
    /// CanHoldItemType (ItemType itemType)
    /// CanReceiveItem (ItemData itemData)
    /// </summary>
    /// <param name="itemData"></param>
    /// <param name="itemType"></param>
    /// <returns>Returns True Or False</returns>
    private bool CanReceiveRetainReturn(ItemData itemData, ItemType itemType)
    {
        // Logs
        if (itemData == null) { Debug.Log("<color=lightblue>ItemSlot: </color><color=red><b>NULL:</b> ItemData is Null</color>"); return false; }
        else
        {
            if (CanReceiveItem(itemData)) Debug.Log($"<color=lightblue>ItemSlot: </color><color=green>Could Receive!</color> <color=white>ItemData: {itemData.itemName}</color>"); 
            else Debug.Log($"<color=lightblue>ItemSlot: </color><color=orange>Could Not Receive!</color> <color=white>ItemData: {itemData.itemName}</color>");

            if (CanHoldItemType(itemType)) Debug.Log($"<color=lightblue>ItemSlot: </color><color=green>Could Hold!</color> <color=white>ItemType: {itemType}</color>");
            else Debug.Log($"<color=lightblue>ItemSlot: </color><color=orange>Could Not Hold!</color> <color=white>ItemType: {itemType}</color>");

            if (CanReceiveItem(itemData) && CanHoldItemType(itemType)) { return true; } else { return false; }
        }
    }

    // used by the FindNextAvailableSlot Method in UnitInventoryUI.cs
    public bool IsOccupied()
    {
        return itemStack != null && itemStack.HasItem();
    }

    #endregion

    #region Trade Logic - Itemslot.cs - Used By UnitInventoryUI.cs
    private bool CanTrade()
    {
        if (hasItem) { canTrade = true; } else { canTrade = false; }

        return canTrade;
    }

    private bool CanTradeWithUnit(Unit unit)
    {
        // Is Unit Trader?
        // Trader Check on Unit Inrange 

        return true;
    }

    private bool UnitInRange()
    {
        // Is Unit Inrange?
        // Check if Units inside unit Trade Radius Range

        // Is Unit Inrange Friends? or Foes?
        // Friendly Foe Check on Unit Inrange 

        // Is Unit Trader?
        // Trader Check on Unit Inrange 

        // Is Unit Tradable?
        // Tradable Check on Unit Inrange 

        return InRange;
    }

    private bool CheckTradeStatus()
    {
        if (canTrade && InRange) { InTradeRange = true; } else { InTradeRange = false; }

        return InTradeRange;
    }
    #endregion

    #region Item Slot Click
    public void OnItemSlotClicked()
    {
        // Logic for handling item slot clicks

        // 1.1 Transfer Item to Closest Player Owned Unit
        // With a Inventory Slot that's Matching itemType
        // but has space, or a into a empty. Which btw, I
        // think this should be the standrd for how items
        // get sent always. By Matching Item to storage w.
        // space left filling that up first before, reduce
        // waste if space, we add any remains to an empty 
        // slot if it exists! otherwise deny the transfer.

        // 1.2 Transfer with the Selected Tranfer rate from
        // the menu. Which we already have programmed prior
        if (canTransfer)
        {
            ToggleSelectionForTransfer();
        }

        // 2. Trade with the Selected Friendly Unit inrange
        // If Inrange & Able to Trade but not the prior two
        // then Trade w. the Selected unit at transfer rate

        if (canTrade)
        {
            ToggleSelectionForTrade();
        }
    }
    #endregion

    #region Slots Toggle Logic - ToggleSelectionsFor ( Transfer + Trade )
    public void ToggleSelectionForTransfer()
    {
        isSelectedForTrade = !isSelectedForTrade;
        HighlightSlot(isSelectedForTrade, Color.blue);
    }

    public void ToggleSelectionForTrade()
    {
        isSelectedForTransfer = !isSelectedForTransfer;
        HighlightSlot(isSelectedForTransfer, Color.green);
    }
    #endregion

    #region Highlight Slot Button ( Bool on/off + Color )
    private void HighlightSlot(bool highlight, Color color)
    {
        // Visual indication of selection state
        // Example: Change background color based on the highlight parameter
        var backgroundColor = highlight ? color : Color.white; // Example colors
        GetComponent<Image>().color = backgroundColor; // Assuming an Image component is attached for visual representation
    }
    #endregion

    private void PlayDropSound()
    {
        AudioManager audioManager = AudioManager.Instance ?? FindObjectOfType<AudioManager>();
        if (audioManager != null && audioManager.DropIntoSlot != null)
        {
            audioManager.PlaySound(audioManager.DropIntoSlot);
        }
    }

    private void HandleItemDrop(ItemStack droppedItem)
    {
        if (droppedItem == null || droppedItem == this.itemStack)
        {
            Debug.Log("<color=lightblue>ItemSlot: </color><color=yellow>Dropped Item is Null or same stack</color>");
            return;
        }

        if (droppedItem.itemData == null || droppedItem.GetQuantity() <= 0)
        {
            Debug.Log("<color=lightblue>ItemSlot: </color><color=yellow>Dropped Item has no item data or quantity <= 0</color>");
            return;
        }

        if (debugSlot) 
        {
            // Debug Slot  
            SwapItems(droppedItem);
            PlayDropSound();
        }
        else
        {
            // Normal Slot  
            if (droppedItem.itemData.itemName != null)
            {
                Debug.Log($"<color=lightblue>ItemSlot: </color><color=green>Item Valid On Drop: </color><color=white>{droppedItem.itemData.itemName}</color>");
            }
            else
            {
                Debug.Log($"<color=lightblue>ItemSlot: </color><color=yellow>Item Valid On Drop (using DisplayName): </color><color=white>{droppedItem.itemData.displayName}</color>");
            }

            // Example: Swap items if the slot is not empty
            if (IsOccupied())
            {
                // Slot is Occupied
                if (itemStack.GetItemData() == droppedItem.GetItemData())
                {
                    // Same Item
                    if (!itemStack.IsFull())
                    {
                        // Add
                        // If slot item matches dropped item - Add to quantity
                        int remainder = itemStack.AddQuantity(droppedItem.GetQuantity());

                        // Return Rest of Dropped Item that didn't fit 
                        if (remainder > 0)
                        {
                            droppedItem.SetQuantity(remainder);
                        }
                        else
                        {
                            droppedItem.ClearStack();
                        }

                        UpdateSlotName();
                        if (droppedItem.itemSlot != null)
                        {
                            droppedItem.itemSlot.RenameSlot();
                        }
                        PlayDropSound();
                    }
                    else
                    {
                        // Reject
                        // If slot item isFull - Reject
                        Debug.Log("<color=lightblue>ItemSlot: </color><color=orange><b>REJECTED:</b> Slot is full: Cannot merge dropped item.</color>");
                    }
                }
                else
                {
                    // Swap
                    // If slot item does not match dropped item - Swap Items
                    // Check if source slot can hold target slot's item type
                    if (droppedItem.itemSlot == null || itemStack.itemData == null || droppedItem.itemSlot.CanHoldItemType(itemStack.itemData.type))
                    {
                        SwapItems(droppedItem);
                        PlayDropSound();
                    }
                    else
                    {
                        Debug.Log("<color=lightblue>ItemSlot: </color><color=orange><b>REJECTED:</b> Cannot swap: Source slot restricted type mismatch.</color>");
                    }
                }
            }
            else
            {
                // Slot is Empty
                if (itemStack == null)
                {
                    itemStack = ItemStackFactory.CreateItemStack(transform);
                }

                if (itemStack != null)
                {
                    itemStack.SetItemData(droppedItem.GetItemData(), droppedItem.GetQuantity());
                    ItemSlot sourceSlot = droppedItem.itemSlot;
                    droppedItem.ClearStack();
                    UpdateSlotName();
                    if (sourceSlot != null)
                    {
                        sourceSlot.RenameSlot();
                    }
                    PlayDropSound();
                }
            }
        }
    }

    private void SwapItems(ItemStack droppedItem)
    {
        var targetData = itemStack != null ? itemStack.GetItemData() : null;
        var targetQuantity = itemStack != null ? itemStack.GetQuantity() : 0;

        var sourceData = droppedItem.GetItemData();
        var sourceQuantity = droppedItem.GetQuantity();

        if (itemStack == null)
        {
            itemStack = ItemStackFactory.CreateItemStack(transform);
        }

        itemStack.SetItemData(sourceData, sourceQuantity);

        if (targetData != null && targetQuantity > 0)
        {
            droppedItem.SetItemData(targetData, targetQuantity);
        }
        else
        {
            droppedItem.ClearStack();
        }

        UpdateSlotName();
        if (droppedItem.itemSlot != null)
        {
            droppedItem.itemSlot.RenameSlot();
        }
    }

    public void CheckAndClearSlotIfEmpty()
    {
        if (itemStack != null && itemStack.GetQuantity() <= 0)
        {
            itemStack.ClearStack();
            // keep itemStack for reuse.
        }
    }

    public void UseItem()
    {
        // Example method that uses an item
        if (itemStack != null)
        {
            itemStack.SubtractQuantity(1);
            CheckAndClearSlotIfEmpty();

            // Additional logic for when the item stack reaches zero.
            if (itemStack.GetQuantity() <= 0)
            {
                // Handle the case of zero quantity, e.g., drop into the ocean
            }
        }
    }

    private void PostItemOperation()
    {
        CheckAndClearSlotIfEmpty();
        // Additional logic after item operations.
    }

    // TODO: If a ItemSlot has a stack in it and its quantity reaches 0
    // I want the to clear the slot - But How? remove/clear item stack?
    // A: Just clear it, removing a Stack or Slot is too tedious do over
    // & over again for no reason when we eventually will fill it anyway.

    // TODO: What happens if a stack in the player hand reaches 0 or if
    // a stack in the player hand gets its inventory destoryed? Depends
    // On the type of destruction that takes place and where, but likely
    // that the item just gets dropped into the ocean to float.
    //
    // More conditions later will be needed, here for several scenarios.
    // For buildings its 100% lost like 100% of the time their inventory
    // is destroyed. However for subs / air units its 100% lost if their
    // inventory gets destroyed.

    // TODO: Why isn't the item the boat starts with, Shown inside the
    // boats units inventory? This is something big we must resolve! 

    public bool IsSlotFull()
    {
        return itemStack != null && itemStack.IsFull();
    }

    // ISSUES: Never really used kinda of a Major issue
    public void UpdateSlotUI(int quantity)
    {
        if (quantity == 0) Debug.Log("<color=lightblue>ItemSlot: </color><color=yellow>Stack Content: </color><color=white>0</color>"); // Slot (Empty)

        if (itemStack != null && itemStack.itemData != null)
        {
            Debug.Log($"<color=lightblue>ItemSlot: </color><color=white>UpdateSlotUI: Updating Slot UI by </color><color=yellow>{quantity}</color>");
            if (itemStack.itemIcon != null) itemStack.itemIcon.sprite = itemStack.itemData.Icon;        // Ensure itemIcon is assigned in the inspector
            if (itemStack.itemQuantityText != null) itemStack.itemQuantityText.text = quantity.ToString();      // Ensure itemQuantityText is assigned in the inspector
        }
        else
        {
            // Null Object Can't Exist Either
            Debug.Log("<color=lightblue>ItemSlot: </color><color=yellow>Stack Content: </color><color=white>Null</color>");
        }
    }

    #region HoverStates - OnpointerEnter + OnpointerExit

    public void OnPointerEnter(PointerEventData eventData)
    {
        // TODO: Visual indication of hover state - STATUS: DONE 
        // Visual indication of hover state
        // Maybe -> HighlightSlot(true); or
        // Something similar to it, to indicate
        // That the slot is being hovered atm
        // Helps players Drop items correctly

        HighlightSlot(true, Color.gray);  // Start Highlight when user is hovering
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HighlightSlot(true, Color.white);  // Stop Highlight when no longer hovered

        HighlightSlot(isSelectedForTrade, Color.blue); // Unhighlight unless it's selected for trade
    }

    #endregion

    // How c# attribute "Name" Work behind the sceen
    //public string name
    //{
    //    get
    //    {
    //        return GetName(this);
    //    }
    //    set
    //    {
    //        SetName(this, value);
    //    }
    //}

    /// <summary>
    /// Returns the name of the game object, handling null cases. The name can be optionally enclosed in parentheses.
    /// </summary>
    /// <param name="parentheseOnReturn">If true, the returned name is enclosed in parentheses.</param>
    /// <returns>A string containing the name of the game object, which is 'slot empty' if null. The name is enclosed in parentheses if specified.</returns>
    public string NameNull(bool parentheseOnReturn = false)
    {
        string str;
        // Solves Name error
        if (null == this.gameObject.name) str = "slot empty";
        else str = this.gameObject.name;

        if (parentheseOnReturn) return "(" + str + ")";
        else return str;
    }

    // Why am I checking fucking ItemData for name in
    public ItemData GetItemData() // Causing a Error - itemData attributes can be null 
    {
        if (itemStack != null)
        {
            return itemStack.itemData; // remember this data can be null
        }
        else
        {
            // Incase the data is null
            Debug.LogWarning("<color=orange>ItemSlot: No ItemStack found in this slot.</color>"); 
            return null;
        }
    }


    #region Drag & Drop Methods

    // Set Stack Slot Parent
    public void ReceiveDroppedItem(ItemStack droppedItem)
    {
        if (droppedItem == null || droppedItem == this.itemStack) return;

        if (itemStack == null)
        {
            if (itemStackPrefab != null)
            {
                GameObject stackObj = Instantiate(itemStackPrefab, transform);
                itemStack = stackObj.GetComponent<ItemStack>();
                if (itemStack == null)
                {
                    Debug.LogError("<color=red><b>FAILED:</b> ItemSlot: Failed to instantiate ItemStack.</color>");
                    return;
                }

                // Set the parent of the instantiated ItemStack to this slot
                stackObj.transform.SetParent(transform);

                // Reset the position of the instantiated ItemStack to align correctly in the slot
                stackObj.transform.localPosition = Vector3.zero;

                itemStack.SetItemSlot(this);
            }
            else
            {
                itemStack = ItemStackFactory.CreateItemStack(transform);
            }
        }
        else if (itemStack.itemSlot == null)
        {
            itemStack.SetItemSlot(this);
        }
        
        if (droppedItem.itemData != null && CanReceiveRetainReturn(droppedItem.itemData, droppedItem.itemData.type))
        { 
            HandleItemDrop(droppedItem);
        }
    }

    // Clear Slot of Item Stack
    public void ClearSlot()
    {
        if (itemStack != null)
        {
            itemStack.ClearStack(); // Clears the associated ItemStack
        }

        UpdateSlotUI(0); // Updates the UI to reflect an empty slot

        Debug.Log($"<color=lightblue>ItemSlot: </color><color=white>{gameObject.name}</color> <color=green>cleared.</color>"); // Optional: Logging for debug purposes
    }


    // Handle Item Drop for Inventory
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null || eventData.pointerDrag == null)
        {
            Debug.Log("<color=lightblue>ItemSlot: </color><color=yellow>Drop was Null!</color>");
            return;
        }

        ItemStack droppedItemStack = eventData.pointerDrag.GetComponent<ItemStack>();

        if (droppedItemStack != null && droppedItemStack != this.itemStack && droppedItemStack.itemData != null)
        {
            if (CanReceiveRetainReturn(droppedItemStack.itemData, droppedItemStack.itemData.type))
            {
                HandleItemDrop(droppedItemStack);
            }
            else
            {
                Debug.Log("<color=lightblue>ItemSlot: </color><color=orange><b>REJECTED:</b> Cannot receive item: Restricted type mismatch.</color>");
            }
        }
        else
        {
            Debug.Log("<color=lightblue>ItemSlot: </color><color=yellow>Dropped ItemStack was null, same stack, or had no itemData.</color>");
        }
    }


    #endregion

}
// ItemSlot.cs - End