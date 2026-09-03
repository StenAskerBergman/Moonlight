using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Authentic Anno 2070 Fleet multi-selection interface.
/// Displays when 2 or more units are selected:
/// - Fleet header with fleet icon, fleet size, and order/stance controls (Attack, Defend, Anchor, Patrol)
/// - Grid of ship cards for all units in the fleet
/// - Each ship card features its unique portrait, green health bar, and focus highlight
/// - Clicking a card focuses that ship in the fleet; double-clicking focuses and selects only that vessel
/// - Also preserves building-to-unit trade target navigation
/// </summary>
public sealed class MultiUnitSelectionPanel : MonoBehaviour
{
    private readonly List<GameObject> activeCards = new List<GameObject>();
    private UnitSelections selections;
    private GameObject fleetPanelRoot;
    private GameObject buildingTargetRoot;
    private RectTransform cardContainer;
    private TMP_Text fleetTitleText;
    private TMP_Text buildingTargetLabel;

    private float lastClickTime = 0f;
    private int lastClickedIndex = -1;
    private const float DoubleClickThreshold = 0.35f;

    private void Awake()
    {
        selections = GetComponent<UnitSelections>();
        BuildPanel();
    }

    private void OnEnable()
    {
        if (selections != null)
        {
            selections.selectionChanged.AddListener(Refresh);
            Refresh(selections.unitsSelected);
        }
    }

    private void OnDisable()
    {
        if (selections != null)
        {
            selections.selectionChanged.RemoveListener(Refresh);
        }
    }

    private void Start()
    {
        if (BuildingSelections.Instance != null)
        {
            BuildingSelections.Instance.selectionChanged.AddListener(OnBuildingSelectionChanged);
        }
        if (selections != null)
        {
            Refresh(selections.unitsSelected);
        }
    }

    private void OnDestroy()
    {
        if (BuildingSelections.Instance != null)
        {
            BuildingSelections.Instance.selectionChanged.RemoveListener(OnBuildingSelectionChanged);
        }
    }

    private void OnBuildingSelectionChanged(Building building)
    {
        if (selections != null)
        {
            Refresh(selections.unitsSelected);
        }
    }

    private void Update()
    {
        if (fleetPanelRoot == null || !fleetPanelRoot.activeSelf) return;

        // Keep HP bars dynamically updated in case of combat
        UpdateCardHealthBars();
    }

    private void Refresh(List<Unit> units)
    {
        if (fleetPanelRoot == null) return;

        Building selectedBuilding = BuildingSelections.Instance != null ? BuildingSelections.Instance.SelectedBuilding : null;
        bool buildingMode = selectedBuilding != null && (selectedBuilding.GetComponent<Depot>() != null || selectedBuilding.GetComponent<WarehouseSockets>() != null);
        bool hasMultipleUnits = units != null && units.Count > 1;

        if (buildingTargetRoot != null)
        {
            buildingTargetRoot.SetActive(buildingMode);
            if (buildingMode && buildingTargetLabel != null)
            {
                int total = units != null ? units.Count : 0;
                int current = selections != null ? selections.FocusedUnitIndex + 1 : 0;
                buildingTargetLabel.text = $"BUILDING TRADE TARGET {current} / {total}";
            }
        }

        // Show Fleet panel when multiple units are selected and not in building trade override
        bool showFleet = !buildingMode && hasMultipleUnits;
        fleetPanelRoot.SetActive(showFleet);

        if (!showFleet)
        {
            ClearCards();
            return;
        }

        if (fleetTitleText != null)
        {
            fleetTitleText.text = $"Fleet ({units.Count})";
        }

        PopulateCards(units);
    }

    private void PopulateCards(List<Unit> units)
    {
        ClearCards();
        if (cardContainer == null || units == null) return;

        for (int i = 0; i < units.Count; i++)
        {
            Unit unit = units[i];
            if (unit == null) continue;

            int index = i;
            GameObject cardGO = CreateUnitCard(cardContainer, unit, index);
            activeCards.Add(cardGO);
            UpdateCardVisual(cardGO, unit, index);
        }
    }

