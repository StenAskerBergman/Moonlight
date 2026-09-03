using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Display filters for the Strategic Map screen.
/// </summary>
[Flags]
public enum StrategicMapFilter
{
    None = 0,
    Islands = 1 << 0,
    Ships = 1 << 1,
    Harbors = 1 << 2,
    Routes = 1 << 3,
    All = Islands | Ships | Harbors | Routes
}

/// <summary>
/// Master window controller for the Anno 2070-style Strategic Map & Trading Routes screen.
/// Renders an interactive 2D top-down projection of actual game islands, settlements,
/// harbors, ships, and route visualization lines, paired with the Trading Route configuration panel.
/// </summary>
public class StrategicMapUI : MonoBehaviour
{
    private static StrategicMapUI _instance;
    public static StrategicMapUI Instance => _instance;

    [Header("Window Elements")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private RectTransform worldMapRect;
    [SerializeField] private StrategicMapRouteDrawer routeDrawer;
    [SerializeField] private TradingRoutePanelUI routePanel;
    [SerializeField] private Button closeButton;

    [Header("Display Filter Buttons")]
    [SerializeField] private Button filterAllButton;
    [SerializeField] private Button filterIslandsButton;
    [SerializeField] private Button filterShipsButton;
    [SerializeField] private Button filterHarborsButton;
    [SerializeField] private Button filterRoutesButton;

    [Header("World Map Content Containers")]
    [SerializeField] private RectTransform islandMarkersContainer;
    [SerializeField] private RectTransform shipMarkersContainer;
    [SerializeField] private RectTransform harborMarkersContainer;

    [Header("Map Settings")]
    [SerializeField] private float mapPadding = 80f;
    [SerializeField] private KeyCode toggleKey = KeyCode.M;

    private StrategicMapFilter activeFilters = StrategicMapFilter.All;
    private bool isOpen = false;

    // Bounds cache
    private float worldMinX, worldMaxX, worldMinZ, worldMaxZ;

    // Spawned marker pools
    private readonly List<GameObject> islandMarkers = new List<GameObject>();
    private readonly List<GameObject> shipMarkers = new List<GameObject>();
    private readonly List<GameObject> harborMarkers = new List<GameObject>();

    // Styling Colors
    private static readonly Color ColorOcean = new Color(0.04f, 0.12f, 0.18f, 0.95f);
    private static readonly Color ColorIsland = new Color(0.18f, 0.38f, 0.35f, 0.9f);
    private static readonly Color ColorIslandSelected = new Color(0.25f, 0.60f, 0.55f, 1f);
    private static readonly Color ColorHarbor = new Color(0.9f, 0.75f, 0.2f, 1f);
    private static readonly Color ColorShip = new Color(0.3f, 0.75f, 1f, 1f);
    private static readonly Color ColorShipAssigned = new Color(0.2f, 1f, 0.5f, 1f);
    private static readonly Color ColorText = new Color(0.9f, 0.95f, 1f, 1f);

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (windowRoot == null)
        {
            BuildRuntimeHierarchy();
        }

        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (filterAllButton != null) filterAllButton.onClick.AddListener(() => SetFilter(StrategicMapFilter.All));
        if (filterIslandsButton != null) filterIslandsButton.onClick.AddListener(() => ToggleFilter(StrategicMapFilter.Islands));
        if (filterShipsButton != null) filterShipsButton.onClick.AddListener(() => ToggleFilter(StrategicMapFilter.Ships));
        if (filterHarborsButton != null) filterHarborsButton.onClick.AddListener(() => ToggleFilter(StrategicMapFilter.Harbors));
        if (filterRoutesButton != null) filterRoutesButton.onClick.AddListener(() => ToggleFilter(StrategicMapFilter.Routes));

        if (windowRoot != null) windowRoot.SetActive(false);
    }

    private void Start()
    {
        if (TradingRouteManager.Instance != null)
        {
            TradingRouteManager.Instance.OnRouteSelected += OnRouteSelected;
            TradingRouteManager.Instance.OnRouteUpdated += OnRouteUpdated;
        }
    }

