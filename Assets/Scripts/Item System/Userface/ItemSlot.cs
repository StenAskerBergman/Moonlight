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

        // Create UI components
        Image itemIcon = stackGO.AddComponent<Image>();
        Text itemQuantityText = stackGO.AddComponent<Text>();

        // Initialize UI components via method
        itemStack.InitializeUIComponents(itemIcon, itemQuantityText);

        // Set up other necessary components

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

    public bool canTrade, canTransfer, InRange, InTradeRange, hasItem;

    public UnitStorageManager storageManager; // Storage Manager reference - field is declared but not assigned?

    private bool isSelectedForTrade = false;
    private bool isSelectedForTransfer = false;

    private string realName, debugName;

    #region Slot Initialization - Awake + InitializeSlot()

    // ItemSlot.cs 
    private void Awake()
    {
        // This might not be ready until start
        itemStack = GetComponentInChildren<ItemStack>();
        if (itemStack == null)
        {
            Debug.LogError("ItemSlot: ItemStack component not found in children.");
            return;
        }

        itemStack = GetComponentInChildren<ItemStack>();
        if (itemStack == null)
        {
            itemStack = ItemStackFactory.CreateItemStack(transform);
        }

        // Assign storageManager
        storageManager = GetComponentInParent<UnitStorageManager>();
        if (storageManager == null)
        {
            Debug.LogError("ItemSlot: storageManager is null!");
        }

        // Set up names
        realName = gameObject.name;
        debugName = gameObject.name + " / (Debug Object)";
        gameObject.name = debugSlot ? debugName : realName;

        // Setup references
        unitInventoryUI = GetComponentInParent<UnitInventoryUI>();

        // Not Ability? Initialize ItemStack
        if (!AbilitySlot)
        {
            // Initialize => ItemStack
            if (IsItemStackSetup(name) == false) return;
        }
    }

    // Method to set item data
    public void SetItemData(ItemData itemData, int quantity)
    {
        if (itemStack == null)
        {
            GameObject stackGO = new GameObject($"ItemStack Name:{itemData.name}");
            stackGO.transform.SetParent(transform, false);
            itemStack = stackGO.AddComponent<ItemStack>();
        }
        itemStack.SetItemData(itemData, quantity);
    }

    private bool empty = false;
    private void UpdateSlotName()
    {
        if (this.itemStack != null && this.itemStack.itemData != null)
        {
            var ItemText = GetComponentInChildren<Text>();

            empty = false;
            this.gameObject.name = $"{this.realName} - {this.itemStack.itemData.itemName} ({this.itemStack.GetQuantity()}/{this.maxQuantity})";

            ItemText.text = this.itemStack.GetQuantity() + "/" + this.maxQuantity;
        }
        else
        {
            empty = true;
            this.gameObject.name = $"{this.realName} - Empty";
        }

        if (debugSlot)
        {
            Debug.Log($"Slot updated: {this.gameObject.name}");
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
        Debug.Log("NameCheck by "+callerName);
        // if name is equal too the name is has then returns true otherwise returns negative meaning name change required
        if (!this.empty)
            if (this.gameObject.name == $"{this.realName} - {this.itemStack.itemData.itemName} ({this.itemStack.GetQuantity()}/{this.maxQuantity})") return true; 
            else return false;
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
        Debug.Log($"<color=yellow>Initializing ItemSlot: </color>" + this.name);

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
                    Debug.LogError("ItemStack component not found on instantiated prefab.");
                    return false;
                }

                // If needed, initialize UI components
                // Assuming your prefab already has the UI components set up, you may not need to call InitializeUIComponents here
            }
            else
            {
                // No prefab assigned, use ItemStackFactory to create the ItemStack
                itemStack = ItemStackFactory.CreateItemStack(transform);

                if (itemStack == null)
                {
                    Debug.LogError("Failed to create ItemStack using ItemStackFactory.");
                    return false;
                }
            }
        }

        // Continue initializing references
        unitInventoryUI = GetComponentInParent<UnitInventoryUI>();
        unitInventory = unitInventoryUI != null ? unitInventoryUI.unitInventory : null;

        if (unitInventory == null)
        {
            Debug.LogError("<color=red>UnitInventory reference could not be assigned!</color>");
            return false;
        }

        UpdateSlotName();
        return true;
    }



    // ItemSlot.cs 
    public void InitializeSlot(ItemData itemData, int quantity)
    {
        if (itemStack == null)
        {
            Debug.LogError("ItemStack component not found.");
            return;
        }

        if (itemStack != null)
        {
            itemStack.SetItemData(itemData, quantity);

            // gets our own Image and sets it to item
            itemStack.GetComponent<Image>().sprite = itemData.Icon;

            // Add button interaction if needed
            Button button = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnItemSlotClicked());

            // Add item slot interaction if needed
            ItemSlot itemSlot = GetComponent<ItemSlot>() ?? gameObject.AddComponent<ItemSlot>();
                     itemSlot.unitInventoryUI = this.unitInventoryUI;
                     itemSlot.unitInventory = this.unitInventory;
    
            // ISSUE: Unclear Location of the Current Actual ItemSlots

            // Location: itemSlots in UnitInventoryUI.cs
            for (int i = 0; i < unitInventoryUI.itemSlots.Length; i++)
            { 
                // If Slot is Empty then...
                if (unitInventoryUI.itemSlots[i] == null)
                {
                    // Set Null Slot to This Slot
                    unitInventoryUI.itemSlots[i] = itemSlot;
                    break;
                }

                // If Slot is Not Empty then...
                if (unitInventoryUI.itemSlots[i] != null)
                {
                    // Check if Slot is already Updated
                    if (unitInventoryUI.itemSlots[i].unitInventoryUI == itemSlot.unitInventoryUI) continue;
                    if (unitInventoryUI.itemSlots[i].unitInventory == itemSlot.unitInventory) continue;

                    // Update Slot - Set Slot to New Slot Values
                    unitInventoryUI.itemSlots[i].unitInventoryUI = itemSlot.unitInventoryUI;
                    unitInventoryUI.itemSlots[i].unitInventory = itemSlot.unitInventory;
                    break;
                }
            }

            // Location: itemSlots in UnitInventory.cs
            for (int i = 0; i < unitInventoryUI.unitInventory.itemSlots.Length; i++)
            {
          
                // If Slot is Empty then...
                if (unitInventoryUI.unitInventory.itemSlots[i] == null)
                {
                    unitInventoryUI.unitInventory.itemSlots[i] = itemSlot;
                    break;
                }

                // If Slot is Not Empty then...
                if (unitInventoryUI.unitInventory.itemSlots[i] != null)
                {
                    // Check if Slot is already Updated
                    if (unitInventoryUI.unitInventory.itemSlots[i].unitInventoryUI == itemSlot.unitInventoryUI) continue;
                    if (unitInventoryUI.unitInventory.itemSlots[i].unitInventory == itemSlot.unitInventory) continue;

                    // Update Slot - Set Slot to New Slot Values
                    unitInventoryUI.unitInventory.itemSlots[i].unitInventoryUI = itemSlot.unitInventoryUI;
                    unitInventoryUI.unitInventory.itemSlots[i].unitInventory = itemSlot.unitInventory;
                    break;
                }
            }
        }

        // Desired Outcome: New Fresh Slots 

        // Set hasItem to true
        // hasItem = true;

        // I think this is the right spot?
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
        return !restrictedType.HasValue || restrictedType.Value == itemData.type;
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
        if (itemData == null) { Debug.Log("ItemData is Null"); return false; }
        else
        {
            if (CanReceiveItem(itemData)) Debug.Log($"Could Receive! + ItemData: {itemData}"); 
            else Debug.Log($"Could Not Receive! + ItemData: {itemData}");

            if (CanHoldItemType(itemType)) Debug.Log($"Could Retain! / Could Hold! + ItemType: {itemType}" );
            else Debug.Log($"Could Not Retain! / Could Not Hold! + ItemType: {itemType}");

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

    // Define local maxQuantity here ( Tends to be more convenient )
    private int maxQuantity; 
    private void HandleItemDrop(ItemStack droppedItem)
    {
        if (debugSlot) 
        {
            // Debug Slot  

            SwapItems(droppedItem);

        }
        else
        {
            // Normal Slot  

            // Add logic to handle the dropped item
            // Example: Check if the slot is empty,
            // or if items can be swapped, etc. 

            if (droppedItem == null)
            {
                Debug.Log("Dropped Item is Null");
            }
            else if (droppedItem != null)
            {
                if (droppedItem.itemData.itemName != null)
                {
                    Debug.Log($"Item Wasn't Null On Drop {droppedItem.itemData.itemName}.");
                }
                else
                {
                    Debug.Log($"Item Wasn't Null On Drop but itemData.itemName Was? Trying to get DisplayName! {droppedItem.itemData.displayName}.");
                }
            }


            // Example: Swap items if the slot is not empty
            if (IsOccupied())
            {

                // Slot is Occupied

                // Swap
                // If slot item does not match dropped item - Swap Items
                SwapItems(droppedItem);

                // Reject
                // If slot item isFull - Reject

                // Add
                // If slot item matches dropped item - Add to quantity

                if (itemStack.GetItemData() == droppedItem.GetItemData())
                {
                    int newAdd = itemStack.AddQuantity(droppedItem.GetQuantity());

                    // Return Rest of Dropped Item that didn't fit 
                    //if (newAdd > unitInventoryUI.unitInventory.maxQuantity) 
                    //{ 
                    //    droppedItem.SetQuantity(newAdd - maxQuantity); 
                    //}

                    // + Assuming maxQuantity is defined within this class
                    if (newAdd > maxQuantity)
                    {
                        droppedItem.SetQuantity(newAdd - maxQuantity);
                    }
                }
             
            }
            else
            {
                // Slot is Empty

                // Set the item stack
           
                // Add the dropped item to this slot - Set new item data
                if (droppedItem != null)
                {
                    if (droppedItem.GetItemData() != null)
                    {
                
                        itemStack.SetItemData(droppedItem.GetItemData(), droppedItem.GetQuantity()); // Null Ref. Error - Something is null
                    }
                }
            }
        }
    }

    private void SwapItems(ItemStack droppedItem)
    {
        var tempData = itemStack.GetItemData();
        var tempQuantity = itemStack.GetQuantity();

        itemStack.SetItemData(droppedItem.GetItemData(), droppedItem.GetQuantity());
        droppedItem.SetItemData(tempData, tempQuantity);

        UpdateSlotName();
    }

    public void CheckAndClearSlotIfEmpty()
    {
        if (itemStack != null && itemStack.GetQuantity() <= 0)
        {
            itemStack.ClearStack();
            itemStack = null; // keep itemStack for reuse.
        }
    }

    public void UseItem()
    {
        // Example method that uses an item
        itemStack.SubtractQuantity(1);
        CheckAndClearSlotIfEmpty();

        // Additional logic for when the item stack reaches zero.
        if (itemStack.GetQuantity() <= 0)
        {
            // Handle the case of zero quantity, e.g., drop into the ocean
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
        if (quantity == 0) Debug.Log("Stack Content: 0"); // Slot (Empty)

        if (itemStack.itemData != null)
        {
            Debug.Log($"UpdateSlotUI: Updating Slot UI by {quantity}");
            itemStack.itemIcon.sprite = itemStack.itemData.Icon;        // Ensure itemIcon is assigned in the inspector
            itemStack.itemQuantityText.text = quantity.ToString();      // Ensure itemQuantityText is assigned in the inspector
        }
        else
        {
            // Null Object Can't Exist Either
            Debug.Log("Stack Content: Null");
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
            Debug.LogWarning("No ItemStack found in this slot."); 
            return null;
        }
    }


    #region Drag & Drop Methods

    // Set Stack Slot Parent
    public void ReceiveDroppedItem(ItemStack droppedItem)
    {
        if (itemStack == null)
        {
            GameObject stackObj = Instantiate(itemStackPrefab, transform);
            itemStack = stackObj.GetComponent<ItemStack>();
            if (itemStack == null)
            {
                Debug.LogError("Failed to instantiate ItemStack.");
                return;
            }

            // Set the parent of the instantiated ItemStack to this slot
            stackObj.transform.SetParent(transform);

            // Reset the position of the instantiated ItemStack to align correctly in the slot
            stackObj.transform.localPosition = Vector3.zero;
        }
        
        if (droppedItem != null)
        { 
            // Assuming droppedItem carries all necessary item data
            itemStack.SetItemData(droppedItem.GetItemData(), droppedItem.GetQuantity());
            UpdateSlotUI(itemStack.GetQuantity());  // Update UI to reflect the new item
        }
    }

    // Clear Slot of Item Stack
    public void ClearSlot()
    {
        if (itemStack != null)
        {
            itemStack.ClearStack(); // Clears the associated ItemStack
        }
        itemStack = null; // Ensures the reference is cleared

        UpdateSlotUI(0); // Updates the UI to reflect an empty slot

        Debug.Log($"{gameObject.name} cleared."); // Optional: Logging for debug purposes
    }


    // Handle Item Drop for Inventory
    public void OnDrop(PointerEventData eventData)
    {
        ItemStack droppedItemStack = eventData.pointerDrag.GetComponent<ItemStack>();

        if ( droppedItemStack != null && CanReceiveRetainReturn(droppedItemStack.itemData, droppedItemStack.itemData.type))
        {
            // Handle the dropped item (e.g., swap, merge)
            if (eventData.pointerDrag != null)
            {
                // Pass 'this' as the ItemSlot instance that received the drop
                UnitInventory unitInventory = FindObjectOfType<UnitInventory>(); // Get reference to UnitInventory
                if (unitInventory != null)
                {
                    // Implement logic for when an item is dropped onto this slot
    
                    // For example, swapping items or adding to this slot's contents
                    HandleItemDrop(eventData.pointerDrag.GetComponent<ItemStack>());
                        
                    // Optionally, notify the inventory system...
                    // Placeholder Line - Replace with actual logic to notify unit inv.

                    // FUTURE - NOT NOW LATER
                    // TODO: Don't forget to remove the dropped item from the inventory 
                }
            }
            else
            {
                Debug.Log("Drop was somehow null, or cannot receive item!");
            }
        } 
        else 
        { 
            Debug.Log("Dropp was Null!"); 
        }
        
    }


    #endregion

}
// ItemSlot.cs - End