    private GameObject CreateUnitCard(Transform parent, Unit unit, int index)
    {
        GameObject card = new GameObject($"ShipCard_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(parent, false);

        RectTransform cardRT = card.GetComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(58f, 68f);

        Image cardImg = card.GetComponent<Image>();
        cardImg.sprite = ShipUIResourceCache.SlotBackground;
        cardImg.type = Image.Type.Sliced;
        cardImg.color = new Color(0.10f, 0.18f, 0.28f, 0.95f);

        // Selection / Focus Border
        GameObject borderGO = new GameObject("FocusBorder", typeof(RectTransform), typeof(Image));
        borderGO.transform.SetParent(card.transform, false);
        RectTransform borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.anchorMin = Vector2.zero;
        borderRT.anchorMax = Vector2.one;
        borderRT.offsetMin = new Vector2(-2f, -2f);
        borderRT.offsetMax = new Vector2(2f, 2f);
        Image borderImg = borderGO.GetComponent<Image>();
        borderImg.sprite = ShipUIResourceCache.SlotBackground;
        borderImg.type = Image.Type.Sliced;
        borderImg.color = new Color(0.2f, 0.7f, 1f, 1f);
        borderImg.enabled = false;

        // Portrait Image
        GameObject portGO = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portGO.transform.SetParent(card.transform, false);
        RectTransform portRT = portGO.GetComponent<RectTransform>();
        portRT.anchorMin = new Vector2(0f, 0.22f);
        portRT.anchorMax = new Vector2(1f, 1f);
        portRT.offsetMin = new Vector2(4f, 2f);
        portRT.offsetMax = new Vector2(-4f, -4f);
        Image portImg = portGO.GetComponent<Image>();
        portImg.preserveAspect = true;

        // Health Bar Track (Dark)
        GameObject hpTrackGO = new GameObject("HealthTrack", typeof(RectTransform), typeof(Image));
        hpTrackGO.transform.SetParent(card.transform, false);
        RectTransform trackRT = hpTrackGO.GetComponent<RectTransform>();
        trackRT.anchorMin = new Vector2(0f, 0f);
        trackRT.anchorMax = new Vector2(1f, 0.22f);
        trackRT.offsetMin = new Vector2(4f, 4f);
        trackRT.offsetMax = new Vector2(-4f, -2f);
        Image trackImg = hpTrackGO.GetComponent<Image>();
        trackImg.color = new Color(0.08f, 0.12f, 0.16f, 1f);

        // Health Bar Fill (Bright Green)
        GameObject hpFillGO = new GameObject("HealthFill", typeof(RectTransform), typeof(Image));
        hpFillGO.transform.SetParent(hpTrackGO.transform, false);
        RectTransform fillRT = hpFillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.sizeDelta = Vector2.zero;
        Image fillImg = hpFillGO.GetComponent<Image>();
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.color = new Color(0f, 0.95f, 0.40f, 1f); // Authentic Anno 2070 green

        // Order / Stance Badge (Top-Right of card)
        GameObject badgeGO = new GameObject("OrderBadge", typeof(RectTransform), typeof(Image));
        badgeGO.transform.SetParent(card.transform, false);
        RectTransform badgeRT = badgeGO.GetComponent<RectTransform>();
        badgeRT.anchorMin = new Vector2(1f, 1f);
        badgeRT.anchorMax = new Vector2(1f, 1f);
        badgeRT.pivot = new Vector2(1f, 1f);
        badgeRT.anchoredPosition = new Vector2(-2f, -2f);
        badgeRT.sizeDelta = new Vector2(16f, 16f);
        Image badgeImg = badgeGO.GetComponent<Image>();
        badgeImg.sprite = ShipUIResourceCache.ShipMoveIcon;
        badgeImg.preserveAspect = true;
        badgeImg.enabled = false;

        // Button Wiring
        Button btn = card.GetComponent<Button>();
        btn.onClick.AddListener(() => OnCardClicked(index, unit));

        return card;
    }

    private void UpdateCardVisual(GameObject cardGO, Unit unit, int index)
    {
        if (cardGO == null || unit == null) return;

        // 1. Portrait
        Image portImg = cardGO.transform.Find("Portrait")?.GetComponent<Image>();
        if (portImg != null)
        {
            Sprite portrait = null;
            var naval = unit.GetComponent<NavalUnit>();
            if (naval != null && naval.Definition != null && naval.Definition.portraitIcon != null)
            {
                portrait = naval.Definition.portraitIcon;
            }
            if (portrait == null)
            {
                portrait = ShipUIResourceCache.GetVesselPortrait(unit.name);
            }
            if (portrait != null)
            {
                portImg.sprite = portrait;
            }
        }

        // 2. Health Bar
        Image fillImg = cardGO.transform.Find("HealthTrack/HealthFill")?.GetComponent<Image>();
        if (fillImg != null)
        {
            var dmg = unit.GetComponent<Damageable>();
            int maxHp = dmg != null ? dmg.totalHealth : 100;
            int curHp = dmg != null ? dmg.currentHealth : 100;
            fillImg.fillAmount = maxHp > 0 ? (float)curHp / maxHp : 1f;
        }

        // 3. Focused Highlight
        bool isFocused = selections != null && selections.FocusedUnit == unit;
        Image borderImg = cardGO.transform.Find("FocusBorder")?.GetComponent<Image>();
        if (borderImg != null)
        {
            borderImg.enabled = isFocused;
        }

        // 4. Order Badge on Focused Unit
        Image badgeImg = cardGO.transform.Find("OrderBadge")?.GetComponent<Image>();
        if (badgeImg != null)
        {
            badgeImg.enabled = isFocused;
        }
    }

    private void UpdateCardHealthBars()
    {
        if (selections == null || selections.unitsSelected == null) return;

        for (int i = 0; i < activeCards.Count && i < selections.unitsSelected.Count; i++)
        {
            Unit u = selections.unitsSelected[i];
            GameObject c = activeCards[i];
            if (u == null || c == null) continue;

            Image fillImg = c.transform.Find("HealthTrack/HealthFill")?.GetComponent<Image>();
            if (fillImg != null)
            {
                var dmg = u.GetComponent<Damageable>();
                int maxHp = dmg != null ? dmg.totalHealth : 100;
                int curHp = dmg != null ? dmg.currentHealth : 100;
                fillImg.fillAmount = maxHp > 0 ? (float)curHp / maxHp : 1f;
            }

            bool isFocused = selections.FocusedUnit == u;
            Image borderImg = c.transform.Find("FocusBorder")?.GetComponent<Image>();
            if (borderImg != null) borderImg.enabled = isFocused;

            Image badgeImg = c.transform.Find("OrderBadge")?.GetComponent<Image>();
            if (badgeImg != null) badgeImg.enabled = isFocused;
        }
    }

    private void OnCardClicked(int index, Unit unit)
    {
        if (selections == null || unit == null) return;

        float timeNow = Time.time;
        if (lastClickedIndex == index && (timeNow - lastClickTime) <= DoubleClickThreshold)
        {
            // Double-click: select only this vessel, smoothly transitioning to single-unit view
            selections.SelectOnly(unit);
            lastClickedIndex = -1;
            lastClickTime = 0f;
            return;
        }

        lastClickedIndex = index;
        lastClickTime = timeNow;

        // Single click: focus this unit in the fleet
        selections.FocusSelectedUnit(index);
    }

    private void ClearCards()
    {
        foreach (var c in activeCards)
        {
            if (c != null) Destroy(c);
        }
        activeCards.Clear();
    }

    #region Hierarchy Construction

    private void BuildPanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("MultiUnitSelectionPanel: No Canvas was found; Fleet panel cannot be displayed.");
            return;
        }

