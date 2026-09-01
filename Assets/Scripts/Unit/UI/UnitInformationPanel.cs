using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Data-driven Selected Unit UI for Moonlight naval units (and general units).
/// Dynamically reads UnitDefinition, NavalUnit, UnitInventory, UnitEquipment, and UnitAbilities
/// without requiring ship-specific UI prefabs or Inspector overrides.
/// </summary>
public class UnitInformationPanel : MonoBehaviour
{
    public static UnitInformationPanel Instance { get; private set; }

    [Header("Root Panel")]
    [SerializeField] private GameObject rootPanel;

    [Header("Header Elements")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private Text titleText;
    [SerializeField] private TextMeshProUGUI titleTMP;
    [SerializeField] private Text roleText;
    [SerializeField] private TextMeshProUGUI roleTMP;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Text healthText;
    [SerializeField] private TextMeshProUGUI healthTMP;

    [Header("Compact Stats")]
    [SerializeField] private Text statsSummaryText;
    [SerializeField] private TextMeshProUGUI statsSummaryTMP;
    [SerializeField] private Text targetsText;
    [SerializeField] private TextMeshProUGUI targetsTMP;

    [Header("Cargo Section")]
    [SerializeField] private Transform cargoSlotContainer;
    [SerializeField] private GameObject cargoSlotPrefab;

    [Header("Item / Upgrade Section")]
    [SerializeField] private GameObject itemSectionRoot;
    [SerializeField] private Transform itemSlotContainer;
    [SerializeField] private GameObject itemSlotPrefab;

    [Header("Ability Section")]
    [SerializeField] private GameObject abilitySectionRoot;
    [SerializeField] private Transform abilityButtonContainer;
    [SerializeField] private GameObject abilityButtonPrefab;

    [Header("Passive Section")]
    [SerializeField] private GameObject passiveSectionRoot;
    [SerializeField] private Text passiveSummaryText;
    [SerializeField] private TextMeshProUGUI passiveSummaryTMP;

    [Header("Tooltip")]
    [SerializeField] private GameObject tooltipBox;
    [SerializeField] private Text tooltipText;
    [SerializeField] private TextMeshProUGUI tooltipTMP;

    // Runtime state
    private Unit currentUnit;
    private NavalUnit currentNaval;
    private UnitInventory currentInventory;
    private UnitEquipment currentEquipment;
    private UnitAbilities currentAbilities;
    private Damageable currentDamageable;

    private readonly List<GameObject> activeCargoSlots = new List<GameObject>();
    private readonly List<GameObject> activeItemSlots = new List<GameObject>();
    private readonly List<GameObject> activeAbilityButtons = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;

        if (rootPanel == null) rootPanel = this.gameObject;
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
        if (currentUnit == null) return;

        UpdateStatsRow();
        UpdateAbilityCooldowns();

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
        RefreshSelectedUnit();
    }

    public void SelectUnit(Unit unit)
    {
        BindUnit(unit);
    }

    private void RefreshSelectedUnit()
    {
        Unit selected = null;
        if (UnitSelections.Instance != null && UnitSelections.Instance.unitsSelected.Count > 0)
        {
            selected = UnitSelections.Instance.FocusedUnit;
        }

        if (selected != null)
        {
            BindUnit(selected);
        }
        else
        {
            UnbindCurrentUnit();
            if (rootPanel != null) rootPanel.SetActive(false);
        }
    }

    private void BindUnit(Unit unit)
    {
        if (unit == currentUnit && unit != null)
        {
            RefreshAll();
            return;
        }

        UnbindCurrentUnit();
        currentUnit = unit;
        if (currentUnit == null)
        {
            if (rootPanel != null) rootPanel.SetActive(false);
            return;
        }

        currentNaval = currentUnit.GetComponent<NavalUnit>();
        currentInventory = currentUnit.GetComponent<UnitInventory>();
        currentEquipment = currentUnit.GetComponent<UnitEquipment>();
        currentAbilities = currentUnit.GetComponent<UnitAbilities>();
        currentDamageable = currentUnit.GetComponent<Damageable>();

        if (currentInventory != null)
        {
            currentInventory.OnUnitInventoryChanged += RefreshCargo;
        }
        if (currentEquipment != null)
        {
            currentEquipment.OnEquipmentChanged += RefreshItems;
        }
        if (currentAbilities != null)
        {
            currentAbilities.OnAbilitiesChanged += RefreshAbilities;
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
            currentEquipment.OnEquipmentChanged -= RefreshItems;
        }
        if (currentAbilities != null)
        {
            currentAbilities.OnAbilitiesChanged -= RefreshAbilities;
        }

        currentUnit = null;
        currentNaval = null;
        currentInventory = null;
        currentEquipment = null;
        currentAbilities = null;
        currentDamageable = null;
    }

    public void RefreshAll()
    {
        if (currentUnit == null) return;

        UpdateHeader();
        UpdateStatsRow();
        RefreshCargo();
        RefreshItems();
        RefreshAbilities();
        RefreshPassives();
    }

