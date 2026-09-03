using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Authentic Anno 2070 Selected Unit UI for Moonlight naval vessels and units.
/// Dynamically adapts to each ship design:
/// - Ship portrait and faction classification (e.g. TYCOON WARSHIP, ECO WARSHIP, SUBMARINE)
/// - Top-right curved red order tab (Patrol, Attack, Defensive Stance)
/// - Dynamic cargo holds matching cargoSlotCount (1 to 8 holds)
/// - Equipment sockets with cyan vehicle badges + active abilities with hotkeys and cooldown sweeps
/// - Submarine Dive/Surface toggle, Build Harbor, and Deliver Cargo contextual actions
/// - Bottom stats footer: Upkeep (Balance icon), Firepower (Crosshair icon), and Health (Green cross icon)
/// </summary>
public class UnitInformationPanel : MonoBehaviour
{
    public static UnitInformationPanel Instance { get; private set; }

    [Header("Root Panel")]
    [SerializeField] private GameObject rootPanel;

    [Header("Header Elements")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text categoryLabel;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private Button renameButton;

    [Header("Stance Buttons (Top Right Accent)")]
    [SerializeField] private Button patrolButton;
    [SerializeField] private Button attackButton;
    [SerializeField] private Button stanceButton;

    [Header("Mini Action Buttons (Top Left)")]
    [SerializeField] private Button addFleetButton;
    [SerializeField] private Button targetButton;

    [Header("Cargo Section (Dynamic Holds)")]
    [SerializeField] private Transform cargoSlotContainer;
    [SerializeField] private GameObject cargoSlotPrefab;
    [SerializeField] private GameObject tradeQuantityBar;
    [SerializeField] private Button tradeQty1Btn;
    [SerializeField] private Button tradeQty10Btn;
    [SerializeField] private Button tradeQtyMaxBtn;

    [Header("Sockets & Abilities (2x2 Grid)")]
    [SerializeField] private Transform socketsContainer;
    [SerializeField] private GameObject socketPrefab;

    [Header("Contextual Action Bar")]
    [SerializeField] private GameObject contextualBarRoot;
    [SerializeField] private Button buildHarborButton;
    [SerializeField] private Button deliverCargoButton;
    [SerializeField] private Button diveSurfaceButton;
    [SerializeField] private TMP_Text diveSurfaceText;

    [Header("Bottom Stats Row")]
    [SerializeField] private Image upkeepIcon;
    [SerializeField] private TMP_Text upkeepText;
    [SerializeField] private Image firepowerIcon;
    [SerializeField] private TMP_Text firepowerText;
    [SerializeField] private Image healthIcon;
    [SerializeField] private TMP_Text healthText;

    [Header("Tooltip")]
    [SerializeField] private GameObject tooltipBox;
    [SerializeField] private TMP_Text tooltipText;

    // Runtime state
    private Unit currentUnit;
    private NavalUnit currentNaval;
    private UnitInventory currentInventory;
    private UnitEquipment currentEquipment;
    private UnitAbilities currentAbilities;
    private Damageable currentDamageable;

    // Contextual interaction components
    private BuildInteraction buildInteraction;
    private DeliverInteraction deliverInteraction;
    private DiveInteraction diveInteraction;

    private readonly List<GameObject> activeCargoHoldViews = new List<GameObject>();
    private readonly List<GameObject> activeSocketViews = new List<GameObject>();
    private readonly List<UnitAbilities.RuntimeAbility> displayedAbilities = new List<UnitAbilities.RuntimeAbility>();
    private readonly List<GameObject> activeAbilityButtons = new List<GameObject>();

    private int activeTradeQuantity = 10;
    private bool isDefensiveStance = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;

        EnsurePanelHierarchy();
        WireControls();
        Close();
    }

    private void OnEnable()
    {
        if (UnitSelections.Instance != null)
        {
            UnitSelections.Instance.selectionChanged.AddListener(OnSelectionChanged);
        }
        RefreshSelectedUnit();
    }

    private void OnDisable()
    {
        if (UnitSelections.Instance != null)
        {
            UnitSelections.Instance.selectionChanged.RemoveListener(OnSelectionChanged);
        }
        UnbindCurrentUnit();
    }

    private void Update()
    {
        if (currentUnit == null || rootPanel == null || !rootPanel.activeSelf) return;

        UpdateDynamicStats();
        UpdateAbilityCooldowns();
        UpdateContextualActions();

        // Hotkey activation Q, W, E, R
        if (currentAbilities != null)
        {
            if (Input.GetKeyDown(KeyCode.Q)) TryActivateAbility(0);
            if (Input.GetKeyDown(KeyCode.W)) TryActivateAbility(1);
            if (Input.GetKeyDown(KeyCode.E)) TryActivateAbility(2);
            if (Input.GetKeyDown(KeyCode.R)) TryActivateAbility(3);
        }
    }

    private void OnSelectionChanged(List<Unit> selectedUnits)
    {
        // If multiple units are selected, hide this single panel (Fleet panel handles multi selection)
        if (selectedUnits != null && selectedUnits.Count > 1)
        {
            Close();
            return;
        }

        RefreshSelectedUnit();
    }