        Transform parent = canvas.transform.Find("Graphical User Interface/HUD Bot") ?? canvas.transform;

        // --- 1. Building Target Navigator (Retained for trade compatibility) ---
        buildingTargetRoot = new GameObject("BuildingTradeTargetScreen", typeof(RectTransform), typeof(Image));
        buildingTargetRoot.transform.SetParent(parent, false);
        Image bImg = buildingTargetRoot.GetComponent<Image>();
        bImg.sprite = ShipUIResourceCache.SlotBackground;
        bImg.type = Image.Type.Sliced;
        bImg.color = new Color(0.05f, 0.09f, 0.16f, 0.96f);

        RectTransform bPanel = buildingTargetRoot.GetComponent<RectTransform>();
        bPanel.anchorMin = new Vector2(0.5f, 1f);
        bPanel.anchorMax = new Vector2(0.5f, 1f);
        bPanel.pivot = new Vector2(0.5f, 1f);
        bPanel.anchoredPosition = new Vector2(0f, -12f);
        bPanel.sizeDelta = new Vector2(480f, 48f);

        Button prevBtn = CreateNavButton("PrevBtn", bPanel, "<", new Vector2(10f, 0f));
        prevBtn.onClick.AddListener(() => selections?.FocusSelectedUnitOffset(-1));

