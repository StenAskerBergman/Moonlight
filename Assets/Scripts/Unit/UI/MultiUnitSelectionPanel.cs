using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime multi-selection navigator. Inactive units collapse to their card
/// number; the focused unit expands and supplies the target for unit HUD/actions.
/// </summary>
public sealed class MultiUnitSelectionPanel : MonoBehaviour
{
    private readonly List<GameObject> cards = new List<GameObject>();
    private UnitSelections selections;
    private GameObject panelRoot;
    private GameObject buildingTargetRoot;
    private RectTransform cardContent;
    private Text targetLabel;

    private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.095f, 0.94f);
    private static readonly Color CardColor = new Color(0.12f, 0.16f, 0.20f, 1f);
    private static readonly Color FocusColor = new Color(0.08f, 0.43f, 0.62f, 1f);

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
        Refresh(selections.unitsSelected);
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
        Refresh(selections.unitsSelected);
    }

    private void BuildPanel()
    {
        Canvas canvas = null;
        if (selections != null && selections.inventoryUIPanel != null)
        {
            canvas = selections.inventoryUIPanel.GetComponentInParent<Canvas>();
        }
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("MultiUnitSelectionPanel: No Canvas was found; multi-unit cards cannot be displayed.");
            return;
        }

        buildingTargetRoot = CreateUIObject("Building-to-Unit-Screen", canvas.transform, typeof(Image));
        buildingTargetRoot.GetComponent<Image>().color = PanelColor;
        RectTransform buildingPanel = buildingTargetRoot.GetComponent<RectTransform>();
        buildingPanel.anchorMin = new Vector2(0.5f, 1f);
        buildingPanel.anchorMax = new Vector2(0.5f, 1f);
        buildingPanel.pivot = new Vector2(0.5f, 1f);
        buildingPanel.anchoredPosition = new Vector2(0f, -12f);
        buildingPanel.sizeDelta = new Vector2(620f, 58f);

        Button previous = CreateButton("Previous Unit", buildingPanel, "<", new Vector2(12f, -10f), new Vector2(42f, 38f));
        previous.onClick.AddListener(() => selections.FocusSelectedUnitOffset(-1));
        Button next = CreateButton("Next Unit", buildingPanel, ">", new Vector2(566f, -10f), new Vector2(42f, 38f));
        next.onClick.AddListener(() => selections.FocusSelectedUnitOffset(1));

        GameObject target = CreateUIObject("Target Label", buildingPanel, typeof(Text));
        SetTopLeft(target.GetComponent<RectTransform>(), new Vector2(62f, -10f), new Vector2(496f, 38f));
        targetLabel = target.GetComponent<Text>();
        ConfigureText(targetLabel, 15, TextAnchor.MiddleCenter);
        buildingTargetRoot.SetActive(false);

        panelRoot = CreateUIObject("Drag-to-Unit-Screen", canvas.transform, typeof(Image));
        Image panelImage = panelRoot.GetComponent<Image>();
        panelImage.color = PanelColor;

        RectTransform panel = panelRoot.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 1f);
        panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -12f);
        panel.sizeDelta = new Vector2(620f, 128f);

        GameObject title = CreateUIObject("Selected Units Label", panel.transform, typeof(Text));
        RectTransform titleRect = title.GetComponent<RectTransform>();
        SetTopLeft(titleRect, new Vector2(12f, -10f), new Vector2(596f, 38f));
        Text selectedUnitsLabel = title.GetComponent<Text>();
        ConfigureText(selectedUnitsLabel, 15, TextAnchor.MiddleCenter);
        selectedUnitsLabel.text = "SELECTED UNITS";

        GameObject viewport = CreateUIObject("Card Viewport", panel.transform, typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        SetTopLeft(viewportRect, new Vector2(12f, -56f), new Vector2(596f, 60f));
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);

        GameObject content = CreateUIObject("Cards", viewport.transform, typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        cardContent = content.GetComponent<RectTransform>();
        cardContent.anchorMin = new Vector2(0f, 0f);
        cardContent.anchorMax = new Vector2(0f, 1f);
        cardContent.pivot = new Vector2(0f, 0.5f);
        cardContent.anchoredPosition = Vector2.zero;
        cardContent.sizeDelta = new Vector2(0f, 0f);

        HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = cardContent;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        panelRoot.SetActive(false);
    }

    private void Refresh(List<Unit> selectedUnits)
    {
        if (panelRoot == null) return;

        bool hasUnits = selectedUnits != null && selectedUnits.Count > 0;
        bool buildingMode = hasUnits && BuildingSelections.Instance != null && BuildingSelections.Instance.SelectedBuilding != null;
        buildingTargetRoot.SetActive(buildingMode);
        if (buildingMode)
        {
            int targetIndex = selections.FocusedUnitIndex;
            Unit targetUnit = selections.FocusedUnit;
            string targetName = targetUnit != null && !string.IsNullOrWhiteSpace(targetUnit.displayName)
                ? targetUnit.displayName
                : targetUnit != null ? targetUnit.name : "None";
            targetLabel.text = $"BUILDING TRADE TARGET  {targetIndex + 1} / {selectedUnits.Count}   {targetName}";
        }

        bool showDragScreen = !buildingMode && hasUnits && selectedUnits.Count > 1 && selections.LastSelectionWasDrag;
        panelRoot.SetActive(showDragScreen);
        if (!showDragScreen) return;

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null)
            {
                cards[i].SetActive(false);
                Destroy(cards[i]);
            }
        }
        cards.Clear();

        int focusedIndex = selections.FocusedUnitIndex;
        for (int i = 0; i < selectedUnits.Count; i++)
        {
            Unit unit = selectedUnits[i];
            if (unit == null) continue;

            int cardIndex = i;
            bool focused = i == focusedIndex;
            GameObject card = CreateUIObject($"Unit Card {i + 1}", cardContent, typeof(Image), typeof(Button), typeof(LayoutElement));
            card.GetComponent<Image>().color = focused ? FocusColor : CardColor;

            LayoutElement element = card.GetComponent<LayoutElement>();
            element.preferredWidth = focused ? 210f : 46f;
            element.minWidth = element.preferredWidth;

            GameObject labelObject = CreateUIObject("Label", card.transform, typeof(Text));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(5f, 2f);
            labelRect.offsetMax = new Vector2(-5f, -2f);

            Text label = labelObject.GetComponent<Text>();
            ConfigureText(label, focused ? 12 : 18, TextAnchor.MiddleCenter);
            label.text = focused ? BuildExpandedLabel(i + 1, unit) : (i + 1).ToString();

            card.GetComponent<Button>().onClick.AddListener(() => selections.FocusSelectedUnit(cardIndex));
            cards.Add(card);
        }
    }

    private static string BuildExpandedLabel(int cardNumber, Unit unit)
    {
        string displayName = string.IsNullOrWhiteSpace(unit.displayName) ? unit.name : unit.displayName;
        UnitInventory inventory = unit.GetComponent<UnitInventory>();
        if (inventory == null || inventory.itemSlots == null)
        {
            return $"{cardNumber}   {displayName}";
        }

        int used = 0;
        int capacity = 0;
        foreach (ItemSlot slot in inventory.itemSlots)
        {
            if (slot == null || slot.itemStack == null) continue;
            used += slot.itemStack.GetQuantity();
            capacity += slot.itemStack.GetMaxQuantity();
        }
        return $"{cardNumber}   {displayName}\nCargo {used} / {capacity}";
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size)
    {
        GameObject buttonObject = CreateUIObject(name, parent, typeof(Image), typeof(Button));
        SetTopLeft(buttonObject.GetComponent<RectTransform>(), position, size);
        buttonObject.GetComponent<Image>().color = FocusColor;

        GameObject labelObject = CreateUIObject("Text", buttonObject.transform, typeof(Text));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        ConfigureText(labelObject.GetComponent<Text>(), 22, TextAnchor.MiddleCenter);
        labelObject.GetComponent<Text>().text = label;
        return buttonObject.GetComponent<Button>();
    }

    private static GameObject CreateUIObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        foreach (System.Type component in components) result.AddComponent(component);
        return result;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void ConfigureText(Text text, int size, TextAnchor alignment)
    {
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
    }
}
