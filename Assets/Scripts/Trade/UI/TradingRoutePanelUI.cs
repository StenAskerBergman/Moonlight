using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Right-hand Trading Route configuration panel matching Anno 2070 information hierarchy:
/// 1. Trading Routes list (route selection, new route, rename, delete, mode toggle)
/// 2. Stations list (harbor identities, reorder, cargo targets with item, target amount, load/unload)
/// 3. Ships list (assigned ships, current status, add ship, unassign ship)
/// </summary>
public class TradingRoutePanelUI : MonoBehaviour
{
    [Header("Panels / Containers")]
    [SerializeField] private Transform routesContainer;
    [SerializeField] private Transform stationsContainer;
    [SerializeField] private Transform shipsContainer;

    [Header("Action Buttons")]
    [SerializeField] private Button newRouteButton;
    [SerializeField] private Button addStationButton;
    [SerializeField] private Button addShipButton;

    [Header("Route Settings Header")]
    [SerializeField] private InputField routeNameInput;
    [SerializeField] private Dropdown routeModeDropdown;
    [SerializeField] private Button deleteRouteButton;

    [Header("Dialogs")]
    [SerializeField] private CargoTargetDialogUI cargoDialog;
    [SerializeField] private GameObject islandPickerModal;
    [SerializeField] private Transform islandPickerContainer;
    [SerializeField] private GameObject shipPickerModal;
    [SerializeField] private Transform shipPickerContainer;

    // Styling Colors
    private static readonly Color BgColor = new Color(0.08f, 0.16f, 0.26f, 0.95f);
    private static readonly Color EntryBgNormal = new Color(0.11f, 0.22f, 0.35f, 0.9f);
    private static readonly Color EntryBgSelected = new Color(0.18f, 0.42f, 0.65f, 1f);
    private static readonly Color SlotBg = new Color(0.06f, 0.12f, 0.20f, 1f);
    private static readonly Color AccentCyan = new Color(0.25f, 0.75f, 1f, 1f);
    private static readonly Color TextWhite = new Color(0.92f, 0.96f, 1f, 1f);
    private static readonly Color TextDim = new Color(0.60f, 0.72f, 0.84f, 1f);
    private static readonly Color GreenLoad = new Color(0.35f, 0.85f, 0.45f, 1f);
    private static readonly Color RedUnload = new Color(0.90f, 0.35f, 0.30f, 1f);

    private readonly List<GameObject> spawnedRouteEntries = new List<GameObject>();
    private readonly List<GameObject> spawnedStationEntries = new List<GameObject>();
    private readonly List<GameObject> spawnedShipEntries = new List<GameObject>();

    private void Awake()
    {
        if (newRouteButton != null) newRouteButton.onClick.AddListener(OnNewRouteClicked);
        if (addStationButton != null) addStationButton.onClick.AddListener(OnAddStationClicked);
        if (addShipButton != null) addShipButton.onClick.AddListener(OnAddShipClicked);
        if (deleteRouteButton != null) deleteRouteButton.onClick.AddListener(OnDeleteRouteClicked);

        if (routeNameInput != null)
        {
            routeNameInput.onEndEdit.AddListener(OnRouteNameEdited);
        }

        if (routeModeDropdown != null)
        {
            routeModeDropdown.ClearOptions();
            routeModeDropdown.AddOptions(new List<string> { "Continuous", "Smart", "One-Time" });
            routeModeDropdown.onValueChanged.AddListener(OnRouteModeChanged);
        }
    }

    private void Start()
    {
        if (TradingRouteManager.Instance != null)
        {
            TradingRouteManager.Instance.OnRoutesChanged += RefreshAll;
            TradingRouteManager.Instance.OnRouteSelected += OnRouteSelected;
            TradingRouteManager.Instance.OnRouteUpdated += OnRouteUpdated;
        }

        RefreshAll();
    }

    private void OnDestroy()
    {
        if (TradingRouteManager.Instance != null)
        {
            TradingRouteManager.Instance.OnRoutesChanged -= RefreshAll;
            TradingRouteManager.Instance.OnRouteSelected -= OnRouteSelected;
            TradingRouteManager.Instance.OnRouteUpdated -= OnRouteUpdated;
        }
    }