        Button nextBtn = CreateNavButton("NextBtn", bPanel, ">", new Vector2(430f, 0f));
        nextBtn.onClick.AddListener(() => selections?.FocusSelectedUnitOffset(1));

        GameObject bTextGO = new GameObject("TargetText", typeof(RectTransform), typeof(TextMeshProUGUI));
        bTextGO.transform.SetParent(bPanel, false);
        RectTransform bTextRT = bTextGO.GetComponent<RectTransform>();
        bTextRT.anchorMin = new Vector2(0f, 0f);
        bTextRT.anchorMax = new Vector2(1f, 1f);
        bTextRT.offsetMin = new Vector2(50f, 0f);
        bTextRT.offsetMax = new Vector2(-50f, 0f);
        buildingTargetLabel = bTextGO.GetComponent<TextMeshProUGUI>();
        buildingTargetLabel.fontSize = 13;
        buildingTargetLabel.alignment = TextAlignmentOptions.Center;
        buildingTargetLabel.color = Color.white;
        buildingTargetRoot.SetActive(false);

        // --- 2. Anno 2070 Fleet Panel ---
        fleetPanelRoot = new GameObject("Anno2070_FleetPanel", typeof(RectTransform), typeof(Image));
        fleetPanelRoot.transform.SetParent(parent, false);

        RectTransform rootRT = fleetPanelRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(1f, 0f);
        rootRT.anchorMax = new Vector2(1f, 0f);
        rootRT.pivot = new Vector2(1f, 0f);
        rootRT.anchoredPosition = new Vector2(-20f, 20f);
        rootRT.sizeDelta = new Vector2(360f, 240f);

        Image rootImg = fleetPanelRoot.GetComponent<Image>();
        rootImg.sprite = ShipUIResourceCache.SlotBackground;
        rootImg.type = Image.Type.Sliced;
        rootImg.color = new Color(0.04f, 0.09f, 0.16f, 0.96f);

