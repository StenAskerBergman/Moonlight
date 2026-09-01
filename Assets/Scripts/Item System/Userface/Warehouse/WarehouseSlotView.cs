using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One slot in the warehouse panel: icon, amount label and stock bar. Used by all
/// three tabs — the Goods grid, the Items sockets and the Trade rule list — since the
/// visual is the same and only the numbers behind it differ.
///
/// Instantiated from a template by the tab views; the template itself is built by
/// WarehousePanelBuilder and styled from <see cref="WarehousePanelStyle"/>.
/// </summary>
public sealed class WarehouseSlotView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text amountLabel;
    [SerializeField] private Image stockBarFill;
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private TMP_Text lockedLabel;

    private WarehousePanelStyle style;
    private System.Action<WarehouseSlotView> clicked;

    public ItemData Item { get; private set; }
    public bool IsLocked { get; private set; }

    public void Initialise(WarehousePanelStyle panelStyle, System.Action<WarehouseSlotView> onClicked)
    {
        style = panelStyle;
        clicked = onClicked;
    }

    /// <summary>
    /// Show a stocked good. <paramref name="capacity"/> of 0 or less means "no known
    /// ceiling", which hides the bar and prints the raw amount instead of "max".
    /// </summary>
    public void SetGood(ItemData item, int amount, int capacity)
    {
        Item = item;
        IsLocked = false;
        gameObject.SetActive(true);

        ApplyIcon(item);
        ApplyLocked(false, null);

        if (amountLabel != null)
        {
            amountLabel.text = capacity > 0 && amount >= capacity ? "max" : amount.ToString();
        }

        ApplyStockBar(capacity > 0 ? Mathf.Clamp01((float)amount / capacity) : -1f);
    }

    /// <summary>Show a good the player hasn't unlocked yet: visible, dimmed, with its requirement.</summary>
    public void SetLockedGood(ItemData item, PopulationUnlock unlock, int currentPopulation)
    {
        Item = item;
        IsLocked = true;
        gameObject.SetActive(true);

        ApplyIcon(item);
        if (amountLabel != null) amountLabel.text = string.Empty;
        ApplyStockBar(-1f);
        ApplyLocked(true, $"{currentPopulation}/{unlock.requiredPopulation}");
    }

    /// <summary>Show a socketed item, or an empty socket when <paramref name="item"/> is null.</summary>
    public void SetSocket(ItemData item)
    {
        Item = item;
        IsLocked = false;
        gameObject.SetActive(true);

        ApplyIcon(item);
        ApplyLocked(false, null);
        if (amountLabel != null) amountLabel.text = string.Empty;
        ApplyStockBar(-1f);
    }

    public void SetEmpty()
    {
        Item = null;
        IsLocked = false;
        gameObject.SetActive(false);
    }

    private void ApplyIcon(ItemData item)
    {
        if (icon == null) return;

        icon.sprite = item != null ? item.Icon : null;
        icon.enabled = icon.sprite != null;
        if (style != null) icon.color = style.slotIconTint;
    }

    private void ApplyLocked(bool locked, string progressText)
    {
        if (lockedOverlay != null) lockedOverlay.SetActive(locked);
        if (lockedLabel != null) lockedLabel.text = progressText ?? string.Empty;

        if (style == null) return;

        if (background != null)
        {
            background.color = locked ? style.slotBackgroundLocked : style.slotBackground;
        }
        if (icon != null)
        {
            icon.color = locked ? style.slotIconTintLocked : style.slotIconTint;
        }
    }

    // fill < 0 hides the bar entirely (unknown capacity, or a slot that isn't stock-based).
    private void ApplyStockBar(float fill)
    {
        if (stockBarFill == null) return;

        Transform bar = stockBarFill.transform.parent != null
            ? stockBarFill.transform.parent
            : stockBarFill.transform;

        bool visible = fill >= 0f;
        bar.gameObject.SetActive(visible);
        if (!visible) return;

        stockBarFill.fillAmount = fill;

        if (style != null)
        {
            stockBarFill.color = fill <= style.lowStockThreshold ? style.stockBarLow : style.stockBarNormal;
        }
    }

    public void OnPointerClick(PointerEventData eventData) => clicked?.Invoke(this);
}
