using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Anno 2070-style compact cursor HUD displayed next to an active building blueprint:
/// shows credit cost, required construction goods (colored red when unaffordable), and a
/// single crisp blockage / connection hint when invalid. Self-creates a non-blocking
/// overlay canvas on demand so zero scene wiring is required.
/// </summary>
public sealed class PlacementCursorCostHUD : MonoBehaviour
{
    private static PlacementCursorCostHUD instance;

    private Canvas overlayCanvas;
    private RectTransform panelRect;
    private Image panelBackground;
    private TMP_Text costText;
    private TMP_Text reasonText;
    private GameObject iconRowObject;
    private readonly List<Image> itemIconSlots = new List<Image>();

    private readonly StringBuilder sb = new StringBuilder(128);
    private bool isVisible;

    public static void UpdateReadout(
        GameObject prefab,
        BuildingCost cost,
        PlacementValidationResult validation,
        Bank bank,
        Inventory islandGoods,
        Unit foundingBoat)
    {
        if (prefab == null || validation == null)
        {
            Hide();
            return;
        }

        EnsureInstance().Refresh(prefab, cost, validation, bank, islandGoods, foundingBoat);
    }

    public static void Hide()
    {
        if (instance == null || !instance.isVisible) return;
        instance.SetVisible(false);
    }

    private static PlacementCursorCostHUD EnsureInstance()
    {
        if (instance == null)
        {
            GameObject host = new GameObject("Placement Cursor Cost HUD");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<PlacementCursorCostHUD>();
            instance.BuildUI();
        }
        return instance;
    }

    private void Refresh(
        GameObject prefab,
        BuildingCost cost,
        PlacementValidationResult validation,
        Bank bank,
        Inventory islandGoods,
        Unit foundingBoat)
    {
        if (panelRect == null) BuildUI();

        sb.Clear();

        int creditPrice = cost != null ? Mathf.Max(0, cost.GetPrice()) : 0;
        if (creditPrice > 0)
        {
            bool canCredits = bank == null || bank.CanAfford(creditPrice);
            string hex = canCredits ? "#B6F7FF" : "#FF5B5B";
            sb.Append("<color=").Append(hex).Append(">C ").Append(creditPrice).Append("</color>");
        }

        int slotIndex = 0;
        if (cost != null && cost.TryGetCosts(out Dictionary<ItemData, int> costItems))
        {
            UnitInventory boatInv = validation.IsUnsettledHarbor && foundingBoat != null
                ? foundingBoat.GetComponent<UnitInventory>()
                : null;
            Dictionary<ItemData, int> boatItems = boatInv != null ? boatInv.GetAllItems() : null;

            foreach (var kvp in costItems)
            {
                if (kvp.Key == null || kvp.Value <= 0) continue;

                bool canItem;
                if (validation.IsUnsettledHarbor)
                {
                    canItem = boatItems != null
                        && boatItems.TryGetValue(kvp.Key, out int have)
                        && have >= kvp.Value;
                }
                else
                {
                    canItem = islandGoods != null && islandGoods.HasItem(kvp.Key, kvp.Value);
                }

                string label = !string.IsNullOrEmpty(kvp.Key.displayName) ? kvp.Key.displayName : kvp.Key.name;
                string hex = canItem ? "#E8F6FF" : "#FF5B5B";
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("<color=").Append(hex).Append(">").Append(kvp.Value).Append("x ").Append(label).Append("</color>");

                if (kvp.Key.Icon != null && slotIndex < itemIconSlots.Count)
                {
                    Image iconImg = itemIconSlots[slotIndex++];
                    iconImg.sprite = kvp.Key.Icon;
                    iconImg.color = canItem ? Color.white : new Color(1f, 0.45f, 0.45f, 1f);
                    iconImg.gameObject.SetActive(true);
                }
            }
        }

        for (int i = slotIndex; i < itemIconSlots.Count; i++)
        {
            itemIconSlots[i].gameObject.SetActive(false);
        }
        if (iconRowObject != null) iconRowObject.SetActive(slotIndex > 0);

        costText.text = sb.ToString();
        costText.gameObject.SetActive(sb.Length > 0);

        string reason = validation.FailureReason != PlacementFailureReason.None
            ? validation.FormatReasonLabel()
            : string.Empty;

        bool hasReason = !string.IsNullOrEmpty(reason);
        reasonText.text = reason;
        reasonText.color = !validation.IsPlacementValid
            ? new Color(1f, 0.42f, 0.40f, 1f)
            : new Color(1f, 0.78f, 0.28f, 1f);
        reasonText.gameObject.SetActive(hasReason);

        if (sb.Length == 0 && !hasReason)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        UpdateScreenPosition();
    }

    private void LateUpdate()
    {
        if (!isVisible) return;
        UpdateScreenPosition();
    }

    private void UpdateScreenPosition()
    {
        if (panelRect == null) return;

        Vector2 pos = Input.mousePosition;
        pos += new Vector2(28f, -18f);

        float w = panelRect.rect.width > 1f ? panelRect.rect.width : 190f;
        float h = panelRect.rect.height > 1f ? panelRect.rect.height : 64f;
        pos.x = Mathf.Clamp(pos.x, 12f, Mathf.Max(12f, Screen.width - w - 12f));
        pos.y = Mathf.Clamp(pos.y, h + 12f, Mathf.Max(h + 12f, Screen.height - 12f));

        panelRect.position = pos;
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;
        if (panelRect != null && panelRect.gameObject.activeSelf != visible)
        {
            panelRect.gameObject.SetActive(visible);
        }
    }

    private void BuildUI()
    {
        overlayCanvas = gameObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 520;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("CursorReadoutPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(transform, false);

        panelRect = panel.GetComponent<RectTransform>();
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.sizeDelta = new Vector2(210f, 0f);

        panelBackground = panel.GetComponent<Image>();
        panelBackground.color = new Color(0.05f, 0.11f, 0.17f, 0.84f);
        panelBackground.raycastTarget = false;

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Compact optional row for up to 3 item sprites
        GameObject iconRow = new GameObject("Icons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        iconRow.transform.SetParent(panel.transform, false);
        iconRowObject = iconRow;
        iconRowObject.SetActive(false);
        HorizontalLayoutGroup iconLayout = iconRow.GetComponent<HorizontalLayoutGroup>();
        iconLayout.spacing = 4f;
        iconLayout.childControlWidth = false;
        iconLayout.childControlHeight = false;
        iconLayout.childForceExpandWidth = false;
        iconLayout.childForceExpandHeight = false;

        for (int i = 0; i < 3; i++)
        {
            GameObject iconObj = new GameObject($"Icon_{i}", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(iconRow.transform, false);
            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.sizeDelta = new Vector2(18f, 18f);
            Image img = iconObj.GetComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            iconObj.SetActive(false);
            itemIconSlots.Add(img);
        }

        costText = CreateLabel(panel.transform, "CostText", 14f, FontStyles.Bold);
        reasonText = CreateLabel(panel.transform, "ReasonText", 12.5f, FontStyles.Normal);

        SetVisible(false);
    }

    private static TMP_Text CreateLabel(Transform parent, string labelName, float fontSize, FontStyles style)
    {
        GameObject go = new GameObject(labelName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Left;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
