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
        Unit target = selected_unit ?? (UnitSelections.Instance != null ? UnitSelections.Instance.FocusedUnit : null);
        if (target != null)
        {
            this.unit = target;
            SetUnitInventory(target.unitInventory ?? target.GetComponent<UnitInventory>());
            SetInventory(target.inventory ?? target.GetComponent<Inventory>());
        }
        else if (UnitSelections.Instance != null && UnitSelections.Instance.unitsSelected.Count > 0)
        {
            Unit selectedUnit = UnitSelections.Instance.FocusedUnit ?? UnitSelections.Instance.unitsSelected[0];
            if (selectedUnit != null)
            {
                this.unit = selectedUnit;
                SetUnitInventory(selectedUnit.unitInventory ?? selectedUnit.GetComponent<UnitInventory>());
                SetInventory(selectedUnit.inventory ?? selectedUnit.GetComponent<Inventory>());
            }
            else
            {
                ClearSlots();
            }
        }
        else
        {
            ClearSlots(); // No units are selected
        }

        ApplyAnno2070Theme(this.unit);
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
        ApplyAnno2070Theme(this.unit);
        Debug.Log("<color=green>UnitInventoryUI: Succesful Set Unit! Unit: </color>" + (unit != null ? unit.name : "null"));
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
        SyncSlots();
        ApplyAnno2070Theme(unit ?? (UnitSelections.Instance != null ? UnitSelections.Instance.FocusedUnit : null));
    }

    protected override void Start()
    {
        base.Start();
        SyncSlots();
        ApplyAnno2070Theme(unit ?? (UnitSelections.Instance != null ? UnitSelections.Instance.FocusedUnit : null));
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ApplyAnno2070Theme(unit ?? (UnitSelections.Instance != null ? UnitSelections.Instance.FocusedUnit : null));
    }

    protected virtual void Update()
    {
        Unit target = unit ?? (UnitSelections.Instance != null ? UnitSelections.Instance.FocusedUnit : null);
        if (target != null && statsFooterRoot != null && statsFooterRoot.activeSelf)
        {
            Damageable dmg = target.GetComponent<Damageable>();
            if (dmg != null && footerHealthText != null)
            {
                footerHealthText.text = $"{dmg.currentHealth}/{dmg.totalHealth}";
            }
        }
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
                int abilityIdx = i;
                abilitySlots[i].ShowAbility(definition.icon, definition.displayName);
                Button btn = abilitySlots[i].GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        if (unitAbilities != null && abilityIdx < unitAbilities.Abilities.Count)
                        {
                            unitAbilities.Activate(abilityIdx);
                        }
                    });
                }
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

    #region Anno 2070 Dynamic Theming

    private GameObject statsFooterRoot;
    private Image footerUpkeepIcon;
    private Text footerUpkeepText;
    private Image footerFirepowerIcon;
    private Text footerFirepowerText;
    private Image footerHealthIcon;
    private Text footerHealthText;

    private Image navPortraitImage;
    private Text navCategoryLabel;
    private GameObject navStanceBar;

    public void ApplyAnno2070Theme(Unit targetUnit)
    {
        // 1. Theme this panel's root Image (Inventory Prefab Ship)
        Image rootImg = GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.sprite = ShipUIResourceCache.SlotBackground;
            rootImg.type = Image.Type.Sliced;
            rootImg.color = new Color(0.035f, 0.082f, 0.145f, 0.98f); // Dark slate-blue frame
            rootImg.raycastTarget = true;
        }

        // Parent background (Content Area: Unit)
        if (transform.parent != null)
        {
            Image parentImg = transform.parent.GetComponent<Image>();
            if (parentImg != null)
            {
                parentImg.sprite = ShipUIResourceCache.SlotBackground;
                parentImg.type = Image.Type.Sliced;
                parentImg.color = new Color(0.035f, 0.082f, 0.145f, 0.98f);
            }
        }

        // 2. Theme "Item Section" (Equipment / Abilities on the right)
        Transform itemSection = transform.Find("Item Section");
        if (itemSection != null)
        {
            Image secImg = itemSection.GetComponent<Image>();
            if (secImg != null)
            {
                secImg.sprite = ShipUIResourceCache.SlotBackground;
                secImg.type = Image.Type.Sliced;
                secImg.color = new Color(0.035f, 0.11f, 0.19f, 0.95f); // Dark cyan-blue glass
            }

            // Theme all slots inside Item Section
            foreach (ItemSlot slot in itemSection.GetComponentsInChildren<ItemSlot>(true))
            {
                if (slot == null) continue;
                slot.AbilitySlot = true;
                Image sImg = slot.GetComponent<Image>();
                if (sImg != null)
                {
                    sImg.sprite = ShipUIResourceCache.SlotBackground;
                    sImg.type = Image.Type.Sliced;
                    sImg.color = new Color(0.06f, 0.14f, 0.22f, 0.95f);
                }

                // Add cyan VehicleBadge if not already present
                if (slot.transform.Find("VehicleBadge") == null)
                {
                    GameObject badgeGO = new GameObject("VehicleBadge", typeof(RectTransform), typeof(Image));
                    badgeGO.transform.SetParent(slot.transform, false);
                    RectTransform bRT = badgeGO.GetComponent<RectTransform>();
                    bRT.anchorMin = Vector2.zero;
                    bRT.anchorMax = Vector2.zero;
                    bRT.pivot = Vector2.zero;
                    bRT.anchoredPosition = new Vector2(4f, 4f);
                    bRT.sizeDelta = new Vector2(18f, 18f);
                    Image bImg = badgeGO.GetComponent<Image>();
                    bImg.sprite = ShipUIResourceCache.VehicleBadge;
                    bImg.color = new Color(0f, 0.85f, 0.95f, 0.9f);
                    bImg.raycastTarget = false;
                }
            }
        }

        // 3. Theme Cargo Slots (Row 1, Row 2)
        SyncSlots();
        var slots = (itemSlots != null && itemSlots.Length > 0) ? itemSlots : inventorySlots?.ToArray();
        if (slots != null)
        {
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                Image sImg = slot.GetComponent<Image>();
                if (sImg != null)
                {
                    sImg.sprite = ShipUIResourceCache.SlotBackground;
                    sImg.type = Image.Type.Sliced;
                    sImg.color = new Color(0.07f, 0.14f, 0.24f, 0.95f);
                }

                // Theme quantity text to crisp white/cyan
                foreach (Text txt in slot.GetComponentsInChildren<Text>(true))
                {
                    if (txt.transform == slot.transform) continue;
                    txt.color = new Color(0.88f, 0.95f, 1f, 1f);
                    txt.fontStyle = FontStyle.Bold;
                }
                foreach (TextMeshProUGUI tmp in slot.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (tmp.transform == slot.transform) continue;
                    tmp.color = new Color(0.88f, 0.95f, 1f, 1f);
                    tmp.fontStyle = FontStyles.Bold;
                }
            }
        }

        // 4. Theme Header ("Nav bar")
        Transform navBar = FindNavBarTransform();
        if (navBar != null)
        {
            Image navImg = navBar.GetComponent<Image>();
            if (navImg != null)
            {
                navImg.sprite = ShipUIResourceCache.SlotBackground;
                navImg.type = Image.Type.Sliced;
                navImg.color = new Color(0.04f, 0.09f, 0.16f, 1f); // Dark header bar
            }

            // A. Ship Portrait on the left of Nav bar
            Transform portT = navBar.Find("Anno_ShipPortrait");
            if (portT == null)
            {
                GameObject portGO = new GameObject("Anno_ShipPortrait", typeof(RectTransform), typeof(Image));
                portGO.transform.SetParent(navBar, false);
                portGO.transform.SetAsFirstSibling();
                RectTransform portRT = portGO.GetComponent<RectTransform>();
                portRT.anchorMin = new Vector2(0f, 0.5f);
                portRT.anchorMax = new Vector2(0f, 0.5f);
                portRT.pivot = new Vector2(0f, 0.5f);
                portRT.anchoredPosition = new Vector2(4f, 0f);
                portRT.sizeDelta = new Vector2(34f, 34f);
                navPortraitImage = portGO.GetComponent<Image>();
                navPortraitImage.preserveAspect = true;
            }
            else
            {
                navPortraitImage = portT.GetComponent<Image>();
            }

            if (navPortraitImage != null)
            {
                Sprite portrait = null;
                NavalUnit naval = targetUnit != null ? targetUnit.GetComponent<NavalUnit>() : null;
                if (naval != null && naval.Definition != null && naval.Definition.portraitIcon != null)
                {
                    portrait = naval.Definition.portraitIcon;
                }
                if (portrait == null && targetUnit != null)
                {
                    portrait = ShipUIResourceCache.GetVesselPortrait(targetUnit.name);
                }
                if (portrait == null)
                {
                    portrait = ShipUIResourceCache.FleetIcon;
                }
                navPortraitImage.sprite = portrait;
                navPortraitImage.enabled = true;
            }

            // B. Faction / Class Subtitle Label
            Transform catT = navBar.Find("Anno_CategoryLabel");
            if (catT == null)
            {
                GameObject catGO = new GameObject("Anno_CategoryLabel", typeof(RectTransform), typeof(Text));
                catGO.transform.SetParent(navBar, false);
                RectTransform catRT = catGO.GetComponent<RectTransform>();
                catRT.anchorMin = new Vector2(0f, 0.5f);
                catRT.anchorMax = new Vector2(0.5f, 0.5f);
                catRT.pivot = new Vector2(0f, 0.5f);
                catRT.anchoredPosition = new Vector2(44f, 8f);
                catRT.sizeDelta = new Vector2(0f, 14f);
                navCategoryLabel = catGO.GetComponent<Text>();
                navCategoryLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                navCategoryLabel.fontSize = 10;
                navCategoryLabel.fontStyle = FontStyle.Bold;
                navCategoryLabel.color = new Color(0.38f, 0.68f, 0.88f, 1f);
                navCategoryLabel.alignment = TextAnchor.MiddleLeft;
            }
            else
            {
                navCategoryLabel = catT.GetComponent<Text>();
            }

            if (navCategoryLabel != null)
            {
                NavalUnit naval = targetUnit != null ? targetUnit.GetComponent<NavalUnit>() : null;
                string cat = (naval != null && naval.Definition != null) 
                    ? naval.Definition.GetFactionCategory() 
                    : (targetUnit != null ? targetUnit.unitType.ToString().ToUpperInvariant() : "WARSHIP");
                navCategoryLabel.text = cat;
            }

            // C. Ship Name Text
            Transform nameT = navBar.Find("Ship Name Txt");
            if (nameT != null)
            {
                RectTransform nameRT = nameT.GetComponent<RectTransform>();
                nameRT.anchoredPosition = new Vector2(44f, -7f); // offset under category label
                foreach (Text t in nameT.GetComponentsInChildren<Text>(true))
                {
                    t.color = Color.white;
                    t.fontStyle = FontStyle.Bold;
                }
                foreach (TextMeshProUGUI tmp in nameT.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    tmp.color = Color.white;
                    tmp.fontStyle = FontStyles.Bold;
                }
            }

            // D. Checkmark / Edit Name Button
            Transform editBtn = navBar.Find("Edit Name Btn + Img");
            if (editBtn != null)
            {
                Image eImg = editBtn.GetComponent<Image>();
                if (eImg != null) eImg.color = new Color(0.08f, 0.20f, 0.32f, 1f);
                foreach (Image childImg in editBtn.GetComponentsInChildren<Image>(true))
                {
                    if (childImg != eImg) childImg.color = new Color(0f, 0.90f, 0.95f, 1f);
                }
            }

            // E. Stance Buttons (Patrol, Attack, Defend) in top-right of Nav bar
            if (navStanceBar == null)
            {
                Transform existingStance = navBar.Find("Anno_StanceBar");
                if (existingStance != null)
                {
                    navStanceBar = existingStance.gameObject;
                }
                else
                {
                    navStanceBar = new GameObject("Anno_StanceBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                    navStanceBar.transform.SetParent(navBar, false);
                    RectTransform sRT = navStanceBar.GetComponent<RectTransform>();
                    sRT.anchorMin = new Vector2(1f, 0.5f);
                    sRT.anchorMax = new Vector2(1f, 0.5f);
                    sRT.pivot = new Vector2(1f, 0.5f);
                    sRT.anchoredPosition = new Vector2(-8f, 0f);
                    sRT.sizeDelta = new Vector2(86f, 26f);

                    HorizontalLayoutGroup hlg = navStanceBar.GetComponent<HorizontalLayoutGroup>();
                    hlg.spacing = 4f;
                    hlg.childAlignment = TextAnchor.MiddleRight;
                    hlg.childControlWidth = false;
                    hlg.childControlHeight = false;

                    CreateStanceButton(navStanceBar.transform, "Patrol", ShipUIResourceCache.CycleIcon, () =>
                    {
                        Debug.Log($"[Anno 2070] Patrol ordered for {(unit != null ? unit.name : "vessel")}.");
                    });

                    CreateStanceButton(navStanceBar.transform, "Defend", ShipUIResourceCache.ShieldIcon, () =>
                    {
                        Debug.Log($"[Anno 2070] Defensive stance ordered for {(unit != null ? unit.name : "vessel")}.");
                    });

                    CreateStanceButton(navStanceBar.transform, "Attack", ShipUIResourceCache.ShipMoveIcon, () =>
                    {
                        Debug.Log($"[Anno 2070] Aggressive / Attack stance ordered for {(unit != null ? unit.name : "vessel")}.");
                    });
                }
            }
        }

        // 5. Theme the 6 Action Buttons below the panel (Build, Destroy, Relation, Trade, Func., Cam)
        ThemeActionButtons();

        // 6. Dock and update the Anno 2070 Stats Footer
        UpdateStatsFooter(targetUnit);
    }

    private void CreateStanceButton(Transform parent, string name, Sprite icon, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGO = new GameObject($"Stance_{name}", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(24f, 24f);

        Image img = btnGO.GetComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.color = new Color(0.85f, 0.94f, 1f, 1f);

        Button btn = btnGO.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.85f, 0.94f, 1f, 0.9f);
        cb.highlightedColor = new Color(0f, 0.85f, 1f, 1f);
        cb.pressedColor = new Color(0f, 0.60f, 0.90f, 1f);
        btn.colors = cb;
        if (onClick != null) btn.onClick.AddListener(onClick);
    }

    private Transform FindNavBarTransform()
    {
        if (transform.parent != null && transform.parent.parent != null)
        {
            Transform nav = transform.parent.parent.Find("Nav bar");
            if (nav != null) return nav;
        }
        if (transform.parent != null)
        {
            Transform nav = transform.parent.Find("Nav bar");
            if (nav != null) return nav;
        }
        Transform hudBot = transform.root.Find("Graphical User Interface/HUD Bot") ?? transform.root.Find("HUD Bot");
        if (hudBot != null)
        {
            foreach (Transform child in hudBot.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Nav bar") return child;
            }
        }
        return null;
    }

    private void ThemeActionButtons()
    {
        Transform hudBot = transform.root.Find("Graphical User Interface/HUD Bot") ?? transform.root.Find("HUD Bot");
        Transform searchRoot = hudBot ?? transform.root;

        string[] btnKeywords = new string[] { "Build", "Destroy", "Relation", "Trade", "Func", "Cam" };

        foreach (Button btn in searchRoot.GetComponentsInChildren<Button>(true))
        {
            if (btn == null) continue;
            string n = btn.gameObject.name;
            bool isActionBtn = false;
            foreach (string kw in btnKeywords)
            {
                if (n.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isActionBtn = true;
                    break;
                }
            }
            if (!isActionBtn) continue;

            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = ShipUIResourceCache.SlotBackground;
                img.type = Image.Type.Sliced;
                img.color = new Color(0.06f, 0.14f, 0.24f, 1f);
            }

            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.06f, 0.14f, 0.24f, 1f);
            cb.highlightedColor = new Color(0.12f, 0.28f, 0.45f, 1f);
            cb.pressedColor = new Color(0.08f, 0.38f, 0.60f, 1f);
            cb.selectedColor = new Color(0.10f, 0.24f, 0.38f, 1f);
            btn.colors = cb;

            foreach (Text t in btn.GetComponentsInChildren<Text>(true))
            {
                t.color = new Color(0.88f, 0.95f, 1f, 1f);
                t.fontStyle = FontStyle.Bold;
            }
            foreach (TextMeshProUGUI tmp in btn.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                tmp.color = new Color(0.88f, 0.95f, 1f, 1f);
                tmp.fontStyle = FontStyles.Bold;
            }
        }
    }

    private void UpdateStatsFooter(Unit targetUnit)
    {
        if (statsFooterRoot == null)
        {
            statsFooterRoot = new GameObject("Anno_StatsFooter", typeof(RectTransform), typeof(Image));
            statsFooterRoot.transform.SetParent(transform, false);

            RectTransform rt = statsFooterRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 26f);

            Image img = statsFooterRoot.GetComponent<Image>();
            img.color = new Color(0.03f, 0.07f, 0.12f, 1f);

            // 1. Upkeep (Left)
            footerUpkeepIcon = CreateFooterStatPair(statsFooterRoot.transform, "Upkeep", ShipUIResourceCache.BalanceIcon, out footerUpkeepText, new Vector2(10f, 0f));

            // 2. Firepower (Center)
            footerFirepowerIcon = CreateFooterStatPair(statsFooterRoot.transform, "Firepower", ShipUIResourceCache.AttackPowerIcon, out footerFirepowerText, new Vector2(170f, 0f));

            // 3. Health (Right)
            footerHealthIcon = CreateFooterStatPair(statsFooterRoot.transform, "Health", ShipUIResourceCache.HealthIcon, out footerHealthText, new Vector2(340f, 0f));
            if (footerHealthText != null) footerHealthText.color = new Color(0f, 0.95f, 0.40f, 1f);
        }

        if (statsFooterRoot != null)
        {
            statsFooterRoot.SetActive(targetUnit != null);
        }

        if (targetUnit == null) return;

        NavalUnit naval = targetUnit.GetComponent<NavalUnit>();
        Damageable dmg = targetUnit.GetComponent<Damageable>();

        // Upkeep
        int upkeep = 25;
        if (naval != null && naval.Definition != null) upkeep = naval.Definition.maintenanceCost;
        if (footerUpkeepText != null) footerUpkeepText.text = upkeep > 0 ? $"-{upkeep}" : $"{upkeep}";

        // Firepower
        string firepower = "-/-/-";
        if (naval != null && naval.Definition != null) firepower = naval.Definition.GetFirepowerSummary();
        if (footerFirepowerText != null) footerFirepowerText.text = firepower;

        // Health
        int maxHp = dmg != null ? dmg.totalHealth : 100;
        int curHp = dmg != null ? dmg.currentHealth : 100;
        if (naval != null && naval.Definition != null && maxHp <= 100)
        {
            maxHp = naval.Definition.maxHealth;
            if (curHp <= 100) curHp = maxHp;
        }
        if (footerHealthText != null) footerHealthText.text = $"{curHp}/{maxHp}";
    }

    private Image CreateFooterStatPair(Transform parent, string name, Sprite icon, out Text textComp, Vector2 pos)
    {
        GameObject pairGO = new GameObject($"Stat_{name}", typeof(RectTransform));
        pairGO.transform.SetParent(parent, false);

        RectTransform rt = pairGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(140f, 24f);

        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(pairGO.transform, false);
        RectTransform iRT = iconGO.GetComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = Vector2.zero;
        iRT.sizeDelta = new Vector2(18f, 18f);
        Image img = iconGO.GetComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;

        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        txtGO.transform.SetParent(pairGO.transform, false);
        RectTransform tRT = txtGO.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 0f);
        tRT.anchorMax = new Vector2(1f, 1f);
        tRT.offsetMin = new Vector2(22f, 0f);
        tRT.offsetMax = Vector2.zero;

        textComp = txtGO.GetComponent<Text>();
        textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComp.fontSize = 12;
        textComp.fontStyle = FontStyle.Bold;
        textComp.color = new Color(0.9f, 0.95f, 1f, 1f);
        textComp.alignment = TextAnchor.MiddleLeft;

        return img;
    }

    #endregion
}