    public void SelectUnit(Unit unit)
    {
        BindUnit(unit);
    }

    public void Close()
    {
        UnbindCurrentUnit();
        if (rootPanel != null) rootPanel.SetActive(false);
    }

    private void RefreshSelectedUnit()
    {
        Unit selected = null;
        if (UnitSelections.Instance != null && UnitSelections.Instance.unitsSelected.Count == 1)
        {
            selected = UnitSelections.Instance.unitsSelected[0];
        }

        if (selected != null)
        {
            BindUnit(selected);
        }
        else
        {
            Close();
        }
    }

    private void BindUnit(Unit unit)
    {
        if (unit == null)
        {
            Close();
            return;
        }

        if (unit == currentUnit && rootPanel != null && rootPanel.activeSelf)
        {
            RefreshAll();
            return;
        }

        UnbindCurrentUnit();
        currentUnit = unit;

        currentNaval = currentUnit.GetComponent<NavalUnit>();
        currentInventory = currentUnit.GetComponent<UnitInventory>();
        currentEquipment = currentUnit.GetComponent<UnitEquipment>();
        currentAbilities = currentUnit.GetComponent<UnitAbilities>();
        currentDamageable = currentUnit.GetComponent<Damageable>();

        buildInteraction = currentUnit.GetComponent<BuildInteraction>();
        deliverInteraction = currentUnit.GetComponent<DeliverInteraction>();
        diveInteraction = currentUnit.GetComponent<DiveInteraction>();

        if (currentInventory != null)
        {
            currentInventory.OnUnitInventoryChanged += RefreshCargo;
        }
        if (currentEquipment != null)
        {
            currentEquipment.OnEquipmentChanged += RefreshSocketsAndAbilities;
        }
        if (currentAbilities != null)
        {
            currentAbilities.OnAbilitiesChanged += RefreshSocketsAndAbilities;
        }

        if (rootPanel != null) rootPanel.SetActive(true);
        RefreshAll();
    }

    private void UnbindCurrentUnit()
    {
        if (currentInventory != null)
        {
            currentInventory.OnUnitInventoryChanged -= RefreshCargo;
        }
        if (currentEquipment != null)
        {
            currentEquipment.OnEquipmentChanged -= RefreshSocketsAndAbilities;
        }
        if (currentAbilities != null)
        {
            currentAbilities.OnAbilitiesChanged -= RefreshSocketsAndAbilities;
        }

        currentUnit = null;
        currentNaval = null;
        currentInventory = null;
        currentEquipment = null;
        currentAbilities = null;
        currentDamageable = null;

        buildInteraction = null;
        deliverInteraction = null;
        diveInteraction = null;
    }

    public void RefreshAll()
    {
        if (currentUnit == null) return;

        UpdateHeader();
        UpdateDynamicStats();
        RefreshCargo();
        RefreshSocketsAndAbilities();
        UpdateContextualActions();
    }

    #region Header and Identity

    private void UpdateHeader()
    {
        if (currentUnit == null) return;

        // Resolve Portrait
        Sprite portrait = null;
        if (currentNaval != null && currentNaval.Definition != null && currentNaval.Definition.portraitIcon != null)
        {
            portrait = currentNaval.Definition.portraitIcon;
        }
        if (portrait == null)
        {
            portrait = ShipUIResourceCache.GetVesselPortrait(currentUnit.name);
        }

        if (portraitImage != null)
        {
            if (portrait != null)
            {
                portraitImage.sprite = portrait;
                portraitImage.enabled = true;
            }
            else
            {
                portraitImage.sprite = ShipUIResourceCache.FleetIcon;
                portraitImage.enabled = true;
            }
        }

        // Subtitle / Faction Category
        string category = "WARSHIP";
        if (currentNaval != null && currentNaval.Definition != null)
        {
            var def = currentNaval.Definition;
            if (!string.IsNullOrEmpty(def.factionCategory))
            {
                category = def.factionCategory;
            }
            else if (!string.IsNullOrEmpty(def.displayCategory))
            {
                category = def.displayCategory;
            }
            else
            {
                category = def.navalClass == NavalClass.TradeShip ? "TRADE SHIP" : "WARSHIP";
            }
        }
        else
        {
            category = currentUnit.unitType.ToString().ToUpperInvariant();
        }

        if (categoryLabel != null) categoryLabel.text = category;

        // Display Name
        string dName = !string.IsNullOrEmpty(currentUnit.displayName) ? currentUnit.displayName : currentUnit.name.Replace("(Clone)", "").Trim();
        if (nameLabel != null) nameLabel.text = dName;
    }

    #endregion

    #region Stats Footer

