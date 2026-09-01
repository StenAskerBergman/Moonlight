using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One button in the tier strip. Carries its <see cref="WarehouseTierTab"/> descriptor
/// and paints itself active/inactive/locked from <see cref="WarehousePanelStyle"/>.
/// </summary>
public sealed class WarehouseTierTabButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image glyph;

    [Tooltip("Shown instead of the number on the Tech tab.")]
    [SerializeField] private Sprite techGlyph;

    private WarehousePanelStyle style;
    private System.Action<WarehouseTierTab> clicked;

    public WarehouseTierTab Tier { get; private set; }

    public void Bind(WarehouseTierTab tier, WarehousePanelStyle panelStyle, System.Action<WarehouseTierTab> onClicked)
    {
        Tier = tier;
        style = panelStyle;
        clicked = onClicked;

        gameObject.SetActive(true);
        name = $"Tier Tab ({tier.DisplayName})";

        // The Tech tab is an atom icon. Until a sprite is assigned it falls back to the
        // atom character, so the strip never renders a blank button.
        bool useGlyphSprite = tier.IsTech && techGlyph != null;

        if (label != null)
        {
            label.text = tier.IsTech ? "⚛" : tier.Label;
            label.gameObject.SetActive(!useGlyphSprite);
        }

        if (glyph != null)
        {
            glyph.sprite = techGlyph;
            glyph.enabled = useGlyphSprite;
            glyph.gameObject.SetActive(useGlyphSprite);
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => clicked?.Invoke(Tier));
        }
    }

    /// <summary>
    /// <paramref name="reached"/> is false when the island has no population at all in
    /// this tab's demographics — the tab stays clickable (so the player can see what's
    /// coming) but renders dimmed.
    /// </summary>
    public void SetState(bool isActive, bool reached)
    {
        if (style == null) return;

        if (background != null)
        {
            background.color = isActive
                ? style.tabActive
                : reached ? style.tabInactive : style.tabLocked;
        }

        if (label != null)
        {
            label.color = isActive ? style.tabActiveText : style.tabInactiveText;
        }

        if (glyph != null)
        {
            glyph.color = isActive ? style.tabActiveText : style.tabInactiveText;
        }
    }

    public void Hide() => gameObject.SetActive(false);
}
