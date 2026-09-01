// ItemStack.cs - Start
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class ItemStack : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    // READ ME 
    // THIS IS A CODE OBJECT BUT ALSO A FUCKING PREFAB YOU MAKE
    // SURE TO CHECK IF YOU'VE CODED WITH THIS IN MIND YOU FUCK
    // (note to self) - Nets

    // Known Issue
    // Unclear Errors on awake
    // why does the Item Data not show?
    // 

    // AI says Issues: Doesn't account for exceeding the stack Limitation? I dono maybe?
    // Maybe referring to the fact stack on stack on stack limitations is my guess
    // - 2026


    // Item Refs
    public bool filled { get; private set; }
    public Item item { get; private set; }
    public ItemData itemData { get; private set; } // why does the Item Data not show?
    public ItemData current_itemData; 
    public Image itemIcon { get; private set; } 
    public Text itemQuantityText { get; private set; }
    public ItemSlot itemSlot { get; private set; }
    public ItemDragHandler itemDragHandler { get; private set; }
    
    private void Awake()
    {
        if (itemSlot == null)
        {
            itemSlot = GetComponentInParent<ItemSlot>();
        }
        if (itemSlot != null && itemSlot.itemStack == null)
        {
            itemSlot.itemStack = this;
        }
        if (maxQuantity <= 0)
        {
            UpdateMaxQuantity();
        }
        InitializeUIComponents();
    }

    private void Start()
    {
        if (itemSlot == null)
        {
            itemSlot = GetComponentInParent<ItemSlot>();
        }
        if (itemSlot != null && itemSlot.itemStack == null)
        {
            itemSlot.itemStack = this;
        }
        if (maxQuantity <= 0)
        {
            UpdateMaxQuantity();
        }

        if (itemSlot != null && itemSlot.storageManager == null)
        {
            UnitStorageManager storageManager = itemSlot.GetComponentInParent<UnitStorageManager>();
            if (storageManager != null)
            {
                itemSlot.storageManager = storageManager;
            }
        }

        InitializeUIComponents();
    }

    // This will just make it get it by it self from its environment
    public void InitializeUIComponents()
    {
        if (itemIcon == null)
        {
            itemIcon = GetComponent<Image>() ?? GetComponentInChildren<Image>();
            if (itemIcon == null)
            {
                itemIcon = gameObject.AddComponent<Image>();
            }
        }

        if (itemQuantityText == null)
        {
            // A HUD slot authors its own quantity label as a SIBLING of this stack, so
            // the searches below never reach it: they would build a second, invisible
            // label instead and leave the visible one stuck on the prefab's "#".
            itemQuantityText = FindSlotQuantityText()
                ?? GetComponentInChildren<Text>()
                ?? GetComponent<Text>();

            if (itemQuantityText == null)
            {
                itemQuantityText = CreateQuantityTextChild();
            }
        }

        if (itemDragHandler == null)
        {
            itemDragHandler = GetComponent<ItemDragHandler>() ?? gameObject.AddComponent<ItemDragHandler>();
        }
    }

    // The quantity label the owning ItemSlot already provides, if it has one. Only
    // graphics that belong to the slot itself count - this stack's own children are
    // skipped so a previously created fallback label is never re-adopted.
    private Text FindSlotQuantityText()
    {
        if (itemSlot == null)
        {
            itemSlot = GetComponentInParent<ItemSlot>();
        }
        if (itemSlot == null) return null;

        foreach (Text candidate in itemSlot.GetComponentsInChildren<Text>(true))
        {
            if (candidate.transform == itemSlot.transform) continue;
            if (candidate.transform.IsChildOf(transform)) continue;
            return candidate;
        }
        return null;
    }

    // Text and Image both derive from Graphic, which is [DisallowMultipleComponent].
    // AddComponent<Text>() on a GameObject that already carries the icon Image is
    // refused by Unity and returns null, so the quantity label needs its own child.
    private Text CreateQuantityTextChild()
    {
        GameObject textGO = new GameObject("Quantity Text", typeof(RectTransform));
        textGO.transform.SetParent(transform, false);

        RectTransform rt = textGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Text text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // without a font nothing renders
        text.raycastTarget = false;                                   // must not intercept drag/drop
        text.alignment = TextAnchor.LowerRight;
        text.color = Color.white;
        text.fontSize = 14;
        
        // Ensure text is not clipped if the slot is small
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return text;
    }

    // directly infers a reference to use
    public void InitializeUIComponents(Image icon, Text quantityText)
    {
        if (icon != null) itemIcon = icon;
        if (quantityText != null) itemQuantityText = quantityText;

        if (itemDragHandler == null)
        {
            itemDragHandler = GetComponent<ItemDragHandler>() ?? gameObject.AddComponent<ItemDragHandler>();
        }
    }


    // Quantities
    public const int DEFAULT_MAX_STACK_SIZE = 40;
    public int maxQuantity = DEFAULT_MAX_STACK_SIZE;
    public int quantity; // public int quantity { get; private set; }
    public bool hasSpace;

    // Is floating in Ocean
    public bool isAboard = false;
    public bool isFloating;

    #region CRUD Operations

    #region Get - Item, Quantity, MaxQuantity, ItemData, ItemSlot
    public int GetQuantity() { return quantity; }
    public int GetMaxQuantity() 
    { 
        if (maxQuantity <= 0) UpdateMaxQuantity();
        return maxQuantity; 
    }
    // Empty is a NORMAL state for all three of these - an unfilled cargo slot reads
    // them on every UI refresh - so they return null quietly instead of logging.
    // Callers already null-check; nothing here treats null as an error.
    public Item GetItem() { if (item != null) return item; return null; }
    public ItemData GetItemData() { if (itemData != null) return itemData; return null; }
    public ItemSlot GetItemSlot() { if (itemSlot != null && !isAboard) return itemSlot; return null; }
    #endregion

    #region Set - Quantity, MaxQuantity, ItemData

    // Set Quantity - ItemStack.cs
    public void SetQuantity(int _quantity)
    {
        this.quantity = _quantity;
        UpdateStackUI(quantity);
    }

    public void UpdateMaxQuantity()
    {
        if (itemSlot == null)
        {
            itemSlot = GetComponentInParent<ItemSlot>();
        }

        if (itemData != null && itemData.type == ItemType.Consumable)
        {
            // Consumables never stack beyond 1 - see UnitStorage.cs notes
            maxQuantity = 1;
        }
        else if (itemData != null && itemData.maxStackSize > 0)
        {
            maxQuantity = itemData.maxStackSize;
        }
        else if (itemSlot != null && itemSlot.storageManager != null && itemSlot.storageManager.maxQuantity > 0)
        {
            maxQuantity = itemSlot.storageManager.maxQuantity;
        }
        else if (itemSlot != null && itemSlot.unitInventory != null && itemSlot.unitInventory.GetComponent<UnitStorageManager>() != null)
        {
            maxQuantity = itemSlot.unitInventory.GetComponent<UnitStorageManager>().maxQuantity;
        }
        else
        {
            UnitStorageManager storageManager = GetComponentInParent<UnitStorageManager>();
            if (storageManager != null && storageManager.maxQuantity > 0)
            {
                maxQuantity = storageManager.maxQuantity;
            }
            else
            {
                maxQuantity = DEFAULT_MAX_STACK_SIZE;
            }
        }
    }

    // Set Max Quantity - ItemStack.cs (Revised) - this must be set
    public void SetMaxQuantity(int maxQuantity)
    {
        if (maxQuantity > 0)
        {
            this.maxQuantity = maxQuantity;
        }
        else
        {
            UpdateMaxQuantity();
        }
    }

    // Set Item Data - ItemStack.cs 
    public void SetItemData(ItemData data)
    {
        itemData = data;
        UpdateMaxQuantity();
        UpdateStackUI(quantity);
    }

    // Set Item Data - ItemStack.cs / issues: Doesn't account for exceeding the stack Limitation
    public void SetItemData(ItemData data, int quantity)
    {
        itemData = data;
        this.quantity = quantity;
        UpdateMaxQuantity();
        UpdateStackUI(quantity);
    }


    // Set Item Slot - ItemStack.cs 
    public void SetItemSlot(ItemSlot slot)
    {
        itemSlot = slot;
        if (slot != null && slot.itemStack != this)
        {
            slot.itemStack = this;
        }
        if (maxQuantity <= 0 || (itemData == null && slot?.storageManager != null))
        {
            UpdateMaxQuantity();
        }
        UpdateStackUI(quantity);
    }

    // Set Item - ItemStack.cs / issues: Doesn't account for exceeding the stack Limitation
    public void SetItem(Item newItem, int quantity)
    {
        item = newItem;

        UpdateStackUI(quantity);
    }
    
    #endregion

    #region Addition - Int, Stacks

    // Add Quantity - ItemStack.cs (Revised)
    public int AddQuantity(int addQuantity)
    {
        if (maxQuantity <= 0) UpdateMaxQuantity();
        int spaceLeft = maxQuantity - quantity;
        int quantityToAdd = Mathf.Min(addQuantity, spaceLeft);
        quantity += quantityToAdd;
        UpdateStackUI(quantity);
        return Mathf.Max(addQuantity - quantityToAdd, 0); // Ensure no negative return
    }

    // Add Stack - ItemStack.cs (Revised)
    public ItemStack AddStack(ItemStack _itemStack)
    {
        if (maxQuantity <= 0) UpdateMaxQuantity();
        int spaceLeft = maxQuantity - quantity;
        int quantityToAdd = Mathf.Min(_itemStack.quantity, spaceLeft);
        quantity += quantityToAdd;
        _itemStack.quantity = Mathf.Max(_itemStack.quantity - quantityToAdd, 0); // Ensure the stack quantity never goes below 0
        UpdateStackUI(quantity);
        return _itemStack;
    }
    #endregion

    #region Subtraction - Remove, Subtract

    // Subtract Quantity - ItemStack.cs
    public void SubtractQuantity(int _quantity)
    {
        this.quantity -= _quantity;
        UpdateStackUI(quantity);
    }

    // Remove Quantity - ItemStack.cs
    public void RemoveQuantity(int quantity)
    {
        this.quantity -= quantity;
        UpdateStackUI(quantity);
    }
    #endregion

    #endregion

    #region Check Operations - GetSpaceLeft, GetStackSpaceleft, IsFull, ...

    /// <summary>
    /// Gets the space left in a stack untill stack is full
    /// </summary>
    /// <param name="quantity"></param>
    /// <param name="maxQuantity"></param>
    /// <returns>int of space left in stack</returns>
    public int GetSpaceLeft(int quantity, int maxQuantity)
    {
        // currant_quantity - maxQuantity = quantity difference
        int spaceLeft = maxQuantity - quantity;

        // If space Left is negative, set to 0 
        if (spaceLeft < 0) spaceLeft = 0;

        // Since No negatives returns allowed!
        return spaceLeft;
    }

    /// <summary>
    /// This should return the number of units left in a stack
    /// </summary>
    /// <returns>space left in a stack</returns>
    public int GetStackSpaceLeft()
    {
        if (maxQuantity <= 0) UpdateMaxQuantity();
        // maxQuantity - quantity = space left
        int spaceLeft = maxQuantity - quantity;

        // If space Left is negative, set to 0 
        if (spaceLeft < 0)
        {
            Debug.Log($"<color=lightblue>ItemStack: </color><color=yellow>StackMinSpaceCalc: </color><color=white>{spaceLeft}</color>");
            spaceLeft = 0;
        }

        // if space left is to great, set to maxLimit
        if (spaceLeft > maxQuantity) 
        {
            Debug.Log($"<color=lightblue>ItemStack: </color><color=yellow>StackMaxSpaceCalc: </color><color=white>{spaceLeft}</color>");
            spaceLeft = maxQuantity;
        }

        // Since No negatives returns allowed!
        // Since No returns greater than maxQuant allowed!
        return spaceLeft;
    }

    /// <summary>
    /// Check if the current quantity is less than the max quantity
    /// </summary>
    /// <param name="quantity"></param>
    /// <param name="maxQuantity"></param>
    /// <returns>hasSpace</returns>
    public bool IsFull(int quantity, int maxQuantity)
    {
        // Check if the current quantity is less than the max quantity
        if (this.quantity >= maxQuantity) return hasSpace = false;
        return hasSpace = true;
    }

    public bool IsFull()
    {
        if (maxQuantity <= 0) UpdateMaxQuantity();
        // Check if the current quantity is less than the max quantity
        return this.quantity >= this.maxQuantity;
    }

    public bool HasSpace(int quantity, int maxQuantity)
    {
        // Check if the current quantity is less than the max quantity
        if (this.quantity >= maxQuantity) return hasSpace = false;
        return hasSpace = true;
    }

    /// <summary>
    /// Check if the current quantity is less than the max quantity
    /// </summary>
    /// <param name="quantity"></param>
    /// <param name="maxQuantity"></param>
    /// <returns>hasSpace</returns>
    public bool IsStackFull()
    {
        if (maxQuantity <= 0) UpdateMaxQuantity();
        // Check if the current quantity is less than the max quantity
        return this.quantity >= this.maxQuantity;
    }

    /// <summary>
    /// Check Quantity - ItemStack.cs
    /// </summary>
    /// <param name="quantity">Number we want to compare it too</param>
    /// <returns>bool from checking if this quantity is great than quantity inputed</returns>
    public bool CheckQuantity(int quantity)
    {
        return this.quantity >= quantity;
    }

    // Check Max Quantity - ItemStack.cs
    public bool CheckMaxQuantity(int quantity)
    {
        if (maxQuantity <= 0) UpdateMaxQuantity();
        return this.quantity + quantity <= maxQuantity;
    }

    #endregion

    #region Extra Operations

    public void UpdateStackUI(int quantity)
    {
        if (quantity == 0) Debug.Log("<color=lightblue>ItemStack: </color><color=yellow>Stack Content: </color><color=white>0</color>");
        //OLD: Destroy(this.gameObject); // Destroy the item if quantity is 0 (Empty) 
        //NEW: hold on there soldier might be a better way..

        if (itemData != null)
        {
            gameObject.SetActive(true);
            if (itemIcon != null)
            {
                itemIcon.enabled = true;
                itemIcon.sprite = itemData.Icon;
            }
            else
            {
                Debug.LogError("<color=red>ItemStack: itemIcon is null.</color>");
            }

            if (itemQuantityText != null)
            {
                itemQuantityText.text = quantity.ToString();
            }
            else
            {
                Debug.LogError("<color=red>ItemStack: itemQuantityText is null.</color>");
            }

            Debug.Log($"<color=lightblue>ItemStack: </color><color=white>UpdateStackUI by </color><color=yellow>{quantity}</color><color=white> itemQuantityText: </color><color=yellow>{itemQuantityText?.text}</color>");
        }
        else
        {
            Debug.Log("<color=lightblue>ItemStack: </color><color=yellow>Stack Content: </color><color=white>Null</color>");
        }

        // The Prior Solution:
        //
        //    if (itemData != null)
        //    {
        //        itemIcon.sprite = itemData.Icon;                // Ensure itemIcon is assigned in the inspector on the ItemData, that will assign the itemData.icon to the GUI  <-- Null Ref Error
        //        itemQuantityText.text = quantity.ToString();    // Ensure itemQuantityText is assigned in the inspector
        //        Debug.Log($"<color=lightblue>ItemStack: </color><color=white>UpdateStackUI by </color><color=yellow>{quantity}</color><color=white> itemQuantityText: </color><color=yellow>{itemQuantityText.text}</color>");
        //    } 
        //    else 
        //    {
        //        // Null Object Can't Exist Either
        //        // Destroy(this.gameObject); 
        //        Debug.Log("<color=lightblue>ItemStack: </color><color=yellow>Stack Content: </color><color=white>Null</color>");
        //    }
        //}
    }
    public void SwapItemOnDrop(ItemStack _itemStack)
    {
        Item tempItem = item;
        item = _itemStack.item;
        _itemStack.item = tempItem;
    }

    public bool HasItem()
    {
        return itemData != null;
    }

    public void ClearStack()
    {
        // Clear Stack
        item = null;
        itemData = null;
        if (itemIcon != null) 
        {
            itemIcon.sprite = null;
            itemIcon.enabled = false;
        }
        if (itemQuantityText != null) itemQuantityText.text = "";
        quantity = 0;
        UpdateMaxQuantity();

        Debug.Log("<color=lightblue>ItemStack: </color><color=green>Stack cleared.</color>"); // Debugging to confirm the action
        
        // Update the UI
        UpdateStackUI(quantity);
    }
    #endregion

    #region Special Operations - SetReturnPosition (Set Return Position)

    // Stack Return Positions
    public void SetStackPosition(ItemStack stack, Vector2 newPos, Vector2 oldPos)
    {
        // Set The New Stacks Current Drop Location

        oldPos = stack.itemDragHandler.originalPosition;
        stack.itemDragHandler.newPosition = newPos;

        // Return New Position! - Infinity Stacking :O Wowe! 
        if (oldPos == newPos) return; else { oldPos = newPos; }

    }
    #endregion

    #region Interactions - IDropHandler
    // Add methods to interact with the item stack (e.g., drag-and-drop handling)

    // Hover Effect: Start
    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("<color=lightblue>ItemStack: </color><color=yellow>OnMouseEnter</color>");
        // Implement hover effect or tooltip

        if (eventData.pointerDrag != null)
        {
            // Check if the dragged item can be added to the slot
            // if () { }
            Debug.Log($"<color=lightblue>ItemStack: </color><color=yellow>OnPointerEnter: </color><color=white>{eventData.pointerDrag.name}</color>");
        }

        // Temporary Disable Square Selection if clicked on item
    }

    // Hover Effect: End
    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log("<color=lightblue>ItemStack: </color><color=yellow>OnMouseExit</color>");
        // Remove hover effect or tooltip

    }
    // Double Click - Yet to be a defined interaction
    public void OnPointerDoubleClick(PointerEventData eventData)
    {
        Debug.Log("<color=lightblue>ItemStack: </color><color=yellow>OnPointerDoubleClick</color>");
        // Implement logic for double click action on the item stack
        // For example, open item details, use item, etc.

        // When Double Clicking a Stack inslot inside a unit inventory then
        // Select all other stacks.
    }

    // Stack Drop 
    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log("<color=lightblue>ItemStack: </color><color=yellow>OnDrop</color>");
        if (eventData != null && eventData.pointerDrag != null)
        {
            ItemStack droppedItemStack = eventData.pointerDrag.GetComponent<ItemStack>();
            if (droppedItemStack != null && droppedItemStack != this)
            {
                ItemSlot targetSlot = itemSlot != null ? itemSlot : GetComponentInParent<ItemSlot>();
                if (targetSlot != null)
                {
                    targetSlot.OnDrop(eventData);
                }
            }
        }
    }

    #endregion
}

 // ItemStack.cs - end