    private void UpdateHeader()
    {
        string displayName = currentUnit.displayName;
        string role = currentNaval != null && currentNaval.Definition != null
            ? currentNaval.Definition.displayCategory ?? currentNaval.Definition.navalClass.ToString()
            : currentUnit.unitType.ToString();

        SetText(titleText, titleTMP, displayName.ToUpperInvariant());
        SetText(roleText, roleTMP, role);

        int maxHp = currentDamageable != null ? currentDamageable.totalHealth : 100;
        int curHp = currentDamageable != null ? currentDamageable.currentHealth : 100;

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHp;
            healthSlider.value = curHp;
        }

        SetText(healthText, healthTMP, $"HP: {curHp} / {maxHp}");
    }

    private void UpdateStatsRow()
    {
        if (currentUnit == null) return;

        float speed = 0f;
        var agent = currentUnit.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) speed = agent.speed;

        string stateStr = currentNaval != null ? currentNaval.CurrentState.ToString() : "Active";

        int cargoUsed = 0;
        int cargoMax = 0;

        if (currentInventory != null && currentInventory.itemSlots != null)
        {
            foreach (var slot in currentInventory.itemSlots)
            {
                if (slot != null && slot.itemStack != null)
                {
                    cargoUsed += slot.itemStack.GetQuantity();
                    cargoMax += slot.itemStack.GetMaxQuantity();
                }
            }
        }

        string statsStr = $"SPEED: {speed:F1}   |   STATUS: {stateStr}   |   CARGO: {cargoUsed} / {cargoMax}";
        SetText(statsSummaryText, statsSummaryTMP, statsStr);

        // Targets capability
        if (currentNaval != null && currentNaval.Definition != null)
        {
            var def = currentNaval.Definition;
            List<string> targetList = new List<string>();
            if (def.CanTargetSurface) targetList.Add("Surface");
            if (def.CanTargetAir) targetList.Add("Air");
            if (def.CanTargetSubmarine) targetList.Add("Sub");

            string tgtStr = targetList.Count > 0 ? string.Join(" / ", targetList) : "None";
            SetText(targetsText, targetsTMP, $"TARGETS: {tgtStr}");
        }
        else
        {
            SetText(targetsText, targetsTMP, "");
        }
    }

    private void RefreshCargo()
    {
        ClearContainer(activeCargoSlots);

        if (cargoSlotContainer == null || currentInventory == null || currentInventory.itemSlots == null) return;

        var slots = currentInventory.itemSlots;
        for (int i = 0; i < slots.Length; i++)
        {
            var physicalSlot = slots[i];
            GameObject slotGO = null;

            if (cargoSlotPrefab != null)
            {
                slotGO = Instantiate(cargoSlotPrefab, cargoSlotContainer);
            }
            else
            {
                slotGO = CreateDefaultSlotView(cargoSlotContainer, $"CargoSlot_{i}");
            }

            activeCargoSlots.Add(slotGO);

            // Populate slot text and item stack
            string itemName = "Empty";
            int count = 0;
            int max = 40;
            Sprite icon = null;

            if (physicalSlot != null && physicalSlot.itemStack != null)
            {
                count = physicalSlot.itemStack.GetQuantity();
                max = physicalSlot.itemStack.GetMaxQuantity();
                if (physicalSlot.itemStack.HasItem())
                {
                    var data = physicalSlot.itemStack.GetItemData();
                    if (data != null)
                    {
                        itemName = data.displayName ?? data.name;
                        icon = data.Icon;
                    }
                }
            }

            var textComps = slotGO.GetComponentsInChildren<Text>(true);
            var tmpComps = slotGO.GetComponentsInChildren<TextMeshProUGUI>(true);
            string displayLabel = $"{itemName}\n{count} / {max}";

            if (tmpComps != null && tmpComps.Length > 0) tmpComps[0].text = displayLabel;
            else if (textComps != null && textComps.Length > 0) textComps[0].text = displayLabel;

            var images = slotGO.GetComponentsInChildren<Image>(true);
            if (images != null && images.Length > 1 && icon != null)
            {
                images[1].sprite = icon;
                images[1].enabled = true;
            }
        }
    }

    private void RefreshItems()
    {
        ClearContainer(activeItemSlots);

        int capacity = currentEquipment != null ? currentEquipment.SlotCapacity : 0;
        if (capacity == 0)
        {
            if (itemSectionRoot != null) itemSectionRoot.SetActive(false);
            return;
        }

        if (itemSectionRoot != null) itemSectionRoot.SetActive(true);
        if (itemSlotContainer == null) return;

        for (int i = 0; i < capacity; i++)
        {
            GameObject slotGO = null;
            if (itemSlotPrefab != null)
            {
                slotGO = Instantiate(itemSlotPrefab, itemSlotContainer);
            }
            else
            {
                slotGO = CreateDefaultSlotView(itemSlotContainer, $"ItemSlot_{i}");
            }

            activeItemSlots.Add(slotGO);

            ItemData item = currentEquipment.GetItem(i);
            string label = item != null ? (item.displayName ?? item.name) : "[ Empty Upgrade Slot ]";

            var textComps = slotGO.GetComponentsInChildren<Text>(true);
            var tmpComps = slotGO.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (tmpComps != null && tmpComps.Length > 0) tmpComps[0].text = label;
            else if (textComps != null && textComps.Length > 0) textComps[0].text = label;
        }
    }

    private void RefreshAbilities()
    {
        ClearContainer(activeAbilityButtons);

        int abilityCount = currentAbilities != null ? currentAbilities.Abilities.Count : 0;
        int activeCount = 0;

        for (int i = 0; i < abilityCount; i++)
        {
            var runtime = currentAbilities.GetAbility(i);
            if (runtime == null || runtime.definition == null) continue;
            if (runtime.definition.abilityType == AbilityType.Passive) continue; // Passives shown in passive area

            activeCount++;
            int index = i;
            GameObject btnGO = null;

            if (abilityButtonPrefab != null)
            {
                btnGO = Instantiate(abilityButtonPrefab, abilityButtonContainer);
            }
            else if (abilityButtonContainer != null)
            {
                btnGO = CreateDefaultAbilityButton(abilityButtonContainer, runtime, index);
            }

            if (btnGO != null)
            {
                activeAbilityButtons.Add(btnGO);
                Button btn = btnGO.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => TryActivateAbility(index));
                }
            }
        }

        if (abilitySectionRoot != null)
        {
            abilitySectionRoot.SetActive(activeCount > 0);
        }
    }

    private void RefreshPassives()
    {
        if (passiveSectionRoot == null) return;

        List<string> passives = new List<string>();
        if (currentAbilities != null)
        {
            foreach (var ability in currentAbilities.Abilities)
            {
                if (ability.definition != null && ability.definition.abilityType == AbilityType.Passive)
                {
                    passives.Add($"• <b>{ability.definition.displayName}</b>: {ability.definition.description}");
                }
            }
        }

        if (passives.Count > 0)
        {
            passiveSectionRoot.SetActive(true);
            SetText(passiveSummaryText, passiveSummaryTMP, string.Join("\n", passives));
        }
        else
        {
            passiveSectionRoot.SetActive(false);
        }
    }

    private void UpdateAbilityCooldowns()
    {
        if (currentAbilities == null) return;

        int activeIdx = 0;
        for (int i = 0; i < currentAbilities.Abilities.Count; i++)
        {
            var runtime = currentAbilities.GetAbility(i);
            if (runtime == null || runtime.definition == null || runtime.definition.abilityType == AbilityType.Passive) continue;

            if (activeIdx < activeAbilityButtons.Count)
            {
                var btnGO = activeAbilityButtons[activeIdx];
                UpdateAbilityButtonVisual(btnGO, runtime, i);
            }
            activeIdx++;
        }
    }

    private void UpdateAbilityButtonVisual(GameObject btnGO, UnitAbilities.RuntimeAbility runtime, int abilityIndex)
    {
        if (btnGO == null || runtime == null) return;

        // Check if dynamic verb (e.g. Dive <-> Surface)
        string verb = runtime.definition.displayName;
        if (runtime.definition is DiveAbilityDefinition diveDef)
        {
            verb = diveDef.GetDynamicVerb(currentUnit);
        }

        string hotkey = abilityIndex switch
        {
            0 => "Q",
            1 => "W",
            2 => "E",
            3 => "R",
            _ => ""
        };

        string label = $"{verb}\n[{hotkey}]";
        if (runtime.IsOnCooldown)
        {
            label = $"{verb}\n{runtime.cooldownRemaining:F0}s";
        }

        var textComps = btnGO.GetComponentsInChildren<Text>(true);
        var tmpComps = btnGO.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (tmpComps != null && tmpComps.Length > 0) tmpComps[0].text = label;
        else if (textComps != null && textComps.Length > 0) textComps[0].text = label;

        Button btn = btnGO.GetComponentInChildren<Button>();
        if (btn != null)
        {
            btn.interactable = !runtime.IsOnCooldown && currentAbilities.CanActivate(abilityIndex);
        }
    }

    private void TryActivateAbility(int index)
    {
        if (currentAbilities == null) return;
        currentAbilities.Activate(index);
    }

    public void ShowTooltip(string text)
    {
        if (tooltipBox == null) return;
        tooltipBox.SetActive(true);
        SetText(tooltipText, tooltipTMP, text);
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

    private GameObject CreateDefaultSlotView(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.15f, 0.2f, 0.25f, 0.85f);

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(go.transform, false);

        Text txt = textGO.GetComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 12;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;

        RectTransform rt = textGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        return go;
    }

    private GameObject CreateDefaultAbilityButton(Transform parent, UnitAbilities.RuntimeAbility ability, int index)
    {
        GameObject go = new GameObject($"AbilityBtn_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.2f, 0.35f, 0.5f, 0.9f);

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(go.transform, false);

        Text txt = textGO.GetComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 12;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;

        RectTransform rt = textGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        return go;
    }

    private void SetText(Text uiText, TextMeshProUGUI tmpText, string value)
    {
        if (tmpText != null) tmpText.text = value;
        else if (uiText != null) uiText.text = value;
    }
}
