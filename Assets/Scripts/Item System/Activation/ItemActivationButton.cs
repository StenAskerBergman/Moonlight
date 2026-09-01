using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The activate / deactivate control on one item slot.
///
/// Presses toggle: an inactive slot is verified and switched on, an active one is
/// cancelled. Verification failures are reported rather than swallowed, so a slot that
/// refuses to activate says why. While something is active the button shows the time or
/// charges it has left, and the slot underneath is locked against being moved.
/// </summary>
[RequireComponent(typeof(Button))]
[DisallowMultipleComponent]
public sealed class ItemActivationButton : MonoBehaviour
{
    [Tooltip("Slot this button acts on. Left empty, it looks on this GameObject and its parents.")]
    [SerializeField] private ItemSlotBinding binding;

    [Tooltip("Optional label showing remaining time or charges while active.")]
    [SerializeField] private TMP_Text statusLabel;

    [Tooltip("Optional graphic tinted to show the activation state.")]
    [SerializeField] private Image stateGraphic;

    [SerializeField] private Color activeTint = new Color(0.36f, 0.78f, 0.42f);
    [SerializeField] private Color inactiveTint = Color.white;
    [SerializeField] private Color lockedTint = new Color(0.45f, 0.45f, 0.45f);

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (binding == null) binding = GetComponentInParent<ItemSlotBinding>();
        if (stateGraphic == null) stateGraphic = GetComponent<Image>();
    }

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(Toggle);
        Refresh();
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(Toggle);
    }

    private void Update() => Refresh();

    /// <summary>Switch the slot on, or cancel it if it is already running.</summary>
    public void Toggle()
    {
        if (binding == null) return;

        ItemSocketBank bank = binding.Bank;
        if (bank == null) return;

        string reason;
        bool ok = binding.IsActivated
            ? bank.TryDeactivate(binding.SlotIndex, out reason)
            : bank.TryActivate(binding.SlotIndex, out reason);

        if (!ok) Debug.Log(reason, this);

        Refresh();
    }

    /// <summary>Explicit activate, for wiring straight to a UnityEvent.</summary>
    public void Activate()
    {
        ItemSocketBank bank = binding != null ? binding.Bank : null;
        if (bank == null) return;

        string reason;
        if (!bank.TryActivate(binding.SlotIndex, out reason)) Debug.Log(reason, this);
        Refresh();
    }

    /// <summary>Explicit deactivate, for wiring straight to a UnityEvent.</summary>
    public void Deactivate()
    {
        ItemSocketBank bank = binding != null ? binding.Bank : null;
        if (bank == null) return;

        string reason;
        if (!bank.TryDeactivate(binding.SlotIndex, out reason)) Debug.Log(reason, this);
        Refresh();
    }

    private void Refresh()
    {
        if (binding == null) return;

        ItemSlotState slot = binding.Slot;
        bool hasItem = slot != null && slot.IsOccupied;
        bool activated = slot != null && slot.IsActivated;

        if (button != null) button.interactable = hasItem && binding.IsUnlocked;

        if (stateGraphic != null)
        {
            stateGraphic.color = !binding.IsUnlocked ? lockedTint : (activated ? activeTint : inactiveTint);
        }

        if (statusLabel == null) return;

        if (!hasItem) { statusLabel.text = string.Empty; return; }

        if (!activated)
        {
            ItemActivationProfile profile = slot.Item.activation;
            statusLabel.text = profile != null && profile.IsConsumable
                ? slot.RemainingCharges + "x"
                : string.Empty;
            return;
        }

        if (slot.HasTimer)
        {
            statusLabel.text = FormatSeconds(slot.RemainingSeconds);
            return;
        }

        ItemActivationProfile activeProfile = slot.Item.activation;
        statusLabel.text = activeProfile != null && activeProfile.IsConsumable
            ? slot.RemainingCharges + "x"
            : "ON";
    }

    private static string FormatSeconds(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = total / 60;
        return minutes > 0 ? minutes + ":" + (total % 60).ToString("00") : total + "s";
    }
}