    private void UpdateDynamicStats()
    {
        if (currentUnit == null) return;

        // 1. Upkeep / Maintenance
        int upkeep = 25;
        if (currentNaval != null && currentNaval.Definition != null)
        {
            upkeep = currentNaval.Definition.maintenanceCost;
        }
        if (upkeepText != null)
        {
            upkeepText.text = upkeep > 0 ? $"-{upkeep}" : $"{upkeep}";
        }
        if (upkeepIcon != null && upkeepIcon.sprite == null)
        {
            upkeepIcon.sprite = ShipUIResourceCache.BalanceIcon;
        }

        // 2. Firepower / Combat Damage
        string firepower = "-/-/-";
        if (currentNaval != null && currentNaval.Definition != null)
        {
            firepower = currentNaval.Definition.GetFirepowerSummary();
        }
        if (firepowerText != null)
        {
            firepowerText.text = firepower;
        }
        if (firepowerIcon != null && firepowerIcon.sprite == null)
        {
            firepowerIcon.sprite = ShipUIResourceCache.AttackPowerIcon;
        }

        // 3. Health
        int maxHp = currentDamageable != null ? currentDamageable.totalHealth : 100;
        int curHp = currentDamageable != null ? currentDamageable.currentHealth : 100;
        if (currentNaval != null && currentNaval.Definition != null && maxHp <= 100)
        {
            maxHp = currentNaval.Definition.maxHealth;
            if (curHp <= 100) curHp = maxHp;
        }

        if (healthText != null)
        {
            healthText.text = $"{curHp}/{maxHp}";
        }
        if (healthIcon != null && healthIcon.sprite == null)
        {
            healthIcon.sprite = ShipUIResourceCache.HealthIcon;
        }
    }

    #endregion

    #region Dynamic Cargo Holds

