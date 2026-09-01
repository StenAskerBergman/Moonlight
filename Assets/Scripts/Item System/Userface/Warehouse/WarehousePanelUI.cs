using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Root controller for the warehouse / Port Authority panel. Opens when a building with
/// a <see cref="Depot"/> is selected, resolves that island's three datasets into a
/// <see cref="WarehousePanelContext"/>, and drives two rows of tabs:
///
///   - the tier strip (1-4 demographics of the island's faction, plus the Tech atom),
///     which filters the Goods and Items grids;
///   - the main tabs GOODS / ITEMS / TRADE, one per dataset.
///
/// Supersedes the WarehouseInteractionUI stub; don't run both in one scene or they'll
/// fight over the same selection event.
/// </summary>
public sealed class WarehousePanelUI : MonoBehaviour
{
    public static WarehousePanelUI Instance { get; private set; }

    [Header("Style")]
    [SerializeField] private WarehousePanelStyle style;

    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text buildingCategoryLabel;
    [SerializeField] private TMP_Text buildingNameLabel;

    [Header("Tier Strip")]
    [SerializeField] private RectTransform tierStripParent;
    [SerializeField] private WarehouseTierTabButton tierTabTemplate;

    [Header("Main Tabs")]
    [SerializeField] private RectTransform mainTabParent;
    [SerializeField] private Button mainTabTemplate;
    [SerializeField] private List<WarehousePanelTab> tabs = new List<WarehousePanelTab>();

    private readonly List<WarehouseTierTabButton> tierButtons = new List<WarehouseTierTabButton>();
    private readonly List<Button> mainTabButtons = new List<Button>();
    private List<WarehouseTierTab> tierStrip = new List<WarehouseTierTab>();

    private WarehousePanelContext context;
    private WarehouseTierTab activeTier;
    private WarehousePanelTab activeTab;
    private IslandPopulation boundPopulation;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (tierTabTemplate != null) tierTabTemplate.gameObject.SetActive(false);
        if (mainTabTemplate != null) mainTabTemplate.gameObject.SetActive(false);

        foreach (WarehousePanelTab tab in tabs)
        {
            if (tab != null) tab.SetStyle(style);
        }