        // --- Header Bar ---
        GameObject header = new GameObject("FleetHeader", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(fleetPanelRoot.transform, false);
        RectTransform headerRT = header.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = Vector2.zero;
        headerRT.sizeDelta = new Vector2(0f, 52f);

        Image headerImg = header.GetComponent<Image>();
        headerImg.color = new Color(0.06f, 0.14f, 0.24f, 1f);

        // Fleet Icon
        GameObject iconGO = new GameObject("FleetIcon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(header.transform, false);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0f, 0.5f);
        iconRT.anchorMax = new Vector2(0f, 0.5f);
        iconRT.pivot = new Vector2(0f, 0.5f);
        iconRT.anchoredPosition = new Vector2(8f, 0f);
        iconRT.sizeDelta = new Vector2(38f, 38f);
        Image fIconImg = iconGO.GetComponent<Image>();
        fIconImg.sprite = ShipUIResourceCache.FleetIcon;
        fIconImg.preserveAspect = true;

        // Fleet Title
        GameObject titleGO = new GameObject("FleetTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(header.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 0.5f);
        titleRT.anchorMax = new Vector2(0.65f, 0.5f);
        titleRT.pivot = new Vector2(0f, 0.5f);
        titleRT.anchoredPosition = new Vector2(50f, 0f);
        titleRT.sizeDelta = new Vector2(0f, 28f);
        fleetTitleText = titleGO.GetComponent<TextMeshProUGUI>();
        fleetTitleText.fontSize = 16;
        fleetTitleText.fontStyle = FontStyles.Bold;
        fleetTitleText.color = Color.white;
        fleetTitleText.alignment = TextAlignmentOptions.MidlineLeft;

        // Top-Right Curved Red Order Tab
        GameObject orderTab = new GameObject("FleetOrderTab", typeof(RectTransform), typeof(Image));
        orderTab.transform.SetParent(header.transform, false);
        RectTransform tabRT = orderTab.GetComponent<RectTransform>();
        tabRT.anchorMin = new Vector2(1f, 0.5f);
        tabRT.anchorMax = new Vector2(1f, 0.5f);
        tabRT.pivot = new Vector2(1f, 0.5f);
        tabRT.anchoredPosition = new Vector2(-4f, 0f);
        tabRT.sizeDelta = new Vector2(128f, 38f);
        Image tabImg = orderTab.GetComponent<Image>();
        tabImg.sprite = ShipUIResourceCache.SlotBackground;
        tabImg.type = Image.Type.Sliced;
        tabImg.color = new Color(0.55f, 0.12f, 0.12f, 0.95f);

        // 4 Stance Buttons: Attack, Defend, Anchor/Hold, Patrol
        CreateOrderButton(orderTab.transform, "AttackBtn", ShipUIResourceCache.ShipMoveIcon, new Vector2(-96f, 0f), OnFleetAttack);
        CreateOrderButton(orderTab.transform, "DefendBtn", ShipUIResourceCache.ShieldIcon, new Vector2(-64f, 0f), OnFleetDefend);
        CreateOrderButton(orderTab.transform, "AnchorBtn", ShipUIResourceCache.VehicleBadge, new Vector2(-32f, 0f), OnFleetHold);
        CreateOrderButton(orderTab.transform, "PatrolBtn", ShipUIResourceCache.CycleIcon, new Vector2(0f, 0f), OnFleetPatrol);

        // --- Card Grid Container ---
        GameObject bodyGO = new GameObject("FleetCardGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        bodyGO.transform.SetParent(fleetPanelRoot.transform, false);
        RectTransform bodyRT = bodyGO.GetComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0f, 0f);
        bodyRT.anchorMax = new Vector2(1f, 1f);
        bodyRT.offsetMin = new Vector2(10f, 10f);
        bodyRT.offsetMax = new Vector2(-10f, -58f);

        cardContainer = bodyRT;
        GridLayoutGroup grid = bodyGO.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(60f, 72f);
        grid.spacing = new Vector2(8f, 8f);
        grid.childAlignment = TextAnchor.UpperLeft;

        fleetPanelRoot.SetActive(false);
    }

    private Button CreateNavButton(string name, RectTransform parent, string label, Vector2 pos)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(40f, 32f);

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.12f, 0.22f, 0.35f, 0.95f);

        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(go.transform, false);
        RectTransform txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return go.GetComponent<Button>();
    }

    private Button CreateOrderButton(Transform parent, string name, Sprite icon, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(26f, 26f);

        Image img = go.GetComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;

        Button btn = go.GetComponent<Button>();
        if (onClick != null) btn.onClick.AddListener(onClick);

        return btn;
    }

    private void OnFleetAttack()
    {
        Debug.Log("Fleet Attack command ordered for selected fleet.");
    }

    private void OnFleetDefend()
    {
        Debug.Log("Fleet Defensive stance ordered for selected fleet.");
    }

    private void OnFleetHold()
    {
        Debug.Log("Fleet Hold / Anchor position ordered for selected fleet.");
    }

    private void OnFleetPatrol()
    {
        Debug.Log("Fleet Patrol / Cycle route ordered for selected fleet.");
    }

    #endregion
}
