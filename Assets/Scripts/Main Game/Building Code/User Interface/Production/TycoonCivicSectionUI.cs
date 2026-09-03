using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders an Anno 2070-style Public or Special section in the construction menu,
/// complete with contextual header strip, divider line, and interactive building slots.
/// </summary>
[DisallowMultipleComponent]
public sealed class TycoonCivicSectionUI : MonoBehaviour
{
    private static readonly Color HeaderTextColor = new Color(0.90f, 0.95f, 1.00f, 0.95f);
    private static readonly Color PanelBorderColor = new Color(0.20f, 0.35f, 0.48f, 0.55f);
    private static readonly Color SlotBorderColor = new Color(0.20f, 0.35f, 0.48f, 0.55f);
    private static readonly Color SlotBgColor = new Color(0.06f, 0.10f, 0.16f, 0.90f);

    [SerializeField] private string sectionTitle = "Public";
    [SerializeField] private RectTransform headerStrip;
    [SerializeField] private TextMeshProUGUI headerTitle;
    [SerializeField] private RectTransform slotsContainer;

    public string SectionTitle => sectionTitle;

    public void Setup(string title)
    {
        sectionTitle = title;
        BuildStructure();
    }

    public void BuildStructure()
    {
        var rect = (RectTransform)transform;
        rect.sizeDelta = new Vector2(480f, 84f);

        // Find or create Header Strip
        Transform headerT = transform.Find("Section Header");
        if (headerT == null)
        {
            var headerObj = new GameObject("Section Header", typeof(RectTransform));
            headerStrip = (RectTransform)headerObj.transform;
            headerStrip.SetParent(rect, false);
        }
        else
        {
            headerStrip = (RectTransform)headerT;
        }

        headerStrip.anchorMin = new Vector2(0f, 1f);
        headerStrip.anchorMax = new Vector2(1f, 1f);
        headerStrip.pivot = new Vector2(0.5f, 1f);
        headerStrip.anchoredPosition = Vector2.zero;
        headerStrip.sizeDelta = new Vector2(0f, 22f);

        // Label
        Transform labelT = headerStrip.Find("Header Label");
        if (labelT == null)
        {
            var labelObj = new GameObject("Header Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)labelObj.transform;
            labelRect.SetParent(headerStrip, false);
            headerTitle = labelObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            headerTitle = labelT.GetComponent<TextMeshProUGUI>();
        }

        var lRect = (RectTransform)headerTitle.transform;
        lRect.anchorMin = Vector2.zero;
        lRect.anchorMax = Vector2.one;
        lRect.offsetMin = new Vector2(10f, 0f);
        lRect.offsetMax = new Vector2(-10f, 0f);

        headerTitle.text = sectionTitle;
        headerTitle.fontSize = 12f;
        headerTitle.fontStyle = FontStyles.Bold;
        headerTitle.color = HeaderTextColor;
        headerTitle.alignment = TextAlignmentOptions.MidlineLeft;

        // Divider
        Transform divT = headerStrip.Find("Divider");
        if (divT == null)
        {
            var divObj = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var divRect = (RectTransform)divObj.transform;
            divRect.SetParent(headerStrip, false);
            divRect.anchorMin = new Vector2(0f, 0f);
            divRect.anchorMax = new Vector2(1f, 0f);
            divRect.pivot = new Vector2(0.5f, 0f);
            divRect.anchoredPosition = Vector2.zero;
            divRect.sizeDelta = new Vector2(0f, 1f);
            var divImg = divObj.GetComponent<Image>();
            divImg.color = PanelBorderColor;
            divImg.raycastTarget = false;
        }

        // Find or create Slots Container
        Transform slotsT = transform.Find("Slots Container");
        if (slotsT == null)
        {
            var slotsObj = new GameObject("Slots Container", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            slotsContainer = (RectTransform)slotsObj.transform;
            slotsContainer.SetParent(rect, false);
        }
        else
        {
            slotsContainer = (RectTransform)slotsT;
        }

        slotsContainer.anchorMin = new Vector2(0f, 0f);
        slotsContainer.anchorMax = new Vector2(1f, 0f);
        slotsContainer.pivot = new Vector2(0.5f, 0f);
        slotsContainer.anchoredPosition = Vector2.zero;
        slotsContainer.sizeDelta = new Vector2(0f, 58f);

        var layout = slotsContainer.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = slotsContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(6, 6, 2, 2);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    public void ClearSlots()
    {
        if (slotsContainer == null) return;
        for (int i = slotsContainer.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(slotsContainer.GetChild(i).gameObject);
        }
    }

    public GameObject AddSlot(string buildingName, Sprite icon, GameObject prefab)
    {
        if (slotsContainer == null) BuildStructure();

        var root = new GameObject($"Slot ({buildingName})", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        var rect = (RectTransform)root.transform;
        rect.SetParent(slotsContainer, false);
        rect.sizeDelta = new Vector2(52f, 52f);

        var layout = root.GetComponent<LayoutElement>();
        layout.preferredWidth = 52f;
        layout.preferredHeight = 52f;
        layout.minWidth = 52f;
        layout.minHeight = 52f;

        Image border = root.GetComponent<Image>();
        border.color = SlotBorderColor;

        var innerObj = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var innerRect = (RectTransform)innerObj.transform;
        innerRect.SetParent(rect, false);
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(2f, 2f);
        innerRect.offsetMax = new Vector2(-2f, -2f);
        Image fill = innerObj.GetComponent<Image>();
        fill.color = SlotBgColor;
        fill.raycastTarget = false;

        Button button = root.GetComponent<Button>();
        button.targetGraphic = border;

        if (icon != null)
        {
            var iconObj = new GameObject("Building Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var iconRect = (RectTransform)iconObj.transform;
            iconRect.SetParent(innerRect, false);
            iconRect.anchorMin = new Vector2(0.08f, 0.08f);
            iconRect.anchorMax = new Vector2(0.92f, 0.92f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            Image iconImg = iconObj.GetComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.color = Color.white;
            iconImg.raycastTarget = false;
        }

        var bb = root.AddComponent<BuildingButton>();
        bb.SetBuildingPrefab(prefab);

        var tooltip = root.AddComponent<ProductionTooltipTrigger>();
        tooltip.Title = buildingName;
        tooltip.Description = prefab != null ? "Click to place building" : "Building locked";

        return root;
    }
}