    #region Event Handlers

    private void OnRouteSelected(TradingRoute route)
    {
        RefreshRoutesList();
        RefreshSelectedRouteHeader();
        RefreshStationsList();
        RefreshShipsList();
    }

    private void OnRouteUpdated(TradingRoute route)
    {
        RefreshSelectedRouteHeader();
        RefreshStationsList();
        RefreshShipsList();
    }

    public void RefreshAll()
    {
        RefreshRoutesList();
        RefreshSelectedRouteHeader();
        RefreshStationsList();
        RefreshShipsList();
    }

    #endregion

    #region Route List Section

    private void RefreshRoutesList()
    {
        if (routesContainer == null) return;

        foreach (var obj in spawnedRouteEntries)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedRouteEntries.Clear();

        var routes = TradingRouteManager.Instance.Routes;
        var selectedRoute = TradingRouteManager.Instance.SelectedRoute;

        foreach (var route in routes)
        {
            if (route == null) continue;

            bool isSelected = selectedRoute == route;
            GameObject itemObj = CreateRouteListItem(route, isSelected);
            itemObj.transform.SetParent(routesContainer, false);
            spawnedRouteEntries.Add(itemObj);
        }
    }

    private GameObject CreateRouteListItem(TradingRoute route, bool isSelected)
    {
        GameObject row = new GameObject($"Route_{route.id}", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = row.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 36);

        var img = row.GetComponent<Image>();
        img.color = isSelected ? EntryBgSelected : EntryBgNormal;

        // Route Name
        GameObject labelObj = new GameObject("NameText", typeof(RectTransform), typeof(Text));
        labelObj.transform.SetParent(row.transform, false);
        var labelRt = labelObj.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0, 0);
        labelRt.anchorMax = new Vector2(1, 1);
        labelRt.offsetMin = new Vector2(32, 0);
        labelRt.offsetMax = new Vector2(-10, 0);

        var txt = labelObj.GetComponent<Text>();
        txt.text = route.name;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 13;
        txt.color = isSelected ? Color.white : TextWhite;
        txt.alignment = TextAnchor.MiddleLeft;

