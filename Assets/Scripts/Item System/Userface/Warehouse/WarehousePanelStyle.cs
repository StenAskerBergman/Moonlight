using UnityEngine;

/// <summary>
/// Every colour and metric the warehouse panel uses, in one asset. The generated
/// prefab and the runtime controllers read from here and hardcode nothing, so the
/// panel can be restyled without touching code or regenerating the hierarchy.
/// </summary>
[CreateAssetMenu(fileName = "Warehouse Panel Style", menuName = "Data/UI/Warehouse Panel Style")]
public sealed class WarehousePanelStyle : ScriptableObject
{
    [Header("Panel")]
    public Color panelBackground = new Color(0.07f, 0.15f, 0.25f, 0.95f);
    public Color headerBackground = new Color(0.10f, 0.22f, 0.36f, 1f);
    public Color headerText = new Color(0.90f, 0.95f, 1f, 1f);
    public Color subtleText = new Color(0.62f, 0.74f, 0.86f, 1f);

    [Header("Slots")]
    public Color slotBackground = new Color(0.12f, 0.24f, 0.38f, 1f);
    public Color slotBackgroundLocked = new Color(0.10f, 0.14f, 0.19f, 1f);
    public Color slotIconTint = Color.white;
    public Color slotIconTintLocked = new Color(0.45f, 0.50f, 0.56f, 1f);
    public Color amountText = new Color(0.92f, 0.96f, 1f, 1f);

    [Header("Stock Bar")]
    [Tooltip("Fill colour at normal stock levels.")]
    public Color stockBarNormal = new Color(0.42f, 0.80f, 0.36f, 1f);
    [Tooltip("Fill colour once stock drops below the low threshold.")]
    public Color stockBarLow = new Color(0.85f, 0.30f, 0.25f, 1f);
    public Color stockBarBackground = new Color(0.06f, 0.11f, 0.17f, 1f);

    [Range(0f, 1f)]
    [Tooltip("Fill fraction under which the bar switches to the low colour.")]
    public float lowStockThreshold = 0.15f;

    [Header("Tabs")]
    public Color tabActive = new Color(0.20f, 0.48f, 0.72f, 1f);
    public Color tabInactive = new Color(0.11f, 0.20f, 0.30f, 1f);
    public Color tabLocked = new Color(0.09f, 0.12f, 0.16f, 1f);
    public Color tabActiveText = Color.white;
    public Color tabInactiveText = new Color(0.60f, 0.72f, 0.84f, 1f);

    [Header("Metrics")]
    public Vector2 panelSize = new Vector2(384f, 430f);
    public float slotSize = 64f;
    public int goodsColumns = 4;
    public float spacing = 8f;
    public float padding = 10f;

    [Header("Text")]
    public int headerFontSize = 18;
    public int bodyFontSize = 13;
    public int amountFontSize = 14;
}