    private void OnDestroy()
    {
        if (TradingRouteManager.Instance != null)
        {
            TradingRouteManager.Instance.OnRouteSelected -= OnRouteSelected;
            TradingRouteManager.Instance.OnRouteUpdated -= OnRouteUpdated;
        }
    }

    private void Update()
    {
        // Toggle hotkey
        if (Input.GetKeyDown(toggleKey))
        {
            Toggle();
        }
        else if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }

        if (isOpen)
        {
            UpdateShipMarkersPositions();
        }
    }

    #region Open / Close / Toggle

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        isOpen = true;
        if (windowRoot != null) windowRoot.SetActive(true);

        CalculateWorldBounds();
        RebuildMapMarkers();
        UpdateRouteVisualization();

        if (routePanel != null)
        {
            routePanel.RefreshAll();
        }
    }

    public void Close()
    {
        isOpen = false;
        if (windowRoot != null) windowRoot.SetActive(false);
    }

    #endregion

    #region Filters

    public void SetFilter(StrategicMapFilter filter)
    {
        activeFilters = filter;
        ApplyFiltersVisibility();
    }

    public void ToggleFilter(StrategicMapFilter filter)
    {
        if ((activeFilters & filter) != 0)
        {
            activeFilters &= ~filter;
        }
        else
        {
            activeFilters |= filter;
        }
        ApplyFiltersVisibility();
    }

    private void ApplyFiltersVisibility()
    {
        if (islandMarkersContainer != null)
            islandMarkersContainer.gameObject.SetActive((activeFilters & StrategicMapFilter.Islands) != 0);

        if (shipMarkersContainer != null)
            shipMarkersContainer.gameObject.SetActive((activeFilters & StrategicMapFilter.Ships) != 0);

        if (harborMarkersContainer != null)
            harborMarkersContainer.gameObject.SetActive((activeFilters & StrategicMapFilter.Harbors) != 0);

        if (routeDrawer != null)
            routeDrawer.gameObject.SetActive((activeFilters & StrategicMapFilter.Routes) != 0);
    }

    #endregion

    #region World Coordinate Projection

    private void CalculateWorldBounds()
    {
        worldMinX = float.MaxValue;
        worldMaxX = float.MinValue;
        worldMinZ = float.MaxValue;
        worldMaxZ = float.MinValue;

        bool hasPoints = false;

        if (IslandManager.instance != null && IslandManager.instance.islands != null)
        {
            foreach (var island in IslandManager.instance.islands)
            {
                if (island == null) continue;
                hasPoints = true;
                Bounds b = island.bounds;
                worldMinX = Mathf.Min(worldMinX, b.min.x);
                worldMaxX = Mathf.Max(worldMaxX, b.max.x);
                worldMinZ = Mathf.Min(worldMinZ, b.min.z);
                worldMaxZ = Mathf.Max(worldMaxZ, b.max.z);
            }
        }

        if (!hasPoints)
        {
            worldMinX = -200f;
            worldMaxX = 200f;
            worldMinZ = -200f;
            worldMaxZ = 200f;
        }
        else
        {
            worldMinX -= mapPadding;
            worldMaxX += mapPadding;
            worldMinZ -= mapPadding;
            worldMaxZ += mapPadding;
        }
    }

    public Vector2 WorldToMapLocal(Vector3 worldPos)
    {
        if (worldMapRect == null) return Vector2.zero;

        float width = worldMapRect.rect.width;
        float height = worldMapRect.rect.height;

        float spanX = Mathf.Max(1f, worldMaxX - worldMinX);
        float spanZ = Mathf.Max(1f, worldMaxZ - worldMinZ);

        float normX = (worldPos.x - worldMinX) / spanX;
        float normY = (worldPos.z - worldMinZ) / spanZ;

        float localX = (normX - 0.5f) * width;
        float localY = (normY - 0.5f) * height;

        return new Vector2(localX, localY);
    }

    public Vector2 WorldSizeToMapLocal(Vector3 worldSize)
    {
        if (worldMapRect == null) return Vector2.zero;

        float width = worldMapRect.rect.width;
        float height = worldMapRect.rect.height;

        float spanX = Mathf.Max(1f, worldMaxX - worldMinX);
        float spanZ = Mathf.Max(1f, worldMaxZ - worldMinZ);

        float localW = (worldSize.x / spanX) * width;
        float localH = (worldSize.z / spanZ) * height;

        return new Vector2(localW, localH);
    }

    #endregion

    #region Map Markers Generation

    private void RebuildMapMarkers()
    {
        RebuildIslandMarkers();
        RebuildHarborMarkers();
        RebuildShipMarkers();
        ApplyFiltersVisibility();
    }

    private void RebuildIslandMarkers()
    {
        if (islandMarkersContainer == null) return;

        foreach (var m in islandMarkers) if (m != null) Destroy(m);
        islandMarkers.Clear();

        if (IslandManager.instance == null || IslandManager.instance.islands == null) return;

        foreach (var island in IslandManager.instance.islands)
        {
            if (island == null) continue;

            Vector2 localPos = WorldToMapLocal(island.bounds.center);
            Vector2 localSize = WorldSizeToMapLocal(island.bounds.size);
            localSize.x = Mathf.Max(36f, localSize.x);
            localSize.y = Mathf.Max(36f, localSize.y);

            GameObject marker = new GameObject($"Island_{island.id}", typeof(RectTransform), typeof(Image), typeof(Button));
            marker.transform.SetParent(islandMarkersContainer, false);

            var rt = marker.GetComponent<RectTransform>();
            rt.anchoredPosition = localPos;
            rt.sizeDelta = localSize;

            var img = marker.GetComponent<Image>();
            img.color = ColorIsland;

            // Island Name Label
            GameObject nameObj = new GameObject("NameLabel", typeof(RectTransform), typeof(Text));
            nameObj.transform.SetParent(marker.transform, false);
            var nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0, 0);
            nameRt.anchorMax = new Vector2(1, 1);

            var txt = nameObj.GetComponent<Text>();
            txt.text = !string.IsNullOrWhiteSpace(island.islandName) ? island.islandName : $"Island {island.id}";
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 11;
            txt.color = ColorText;
            txt.alignment = TextAnchor.MiddleCenter;

            // Click on island adds it to selected route as a station
            Island capturedIsland = island;
            var btn = marker.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                var route = TradingRouteManager.Instance.SelectedRoute;
                if (route != null)
                {
                    route.AddStation(new TradeRouteStation(capturedIsland));
                    TradingRouteManager.Instance.NotifyRouteUpdated(route);
                }
            });

            islandMarkers.Add(marker);
        }
    }

    private void RebuildHarborMarkers()
    {
        if (harborMarkersContainer == null) return;

        foreach (var m in harborMarkers) if (m != null) Destroy(m);
        harborMarkers.Clear();

        if (IslandManager.instance == null || IslandManager.instance.islands == null) return;

        foreach (var island in IslandManager.instance.islands)
        {
            if (island == null) continue;

            TradePort port = TradePort.ResolveForIsland(island);
            if (port == null) continue;

            Vector3 worldApproach = port.GetApproachPoint();
            Vector2 localPos = WorldToMapLocal(worldApproach);

            GameObject marker = new GameObject($"Harbor_{island.id}", typeof(RectTransform), typeof(Image));
            marker.transform.SetParent(harborMarkersContainer, false);

            var rt = marker.GetComponent<RectTransform>();
            rt.anchoredPosition = localPos;
            rt.sizeDelta = new Vector2(14, 14);

            var img = marker.GetComponent<Image>();
            img.color = ColorHarbor;

            harborMarkers.Add(marker);
        }
    }

    private void RebuildShipMarkers()
    {
        if (shipMarkersContainer == null) return;

        foreach (var m in shipMarkers) if (m != null) Destroy(m);
        shipMarkers.Clear();

        if (UnitSelections.Instance == null || UnitSelections.Instance.unitList == null) return;

        var selectedRoute = TradingRouteManager.Instance.SelectedRoute;

        foreach (var unit in UnitSelections.Instance.unitList)
        {
            if (unit == null || !InfluenceManager.IsBoatUnit(unit)) continue;

            bool isAssignedToRoute = selectedRoute != null && selectedRoute.assignedShipIds.Contains(unit.ID);

            GameObject marker = new GameObject($"ShipMarker_{unit.ID}", typeof(RectTransform), typeof(Image));
            marker.transform.SetParent(shipMarkersContainer, false);

            var rt = marker.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(12, 12);
            rt.anchoredPosition = WorldToMapLocal(unit.transform.position);

            var img = marker.GetComponent<Image>();
            img.color = isAssignedToRoute ? ColorShipAssigned : ColorShip;

            shipMarkers.Add(marker);
        }
    }

    private void UpdateShipMarkersPositions()
    {
        if (UnitSelections.Instance == null || UnitSelections.Instance.unitList == null) return;

        var selectedRoute = TradingRouteManager.Instance.SelectedRoute;
        int markerIdx = 0;

        foreach (var unit in UnitSelections.Instance.unitList)
        {
            if (unit == null || !InfluenceManager.IsBoatUnit(unit)) continue;
            if (markerIdx >= shipMarkers.Count) break;

            GameObject marker = shipMarkers[markerIdx];
            if (marker != null)
            {
                var rt = marker.GetComponent<RectTransform>();
                rt.anchoredPosition = WorldToMapLocal(unit.transform.position);
                rt.localEulerAngles = new Vector3(0, 0, -unit.transform.eulerAngles.y);

                var img = marker.GetComponent<Image>();
                bool isAssigned = selectedRoute != null && selectedRoute.assignedShipIds.Contains(unit.ID);
                img.color = isAssigned ? ColorShipAssigned : ColorShip;
            }

            markerIdx++;
        }
    }

    #endregion

    #region Route Visualization

    private void OnRouteSelected(TradingRoute route)
    {
        UpdateRouteVisualization();
    }

    private void OnRouteUpdated(TradingRoute route)
    {
        UpdateRouteVisualization();
    }

    public void UpdateRouteVisualization()
    {
        if (routeDrawer == null) return;

        var route = TradingRouteManager.Instance.SelectedRoute;
        if (route == null || route.stations == null || route.stations.Count == 0)
        {
            routeDrawer.Clear();
            return;
        }

        var points = new List<Vector2>();

        foreach (var station in route.stations)
        {
            if (station == null) continue;
            Island island = ResolveIsland(station);
            if (island != null)
            {
                TradePort port = TradePort.ResolveForIsland(island);
                Vector3 worldPos = port != null ? port.GetApproachPoint() : island.bounds.center;
                Vector2 mapPt = WorldToMapLocal(worldPos);
                points.Add(mapPt);
            }
        }

        routeDrawer.SetPoints(points, loop: points.Count > 1);
    }

    private Island ResolveIsland(TradeRouteStation station)
    {
        if (station == null) return null;
        if (IslandManager.instance != null && IslandManager.instance.islands != null)
        {
            if (!string.IsNullOrEmpty(station.islandId))
            {
                var match = IslandManager.instance.islands.Find(i => i != null && i.ID == station.islandId);
                if (match != null) return match;
            }
            if (station.islandIndex > 0)
            {
                var match = IslandManager.instance.GetIsland(station.islandIndex);
                if (match != null) return match;
            }
            if (!string.IsNullOrEmpty(station.stationName))
            {
                var match = IslandManager.instance.GetIslandByName(station.stationName);
                if (match != null) return match;
            }
        }
        return null;
    }

    #endregion

    #region Runtime UI Hierarchy Construction (Fallback & Autonomous Setup)

    /// <summary>
    /// Programmatically constructs the complete Strategic Map UI Canvas hierarchy if no authored prefab is provided.
    /// </summary>
    private void BuildRuntimeHierarchy()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("StrategicMapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            transform.SetParent(canvasObj.transform, false);
        }

        // Window Root
        windowRoot = new GameObject("StrategicMapWindow", typeof(RectTransform), typeof(Image));
        windowRoot.transform.SetParent(transform, false);
        var windowRt = windowRoot.GetComponent<RectTransform>();
        windowRt.anchorMin = Vector2.zero;
        windowRt.anchorMax = Vector2.one;
        windowRt.offsetMin = Vector2.zero;
        windowRt.offsetMax = Vector2.zero;

        var windowImg = windowRoot.GetComponent<Image>();
        windowImg.color = new Color(0.03f, 0.08f, 0.14f, 0.98f);

        // Header Bar
        GameObject headerObj = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
        headerObj.transform.SetParent(windowRoot.transform, false);
        var headerRt = headerObj.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0, 1);
        headerRt.anchorMax = new Vector2(1, 1);
        headerRt.sizeDelta = new Vector2(0, 50);
        headerRt.anchoredPosition = new Vector2(0, -25);

        var headerImg = headerObj.GetComponent<Image>();
        headerImg.color = new Color(0.06f, 0.14f, 0.24f, 1f);

        // Header Title
        GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleObj.transform.SetParent(headerObj.transform, false);
        var titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 0);
        titleRt.anchorMax = new Vector2(1, 1);
        titleRt.offsetMin = new Vector2(24, 0);

        var titleTxt = titleObj.GetComponent<Text>();
        titleTxt.text = "STRATEGIC MAP & TRADE ROUTES";
        titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleTxt.fontSize = 20;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.color = ColorText;
        titleTxt.alignment = TextAnchor.MiddleLeft;

        // Top Filter Bar
        GameObject filterBar = new GameObject("FilterBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        filterBar.transform.SetParent(headerObj.transform, false);
        var filterRt = filterBar.GetComponent<RectTransform>();
        filterRt.anchorMin = new Vector2(0.35f, 0);
        filterRt.anchorMax = new Vector2(0.65f, 1);
        filterRt.offsetMin = Vector2.zero;
        filterRt.offsetMax = Vector2.zero;

        var fHlg = filterBar.GetComponent<HorizontalLayoutGroup>();
        fHlg.childControlWidth = true;
        fHlg.childControlHeight = true;
        fHlg.spacing = 8;
        fHlg.padding = new RectOffset(6, 6, 8, 8);

        filterAllButton = CreateFilterBtn(filterBar.transform, "All");
        filterIslandsButton = CreateFilterBtn(filterBar.transform, "Islands");
        filterShipsButton = CreateFilterBtn(filterBar.transform, "Ships");
        filterHarborsButton = CreateFilterBtn(filterBar.transform, "Harbors");
        filterRoutesButton = CreateFilterBtn(filterBar.transform, "Routes");

        // Main Content Area (World Map on Left, Trade Route Panel on Right)
        GameObject contentObj = new GameObject("ContentArea", typeof(RectTransform));
        contentObj.transform.SetParent(windowRoot.transform, false);
        var contentRt = contentObj.GetComponent<RectTransform>();
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = new Vector2(20, 20);
        contentRt.offsetMax = new Vector2(-20, -60);

        // Left Side: World Map View
        GameObject mapObj = new GameObject("WorldMap", typeof(RectTransform), typeof(Image), typeof(Mask));
        mapObj.transform.SetParent(contentObj.transform, false);
        worldMapRect = mapObj.GetComponent<RectTransform>();
        worldMapRect.anchorMin = Vector2.zero;
        worldMapRect.anchorMax = new Vector2(0.68f, 1);
        worldMapRect.offsetMin = Vector2.zero;
        worldMapRect.offsetMax = new Vector2(-10, 0);

        var mapImg = mapObj.GetComponent<Image>();
        mapImg.color = ColorOcean;

        // Route Drawer on World Map
        GameObject drawerObj = new GameObject("RouteDrawer", typeof(RectTransform), typeof(StrategicMapRouteDrawer));
        drawerObj.transform.SetParent(mapObj.transform, false);
        var drawerRt = drawerObj.GetComponent<RectTransform>();
        drawerRt.anchorMin = Vector2.zero;
        drawerRt.anchorMax = Vector2.one;
        drawerRt.offsetMin = Vector2.zero;
        drawerRt.offsetMax = Vector2.zero;
        routeDrawer = drawerObj.GetComponent<StrategicMapRouteDrawer>();

        // Markers Containers
        islandMarkersContainer = CreateLayerContainer(mapObj.transform, "IslandMarkers");
        harborMarkersContainer = CreateLayerContainer(mapObj.transform, "HarborMarkers");
        shipMarkersContainer = CreateLayerContainer(mapObj.transform, "ShipMarkers");

        // Right Side: Trading Route Panel
        GameObject panelObj = new GameObject("TradingRoutePanel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(contentObj.transform, false);
        var panelRt = panelObj.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.68f, 0);
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = new Vector2(10, 0);
        panelRt.offsetMax = Vector2.zero;

        var panelImg = panelObj.GetComponent<Image>();
        panelImg.color = new Color(0.06f, 0.13f, 0.22f, 0.95f);

        routePanel = panelObj.AddComponent<TradingRoutePanelUI>();
        BuildRoutePanelHierarchy(panelObj, routePanel);

        // Close Button (Bottom-left Anno 2070 style)
        GameObject closeBtnObj = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeBtnObj.transform.SetParent(windowRoot.transform, false);
        var closeRt = closeBtnObj.GetComponent<RectTransform>();
        closeRt.anchorMin = Vector2.zero;
        closeRt.anchorMax = Vector2.zero;
        closeRt.sizeDelta = new Vector2(140, 38);
        closeRt.anchoredPosition = new Vector2(90, 38);

        var closeImg = closeBtnObj.GetComponent<Image>();
        closeImg.color = new Color(0.75f, 0.18f, 0.18f, 1f);

        GameObject closeTxtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        var closeTxtRt = closeTxtObj.GetComponent<RectTransform>();
        closeTxtRt.anchorMin = Vector2.zero;
        closeTxtRt.anchorMax = Vector2.one;

        var closeTxt = closeTxtObj.GetComponent<Text>();
        closeTxt.text = "◄  CLOSE";
        closeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        closeTxt.fontSize = 14;
        closeTxt.fontStyle = FontStyle.Bold;
        closeTxt.color = Color.white;
        closeTxt.alignment = TextAnchor.MiddleCenter;

        closeButton = closeBtnObj.GetComponent<Button>();
    }

    private Button CreateFilterBtn(Transform parent, string label)
    {
        GameObject btnObj = new GameObject($"Filter_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);
        var img = btnObj.GetComponent<Image>();
        img.color = new Color(0.12f, 0.25f, 0.40f, 1f);

        GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        txtObj.transform.SetParent(btnObj.transform, false);
        var txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;

        var txt = txtObj.GetComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 12;
        txt.color = ColorText;
        txt.alignment = TextAnchor.MiddleCenter;

        return btnObj.GetComponent<Button>();
    }

    private RectTransform CreateLayerContainer(Transform parent, string name)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private void BuildRoutePanelHierarchy(GameObject panelRoot, TradingRoutePanelUI panelUI)
    {
        // Layout: Vertical with 3 sections
        // 1. Trading Routes (30% height)
        // 2. Stations (45% height)
        // 3. Ships (25% height)

        var routeSection = CreateSectionBox(panelRoot.transform, "Trading Routes", new Vector2(0, 0.70f), Vector2.one, out Transform routeContent, out Button newRouteBtn);
        var stationSection = CreateSectionBox(panelRoot.transform, "Stations", new Vector2(0, 0.28f), new Vector2(1, 0.70f), out Transform stationContent, out Button addStationBtn);
        var shipSection = CreateSectionBox(panelRoot.transform, "Ships", Vector2.zero, new Vector2(1, 0.28f), out Transform shipContent, out Button addShipBtn);

        // Modals: Cargo Target Dialog
        GameObject cargoModalObj = new GameObject("CargoTargetDialog", typeof(RectTransform), typeof(Image), typeof(CargoTargetDialogUI));
        cargoModalObj.transform.SetParent(windowRoot.transform, false);
        var cargoRt = cargoModalObj.GetComponent<RectTransform>();
        cargoRt.sizeDelta = new Vector2(420, 480);
        cargoRt.anchoredPosition = Vector2.zero;

        var cargoImg = cargoModalObj.GetComponent<Image>();
        cargoImg.color = new Color(0.06f, 0.12f, 0.20f, 0.98f);

        var cargoDialog = cargoModalObj.GetComponent<CargoTargetDialogUI>();

        // Wire fields via reflection or helper
        SetPrivateField(panelUI, "routesContainer", routeContent);
        SetPrivateField(panelUI, "stationsContainer", stationContent);
        SetPrivateField(panelUI, "shipsContainer", shipContent);
        SetPrivateField(panelUI, "newRouteButton", newRouteBtn);
        SetPrivateField(panelUI, "addStationButton", addStationBtn);
        SetPrivateField(panelUI, "addShipButton", addShipBtn);
        SetPrivateField(panelUI, "cargoDialog", cargoDialog);

        cargoModalObj.SetActive(false);
    }

    private GameObject CreateSectionBox(Transform parent, string title, Vector2 anchorMin, Vector2 anchorMax, out Transform contentParent, out Button actionBtn)
    {
        GameObject sectionObj = new GameObject($"Section_{title}", typeof(RectTransform), typeof(Image));
        sectionObj.transform.SetParent(parent, false);
        var rt = sectionObj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(6, 6);
        rt.offsetMax = new Vector2(-6, -6);

        var img = sectionObj.GetComponent<Image>();
        img.color = new Color(0.08f, 0.16f, 0.26f, 0.9f);

        // Header
        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(sectionObj.transform, false);
        var hRt = header.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0, 1);
        hRt.anchorMax = Vector2.one;
        hRt.sizeDelta = new Vector2(0, 28);
        hRt.anchoredPosition = new Vector2(0, -14);

        var hImg = header.GetComponent<Image>();
        hImg.color = new Color(0.12f, 0.24f, 0.38f, 1f);

        GameObject titleTxtObj = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
        titleTxtObj.transform.SetParent(header.transform, false);
        var tRt = titleTxtObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(10, 0);

        var txt = titleTxtObj.GetComponent<Text>();
        txt.text = title.ToUpper();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 12;
        txt.fontStyle = FontStyle.Bold;
        txt.color = ColorText;
        txt.alignment = TextAnchor.MiddleLeft;

        // Action Button (+ ADD / + NEW)
        GameObject btnObj = new GameObject("ActionBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(header.transform, false);
        var btnRt = btnObj.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(1, 0.5f);
        btnRt.anchorMax = new Vector2(1, 0.5f);
        btnRt.sizeDelta = new Vector2(90, 22);
        btnRt.anchoredPosition = new Vector2(-50, 0);

        var btnImg = btnObj.GetComponent<Image>();
        btnImg.color = new Color(0.18f, 0.42f, 0.65f, 1f);

        GameObject btnTxtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        btnTxtObj.transform.SetParent(btnObj.transform, false);
        var btnTxtRt = btnTxtObj.GetComponent<RectTransform>();
        btnTxtRt.anchorMin = Vector2.zero;
        btnTxtRt.anchorMax = Vector2.one;

        var btnTxt = btnTxtObj.GetComponent<Text>();
        btnTxt.text = $"+ {title.ToUpper()}";
        btnTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnTxt.fontSize = 11;
        btnTxt.fontStyle = FontStyle.Bold;
        btnTxt.color = Color.white;
        btnTxt.alignment = TextAnchor.MiddleCenter;

        actionBtn = btnObj.GetComponent<Button>();

        // Scroll View Content
        GameObject scrollObj = new GameObject("ScrollArea", typeof(RectTransform), typeof(ScrollRect));
        scrollObj.transform.SetParent(sectionObj.transform, false);
        var sRt = scrollObj.GetComponent<RectTransform>();
        sRt.anchorMin = Vector2.zero;
        sRt.anchorMax = Vector2.one;
        sRt.offsetMin = new Vector2(4, 4);
        sRt.offsetMax = new Vector2(-4, -30);

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(scrollObj.transform, false);
        var cRt = content.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0, 1);
        cRt.anchorMax = new Vector2(1, 1);
        cRt.pivot = new Vector2(0.5f, 1);
        cRt.sizeDelta = new Vector2(0, 0);

        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 4;

        var csf = content.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sr = scrollObj.GetComponent<ScrollRect>();
        sr.content = cRt;
        sr.horizontal = false;
        sr.vertical = true;

        contentParent = content.transform;
        return sectionObj;
    }

    private static void SetPrivateField(object target, string fieldName, object val)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(target, val);
        }
    }

    #endregion
}
