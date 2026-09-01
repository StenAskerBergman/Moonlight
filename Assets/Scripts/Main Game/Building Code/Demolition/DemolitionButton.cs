using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds a HUD button to demolition mode.
///
/// The binding is made at runtime rather than as a persistent onClick entry because
/// DemolitionManager bootstraps itself on load and so has no scene object for the
/// Inspector to point at.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class DemolitionButton : MonoBehaviour
{
    [Tooltip("Optional label that reflects the current mode.")]
    [SerializeField] private TMP_Text label;

    [SerializeField] private string idleText = "Destroy";
    [SerializeField] private string activeText = "Confirm ({0})";
    [SerializeField] private string activeEmptyText = "Cancel";

    [Tooltip("Tint applied to the button while demolition mode is on.")]
    [SerializeField] private Color activeTint = new Color(1f, 0.35f, 0.3f, 1f);

    private Button button;
    private Image background;
    private Color idleTint;

    private void Awake()
    {
        button = GetComponent<Button>();
        background = GetComponent<Image>();
        if (background != null) idleTint = background.color;

        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
    }

    private void OnEnable()
    {
        button.onClick.AddListener(OnClicked);
        Subscribe(true);
        Refresh();
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(OnClicked);
        Subscribe(false);
    }

    private void Subscribe(bool add)
    {
        DemolitionManager manager = DemolitionManager.Instance;
        if (manager == null) return;

        if (add)
        {
            manager.OnModeChanged += Refresh;
            manager.OnMarkedChanged += Refresh;
        }
        else
        {
            manager.OnModeChanged -= Refresh;
            manager.OnMarkedChanged -= Refresh;
        }
    }

    /// <summary>
    /// First press enters demolition mode; while in it, the same button confirms the
    /// marked batch (or leaves the mode when nothing is marked).
    /// </summary>
    private void OnClicked()
    {
        DemolitionManager manager = DemolitionManager.Instance;
        if (manager == null) return;

        if (!manager.IsActive)
        {
            manager.SetMode(true);
            Subscribe(true);
        }
        else if (manager.MarkedCount > 0)
        {
            manager.ConfirmDemolition();
        }
        else
        {
            manager.SetMode(false);
        }

        Refresh();
    }

    private void Refresh()
    {
        DemolitionManager manager = DemolitionManager.Instance;
        bool active = manager != null && manager.IsActive;

        if (background != null) background.color = active ? activeTint : idleTint;

        if (label == null) return;

        if (!active) label.text = idleText;
        else if (manager.MarkedCount > 0) label.text = string.Format(activeText, manager.MarkedCount);
        else label.text = activeEmptyText;
    }
}