    private void RefreshCargo()
    {
        ClearContainer(activeCargoHoldViews);

        if (cargoSlotContainer == null || currentInventory == null || currentInventory.itemSlots == null) return;

        int holdCount = currentInventory.itemSlots.Length;
        if (currentNaval != null && currentNaval.Definition != null)
        {
            holdCount = Mathf.Max(holdCount, currentNaval.Definition.cargoSlotCount);
        }

        // Configure Grid Layout based on hold count
        var grid = cargoSlotContainer.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            if (holdCount <= 2)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 1;
                grid.cellSize = new Vector2(56f, 56f);
            }
            else if (holdCount <= 4)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 2;
                grid.cellSize = new Vector2(52f, 52f);
            }
            else if (holdCount <= 6)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 3;
                grid.cellSize = new Vector2(48f, 48f);
            }
            else
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 4;
                grid.cellSize = new Vector2(44f, 44f);
            }
        }

        for (int i = 0; i < holdCount; i++)
        {
            int slotIdx = i;
            GameObject holdGO = CreateCargoHoldSlot(cargoSlotContainer, slotIdx);
            activeCargoHoldViews.Add(holdGO);

            // Populate hold data
            ItemSlot physicalSlot = (slotIdx < currentInventory.itemSlots.Length) ? currentInventory.itemSlots[slotIdx] : null;
            UpdateHoldSlotVisual(holdGO, physicalSlot, slotIdx);
        }
    }

    private GameObject CreateCargoHoldSlot(Transform parent, int slotIndex)
    {
        GameObject go = new GameObject($"CargoHold_{slotIndex}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image bg = go.GetComponent<Image>();
        bg.sprite = ShipUIResourceCache.SlotBackground;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.12f, 0.22f, 0.35f, 0.95f);

        // Item Icon Image
        GameObject iconGO = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(go.transform, false);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.15f, 0.15f);
        iconRT.anchorMax = new Vector2(0.85f, 0.85f);
        iconRT.sizeDelta = Vector2.zero;
        Image iconImg = iconGO.GetComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.enabled = false;

        // Stack Count Text (Bottom Right)
        GameObject textGO = new GameObject("StackText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(go.transform, false);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(4f, 2f);
        textRT.offsetMax = new Vector2(-4f, -2f);

        TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 12;
        tmp.alignment = TextAlignmentOptions.BottomRight;
        tmp.color = new Color(0.92f, 0.96f, 1f, 1f);
        tmp.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        // Wire Button Click for Trading / Transfer
        Button btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => OnCargoHoldClicked(slotIndex));

        return go;
    }

    private void UpdateHoldSlotVisual(GameObject holdGO, ItemSlot slot, int slotIndex)
    {
        if (holdGO == null) return;

        Image iconImg = holdGO.transform.Find("ItemIcon")?.GetComponent<Image>();
        TextMeshProUGUI tmp = holdGO.transform.Find("StackText")?.GetComponent<TextMeshProUGUI>();

        if (slot != null && slot.itemStack != null && slot.itemStack.HasItem())
        {
            var data = slot.itemStack.GetItemData();
            int qty = slot.itemStack.GetQuantity();

            if (iconImg != null && data != null && data.Icon != null)
            {
                iconImg.sprite = data.Icon;
                iconImg.enabled = true;
            }

            if (tmp != null)
            {
                tmp.text = $"{qty}";
            }
        }
        else
        {
            if (iconImg != null) iconImg.enabled = false;
            if (tmp != null) tmp.text = "";
        }
    }

    private void OnCargoHoldClicked(int slotIndex)
    {
        if (currentInventory == null || slotIndex >= currentInventory.itemSlots.Length) return;

        var slot = currentInventory.itemSlots[slotIndex];
        if (slot != null && slot.itemStack != null && slot.itemStack.HasItem())
        {
            var data = slot.itemStack.GetItemData();
            string itemName = data != null ? data.displayName : "Item";
            int count = slot.itemStack.GetQuantity();
            ShowTooltip($"{itemName}: {count} units\n(Hold trade quantity: {activeTradeQuantity})");
        }
    }

    #endregion

    #region Sockets & Abilities (2x2 Grid)

    private void RefreshSocketsAndAbilities()
    {
        ClearContainer(activeSocketViews);
        activeAbilityButtons.Clear();
        displayedAbilities.Clear();

        if (socketsContainer == null) return;

        int itemCapacity = currentEquipment != null ? currentEquipment.SlotCapacity : 0;
        if (currentNaval != null && currentNaval.Definition != null)
        {
            itemCapacity = Mathf.Max(itemCapacity, currentNaval.Definition.equipmentSlotCount);
        }

        // 1. Create Equipment Sockets (with cyan vehicle badge)
        for (int i = 0; i < itemCapacity; i++)
        {
            int eqIdx = i;
            GameObject socketGO = CreateEquipmentSocket(socketsContainer, eqIdx);
            activeSocketViews.Add(socketGO);

            ItemData equipped = currentEquipment != null ? currentEquipment.GetItem(eqIdx) : null;
            UpdateSocketVisual(socketGO, equipped, eqIdx);
        }

        // 2. Create Active Ability Buttons
        int abilityCount = currentAbilities != null ? currentAbilities.Abilities.Count : 0;
        for (int i = 0; i < abilityCount; i++)
        {
            var runtime = currentAbilities.GetAbility(i);
            if (runtime == null || runtime.definition == null) continue;
            if (runtime.definition.abilityType == AbilityType.Passive) continue; // Passives don't get active buttons

            int aIdx = i;
            displayedAbilities.Add(runtime);
            GameObject abBtnGO = CreateAbilitySlotButton(socketsContainer, runtime, aIdx);
            activeSocketViews.Add(abBtnGO);
            activeAbilityButtons.Add(abBtnGO);
        }
    }

    private GameObject CreateEquipmentSocket(Transform parent, int socketIndex)
    {
        GameObject go = new GameObject($"ItemSocket_{socketIndex}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image bg = go.GetComponent<Image>();
        bg.sprite = ShipUIResourceCache.SlotBackground;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.10f, 0.18f, 0.28f, 1f);

        // Cyan Vehicle Badge (Bottom Left Corner)
        GameObject badgeGO = new GameObject("VehicleBadge", typeof(RectTransform), typeof(Image));
        badgeGO.transform.SetParent(go.transform, false);
        RectTransform badgeRT = badgeGO.GetComponent<RectTransform>();
        badgeRT.anchorMin = new Vector2(0f, 0f);
        badgeRT.anchorMax = new Vector2(0f, 0f);
        badgeRT.pivot = new Vector2(0f, 0f);
        badgeRT.anchoredPosition = new Vector2(3f, 3f);
        badgeRT.sizeDelta = new Vector2(16f, 16f);

        Image badgeImg = badgeGO.GetComponent<Image>();
        badgeImg.sprite = ShipUIResourceCache.VehicleBadge;
        badgeImg.color = new Color(0f, 0.85f, 0.95f, 0.95f);
        badgeImg.preserveAspect = true;

        // Item Icon Image
        GameObject iconGO = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(go.transform, false);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.2f, 0.2f);
        iconRT.anchorMax = new Vector2(0.8f, 0.8f);
        iconRT.sizeDelta = Vector2.zero;
        Image iconImg = iconGO.GetComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.enabled = false;

        return go;
    }

    private void UpdateSocketVisual(GameObject socketGO, ItemData equipped, int socketIndex)
    {
        if (socketGO == null) return;
        Image iconImg = socketGO.transform.Find("ItemIcon")?.GetComponent<Image>();
        if (equipped != null && equipped.Icon != null)
        {
            if (iconImg != null)
            {
                iconImg.sprite = equipped.Icon;
                iconImg.enabled = true;
            }
        }
        else
        {
            if (iconImg != null) iconImg.enabled = false;
        }
    }

    private GameObject CreateAbilitySlotButton(Transform parent, UnitAbilities.RuntimeAbility runtime, int abilityIndex)
    {
        GameObject go = new GameObject($"AbilitySlot_{abilityIndex}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image bg = go.GetComponent<Image>();
        bg.sprite = ShipUIResourceCache.SlotBackground;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.08f, 0.32f, 0.55f, 1f);

        // Ability Icon
        GameObject iconGO = new GameObject("AbilityIcon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(go.transform, false);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.12f, 0.12f);
        iconRT.anchorMax = new Vector2(0.88f, 0.88f);
        iconRT.sizeDelta = Vector2.zero;
        Image iconImg = iconGO.GetComponent<Image>();
        iconImg.preserveAspect = true;

        Sprite abIcon = runtime.definition != null ? runtime.definition.icon : null;
        if (abIcon != null)
        {
            iconImg.sprite = abIcon;
            iconImg.enabled = true;
        }
        else
        {
            iconImg.sprite = ShipUIResourceCache.AttackPowerIcon;
            iconImg.enabled = true;
        }

        // Radial Cooldown Overlay
        GameObject cdGO = new GameObject("CooldownOverlay", typeof(RectTransform), typeof(Image));
        cdGO.transform.SetParent(go.transform, false);
        RectTransform cdRT = cdGO.GetComponent<RectTransform>();
        cdRT.anchorMin = Vector2.zero;
        cdRT.anchorMax = Vector2.one;
        cdRT.sizeDelta = Vector2.zero;
        Image cdImg = cdGO.GetComponent<Image>();
        cdImg.type = Image.Type.Filled;
        cdImg.fillMethod = Image.FillMethod.Radial360;
        cdImg.fillOrigin = (int)Image.Origin360.Top;
        cdImg.color = new Color(0f, 0f, 0f, 0.65f);
        cdImg.fillAmount = 0f;

        // Hotkey & Cooldown Text
        GameObject labelGO = new GameObject("LabelText", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(go.transform, false);
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.sizeDelta = Vector2.zero;
        TextMeshProUGUI tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 11;
        tmp.alignment = TextAlignmentOptions.BottomRight;
        tmp.color = Color.white;
        tmp.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        string key = abilityIndex switch { 0 => "Q", 1 => "W", 2 => "E", 3 => "R", _ => "" };
        tmp.text = $"[{key}]";

        // Button Click
        Button btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => TryActivateAbility(abilityIndex));

        return go;
    }

    private void UpdateAbilityCooldowns()
    {
        if (currentAbilities == null) return;

        for (int i = 0; i < displayedAbilities.Count && i < activeAbilityButtons.Count; i++)
        {
            var runtime = displayedAbilities[i];
            var btnGO = activeAbilityButtons[i];
            if (runtime == null || btnGO == null) continue;

            Image cdImg = btnGO.transform.Find("CooldownOverlay")?.GetComponent<Image>();
            TextMeshProUGUI tmp = btnGO.transform.Find("LabelText")?.GetComponent<TextMeshProUGUI>();
            Button btn = btnGO.GetComponent<Button>();

            string key = i switch { 0 => "Q", 1 => "W", 2 => "E", 3 => "R", _ => "" };

            if (runtime.IsOnCooldown)
            {
                float totalCd = runtime.definition != null ? runtime.definition.cooldown : 1f;
                if (cdImg != null)
                {
                    cdImg.enabled = true;
                    cdImg.fillAmount = totalCd > 0 ? (runtime.cooldownRemaining / totalCd) : 0f;
                }
                if (tmp != null)
                {
                    tmp.text = $"{runtime.cooldownRemaining:F0}s";
                }
                if (btn != null) btn.interactable = false;
            }
            else
            {
                if (cdImg != null) cdImg.enabled = false;
                if (tmp != null) tmp.text = $"[{key}]";
                if (btn != null) btn.interactable = true;
            }
        }
    }

    private void TryActivateAbility(int index)
    {
        if (currentAbilities == null) return;
        currentAbilities.Activate(index);
    }

    #endregion

    #region Contextual Actions (Build Harbor, Deliver Cargo, Dive/Surface)

    private void UpdateContextualActions()
    {
        bool hasContextAction = false;

        // 1. Build Harbor
        if (buildHarborButton != null)
        {
            bool canBuild = buildInteraction != null && buildInteraction.CanBuild();
            buildHarborButton.gameObject.SetActive(canBuild);
            if (canBuild) hasContextAction = true;
        }

        // 2. Deliver Cargo
        if (deliverCargoButton != null)
        {
            bool canDeliver = deliverInteraction != null && deliverInteraction.CanDeliver();
            deliverCargoButton.gameObject.SetActive(canDeliver);
            if (canDeliver) hasContextAction = true;
        }

        // 3. Dive / Surface Submarine
        if (diveSurfaceButton != null)
        {
            bool isSub = (currentNaval != null && currentNaval.Definition != null && currentNaval.Definition.canSubmerge) || diveInteraction != null;
            diveSurfaceButton.gameObject.SetActive(isSub);
            if (isSub)
            {
                hasContextAction = true;
                bool isSubmerged = diveInteraction != null ? diveInteraction.IsSubmerged : false;
                if (diveSurfaceText != null)
                {
                    diveSurfaceText.text = isSubmerged ? "Surface" : "Dive";
                }
            }
        }

        if (contextualBarRoot != null)
        {
            contextualBarRoot.SetActive(hasContextAction);
        }
    }

    private void OnDiveSurfaceClicked()
    {
        if (diveInteraction != null)
        {
            if (diveInteraction.IsSubmerged)
            {
                diveInteraction.Surface();
            }
            else
            {
                diveInteraction.Dive();
            }
        }
        else if (currentAbilities != null)
        {
            // Activate dive ability if present
            for (int i = 0; i < currentAbilities.Abilities.Count; i++)
            {
                if (currentAbilities.Abilities[i].definition is DiveAbilityDefinition)
                {
                    currentAbilities.Activate(i);
                    break;
                }
            }
        }
        UpdateContextualActions();
    }

    private void OnBuildHarborClicked()
    {
        if (buildInteraction != null)
        {
            buildInteraction.Build(null);
        }
    }

    private void OnDeliverCargoClicked()
    {
        if (deliverInteraction != null)
        {
            deliverInteraction.DeliverAll();
        }
    }

    #endregion

    #region Stance and Order Controls (Top-Right Curved Tab)

    private void WireControls()
    {
        if (patrolButton != null) patrolButton.onClick.AddListener(OnPatrolClicked);
        if (attackButton != null) attackButton.onClick.AddListener(OnAttackClicked);
        if (stanceButton != null) stanceButton.onClick.AddListener(OnStanceClicked);

        if (addFleetButton != null) addFleetButton.onClick.AddListener(OnAddFleetClicked);
        if (targetButton != null) targetButton.onClick.AddListener(OnTargetClicked);

        if (tradeQty1Btn != null) tradeQty1Btn.onClick.AddListener(() => SetTradeQuantity(1));
        if (tradeQty10Btn != null) tradeQty10Btn.onClick.AddListener(() => SetTradeQuantity(10));
        if (tradeQtyMaxBtn != null) tradeQtyMaxBtn.onClick.AddListener(() => SetTradeQuantity(40));

        if (buildHarborButton != null) buildHarborButton.onClick.AddListener(OnBuildHarborClicked);
        if (deliverCargoButton != null) deliverCargoButton.onClick.AddListener(OnDeliverCargoClicked);
        if (diveSurfaceButton != null) diveSurfaceButton.onClick.AddListener(OnDiveSurfaceClicked);

        if (renameButton != null) renameButton.onClick.AddListener(OnRenameClicked);
    }

    private void OnPatrolClicked()
    {
        Debug.Log($"Patrol order dispatched for {currentUnit?.name}");
    }

    private void OnAttackClicked()
    {
        Debug.Log($"Attack order / stance dispatched for {currentUnit?.name}");
    }

    private void OnStanceClicked()
    {
        isDefensiveStance = !isDefensiveStance;
        Debug.Log($"Stance toggled for {currentUnit?.name}: {(isDefensiveStance ? "Defensive" : "Aggressive")}");
        if (stanceButton != null)
        {
            var img = stanceButton.GetComponent<Image>();
            if (img != null)
            {
                img.color = isDefensiveStance ? new Color(0.2f, 0.6f, 0.9f, 1f) : Color.white;
            }
        }
    }

    private void OnAddFleetClicked()
    {
        Debug.Log($"Add to fleet clicked for {currentUnit?.name}");
    }

    private void OnTargetClicked()
    {
        if (currentUnit == null) return;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 target = currentUnit.transform.position;
            cam.transform.position = new Vector3(target.x, cam.transform.position.y, target.z - 15f);
        }
    }

    private void SetTradeQuantity(int qty)
    {
        activeTradeQuantity = qty;
        ShowTooltip($"Trade quantity set to: {qty}");
    }

    private void OnRenameClicked()
    {
        if (currentUnit == null) return;
        currentUnit.displayName = $"{currentUnit.name} II";
        UpdateHeader();
    }

    #endregion

    #region Tooltip & Hierarchy Helpers

    public void ShowTooltip(string text)
    {
        if (tooltipBox == null) return;
        tooltipBox.SetActive(true);
        if (tooltipText != null) tooltipText.text = text;
    }

    public void HideTooltip()
    {
        if (tooltipBox != null) tooltipBox.SetActive(false);
    }

    private void ClearContainer(List<GameObject> list)
    {
        foreach (var go in list)
        {
            if (go != null) Destroy(go);
        }
        list.Clear();
    }

    /// <summary>
    /// Builds the authentic Anno 2070 UI panel procedurally under HUD Bot / Canvas if inspector fields are unassigned.
    /// </summary>
    private void EnsurePanelHierarchy()
    {
        if (rootPanel != null) return;

        Canvas canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Transform parent = canvas.transform.Find("Graphical User Interface/HUD Bot") ?? canvas.transform;

        GameObject root = new GameObject("Anno2070_ShipHUD", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        rootPanel = root;

        RectTransform rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(1f, 0f);
        rootRT.anchorMax = new Vector2(1f, 0f);
        rootRT.pivot = new Vector2(1f, 0f);
        rootRT.anchoredPosition = new Vector2(-20f, 20f);
        rootRT.sizeDelta = new Vector2(360f, 240f);

        Image rootImg = root.GetComponent<Image>();
        rootImg.sprite = ShipUIResourceCache.SlotBackground;
        rootImg.type = Image.Type.Sliced;
        rootImg.color = new Color(0.04f, 0.09f, 0.16f, 0.96f);

        // --- 1. Header Bar ---
        GameObject header = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(root.transform, false);
        RectTransform headerRT = header.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = Vector2.zero;
        headerRT.sizeDelta = new Vector2(0f, 54f);

        Image headerImg = header.GetComponent<Image>();
        headerImg.color = new Color(0.06f, 0.14f, 0.24f, 1f);

        // Portrait
        GameObject portGO = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portGO.transform.SetParent(header.transform, false);
        RectTransform portRT = portGO.GetComponent<RectTransform>();
        portRT.anchorMin = new Vector2(0f, 0.5f);
        portRT.anchorMax = new Vector2(0f, 0.5f);
        portRT.pivot = new Vector2(0f, 0.5f);
        portRT.anchoredPosition = new Vector2(8f, 0f);
        portRT.sizeDelta = new Vector2(44f, 44f);
        portraitImage = portGO.GetComponent<Image>();
        portraitImage.preserveAspect = true;

        // Subtitle (Category)
        GameObject catGO = new GameObject("CategoryLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        catGO.transform.SetParent(header.transform, false);
        RectTransform catRT = catGO.GetComponent<RectTransform>();
        catRT.anchorMin = new Vector2(0f, 1f);
        catRT.anchorMax = new Vector2(0.7f, 1f);
        catRT.pivot = new Vector2(0f, 1f);
        catRT.anchoredPosition = new Vector2(58f, -6f);
        catRT.sizeDelta = new Vector2(0f, 18f);
        categoryLabel = catGO.GetComponent<TextMeshProUGUI>();
        categoryLabel.fontSize = 11;
        categoryLabel.fontStyle = FontStyles.Bold;
        categoryLabel.color = new Color(0.48f, 0.72f, 0.90f, 1f);

        // Title (Ship Name)
        GameObject nameGO = new GameObject("NameLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGO.transform.SetParent(header.transform, false);
        RectTransform nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 0f);
        nameRT.anchorMax = new Vector2(0.7f, 0f);
        nameRT.pivot = new Vector2(0f, 0f);
        nameRT.anchoredPosition = new Vector2(58f, 6f);
        nameRT.sizeDelta = new Vector2(0f, 22f);
        nameLabel = nameGO.GetComponent<TextMeshProUGUI>();
        nameLabel.fontSize = 15;
        nameLabel.fontStyle = FontStyles.Bold;
        nameLabel.color = Color.white;

        // Top-Right Curved Red Accent Tab for Stances
        GameObject stanceTab = new GameObject("StanceTab", typeof(RectTransform), typeof(Image));
        stanceTab.transform.SetParent(header.transform, false);
        RectTransform tabRT = stanceTab.GetComponent<RectTransform>();
        tabRT.anchorMin = new Vector2(1f, 0.5f);
        tabRT.anchorMax = new Vector2(1f, 0.5f);
        tabRT.pivot = new Vector2(1f, 0.5f);
        tabRT.anchoredPosition = new Vector2(-4f, 0f);
        tabRT.sizeDelta = new Vector2(104f, 38f);
        Image tabImg = stanceTab.GetComponent<Image>();
        tabImg.sprite = ShipUIResourceCache.SlotBackground;
        tabImg.type = Image.Type.Sliced;
        tabImg.color = new Color(0.55f, 0.12f, 0.12f, 0.95f);

        // 3 Circular Stance Buttons
        patrolButton = CreateCircularButton(stanceTab.transform, "PatrolBtn", ShipUIResourceCache.CycleIcon, new Vector2(-70f, 0f));
        attackButton = CreateCircularButton(stanceTab.transform, "AttackBtn", ShipUIResourceCache.ShipMoveIcon, new Vector2(-36f, 0f));
        stanceButton = CreateCircularButton(stanceTab.transform, "StanceBtn", ShipUIResourceCache.ShieldIcon, new Vector2(-2f, 0f));

        // --- 2. Body Area (Cargo on Left, Sockets/Abilities on Right) ---
        GameObject body = new GameObject("BodyArea", typeof(RectTransform));
        body.transform.SetParent(root.transform, false);
        RectTransform bodyRT = body.GetComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0f, 0f);
        bodyRT.anchorMax = new Vector2(1f, 1f);
        bodyRT.offsetMin = new Vector2(8f, 34f);
        bodyRT.offsetMax = new Vector2(-8f, -58f);

        // Cargo Container (Left)
        GameObject cargoGO = new GameObject("CargoContainer", typeof(RectTransform), typeof(GridLayoutGroup));
        cargoGO.transform.SetParent(body.transform, false);
        RectTransform cargoRT = cargoGO.GetComponent<RectTransform>();
        cargoRT.anchorMin = new Vector2(0f, 0f);
        cargoRT.anchorMax = new Vector2(0.58f, 1f);
        cargoRT.sizeDelta = Vector2.zero;
        cargoSlotContainer = cargoGO.transform;
        GridLayoutGroup cargoGrid = cargoGO.GetComponent<GridLayoutGroup>();
        cargoGrid.cellSize = new Vector2(52f, 52f);
        cargoGrid.spacing = new Vector2(4f, 4f);
        cargoGrid.childAlignment = TextAnchor.MiddleCenter;

        // Sockets & Abilities 2x2 Grid (Right)
        GameObject socketsGO = new GameObject("SocketsContainer", typeof(RectTransform), typeof(GridLayoutGroup));
        socketsGO.transform.SetParent(body.transform, false);
        RectTransform socketsRT = socketsGO.GetComponent<RectTransform>();
        socketsRT.anchorMin = new Vector2(0.60f, 0f);
        socketsRT.anchorMax = new Vector2(1f, 1f);
        socketsRT.sizeDelta = Vector2.zero;
        socketsContainer = socketsGO.transform;
        GridLayoutGroup sockGrid = socketsGO.GetComponent<GridLayoutGroup>();
        sockGrid.cellSize = new Vector2(58f, 58f);
        sockGrid.spacing = new Vector2(6f, 6f);
        sockGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        sockGrid.constraintCount = 2;
        sockGrid.childAlignment = TextAnchor.MiddleCenter;

        // --- 3. Bottom Stats Footer ---
        GameObject footer = new GameObject("StatsFooter", typeof(RectTransform), typeof(Image));
        footer.transform.SetParent(root.transform, false);
        RectTransform footRT = footer.GetComponent<RectTransform>();
        footRT.anchorMin = new Vector2(0f, 0f);
        footRT.anchorMax = new Vector2(1f, 0f);
        footRT.pivot = new Vector2(0.5f, 0f);
        footRT.anchoredPosition = Vector2.zero;
        footRT.sizeDelta = new Vector2(0f, 32f);

        Image footImg = footer.GetComponent<Image>();
        footImg.color = new Color(0.03f, 0.07f, 0.12f, 1f);

        // Upkeep Stat (Left)
        upkeepIcon = CreateStatPair(footer.transform, "Upkeep", ShipUIResourceCache.BalanceIcon, out upkeepText, new Vector2(12f, 0f));

        // Firepower Stat (Center)
        firepowerIcon = CreateStatPair(footer.transform, "Firepower", ShipUIResourceCache.AttackPowerIcon, out firepowerText, new Vector2(120f, 0f));

        // Health Stat (Right)
        healthIcon = CreateStatPair(footer.transform, "Health", ShipUIResourceCache.HealthIcon, out healthText, new Vector2(240f, 0f));
        if (healthText != null) healthText.color = new Color(0f, 0.95f, 0.40f, 1f); // Vibrant Anno green

        // --- 4. Tooltip Box ---
        GameObject tipGO = new GameObject("TooltipBox", typeof(RectTransform), typeof(Image));
        tipGO.transform.SetParent(root.transform, false);
        RectTransform tipRT = tipGO.GetComponent<RectTransform>();
        tipRT.anchorMin = new Vector2(0f, 1f);
        tipRT.anchorMax = new Vector2(1f, 1f);
        tipRT.pivot = new Vector2(0.5f, 0f);
        tipRT.anchoredPosition = new Vector2(0f, 6f);
        tipRT.sizeDelta = new Vector2(0f, 40f);
        Image tipImg = tipGO.GetComponent<Image>();
        tipImg.color = new Color(0.05f, 0.10f, 0.18f, 0.98f);

        GameObject tipTxtGO = new GameObject("TipText", typeof(RectTransform), typeof(TextMeshProUGUI));
        tipTxtGO.transform.SetParent(tipGO.transform, false);
        RectTransform tipTxtRT = tipTxtGO.GetComponent<RectTransform>();
        tipTxtRT.anchorMin = Vector2.zero;
        tipTxtRT.anchorMax = Vector2.one;
        tipTxtRT.offsetMin = new Vector2(8f, 4f);
        tipTxtRT.offsetMax = new Vector2(-8f, -4f);
        tooltipText = tipTxtGO.GetComponent<TextMeshProUGUI>();
        tooltipText.fontSize = 11;
        tooltipText.color = Color.white;
        tooltipBox = tipGO;
        tooltipBox.SetActive(false);
    }

    private Button CreateCircularButton(Transform parent, string name, Sprite icon, Vector2 pos)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(28f, 28f);

        Image img = go.GetComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;

        return go.GetComponent<Button>();
    }

    private Image CreateStatPair(Transform parent, string name, Sprite icon, out TMP_Text textComp, Vector2 pos)
    {
        GameObject pairGO = new GameObject($"Stat_{name}", typeof(RectTransform));
        pairGO.transform.SetParent(parent, false);

        RectTransform pairRT = pairGO.GetComponent<RectTransform>();
        pairRT.anchorMin = new Vector2(0f, 0.5f);
        pairRT.anchorMax = new Vector2(0f, 0.5f);
        pairRT.pivot = new Vector2(0f, 0.5f);
        pairRT.anchoredPosition = pos;
        pairRT.sizeDelta = new Vector2(100f, 26f);

        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(pairGO.transform, false);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0f, 0.5f);
        iconRT.anchorMax = new Vector2(0f, 0.5f);
        iconRT.pivot = new Vector2(0f, 0.5f);
        iconRT.anchoredPosition = new Vector2(0f, 0f);
        iconRT.sizeDelta = new Vector2(20f, 20f);

        Image img = iconGO.GetComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;

        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(pairGO.transform, false);
        RectTransform txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = new Vector2(0f, 0f);
        txtRT.anchorMax = new Vector2(1f, 1f);
        txtRT.offsetMin = new Vector2(24f, 0f);
        txtRT.offsetMax = Vector2.zero;

        textComp = txtGO.GetComponent<TextMeshProUGUI>();
        textComp.fontSize = 12;
        textComp.fontStyle = FontStyles.Bold;
        textComp.color = new Color(0.9f, 0.95f, 1f, 1f);
        textComp.alignment = TextAlignmentOptions.MidlineLeft;

        return img;
    }

    #endregion
}
