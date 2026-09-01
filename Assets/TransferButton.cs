using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The transfer control on one item slot in the Port Authority menu.
///
/// Pressing it moves the slot's item out to the selected vessel when there is one, and
/// back to the island's own pool when there is not — the same button covers transferring
/// between vessels and transferring internally. With the slot empty it works the other
/// way, pulling the vessel's matching item into the slot.
///
/// An activated item is pinned: the transfer is refused, with the reason logged, until
/// the item is deactivated, expires, or is consumed.
///
/// Sits on the slot GameObject alongside its Button. Slots with no
/// <see cref="ItemSlotBinding"/> are inert, so this is harmless on slots that are not
/// wired to a bank.
/// </summary>
[DisallowMultipleComponent]
public class TransferButton : MonoBehaviour
{
    [Tooltip("Slot this button transfers to and from. Left empty, it looks on this GameObject and its parents.")]
    [SerializeField] private ItemSlotBinding binding;

    [Tooltip("Optional graphic tinted to show when the slot is pinned by an active item.")]
    [SerializeField] private Image stateGraphic;

    [SerializeField] private Color lockedTint = new Color(0.45f, 0.45f, 0.45f);

    private Button button;
    private Color originalTint;
    private bool capturedTint;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (binding == null) binding = GetComponentInParent<ItemSlotBinding>();
        if (stateGraphic == null) stateGraphic = GetComponent<Image>();

        if (stateGraphic != null)
        {
            originalTint = stateGraphic.color;
            capturedTint = true;
        }
    }

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(Transfer);
        Refresh();
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(Transfer);
    }

    private void Update() => Refresh();

    /// <summary>Move the item out of the slot, or into it when the slot is empty.</summary>
    public void Transfer()
    {
        if (binding == null) return;

        ItemSocketBank bank = binding.Bank;
        if (bank == null) return;

        string reason;
        bool moved = binding.Slot != null && binding.Slot.IsOccupied
            ? TransferOut(bank, out reason)
            : TransferIn(bank, out reason);

        if (!moved && !string.IsNullOrEmpty(reason)) Debug.Log(reason, this);

        Refresh();
    }

    // Slot -> selected vessel, falling back to the island's own pool.
    private bool TransferOut(ItemSocketBank bank, out string reason)
    {
        ItemEndpoint vessel = ItemEndpoint.From(FocusedUnit());

        if (vessel.IsValid)
        {
            return ItemMover.TryTransferFromSlot(bank, binding.SlotIndex, vessel, out reason);
        }

        return ItemMover.TryMoveToPool(bank, binding.SlotIndex, out reason);
    }

    // Selected vessel -> slot, falling back to the island's own pool.
    private bool TransferIn(ItemSocketBank bank, out string reason)
    {
        ItemEndpoint vessel = ItemEndpoint.From(FocusedUnit());

        if (vessel.IsValid)
        {
            ItemData carried = FirstSlottableItem(vessel);
            if (carried != null)
            {
                return ItemMover.TryTransferToSlot(vessel, bank, binding.SlotIndex, carried, out reason);
            }
        }

        reason = "Nothing to transfer into that slot.";
        return false;
    }

    private static Unit FocusedUnit() =>
        UnitSelections.Instance != null ? UnitSelections.Instance.FocusedUnit : null;

    // Vessels carry cargo as well as slottable items; only the latter can enter a slot.
    private static ItemData FirstSlottableItem(ItemEndpoint vessel)
    {
        Unit unit = UnitSelections.Instance != null ? UnitSelections.Instance.FocusedUnit : null;
        if (unit == null) return null;

        if (unit.unitInventory != null)
        {
            foreach (var entry in unit.unitInventory.GetAllItems())
            {
                if (entry.Key != null && entry.Key.isSocketable && entry.Value > 0) return entry.Key;
            }
        }

        if (unit.inventory != null)
        {
            foreach (var entry in unit.inventory.GetAllItems())
            {
                if (entry.Key != null && entry.Key.isSocketable && entry.Value > 0) return entry.Key;
            }
        }

        return null;
    }

    private void Refresh()
    {
        if (binding == null) return;

        bool locked = binding.IsLocked;

        if (button != null) button.interactable = !locked && binding.IsUnlocked;

        if (stateGraphic != null && capturedTint)
        {
            stateGraphic.color = locked ? lockedTint : originalTint;
        }
    }
}
