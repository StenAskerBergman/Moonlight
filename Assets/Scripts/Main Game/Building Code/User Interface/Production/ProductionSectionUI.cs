using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class ProductionSectionUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ConstructionPageDefinition pageDefinition;
    [SerializeField] private IslandPopulation population;
    [SerializeField] private ProductionPlacementAdapter placementAdapter;

    [Header("Layout")]
    [SerializeField] private Vector2 nodeSize = new Vector2(56f, 56f);
    [SerializeField] private float columnGap = 56f;
    [SerializeField] private float rowGap = 20f;
    [SerializeField] private float canvasPadding = 20f;
    [SerializeField] private float headerHeight = 24f;
    [SerializeField] private float selectorHeight = 56f;
    [SerializeField] private float connectorThickness = 2.5f;

    [Header("Palette")]
    [SerializeField] private Color panelBgColor = new Color(0.06f, 0.11f, 0.16f, 0.97f);
    [SerializeField] private Color panelBorderColor = new Color(0.18f, 0.30f, 0.40f, 0.70f);
    [SerializeField] private Color headerTextColor = new Color(0.72f, 0.84f, 0.92f, 0.95f);
    [SerializeField] private Color arrowBodyColor = new Color(0.09f, 0.17f, 0.24f, 0.85f);
    [SerializeField] private Color arrowTipColor = new Color(0.13f, 0.23f, 0.32f, 0.90f);
    [SerializeField] private Color nodeNormalBg = new Color(0.13f, 0.22f, 0.28f, 0.95f);
    [SerializeField] private Color nodeNormalBorder = new Color(0.24f, 0.38f, 0.48f, 0.70f);
    [SerializeField] private Color nodeLockedBg = new Color(0.10f, 0.12f, 0.14f, 0.85f);
    [SerializeField] private Color nodeLockedBorder = new Color(0.18f, 0.20f, 0.22f, 0.45f);
    [SerializeField] private Color selectorNormalBg = new Color(0.11f, 0.18f, 0.24f, 0.90f);
    [SerializeField] private Color selectorNormalBorder = new Color(0.22f, 0.35f, 0.44f, 0.60f);
    [SerializeField] private Color selectorSelectedBg = new Color(0.24f, 0.22f, 0.12f, 0.95f);
    [SerializeField] private Color selectorSelectedBorder = new Color(0.95f, 0.82f, 0.32f, 1.0f);
    [SerializeField] private Color selectorLockedBg = new Color(0.10f, 0.12f, 0.14f, 0.75f);
    [SerializeField] private Color selectorLockedBorder = new Color(0.16f, 0.18f, 0.20f, 0.40f);
    [SerializeField] private Color connectorColor = new Color(0.55f, 0.70f, 0.78f, 0.85f);

    [Header("Events")]
    public UnityEvent ExpandedLineChanged = new UnityEvent();

    private RectTransform headerStrip;
    private TMP_Text headerTitle;
    private RectTransform chainCanvas;
    private ProductionArrowBackground arrowBackground;
    private RectTransform connectorLayer;
    private RectTransform nodeLayer;
    private RectTransform selectorStrip;
    private RectTransform tooltipBox;
    private TMP_Text tooltipTitle;
    private TMP_Text tooltipDesc;
    private LayoutElement layoutElement;

    private readonly List<SelectorView> selectors = new List<SelectorView>();
    private readonly Dictionary<string, bool> previousUnlockStates = new Dictionary<string, bool>();
    private ProductionLineDefinition expandedLine;
    private PopulationClass populationClassFilter;
    private float currentCanvasHeight;

    public ConstructionPageDefinition PageDefinition => pageDefinition;
    public ProductionLineDefinition ExpandedLine => expandedLine;
    public bool IsExpanded => expandedLine != null;
    public float CollapsedHeight => headerHeight + selectorHeight + 4f;
    public float PreferredHeight => CollapsedHeight + (currentCanvasHeight > 0f ? currentCanvasHeight + 4f : 0f);

    private sealed class SelectorView
    {
        public ProductionLineDefinition Line;
        public Button Button;
        public Image BorderImage;
        public Image FillImage;
        public Image IconImage;
        public GameObject NewBadge;
        public ProductionTooltipTrigger Tooltip;
    }

    private void Awake()
    {
        EnsureVisualTree();
        RebuildSelectors();
        ClearExpandedChain();
    }

    private void OnEnable()
    {
        EnsureVisualTree();
        BindPopulation(population);
        RefreshUnlockStates(false);
        ProductionTooltipTrigger.OnTooltipShow += HandleTooltipShow;
        ProductionTooltipTrigger.OnTooltipHide += HandleTooltipHide;
    }

    private void OnDisable()
    {
        if (population != null) population.PopulationChanged -= OnPopulationChanged;
        ProductionTooltipTrigger.OnTooltipShow -= HandleTooltipShow;
        ProductionTooltipTrigger.OnTooltipHide -= HandleTooltipHide;
        HandleTooltipHide();
    }

    public void SetPage(
        ConstructionPageDefinition definition,
        IslandPopulation islandPopulation = null,
        PopulationClass classFilter = PopulationClass.None)
    {
        EnsureVisualTree();

        if (pageDefinition == definition &&
            population == islandPopulation &&
            populationClassFilter == classFilter)
        {
            return;
        }

        ClearExpandedChain();
        pageDefinition = definition;
        populationClassFilter = classFilter;
        BindPopulation(islandPopulation);
        RebuildSelectors();
        RefreshUnlockStates(false);
    }

    public void BindPopulation(IslandPopulation islandPopulation)
    {
        if (population == islandPopulation)
        {
            if (isActiveAndEnabled && population != null)
            {
                population.PopulationChanged -= OnPopulationChanged;
                population.PopulationChanged += OnPopulationChanged;
            }
            return;
        }

        if (population != null) population.PopulationChanged -= OnPopulationChanged;
        population = islandPopulation;
        if (isActiveAndEnabled && population != null) population.PopulationChanged += OnPopulationChanged;
        RefreshUnlockStates(false);
    }

    public void ExpandLine(string lineId)
    {
        if (pageDefinition == null || pageDefinition.ProductionLines == null) return;

        foreach (ProductionLineDefinition line in pageDefinition.ProductionLines)
        {
            if (line != null &&
                IsVisibleOnThisPage(line) &&
                string.Equals(line.Id, lineId, StringComparison.Ordinal))
            {
                ExpandLine(line);
                return;
            }
        }
    }

    public void ClearExpandedChain()
    {
        expandedLine = null;

        if (chainCanvas != null)
        {
            ClearChildren(connectorLayer);
            ClearChildren(nodeLayer);
            chainCanvas.gameObject.SetActive(false);
            SetCanvasHeight(0f);
        }

        UpdateHeaderText();
        RefreshSelectorVisuals();
        ExpandedLineChanged.Invoke();
    }

    private void ExpandLine(ProductionLineDefinition line)
    {
        if (line == null || !IsUnlocked(line.UnlockCondition)) return;

        expandedLine = line;
        ClearChildren(connectorLayer);
        ClearChildren(nodeLayer);
        chainCanvas.gameObject.SetActive(true);

        int maxColumn = 0;
        int minRow = int.MaxValue;
        int maxRow = 0;

        if (line.Nodes != null)
        {
            foreach (ProductionNodeDefinition node in line.Nodes)
            {
                if (node == null) continue;
                maxColumn = Mathf.Max(maxColumn, node.Column);
                minRow = Mathf.Min(minRow, node.Row);
                maxRow = Mathf.Max(maxRow, node.Row);
            }
        }

        if (minRow == int.MaxValue) minRow = 0;
        float height = canvasPadding * 2f + (maxRow - minRow + 1) * nodeSize.y + (maxRow - minRow) * rowGap;

        // The arrow canvas expands across the full available panel width
        RectTransform parentRect = transform as RectTransform;
        float width = parentRect != null && parentRect.rect.width > 0f ? parentRect.rect.width : 480f;

        SetCanvasHeight(height);
        chainCanvas.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

        if (line.Connections != null)
        {
            foreach (ProductionConnectionDefinition connection in line.Connections)
            {
                DrawConnection(line, connection, minRow, height);
            }
        }

        if (line.Nodes != null)
        {
            foreach (ProductionNodeDefinition node in line.Nodes)
            {
                if (node != null) CreateNodeButton(node, minRow, height);
            }
        }

        foreach (SelectorView selector in selectors)
        {
            if (selector.Line == line && selector.NewBadge != null) selector.NewBadge.SetActive(false);
        }

        UpdateHeaderText();
        RefreshSelectorVisuals();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        ExpandedLineChanged.Invoke();
    }

    private void EnsureVisualTree()
    {
        if (selectorStrip != null) return;
        ClearChildren(transform);

        layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = gameObject.AddComponent<LayoutElement>();

        // Unified Production container background
        Image panelBg = GetComponent<Image>();
        if (panelBg == null) panelBg = gameObject.AddComponent<Image>();
        panelBg.color = panelBgColor;
        panelBg.raycastTarget = false;

        var sectionLayout = GetComponent<VerticalLayoutGroup>();
        if (sectionLayout == null) sectionLayout = gameObject.AddComponent<VerticalLayoutGroup>();
        sectionLayout.childControlWidth = true;
        sectionLayout.childControlHeight = false;
        sectionLayout.childForceExpandWidth = true;
        sectionLayout.childForceExpandHeight = false;
        sectionLayout.spacing = 2f;
        sectionLayout.padding = new RectOffset(4, 4, 4, 4);

        // 1. Production Contextual Header
        headerStrip = CreateRect("Production Header", transform);
        headerStrip.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, headerHeight);
        var headerLayout = headerStrip.gameObject.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = headerHeight;
        headerLayout.minHeight = headerHeight;

        headerTitle = CreateLabel("Header Label", headerStrip, "Production", 12f);
        headerTitle.fontStyle = FontStyles.Bold;
        headerTitle.color = headerTextColor;
        headerTitle.alignment = TextAlignmentOptions.MidlineLeft;
        SetAnchors(headerTitle.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f));
        headerTitle.margin = new Vector4(10f, 0f, 10f, 0f);

        // Subtle divider line below header
        var headerDivider = CreateRect("Divider", headerStrip);
        SetAnchors(headerDivider, new Vector2(0f, 0f), new Vector2(1f, 0f));
        headerDivider.sizeDelta = new Vector2(0f, 1f);
        var divImg = headerDivider.gameObject.AddComponent<Image>();
        divImg.color = panelBorderColor;
        divImg.raycastTarget = false;

        // 2. DAG Arrow Canvas
        chainCanvas = CreateRect("Production Chain Canvas", transform);
        chainCanvas.pivot = new Vector2(0.5f, 0f);
        var chainLayout = chainCanvas.gameObject.AddComponent<LayoutElement>();
        chainLayout.flexibleWidth = 1f;

        // Anno-style right-facing arrow silhouette
        arrowBackground = chainCanvas.gameObject.AddComponent<ProductionArrowBackground>();
        arrowBackground.TipWidth = 52f;
        arrowBackground.BodyColor = arrowBodyColor;
        arrowBackground.TipColor = arrowTipColor;
        arrowBackground.raycastTarget = false;

        connectorLayer = CreateStretchRect("Connectors", chainCanvas);
        nodeLayer = CreateStretchRect("Nodes", chainCanvas);

        // 3. Production Line Selectors
        selectorStrip = CreateRect("Production Line Selectors", transform);
        selectorStrip.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, selectorHeight);
        var stripLayoutElement = selectorStrip.gameObject.AddComponent<LayoutElement>();
        stripLayoutElement.preferredHeight = selectorHeight;
        stripLayoutElement.minHeight = selectorHeight;
        var stripLayout = selectorStrip.gameObject.AddComponent<HorizontalLayoutGroup>();
        stripLayout.spacing = 6f;
        stripLayout.padding = new RectOffset(6, 6, 2, 2);
        stripLayout.childAlignment = TextAnchor.MiddleLeft;
        stripLayout.childControlWidth = false;
        stripLayout.childControlHeight = false;
        stripLayout.childForceExpandWidth = false;
        stripLayout.childForceExpandHeight = false;

        // 4. Floating Tooltip Overlay
        CreateTooltipBox();

        if (placementAdapter == null)
        {
            placementAdapter = GetComponent<ProductionPlacementAdapter>();
            if (placementAdapter == null) placementAdapter = gameObject.AddComponent<ProductionPlacementAdapter>();
        }
    }

    private void CreateTooltipBox()
    {
        var root = new GameObject("Production Tooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        tooltipBox = (RectTransform)root.transform;
        tooltipBox.SetParent(transform, false);
        tooltipBox.anchorMin = new Vector2(0.5f, 1f);
        tooltipBox.anchorMax = new Vector2(0.5f, 1f);
        tooltipBox.pivot = new Vector2(0.5f, 0f);
        tooltipBox.sizeDelta = new Vector2(180f, 36f);

        Image bg = root.GetComponent<Image>();
        bg.color = new Color(0.04f, 0.08f, 0.12f, 0.96f);
        bg.raycastTarget = false;

        CanvasGroup cg = root.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        tooltipTitle = CreateLabel("Title", tooltipBox, "", 10.5f);
        tooltipTitle.fontStyle = FontStyles.Bold;
        tooltipTitle.color = Color.white;
        SetAnchors(tooltipTitle.rectTransform, new Vector2(0f, 0.45f), new Vector2(1f, 1f));
        tooltipTitle.alignment = TextAlignmentOptions.Center;

        tooltipDesc = CreateLabel("Desc", tooltipBox, "", 9f);
        tooltipDesc.color = new Color(0.65f, 0.78f, 0.85f, 0.9f);
        SetAnchors(tooltipDesc.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f));
        tooltipDesc.alignment = TextAlignmentOptions.Center;

        root.SetActive(false);
    }

    private void HandleTooltipShow(string title, string desc, RectTransform target)
    {
        if (tooltipBox == null) return;
        tooltipTitle.text = title ?? string.Empty;
        tooltipDesc.text = desc ?? string.Empty;

        if (target != null)
        {
            // Position tooltip right above target
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 targetTop = (corners[1] + corners[2]) * 0.5f;

            RectTransform parentRect = transform as RectTransform;
            if (parentRect != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    RectTransformUtility.WorldToScreenPoint(null, targetTop),
                    null,
                    out Vector2 localPoint);
                tooltipBox.anchoredPosition = new Vector2(localPoint.x, localPoint.y + 6f);
            }
        }

        tooltipBox.gameObject.SetActive(true);
        tooltipBox.SetAsLastSibling();
    }

    private void HandleTooltipHide()
    {
        if (tooltipBox != null) tooltipBox.gameObject.SetActive(false);
    }

    private void UpdateHeaderText()
    {
        if (headerTitle == null) return;
        headerTitle.text = expandedLine != null
            ? $"Production: {expandedLine.DisplayName}"
            : "Production";
    }

    private void RebuildSelectors()
    {
        if (selectorStrip == null) return;
        ClearChildren(selectorStrip);
        selectors.Clear();
        previousUnlockStates.Clear();

        if (pageDefinition == null || pageDefinition.ProductionLines == null) return;

        foreach (ProductionLineDefinition line in pageDefinition.ProductionLines)
        {
            if (line == null || !IsVisibleOnThisPage(line)) continue;
            selectors.Add(CreateSelector(line));
            previousUnlockStates[line.Id ?? string.Empty] = IsUnlocked(line.UnlockCondition);
        }
    }

    private SelectorView CreateSelector(ProductionLineDefinition line)
    {
        // Outer root (Border)
        var root = new GameObject($"Line ({line.DisplayName})", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        var rect = (RectTransform)root.transform;
        rect.SetParent(selectorStrip, false);
        rect.sizeDelta = new Vector2(52f, 52f);

        var layout = root.GetComponent<LayoutElement>();
        layout.preferredWidth = 52f;
        layout.preferredHeight = 52f;
        layout.minWidth = 52f;
        layout.minHeight = 52f;

        Image border = root.GetComponent<Image>();
        border.color = selectorNormalBorder;

        // Inner Fill
        var innerObj = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var innerRect = (RectTransform)innerObj.transform;
        innerRect.SetParent(rect, false);
        SetAnchors(innerRect, Vector2.zero, Vector2.one);
        innerRect.offsetMin = new Vector2(2f, 2f);
        innerRect.offsetMax = new Vector2(-2f, -2f);
        Image fill = innerObj.GetComponent<Image>();
        fill.color = selectorNormalBg;
        fill.raycastTarget = false;

        // Button
        Button button = root.GetComponent<Button>();
        button.targetGraphic = border;
        button.onClick.AddListener(() => ToggleLine(line));

        // Output Icon
        Image icon = CreateImage("Output Icon", innerRect);
        icon.sprite = line.OutputIcon;
        icon.preserveAspect = true;
        SetAnchors(icon.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));

        GameObject badge = CreateBadge(rect);

        // Tooltip
        var tooltip = root.AddComponent<ProductionTooltipTrigger>();
        tooltip.Title = line.DisplayName;
        tooltip.Description = IsUnlocked(line.UnlockCondition)
            ? "Production Line"
            : GetLockDescription(line.UnlockCondition);

        return new SelectorView
        {
            Line = line,
            Button = button,
            BorderImage = border,
            FillImage = fill,
            IconImage = icon,
            NewBadge = badge,
            Tooltip = tooltip,
        };
    }

    private void ToggleLine(ProductionLineDefinition line)
    {
        if (line == expandedLine)
        {
            ClearExpandedChain();
            return;
        }

        ExpandLine(line);
    }

    private void CreateNodeButton(ProductionNodeDefinition node, int minRow, float canvasHeight)
    {
        // Outer root (Border)
        var root = new GameObject($"Node ({node.Id})", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rect = (RectTransform)root.transform;
        rect.SetParent(nodeLayer, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = nodeSize;
        rect.anchoredPosition = NodeCenter(node, minRow, canvasHeight);

        bool unlocked = IsUnlocked(node.UnlockCondition);
        Image border = root.GetComponent<Image>();
        border.color = unlocked ? nodeNormalBorder : nodeLockedBorder;

        // Inner Fill
        var innerObj = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var innerRect = (RectTransform)innerObj.transform;
        innerRect.SetParent(rect, false);
        SetAnchors(innerRect, Vector2.zero, Vector2.one);
        innerRect.offsetMin = new Vector2(1.5f, 1.5f);
        innerRect.offsetMax = new Vector2(-1.5f, -1.5f);
        Image fill = innerObj.GetComponent<Image>();
        fill.color = unlocked ? nodeNormalBg : nodeLockedBg;
        fill.raycastTarget = false;

        Button button = root.GetComponent<Button>();
        button.targetGraphic = border;
        button.interactable = unlocked && node.BuildingData != null;
        button.onClick.AddListener(() => placementAdapter.BeginPlacement(node.BuildingData));

        Image icon = CreateImage("Building Icon", innerRect);
        icon.sprite = node.Icon;
        icon.preserveAspect = true;
        icon.color = unlocked ? Color.white : new Color(0.40f, 0.42f, 0.44f, 0.55f);
        SetAnchors(icon.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));

        string labelText = !string.IsNullOrWhiteSpace(node.DisplayName)
            ? node.DisplayName
            : node.BuildingData != null && !string.IsNullOrWhiteSpace(node.BuildingData.buildingName)
                ? node.BuildingData.buildingName
                : node.Id;

        // Tooltip instead of permanent text label
        var tooltip = root.AddComponent<ProductionTooltipTrigger>();
        tooltip.Title = labelText;
        tooltip.Description = unlocked ? "Click to place building" : "Building locked";

        // Drag and drop into Context Menu & Action Bar
        var dragHandler = root.AddComponent<BuildingSlotDragHandler>();
        GameObject resolvedPrefab = null;
        if (node.BuildingData != null && BuildingPrefabRegistry.Instance != null)
        {
            resolvedPrefab = BuildingPrefabRegistry.Instance.GetPrefab(node.BuildingData.Id);
        }
        dragHandler.SetPayload(resolvedPrefab, node.Icon, labelText, node.BuildingData);
    }

    private void DrawConnection(ProductionLineDefinition line, ProductionConnectionDefinition connection, int minRow, float canvasHeight)
    {
        if (connection == null) return;
        ProductionNodeDefinition fromNode = line.FindNode(connection.FromNodeId);
        ProductionNodeDefinition toNode = line.FindNode(connection.ToNodeId);
        if (fromNode == null || toNode == null) return;

        Vector2 from = NodeCenter(fromNode, minRow, canvasHeight) + Vector2.right * (nodeSize.x * 0.5f);
        Vector2 to = NodeCenter(toNode, minRow, canvasHeight) - Vector2.right * (nodeSize.x * 0.5f);
        ProductionConnectorView connector = ProductionConnectorView.Create(connectorLayer, connectorColor);
        connector.Draw(from, to, connection.Type, connection.JunctionPosition, connectorThickness);
    }

    private Vector2 NodeCenter(ProductionNodeDefinition node, int minRow, float canvasHeight)
    {
        float x = canvasPadding + nodeSize.x * 0.5f + node.Column * (nodeSize.x + columnGap);
        float yFromTop = canvasPadding + nodeSize.y * 0.5f + (node.Row - minRow) * (nodeSize.y + rowGap);
        return new Vector2(x, canvasHeight - yFromTop);
    }

    private void OnPopulationChanged()
    {
        RefreshUnlockStates(true);
        if (expandedLine != null)
        {
            ProductionLineDefinition line = expandedLine;
            if (IsUnlocked(line.UnlockCondition)) ExpandLine(line);
            else ClearExpandedChain();
        }
    }

    private void RefreshUnlockStates(bool showNewUnlocks)
    {
        foreach (SelectorView selector in selectors)
        {
            bool unlocked = IsUnlocked(selector.Line.UnlockCondition);
            string id = selector.Line.Id ?? string.Empty;
            bool wasUnlocked = previousUnlockStates.TryGetValue(id, out bool previous) && previous;

            selector.Button.interactable = unlocked;
            if (showNewUnlocks && unlocked && !wasUnlocked && selector.NewBadge != null)
            {
                selector.NewBadge.SetActive(true);
            }

            if (selector.Tooltip != null)
            {
                selector.Tooltip.Description = unlocked
                    ? "Production Line"
                    : GetLockDescription(selector.Line.UnlockCondition);
            }

            previousUnlockStates[id] = unlocked;
        }

        RefreshSelectorVisuals();
    }

    private void RefreshSelectorVisuals()
    {
        foreach (SelectorView selector in selectors)
        {
            bool unlocked = IsUnlocked(selector.Line.UnlockCondition);
            bool isSelected = selector.Line == expandedLine;

            if (!unlocked)
            {
                selector.BorderImage.color = selectorLockedBorder;
                selector.FillImage.color = selectorLockedBg;
                if (selector.IconImage != null)
                {
                    selector.IconImage.color = new Color(0.40f, 0.42f, 0.44f, 0.55f);
                    selector.IconImage.enabled = selector.IconImage.sprite != null;
                }
            }
            else if (isSelected)
            {
                selector.BorderImage.color = selectorSelectedBorder;
                selector.FillImage.color = selectorSelectedBg;
                if (selector.IconImage != null)
                {
                    selector.IconImage.color = Color.white;
                    selector.IconImage.enabled = selector.IconImage.sprite != null;
                }
            }
            else
            {
                selector.BorderImage.color = selectorNormalBorder;
                selector.FillImage.color = selectorNormalBg;
                if (selector.IconImage != null)
                {
                    selector.IconImage.color = Color.white;
                    selector.IconImage.enabled = selector.IconImage.sprite != null;
                }
            }
        }
    }

    private bool IsUnlocked(PopulationUnlock condition)
    {
        if (condition.IsUngated) return true;
        if (population == null)
        {
            population = ResolvePopulationFallback();
            if (population != null) BindPopulation(population);
        }
        return population != null && population.IsUnlocked(condition);
    }

    private IslandPopulation ResolvePopulationFallback()
    {
        IslandManager manager = IslandManager.instance;
        if (manager != null)
        {
            Island island = manager.GetHoveredIsland();
            if (island != null)
            {
                IslandPopulation pop = island.GetComponent<IslandPopulation>();
                if (pop != null) return pop;
            }
        }
        return FindObjectOfType<IslandPopulation>();
    }

    private bool IsVisibleOnThisPage(ProductionLineDefinition line)
    {
        if (populationClassFilter == PopulationClass.None) return true;
        return line.Tier == populationClassFilter;
    }

    private void SetCanvasHeight(float height)
    {
        currentCanvasHeight = Mathf.Max(0f, height);

        if (chainCanvas != null)
        {
            chainCanvas.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, currentCanvasHeight);
            LayoutElement chainElement = chainCanvas.GetComponent<LayoutElement>();
            if (chainElement != null)
            {
                chainElement.preferredHeight = currentCanvasHeight;
                chainElement.minHeight = currentCanvasHeight;
            }
        }

        if (layoutElement != null)
        {
            layoutElement.preferredHeight = PreferredHeight;
            layoutElement.minHeight = PreferredHeight;
        }
    }

    private static string GetLockDescription(PopulationUnlock condition)
    {
        if (condition.IsUngated) return "Available";
        string className = condition.populationClass != PopulationClass.None
            ? PopulationClasses.DisplayName(condition.populationClass)
            : "Residents";
        return $"Requires {condition.requiredPopulation} {className}";
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var root = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private static RectTransform CreateStretchRect(string name, Transform parent)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private static Image CreateImage(string name, Transform parent)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateLabel(string name, Transform parent, string text, float fontSize)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);

        TextMeshProUGUI label = root.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.raycastTarget = false;
        return label;
    }

    private static GameObject CreateBadge(Transform parent)
    {
        var root = new GameObject("New Unlock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(2f, 2f);
        rect.sizeDelta = new Vector2(28f, 14f);

        Image image = root.GetComponent<Image>();
        image.color = new Color(0.18f, 0.65f, 0.38f, 0.95f);
        image.raycastTarget = false;

        TMP_Text label = CreateLabel("Label", rect, "NEW", 9f);
        SetAnchors(label.rectTransform, Vector2.zero, Vector2.one);
        label.alignment = TextAlignmentOptions.Center;
        root.SetActive(false);
        return root;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            child.SetParent(null, false);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }
}