        BuildMainTabButtons();
        Close();
    }

    private void OnEnable()
    {
        if (BuildingSelections.Instance != null)
        {
            BuildingSelections.Instance.selectionChanged.AddListener(OnSelectionChanged);
        }
    }

    private void OnDisable()
    {
        if (BuildingSelections.Instance != null)
        {
            BuildingSelections.Instance.selectionChanged.RemoveListener(OnSelectionChanged);
        }

        UnbindPopulation();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnSelectionChanged(Building building)
    {
        // Only warehouses get this panel; any other building (or a deselect) closes it.
        if (building == null || (building.GetComponent<Depot>() == null && building.GetComponent<WarehouseSockets>() == null))
        {
            Close();
            return;
        }

        Island island = building.GetComponentInParent<Island>();
        if (island == null)
        {
            Debug.LogWarning($"Warehouse '{building.name}' is not parented under an Island, so its panel has no stockpile to show.", building);
            Close();
            return;
        }

        Open(new WarehousePanelContext(building, island));
    }

    private void Update()
    {
        // If the selected warehouse was deleted or destroyed while the panel is open, close it cleanly.
        if (context != null && (context.Building == null || !context.Building.gameObject))
        {
            Close();
        }
    }

    private void Open(WarehousePanelContext newContext)
    {
        context = newContext;

        // The Items tab's Local slots follow whichever building is selected.
        ItemSlotBindingSource.SelectedBuilding = context.Building;

        BindPopulation(context.Population);
        BuildTierStrip();

        if (panelRoot != null) panelRoot.SetActive(true);

        if (buildingCategoryLabel != null) buildingCategoryLabel.text = "TRADE BUILDING";
        if (buildingNameLabel != null) buildingNameLabel.text = context.Building.name;

        // Keep whichever main tab was last open across selections; fall back to the first.
        if (activeTab == null || !tabs.Contains(activeTab))
        {
            activeTab = tabs.Count > 0 ? tabs[0] : null;
        }

        ShowTab(activeTab);
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);

        ItemSlotBindingSource.SelectedBuilding = null;

        UnbindPopulation();
        context = null;
    }

    #region Tier strip

    private void BuildTierStrip()
    {
        tierStrip = WarehouseTierTab.BuildStrip(context.Faction);

        while (tierButtons.Count < tierStrip.Count)
        {
            if (tierTabTemplate == null || tierStripParent == null) break;

            WarehouseTierTabButton created = Instantiate(tierTabTemplate, tierStripParent);
            tierButtons.Add(created);
        }

        for (int i = 0; i < tierButtons.Count; i++)
        {
            if (i < tierStrip.Count) tierButtons[i].Bind(tierStrip[i], style, SetActiveTier);
            else tierButtons[i].Hide();
        }

        // Keep the equivalent tab selected across selections where the strip is the same
        // shape; otherwise start at the first demographic.
        WarehouseTierTab restored = null;
        if (activeTier != null)
        {
            foreach (WarehouseTierTab tier in tierStrip)
            {
                if (tier.Faction == activeTier.Faction && tier.PrimaryClass == activeTier.PrimaryClass)
                {
                    restored = tier;
                    break;
                }
            }
        }

        activeTier = restored ?? (tierStrip.Count > 0 ? tierStrip[0] : null);
        RefreshTierStates();
    }

    private void SetActiveTier(WarehouseTierTab tier)
    {
        if (tier == null || activeTier == tier) return;

        activeTier = tier;
        RefreshTierStates();

        foreach (WarehousePanelTab tab in tabs)
        {
            if (tab != null) tab.SetActiveTier(activeTier);
        }
    }

    private void RefreshTierStates()
    {
        foreach (WarehouseTierTabButton button in tierButtons)
        {
            if (!button.gameObject.activeSelf || button.Tier == null) continue;

            bool reached = false;
            foreach (PopulationClass populationClass in button.Tier.Classes)
            {
                if (context != null && context.GetPopulation(button.Tier.Faction, populationClass) > 0)
                {
                    reached = true;
                    break;
                }
            }

            button.SetState(button.Tier == activeTier, reached);
        }
    }

    private void BindPopulation(IslandPopulation population)
    {
        if (boundPopulation == population) return;

        UnbindPopulation();
        boundPopulation = population;

        if (boundPopulation != null) boundPopulation.PopulationChanged += OnPopulationChanged;
    }

    private void UnbindPopulation()
    {
        if (boundPopulation == null) return;

        boundPopulation.PopulationChanged -= OnPopulationChanged;
        boundPopulation = null;
    }

    // A band being crossed while the panel is open should unlock goods live.
    private void OnPopulationChanged()
    {
        if (context == null) return;

        RefreshTierStates();
        if (activeTab != null) activeTab.Rebuild();
    }

    #endregion

    #region Main tabs

    private void BuildMainTabButtons()
    {
        if (mainTabTemplate == null || mainTabParent == null) return;

        foreach (WarehousePanelTab tab in tabs)
        {
            if (tab == null) continue;

            Button button = Instantiate(mainTabTemplate, mainTabParent);
            button.gameObject.SetActive(true);
            button.name = $"Main Tab ({tab.TabLabel})";

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = tab.TabLabel;

            WarehousePanelTab captured = tab;
            button.onClick.AddListener(() => ShowTab(captured));

            mainTabButtons.Add(button);
        }
    }

    public void ShowTab(WarehousePanelTab tab)
    {
        if (tab == null || context == null) return;

        activeTab = tab;

        for (int i = 0; i < tabs.Count; i++)
        {
            WarehousePanelTab candidate = tabs[i];
            if (candidate == null) continue;

            bool isActive = candidate == tab;
            candidate.gameObject.SetActive(isActive);

            if (isActive) candidate.Bind(context, activeTier);

            if (i < mainTabButtons.Count && style != null)
            {
                Image background = mainTabButtons[i].GetComponent<Image>();
                if (background != null) background.color = isActive ? style.tabActive : style.tabInactive;

                TMP_Text label = mainTabButtons[i].GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.color = isActive ? style.tabActiveText : style.tabInactiveText;
            }
        }

        // Trade is island-wide, so the demographic filter is meaningless there.
        if (tierStripParent != null)
        {
            tierStripParent.gameObject.SetActive(tab.UsesTierTabs);
        }
    }

    #endregion
}