        // Loop / Route Icon Indicator
        GameObject iconObj = new GameObject("LoopIcon", typeof(RectTransform), typeof(Text));
        iconObj.transform.SetParent(row.transform, false);
        var iconRt = iconObj.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0, 0.5f);
        iconRt.anchorMax = new Vector2(0, 0.5f);
        iconRt.sizeDelta = new Vector2(24, 24);
        iconRt.anchoredPosition = new Vector2(16, 0);

        var iconTxt = iconObj.GetComponent<Text>();
        iconTxt.text = "⇄";
        iconTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        iconTxt.fontSize = 16;
        iconTxt.color = isSelected ? Color.white : AccentCyan;
        iconTxt.alignment = TextAnchor.MiddleCenter;

        var btn = row.GetComponent<Button>();
        string routeId = route.id;
        btn.onClick.AddListener(() =>
        {
            TradingRouteManager.Instance.SelectRoute(routeId);
        });

        return row;
    }

    private void RefreshSelectedRouteHeader()
    {
        var route = TradingRouteManager.Instance.SelectedRoute;
        bool hasRoute = route != null;

        if (routeNameInput != null)
        {
            routeNameInput.interactable = hasRoute;
            routeNameInput.text = hasRoute ? route.name : "";
        }

        if (routeModeDropdown != null)
        {
            routeModeDropdown.interactable = hasRoute;
            if (hasRoute)
            {
                routeModeDropdown.value = (int)route.mode;
            }
        }

        if (deleteRouteButton != null)
        {
            deleteRouteButton.interactable = hasRoute;
        }

        if (addStationButton != null)
        {
            addStationButton.interactable = hasRoute;
        }

        if (addShipButton != null)
        {
            addShipButton.interactable = hasRoute;
        }
    }

    private void OnNewRouteClicked()
    {
        TradingRouteManager.Instance.CreateRoute();
    }

    private void OnDeleteRouteClicked()
    {
        var route = TradingRouteManager.Instance.SelectedRoute;
        if (route != null)
        {
            TradingRouteManager.Instance.DeleteRoute(route.id);
        }
    }

    private void OnRouteNameEdited(string newName)
    {
        var route = TradingRouteManager.Instance.SelectedRoute;
        if (route != null && !string.IsNullOrWhiteSpace(newName))
        {
            route.name = newName;
            TradingRouteManager.Instance.NotifyRouteUpdated(route);
        }
    }

    private void OnRouteModeChanged(int modeIdx)
    {
        var route = TradingRouteManager.Instance.SelectedRoute;
        if (route != null)
        {
            route.mode = (TradeRouteMode)modeIdx;
            TradingRouteManager.Instance.NotifyRouteUpdated(route);
        }
    }

    #endregion

    #region Stations List Section

    private void RefreshStationsList()
    {
        if (stationsContainer == null) return;

        foreach (var obj in spawnedStationEntries)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedStationEntries.Clear();

        var route = TradingRouteManager.Instance.SelectedRoute;
        if (route == null || route.stations == null) return;

        for (int i = 0; i < route.stations.Count; i++)
        {
            var station = route.stations[i];
            int stationIdx = i;
            GameObject stationRow = CreateStationRow(route, station, stationIdx);
            stationRow.transform.SetParent(stationsContainer, false);
            spawnedStationEntries.Add(stationRow);
        }
    }

    private GameObject CreateStationRow(TradingRoute route, TradeRouteStation station, int index)
    {
        GameObject row = new GameObject($"Station_{station.id}", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        var rt = row.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 80);

        var img = row.GetComponent<Image>();
        img.color = EntryBgNormal;

        var vlg = row.GetComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 4;
        vlg.padding = new RectOffset(6, 6, 6, 6);

        // Header Row: Station Name + Move Up/Down + Remove
        GameObject headerObj = new GameObject("StationHeader", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        headerObj.transform.SetParent(row.transform, false);
        var headerRt = headerObj.GetComponent<RectTransform>();
        headerRt.sizeDelta = new Vector2(0, 26);

        var hlg = headerObj.GetComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.spacing = 6;

        // Station Number + Name
        GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleObj.transform.SetParent(headerObj.transform, false);
        var titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.sizeDelta = new Vector2(170, 26);

        var titleTxt = titleObj.GetComponent<Text>();
        var island = TradingRouteManager.ResolveIsland(station);
        bool hasOperationalHarbor = island != null && TradePort.HasOperationalHarborOnIsland(island);
        string harborWarning = hasOperationalHarbor ? "" : " ⚠ [No Harbor]";

        titleTxt.text = $"{index + 1}. {station.stationName}{harborWarning}";
        titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleTxt.fontSize = 13;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.color = hasOperationalHarbor ? AccentCyan : new Color(1f, 0.45f, 0.35f, 1f);
        titleTxt.alignment = TextAnchor.MiddleLeft;

        // Move Up Button
        if (index > 0)
        {
            CreateMiniButton(headerObj.transform, "▲", () =>
            {
                route.MoveStation(index, index - 1);
                TradingRouteManager.Instance.NotifyRouteUpdated(route);
            });
        }

        // Move Down Button
        if (index < route.stations.Count - 1)
        {
            CreateMiniButton(headerObj.transform, "▼", () =>
            {
                route.MoveStation(index, index + 1);
                TradingRouteManager.Instance.NotifyRouteUpdated(route);
            });
        }

        // Remove Button
        CreateMiniButton(headerObj.transform, "✕", () =>
        {
            route.RemoveStation(station.id);
            TradingRouteManager.Instance.NotifyRouteUpdated(route);
        }, new Color(0.8f, 0.2f, 0.2f, 0.8f));

        // Cargo Slots Container (Horizontal Row)
        GameObject slotsContainer = new GameObject("CargoSlots", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        slotsContainer.transform.SetParent(row.transform, false);
        var slotsRt = slotsContainer.GetComponent<RectTransform>();
        slotsRt.sizeDelta = new Vector2(0, 42);

        var slotsHlg = slotsContainer.GetComponent<HorizontalLayoutGroup>();
        slotsHlg.childControlWidth = false;
        slotsHlg.childControlHeight = false;
        slotsHlg.spacing = 6;

        // Spawn Cargo Target Slots
        if (station.cargoTargets != null)
        {
            foreach (var target in station.cargoTargets)
            {
                if (target == null || target.item == null) continue;
                TradeCargoTarget capturedTarget = target;
                CreateCargoSlotUI(slotsContainer.transform, target, () =>
                {
                    // Open edit dialog
                    if (cargoDialog != null)
                    {
                        cargoDialog.Open(
                            capturedTarget.item,
                            capturedTarget.desiredShipAmount,
                            (item, amount) =>
                            {
                                capturedTarget.item = item;
                                capturedTarget.desiredShipAmount = amount;
                                TradingRouteManager.Instance.NotifyRouteUpdated(route);
                            },
                            () =>
                            {
                                station.RemoveTarget(capturedTarget.item);
                                TradingRouteManager.Instance.NotifyRouteUpdated(route);
                            }
                        );
                    }
                });
            }
        }

        // Add Target Slot Button
        CreateAddCargoSlotButton(slotsContainer.transform, () =>
        {
            if (cargoDialog != null)
            {
                cargoDialog.Open(
                    null,
                    40,
                    (item, amount) =>
                    {
                        station.SetTarget(item, amount);
                        TradingRouteManager.Instance.NotifyRouteUpdated(route);
                    }
                );
            }
        });

        return row;
    }

    private void CreateCargoSlotUI(Transform parent, TradeCargoTarget target, Action onClick)
    {
        GameObject slotObj = new GameObject($"Slot_{target.item.name}", typeof(RectTransform), typeof(Image), typeof(Button));
        slotObj.transform.SetParent(parent, false);
        var rt = slotObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(58, 40);

        var img = slotObj.GetComponent<Image>();
        img.color = SlotBg;

        // Icon
        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(slotObj.transform, false);
        var iconRt = iconObj.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0, 0.5f);
        iconRt.anchorMax = new Vector2(0, 0.5f);
        iconRt.sizeDelta = new Vector2(24, 24);
        iconRt.anchoredPosition = new Vector2(14, 0);

        var iconImg = iconObj.GetComponent<Image>();
        if (target.item.Icon != null)
        {
            iconImg.sprite = target.item.Icon;
        }

        // Amount / Instruction Text
        GameObject amtObj = new GameObject("Amount", typeof(RectTransform), typeof(Text));
        amtObj.transform.SetParent(slotObj.transform, false);
        var amtRt = amtObj.GetComponent<RectTransform>();
        amtRt.anchorMin = new Vector2(0.5f, 0);
        amtRt.anchorMax = new Vector2(1, 1);
        amtRt.offsetMin = Vector2.zero;
        amtRt.offsetMax = new Vector2(-2, 0);

        var amtTxt = amtObj.GetComponent<Text>();
        amtTxt.text = target.desiredShipAmount.ToString();
        amtTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        amtTxt.fontSize = 12;
        amtTxt.fontStyle = FontStyle.Bold;
        amtTxt.color = target.desiredShipAmount > 0 ? GreenLoad : RedUnload;
        amtTxt.alignment = TextAnchor.MiddleCenter;

        var btn = slotObj.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
    }

    private void CreateAddCargoSlotButton(Transform parent, Action onClick)
    {
        GameObject btnObj = new GameObject("AddSlotBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);
        var rt = btnObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(36, 40);

        var img = btnObj.GetComponent<Image>();
        img.color = new Color(0.15f, 0.28f, 0.42f, 0.8f);

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(btnObj.transform, false);
        var textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;

        var txt = textObj.GetComponent<Text>();
        txt.text = "+";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 20;
        txt.color = AccentCyan;
        txt.alignment = TextAnchor.MiddleCenter;

        var btn = btnObj.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
    }

    private void CreateMiniButton(Transform parent, string label, Action onClick, Color? bgColor = null)
    {
        GameObject btnObj = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);
        var rt = btnObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(22, 22);

        var img = btnObj.GetComponent<Image>();
        img.color = bgColor ?? new Color(0.2f, 0.35f, 0.5f, 0.8f);

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(btnObj.transform, false);
        var textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;

        var txt = textObj.GetComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 12;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;

        var btn = btnObj.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
    }

    private void OnAddStationClicked()
    {
        var route = TradingRouteManager.Instance.SelectedRoute;
        if (route == null) return;

        if (islandPickerModal != null && islandPickerContainer != null)
        {
            islandPickerModal.SetActive(true);
            PopulateIslandPicker(route);
        }
        else
        {
            // Fallback: pick first available island not yet last in sequence
            if (IslandManager.instance != null && IslandManager.instance.islands != null)
            {
                foreach (var island in IslandManager.instance.islands)
                {
                    if (island != null)
                    {
                        route.AddStation(new TradeRouteStation(island));
                        TradingRouteManager.Instance.NotifyRouteUpdated(route);
                        break;
                    }
                }
            }
        }
    }

    private void PopulateIslandPicker(TradingRoute route)
    {
        if (islandPickerContainer == null) return;

        foreach (Transform child in islandPickerContainer)
        {
            Destroy(child.gameObject);
        }

        if (IslandManager.instance == null || IslandManager.instance.islands == null) return;

        foreach (var island in IslandManager.instance.islands)
        {
            if (island == null) continue;
            Island capturedIsland = island;

            GameObject item = new GameObject($"Island_{island.id}", typeof(RectTransform), typeof(Image), typeof(Button));
            item.transform.SetParent(islandPickerContainer, false);
            var rt = item.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 32);

            var img = item.GetComponent<Image>();
            img.color = EntryBgNormal;

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(item.transform, false);
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10, 0);

            var txt = textObj.GetComponent<Text>();
            string baseName = !string.IsNullOrWhiteSpace(island.islandName) ? island.islandName : $"Island {island.id}";
            bool hasHarbor = TradePort.HasOperationalHarborOnIsland(island);
            txt.text = hasHarbor ? baseName : $"{baseName} (No Harbor)";
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 13;
            txt.color = hasHarbor ? TextWhite : TextDim;
            txt.alignment = TextAnchor.MiddleLeft;

            var btn = item.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                route.AddStation(new TradeRouteStation(capturedIsland));
                TradingRouteManager.Instance.NotifyRouteUpdated(route);
                if (islandPickerModal != null) islandPickerModal.SetActive(false);
            });
        }
    }

    #endregion

    #region Ships List Section

    private void RefreshShipsList()
    {
        if (shipsContainer == null) return;

        foreach (var obj in spawnedShipEntries)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedShipEntries.Clear();

        var route = TradingRouteManager.Instance.SelectedRoute;
        if (route == null || route.assignedShipIds == null) return;

        foreach (var shipId in route.assignedShipIds)
        {
            Unit ship = TradingRouteManager.Instance.FindUnitById(shipId);
            if (ship == null) continue;

            GameObject shipRow = CreateShipRow(route, ship);
            shipRow.transform.SetParent(shipsContainer, false);
            spawnedShipEntries.Add(shipRow);
        }
    }

    private GameObject CreateShipRow(TradingRoute route, Unit ship)
    {
        GameObject row = new GameObject($"Ship_{ship.ID}", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        var rt = row.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 34);

        var img = row.GetComponent<Image>();
        img.color = EntryBgNormal;

        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.spacing = 8;
        hlg.padding = new RectOffset(8, 8, 4, 4);

        // Ship Icon
        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Text));
        iconObj.transform.SetParent(row.transform, false);
        var iconRt = iconObj.GetComponent<RectTransform>();
        iconRt.sizeDelta = new Vector2(24, 26);

        var iconTxt = iconObj.GetComponent<Text>();
        iconTxt.text = "⛵";
        iconTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        iconTxt.fontSize = 16;
        iconTxt.color = AccentCyan;
        iconTxt.alignment = TextAnchor.MiddleCenter;

        // Ship Name + State
        GameObject nameObj = new GameObject("Name", typeof(RectTransform), typeof(Text));
        nameObj.transform.SetParent(row.transform, false);
        var nameRt = nameObj.GetComponent<RectTransform>();
        nameRt.sizeDelta = new Vector2(170, 26);

        var nameTxt = nameObj.GetComponent<Text>();
        var controller = ship.GetComponent<ShipTradeRouteController>();
        string stateStr = "";
        if (controller != null)
        {
            if (controller.IsPaused)
            {
                stateStr = " (Paused)";
            }
            else if (controller.CurrentState == TradeRouteState.WaitingForDock)
            {
                int rank = controller.CurrentTargetPort != null ? controller.CurrentTargetPort.GetQueueIndex(controller) + 1 : 0;
                stateStr = rank > 0 ? $" (Queue #{rank})" : " (Waiting Dock)";
            }
            else if (controller.CurrentState == TradeRouteState.WaitingForCargoCondition)
            {
                stateStr = " (Waiting Cargo)";
            }
            else
            {
                stateStr = $" ({controller.CurrentState})";
            }
        }

        nameTxt.text = $"{ship.displayName}{stateStr}";
        nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameTxt.fontSize = 13;
        nameTxt.color = (controller != null && controller.IsPaused) ? new Color(1f, 0.75f, 0.3f, 1f) : TextWhite;
        nameTxt.alignment = TextAnchor.MiddleLeft;

        // Resume Button if paused
        if (controller != null && controller.IsPaused)
        {
            CreateMiniButton(row.transform, "▶", () =>
            {
                controller.ResumeRoute();
                TradingRouteManager.Instance.NotifyRouteUpdated(TradingRouteManager.Instance.SelectedRoute);
            }, new Color(0.2f, 0.7f, 0.3f, 0.85f));
        }

        // Unassign Button
        CreateMiniButton(row.transform, "✕", () =>
        {
            TradingRouteManager.Instance.UnassignShip(ship);
        }, new Color(0.7f, 0.2f, 0.2f, 0.8f));

        return row;
    }

    private void OnAddShipClicked()
    {
        var route = TradingRouteManager.Instance.SelectedRoute;
        if (route == null) return;

        if (shipPickerModal != null && shipPickerContainer != null)
        {
            shipPickerModal.SetActive(true);
            PopulateShipPicker(route);
        }
    }

    private void PopulateShipPicker(TradingRoute route)
    {
        if (shipPickerContainer == null) return;

        foreach (Transform child in shipPickerContainer)
        {
            Destroy(child.gameObject);
        }

        if (UnitSelections.Instance == null || UnitSelections.Instance.unitList == null) return;

        foreach (var unit in UnitSelections.Instance.unitList)
        {
            if (unit == null) continue;

            // Only trade/surface/cargo ships
            if (!InfluenceManager.IsBoatUnit(unit)) continue;

            Unit capturedUnit = unit;
            TradingRoute assignedRoute = TradingRouteManager.Instance.GetAssignedRouteForShip(unit.ID);
            bool isAlreadyOnThisRoute = assignedRoute == route;

            GameObject item = new GameObject($"ShipPick_{unit.ID}", typeof(RectTransform), typeof(Image), typeof(Button));
            item.transform.SetParent(shipPickerContainer, false);
            var rt = item.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 32);

            var img = item.GetComponent<Image>();
            img.color = isAlreadyOnThisRoute ? EntryBgSelected : EntryBgNormal;

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(item.transform, false);
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10, 0);

            var txt = textObj.GetComponent<Text>();
            string assignmentInfo = assignedRoute != null ? $" [On: {assignedRoute.name}]" : " [Idle]";
            txt.text = $"{unit.displayName}{assignmentInfo}";
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 13;
            txt.color = isAlreadyOnThisRoute ? AccentCyan : TextWhite;
            txt.alignment = TextAnchor.MiddleLeft;

            var btn = item.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                TradingRouteManager.Instance.AssignShip(route.id, capturedUnit);
                if (shipPickerModal != null) shipPickerModal.SetActive(false);
            });
        }
    }

    #endregion
}
