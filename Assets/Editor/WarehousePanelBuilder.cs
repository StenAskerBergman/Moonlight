using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates the warehouse / Port Authority panel prefab: hierarchy, tier strip,
/// GOODS/ITEMS/TRADE tabs, slot templates, layout groups, scrolling and controller
/// wiring. Run it once from the menu; restyle the result by editing the generated
/// WarehousePanelStyle asset (or the prefab directly) rather than re-running, since a
/// re-run overwrites the prefab.
///
/// Menu: Tools > Moonlight > Build Warehouse Panel Prefab
/// </summary>
public static class WarehousePanelBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/UI";
    private const string PrefabPath = PrefabFolder + "/Warehouse Panel.prefab";
    private const string StyleFolder = "Assets/Scripts/Item System/Userface/Warehouse";
    private const string StylePath = StyleFolder + "/Warehouse Panel Style.asset";

    [MenuItem("Tools/Moonlight/Build Warehouse Panel Prefab")]
    public static void Build()
    {
        WarehousePanelStyle style = LoadOrCreateStyle();

        GameObject root = new GameObject("Warehouse Panel", typeof(RectTransform), typeof(WarehousePanelUI));
        try
        {
            GameObject panel = BuildPanel(root, style, out GameObject header, out RectTransform content,
                out RectTransform tierStrip, out RectTransform mainTabs);

            WarehouseSlotView slotTemplate = BuildSlotTemplate(style);
            WarehouseTierTabButton tierTemplate = BuildTierTabTemplate(style);
            Button mainTabTemplate = BuildMainTabTemplate(style);

            slotTemplate.transform.SetParent(root.transform, false);
            tierTemplate.transform.SetParent(root.transform, false);
            mainTabTemplate.transform.SetParent(root.transform, false);
            slotTemplate.gameObject.SetActive(false);
            tierTemplate.gameObject.SetActive(false);
            mainTabTemplate.gameObject.SetActive(false);

            WarehouseGoodsTab goods = BuildGoodsTab(content, style, slotTemplate);
            WarehouseItemsTab items = BuildItemsTab(content, style, slotTemplate);
            WarehouseTradeTab trade = BuildTradeTab(content, style, slotTemplate);

            WireRoot(root, style, panel, header, tierStrip, tierTemplate, mainTabs, mainTabTemplate,
                goods, items, trade);

            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Built warehouse panel prefab at {PrefabPath}. Drop it under a Canvas and it wires itself to BuildingSelections on Awake.");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static WarehousePanelStyle LoadOrCreateStyle()
    {
        WarehousePanelStyle style = AssetDatabase.LoadAssetAtPath<WarehousePanelStyle>(StylePath);
        if (style != null) return style;

        style = ScriptableObject.CreateInstance<WarehousePanelStyle>();
        Directory.CreateDirectory(StyleFolder);
        AssetDatabase.CreateAsset(style, StylePath);
        AssetDatabase.SaveAssets();

        Debug.Log($"Created {StylePath}. Edit it to restyle the panel; the prefab reads every colour and metric from it.");
        return style;
    }

    #region Structure

    private static GameObject BuildPanel(
        GameObject root,
        WarehousePanelStyle style,
        out GameObject header,
        out RectTransform content,
        out RectTransform tierStrip,
        out RectTransform mainTabs)
    {
        RectTransform rootRect = root.GetComponent<RectTransform>();
        AnchorBottomRight(rootRect, style.panelSize);

        GameObject panel = CreateChild(root.transform, "Panel", out RectTransform panelRect);
        Stretch(panelRect);
        AddImage(panel, style.panelBackground);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(
            (int)style.padding, (int)style.padding, (int)style.padding, (int)style.padding);
        layout.spacing = style.spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        header = BuildHeader(panelRect, style);

        // Content flexes; the two tab rows keep their natural height.
        GameObject contentGO = CreateChild(panelRect, "Content", out content);
        LayoutElement contentLayout = contentGO.AddComponent<LayoutElement>();
        contentLayout.flexibleHeight = 1f;
        contentLayout.minHeight = 160f;

        GameObject tierGO = CreateChild(panelRect, "Tier Strip", out tierStrip);
        HorizontalLayoutGroup tierLayout = tierGO.AddComponent<HorizontalLayoutGroup>();
        tierLayout.spacing = 4f;
        tierLayout.childControlWidth = true;
        tierLayout.childControlHeight = true;
        tierLayout.childForceExpandWidth = true;
        AddFixedHeight(tierGO, 40f);

        GameObject mainTabsGO = CreateChild(panelRect, "Main Tabs", out mainTabs);
        HorizontalLayoutGroup mainLayout = mainTabsGO.AddComponent<HorizontalLayoutGroup>();
        mainLayout.spacing = 2f;
        mainLayout.childControlWidth = true;
        mainLayout.childControlHeight = true;
        mainLayout.childForceExpandWidth = true;
        AddFixedHeight(mainTabsGO, 26f);

        return panel;
    }

    private static GameObject BuildHeader(RectTransform parent, WarehousePanelStyle style)
    {
        GameObject header = CreateChild(parent, "Header", out RectTransform headerRect);
        AddImage(header, style.headerBackground);
        AddFixedHeight(header, 42f);

        HorizontalLayoutGroup layout = header.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 4, 4);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;

        GameObject icon = CreateChild(headerRect, "Building Icon", out RectTransform iconRect);
        AddImage(icon, style.slotBackground);
        LayoutElement iconLayout = icon.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 34f;
        iconLayout.preferredHeight = 34f;
        iconLayout.flexibleWidth = 0f;

        GameObject titles = CreateChild(headerRect, "Titles", out RectTransform titlesRect);
        VerticalLayoutGroup titleLayout = titles.AddComponent<VerticalLayoutGroup>();
        titleLayout.childControlWidth = true;
        titleLayout.childControlHeight = true;
        titleLayout.spacing = -2f;

        CreateLabel(titlesRect, "Category Label", "TRADE BUILDING",
            style.subtleText, Mathf.Max(9, style.bodyFontSize - 3), FontStyles.Normal);
        CreateLabel(titlesRect, "Name Label", "Port authority",
            style.headerText, style.headerFontSize, FontStyles.Bold);

        return header;
    }

    #endregion

    #region Templates

    private static WarehouseSlotView BuildSlotTemplate(WarehousePanelStyle style)
    {
        GameObject slot = new GameObject("Slot Template", typeof(RectTransform));
        RectTransform slotRect = (RectTransform)slot.transform;
        slotRect.sizeDelta = new Vector2(style.slotSize, style.slotSize);

        Image background = AddImage(slot, style.slotBackground);

        // Icon fills the slot minus a margin, leaving the right-hand strip for the bar.
        GameObject iconGO = CreateChild(slotRect, "Icon", out RectTransform iconRect);
        iconRect.anchorMin = new Vector2(0f, 0f);
        iconRect.anchorMax = new Vector2(1f, 1f);
        iconRect.offsetMin = new Vector2(4f, 4f);
        iconRect.offsetMax = new Vector2(-12f, -4f);
        Image icon = AddImage(iconGO, style.slotIconTint);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        // Vertical stock bar pinned to the right edge, matching the reference layout.
        GameObject barGO = CreateChild(slotRect, "Stock Bar", out RectTransform barRect);
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.sizeDelta = new Vector2(6f, -8f);
        barRect.anchoredPosition = new Vector2(-3f, 0f);
        AddImage(barGO, style.stockBarBackground).raycastTarget = false;

        GameObject fillGO = CreateChild(barRect, "Fill", out RectTransform fillRect);
        Stretch(fillRect);
        Image fill = AddImage(fillGO, style.stockBarNormal);
        fill.raycastTarget = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        fill.fillAmount = 1f;

        GameObject amountGO = CreateChild(slotRect, "Amount", out RectTransform amountRect);
        amountRect.anchorMin = new Vector2(0f, 0f);
        amountRect.anchorMax = new Vector2(1f, 0f);
        amountRect.pivot = new Vector2(0.5f, 0f);
        amountRect.sizeDelta = new Vector2(-14f, 18f);
        amountRect.anchoredPosition = new Vector2(-6f, 2f);
        TMP_Text amount = AddText(amountGO, "0", style.amountText, style.amountFontSize, FontStyles.Bold);
        amount.alignment = TextAlignmentOptions.BottomLeft;

        GameObject lockedGO = CreateChild(slotRect, "Locked Overlay", out RectTransform lockedRect);
        Stretch(lockedRect);
        AddImage(lockedGO, new Color(0f, 0f, 0f, 0.55f)).raycastTarget = false;

        GameObject lockedLabelGO = CreateChild(lockedRect, "Locked Label", out RectTransform lockedLabelRect);
        Stretch(lockedLabelRect);
        TMP_Text lockedLabel = AddText(lockedLabelGO, string.Empty, style.subtleText,
            Mathf.Max(9, style.bodyFontSize - 2), FontStyles.Normal);
        lockedLabel.alignment = TextAlignmentOptions.Center;

        lockedGO.SetActive(false);

        WarehouseSlotView view = slot.AddComponent<WarehouseSlotView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("background").objectReferenceValue = background;
        so.FindProperty("icon").objectReferenceValue = icon;
        so.FindProperty("amountLabel").objectReferenceValue = amount;
        so.FindProperty("stockBarFill").objectReferenceValue = fill;
        so.FindProperty("lockedOverlay").objectReferenceValue = lockedGO;
        so.FindProperty("lockedLabel").objectReferenceValue = lockedLabel;
        so.ApplyModifiedPropertiesWithoutUndo();

        return view;
    }

    private static WarehouseTierTabButton BuildTierTabTemplate(WarehousePanelStyle style)
    {
        GameObject tab = new GameObject("Tier Tab Template", typeof(RectTransform));
        RectTransform tabRect = (RectTransform)tab.transform;
        tabRect.sizeDelta = new Vector2(52f, 36f);

        Image background = AddImage(tab, style.tabInactive);
        Button button = tab.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;

        GameObject labelGO = CreateChild(tabRect, "Label", out RectTransform labelRect);
        Stretch(labelRect);
        TMP_Text label = AddText(labelGO, "1", style.tabInactiveText, style.headerFontSize, FontStyles.Bold);
        label.alignment = TextAlignmentOptions.Center;

        GameObject glyphGO = CreateChild(tabRect, "Glyph", out RectTransform glyphRect);
        glyphRect.anchorMin = new Vector2(0.5f, 0.5f);
        glyphRect.anchorMax = new Vector2(0.5f, 0.5f);
        glyphRect.sizeDelta = new Vector2(22f, 22f);
        Image glyph = AddImage(glyphGO, style.tabInactiveText);
        glyph.preserveAspect = true;
        glyph.raycastTarget = false;
        glyphGO.SetActive(false);

        WarehouseTierTabButton view = tab.AddComponent<WarehouseTierTabButton>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("background").objectReferenceValue = background;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("glyph").objectReferenceValue = glyph;
        so.ApplyModifiedPropertiesWithoutUndo();

        return view;
    }

    private static Button BuildMainTabTemplate(WarehousePanelStyle style)
    {
        GameObject tab = new GameObject("Main Tab Template", typeof(RectTransform));
        RectTransform tabRect = (RectTransform)tab.transform;
        tabRect.sizeDelta = new Vector2(110f, 24f);

        Image background = AddImage(tab, style.tabInactive);
        Button button = tab.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;

        GameObject labelGO = CreateChild(tabRect, "Label", out RectTransform labelRect);
        Stretch(labelRect);
        TMP_Text label = AddText(labelGO, "GOODS", style.tabInactiveText, style.bodyFontSize, FontStyles.Bold);
        label.alignment = TextAlignmentOptions.Center;

        return button;
    }

    #endregion

    #region Tabs

    private static WarehouseGoodsTab BuildGoodsTab(RectTransform content, WarehousePanelStyle style, WarehouseSlotView slotTemplate)
    {
        GameObject tab = CreateChild(content, "Goods Tab", out RectTransform tabRect);
        Stretch(tabRect);

        RectTransform grid = BuildScrollableGrid(tabRect, style, "Goods Grid");

        WarehouseGoodsTab view = tab.AddComponent<WarehouseGoodsTab>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("style").objectReferenceValue = style;
        so.FindProperty("contentParent").objectReferenceValue = grid;
        so.FindProperty("slotTemplate").objectReferenceValue = slotTemplate;
        so.ApplyModifiedPropertiesWithoutUndo();

        return view;
    }

    private static WarehouseItemsTab BuildItemsTab(RectTransform content, WarehousePanelStyle style, WarehouseSlotView slotTemplate)
    {
        GameObject tab = CreateChild(content, "Items Tab", out RectTransform tabRect);
        Stretch(tabRect);

        VerticalLayoutGroup layout = tab.AddComponent<VerticalLayoutGroup>();
        layout.spacing = style.spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        CreateLabel(tabRect, "Sockets Header", "WAREHOUSE SOCKETS",
            style.subtleText, Mathf.Max(9, style.bodyFontSize - 2), FontStyles.Bold);

        GameObject socketRow = CreateChild(tabRect, "Sockets", out RectTransform socketRect);
        HorizontalLayoutGroup socketLayout = socketRow.AddComponent<HorizontalLayoutGroup>();
        socketLayout.spacing = style.spacing;
        socketLayout.childControlWidth = false;
        socketLayout.childControlHeight = false;
        socketLayout.childAlignment = TextAnchor.MiddleLeft;
        AddFixedHeight(socketRow, style.slotSize + 4f);

        CreateLabel(tabRect, "Pool Header", "ON THIS ISLAND",
            style.subtleText, Mathf.Max(9, style.bodyFontSize - 2), FontStyles.Bold);

        GameObject poolHost = CreateChild(tabRect, "Pool", out RectTransform poolRect);
        LayoutElement poolLayout = poolHost.AddComponent<LayoutElement>();
        poolLayout.flexibleHeight = 1f;
        RectTransform pool = BuildScrollableGrid(poolRect, style, "Item Grid");

        WarehouseSlotView socketTemplate = Object.Instantiate(slotTemplate);
        socketTemplate.name = "Socket Template";
        socketTemplate.transform.SetParent(tab.transform, false);
        socketTemplate.gameObject.SetActive(false);

        WarehouseItemsTab view = tab.AddComponent<WarehouseItemsTab>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("style").objectReferenceValue = style;
        so.FindProperty("contentParent").objectReferenceValue = pool;
        so.FindProperty("slotTemplate").objectReferenceValue = slotTemplate;
        so.FindProperty("socketParent").objectReferenceValue = socketRect;
        so.FindProperty("socketTemplate").objectReferenceValue = socketTemplate;
        so.ApplyModifiedPropertiesWithoutUndo();

        return view;
    }

    private static WarehouseTradeTab BuildTradeTab(RectTransform content, WarehousePanelStyle style, WarehouseSlotView slotTemplate)
    {
        GameObject tab = CreateChild(content, "Trade Tab", out RectTransform tabRect);
        Stretch(tabRect);

        VerticalLayoutGroup layout = tab.AddComponent<VerticalLayoutGroup>();
        layout.spacing = style.spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        GameObject slotHeader = CreateChild(tabRect, "Slot Header", out RectTransform slotHeaderRect);
        HorizontalLayoutGroup slotHeaderLayout = slotHeader.AddComponent<HorizontalLayoutGroup>();
        slotHeaderLayout.childControlWidth = true;
        slotHeaderLayout.childControlHeight = true;
        AddFixedHeight(slotHeader, 18f);

        CreateLabel(slotHeaderRect, "Label", "TRADE SLOTS",
            style.subtleText, Mathf.Max(9, style.bodyFontSize - 2), FontStyles.Bold);
        TMP_Text slotCount = CreateLabel(slotHeaderRect, "Slot Count", "0/2",
            style.headerText, style.bodyFontSize, FontStyles.Bold);
        slotCount.alignment = TextAlignmentOptions.Right;

        GameObject listHost = CreateChild(tabRect, "Rules", out RectTransform listRect);
        LayoutElement listLayout = listHost.AddComponent<LayoutElement>();
        listLayout.flexibleHeight = 1f;
        RectTransform rules = BuildScrollableGrid(listRect, style, "Rule Grid");

        // Rule editor sits under the list and is hidden until a rule row is clicked.
        GameObject editor = CreateChild(tabRect, "Rule Editor", out RectTransform editorRect);
        AddImage(editor, style.slotBackground);
        VerticalLayoutGroup editorLayout = editor.AddComponent<VerticalLayoutGroup>();
        editorLayout.padding = new RectOffset(6, 6, 6, 6);
        editorLayout.spacing = 4f;
        editorLayout.childControlWidth = true;
        editorLayout.childControlHeight = true;
        editorLayout.childForceExpandHeight = false;

        TMP_Text itemName = CreateLabel(editorRect, "Item Name", "—",
            style.headerText, style.bodyFontSize, FontStyles.Bold);

        TMP_Dropdown dropdown = BuildDropdown(editorRect, style);
        Slider slider = BuildSlider(editorRect, style);
        TMP_Text stockLabel = CreateLabel(editorRect, "Stock Label", "No active trade rule",
            style.subtleText, style.bodyFontSize, FontStyles.Normal);

        editor.SetActive(false);

        WarehouseTradeTab view = tab.AddComponent<WarehouseTradeTab>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("style").objectReferenceValue = style;
        so.FindProperty("contentParent").objectReferenceValue = rules;
        so.FindProperty("slotTemplate").objectReferenceValue = slotTemplate;
        so.FindProperty("ruleEditorRoot").objectReferenceValue = editor;
        so.FindProperty("ruleItemNameLabel").objectReferenceValue = itemName;
        so.FindProperty("ruleModeDropdown").objectReferenceValue = dropdown;
        so.FindProperty("ruleStockSlider").objectReferenceValue = slider;
        so.FindProperty("ruleStockLabel").objectReferenceValue = stockLabel;
        so.FindProperty("slotCountLabel").objectReferenceValue = slotCount;
        so.ApplyModifiedPropertiesWithoutUndo();

        return view;
    }

    private static void WireRoot(
        GameObject root,
        WarehousePanelStyle style,
        GameObject panel,
        GameObject header,
        RectTransform tierStrip,
        WarehouseTierTabButton tierTemplate,
        RectTransform mainTabs,
        Button mainTabTemplate,
        WarehouseGoodsTab goods,
        WarehouseItemsTab items,
        WarehouseTradeTab trade)
    {
        Transform titles = header.transform.Find("Titles");

        WarehousePanelUI ui = root.GetComponent<WarehousePanelUI>();
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("style").objectReferenceValue = style;
        so.FindProperty("panelRoot").objectReferenceValue = panel;
        so.FindProperty("buildingCategoryLabel").objectReferenceValue =
            titles.Find("Category Label").GetComponent<TMP_Text>();
        so.FindProperty("buildingNameLabel").objectReferenceValue =
            titles.Find("Name Label").GetComponent<TMP_Text>();
        so.FindProperty("tierStripParent").objectReferenceValue = tierStrip;
        so.FindProperty("tierTabTemplate").objectReferenceValue = tierTemplate;
        so.FindProperty("mainTabParent").objectReferenceValue = mainTabs;
        so.FindProperty("mainTabTemplate").objectReferenceValue = mainTabTemplate;

        SerializedProperty tabs = so.FindProperty("tabs");
        tabs.arraySize = 3;
        tabs.GetArrayElementAtIndex(0).objectReferenceValue = goods;
        tabs.GetArrayElementAtIndex(1).objectReferenceValue = items;
        tabs.GetArrayElementAtIndex(2).objectReferenceValue = trade;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion

    #region UGUI helpers

    private static RectTransform BuildScrollableGrid(RectTransform parent, WarehousePanelStyle style, string name)
    {
        GameObject viewport = CreateChild(parent, name + " Viewport", out RectTransform viewportRect);
        Stretch(viewportRect);
        viewport.AddComponent<RectMask2D>();

        ScrollRect scroll = viewport.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        scroll.viewport = viewportRect;

        GameObject contentGO = CreateChild(viewportRect, name, out RectTransform contentRect);
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;
        scroll.content = contentRect;

        GridLayoutGroup grid = contentGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(style.slotSize, style.slotSize);
        grid.spacing = new Vector2(style.spacing, style.spacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, style.goodsColumns);
        grid.childAlignment = TextAnchor.UpperLeft;

        // Grid rows are added at runtime, so the content height has to follow them.
        ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return contentRect;
    }

    private static TMP_Dropdown BuildDropdown(RectTransform parent, WarehousePanelStyle style)
    {
        GameObject go = CreateChild(parent, "Mode Dropdown", out RectTransform rect);
        AddFixedHeight(go, 24f);
        Image background = AddImage(go, style.slotBackgroundLocked);

        TMP_Dropdown dropdown = go.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = background;
        dropdown.transition = Selectable.Transition.None;

        GameObject labelGO = CreateChild(rect, "Label", out RectTransform labelRect);
        Stretch(labelRect);
        labelRect.offsetMin = new Vector2(8f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);
        TMP_Text label = AddText(labelGO, "No trade", style.headerText, style.bodyFontSize, FontStyles.Normal);
        label.alignment = TextAlignmentOptions.Left;
        dropdown.captionText = label;

        // Dropdown needs a template to expand; build the minimum viable one.
        GameObject templateGO = CreateChild(rect, "Template", out RectTransform templateRect);
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.sizeDelta = new Vector2(0f, 72f);
        AddImage(templateGO, style.panelBackground);
        templateGO.AddComponent<RectMask2D>();

        ScrollRect templateScroll = templateGO.AddComponent<ScrollRect>();
        templateScroll.horizontal = false;
        templateScroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewportGO = CreateChild(templateRect, "Viewport", out RectTransform viewportRect);
        Stretch(viewportRect);
        viewportGO.AddComponent<RectMask2D>();
        templateScroll.viewport = viewportRect;

        GameObject contentGO = CreateChild(viewportRect, "Content", out RectTransform contentRect);
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 24f);
        templateScroll.content = contentRect;

        GameObject itemGO = CreateChild(contentRect, "Item", out RectTransform itemRect);
        itemRect.anchorMin = new Vector2(0f, 0.5f);
        itemRect.anchorMax = new Vector2(1f, 0.5f);
        itemRect.sizeDelta = new Vector2(0f, 24f);

        Toggle itemToggle = itemGO.AddComponent<Toggle>();
        itemToggle.transition = Selectable.Transition.None;

        GameObject itemBackgroundGO = CreateChild(itemRect, "Item Background", out RectTransform itemBackgroundRect);
        Stretch(itemBackgroundRect);
        Image itemBackground = AddImage(itemBackgroundGO, style.tabActive);
        itemToggle.targetGraphic = itemBackground;
        itemToggle.graphic = itemBackground;

        GameObject itemLabelGO = CreateChild(itemRect, "Item Label", out RectTransform itemLabelRect);
        Stretch(itemLabelRect);
        itemLabelRect.offsetMin = new Vector2(8f, 0f);
        TMP_Text itemLabel = AddText(itemLabelGO, "Option", style.headerText, style.bodyFontSize, FontStyles.Normal);
        itemLabel.alignment = TextAlignmentOptions.Left;

        dropdown.template = templateRect;
        dropdown.itemText = itemLabel;
        templateGO.SetActive(false);

        return dropdown;
    }

    private static Slider BuildSlider(RectTransform parent, WarehousePanelStyle style)
    {
        GameObject go = CreateChild(parent, "Stock Slider", out RectTransform rect);
        AddFixedHeight(go, 18f);

        Slider slider = go.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;

        GameObject backgroundGO = CreateChild(rect, "Background", out RectTransform backgroundRect);
        Stretch(backgroundRect);
        backgroundRect.offsetMin = new Vector2(0f, 6f);
        backgroundRect.offsetMax = new Vector2(0f, -6f);
        AddImage(backgroundGO, style.stockBarBackground);

        GameObject fillAreaGO = CreateChild(rect, "Fill Area", out RectTransform fillAreaRect);
        Stretch(fillAreaRect);
        fillAreaRect.offsetMin = new Vector2(0f, 6f);
        fillAreaRect.offsetMax = new Vector2(0f, -6f);

        GameObject fillGO = CreateChild(fillAreaRect, "Fill", out RectTransform fillRect);
        Stretch(fillRect);
        Image fill = AddImage(fillGO, style.stockBarNormal);

        GameObject handleAreaGO = CreateChild(rect, "Handle Slide Area", out RectTransform handleAreaRect);
        Stretch(handleAreaRect);

        GameObject handleGO = CreateChild(handleAreaRect, "Handle", out RectTransform handleRect);
        handleRect.sizeDelta = new Vector2(12f, 0f);
        Image handle = AddImage(handleGO, style.headerText);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;

        return slider;
    }

    private static GameObject CreateChild(Transform parent, string name, out RectTransform rect)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return go;
    }

    private static TMP_Text CreateLabel(
        Transform parent, string name, string text, Color color, int fontSize, FontStyles fontStyle)
    {
        GameObject go = CreateChild(parent, name, out RectTransform rect);
        AddFixedHeight(go, fontSize + 6f);
        return AddText(go, text, color, fontSize, fontStyle);
    }

    private static TMP_Text AddText(GameObject go, string text, Color color, int fontSize, FontStyles fontStyle)
    {
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.color = color;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = TextAlignmentOptions.Left;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private static Image AddImage(GameObject go, Color color)
    {
        Image image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static void AddFixedHeight(GameObject go, float height)
    {
        LayoutElement element = go.GetComponent<LayoutElement>();
        if (element == null) element = go.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
        element.flexibleHeight = 0f;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void AnchorBottomRight(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(-12f, 12f);
    }

    #endregion
}
