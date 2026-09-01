using UnityEngine;

/// <summary>
/// Ties one slot GameObject in the Port Authority UI to a slot index in a bank.
///
/// The Items tab's two rows resolve their bank differently: an Island row always points
/// at the selected island's <see cref="IslandItemSockets"/>, while a Local row follows
/// whichever building is selected. Rather than hard-wire either, this component resolves
/// its bank on demand from <see cref="ItemSlotBindingSource"/>, so the same prefab works
/// in both rows and survives the selection changing under it.
/// </summary>
[DisallowMultipleComponent]
public sealed class ItemSlotBinding : MonoBehaviour
{
    [Tooltip("Island = the island-wide bank, which affects every building. Local = the selected building's own bank.")]
    [SerializeField] private ItemSocketScope scope = ItemSocketScope.Island;

    [Tooltip("Which slot in that bank this GameObject represents. 0-based.")]
    [SerializeField, Min(0)] private int slotIndex;

    [Tooltip("Optional. Leave empty to resolve the bank from the current selection; set it to pin this slot to one specific bank.")]
    [SerializeField] private ItemSocketBank explicitBank;

    public ItemSocketScope Scope => scope;
    public int SlotIndex => slotIndex;

    /// <summary>The bank this slot currently maps onto, or null when nothing is selected.</summary>
    public ItemSocketBank Bank =>
        explicitBank != null ? explicitBank : ItemSlotBindingSource.Resolve(scope);

    public ItemSlotState Slot
    {
        get
        {
            ItemSocketBank bank = Bank;
            return bank != null ? bank.GetSlot(slotIndex) : null;
        }
    }

    public ItemData Item
    {
        get
        {
            ItemSlotState slot = Slot;
            return slot != null ? slot.Item : null;
        }
    }

    public bool IsActivated
    {
        get
        {
            ItemSlotState slot = Slot;
            return slot != null && slot.IsActivated;
        }
    }

    /// <summary>An activated slot is pinned; nothing may leave it until it is switched off.</summary>
    public bool IsLocked
    {
        get
        {
            ItemSlotState slot = Slot;
            return slot != null && slot.IsLocked;
        }
    }

    public bool IsUnlocked
    {
        get
        {
            ItemSocketBank bank = Bank;
            return bank != null && bank.IsSlotUnlocked(slotIndex);
        }
    }

    public void Configure(ItemSocketScope newScope, int newSlotIndex, ItemSocketBank bank = null)
    {
        scope = newScope;
        slotIndex = Mathf.Max(0, newSlotIndex);
        explicitBank = bank;
    }
}

/// <summary>
/// Resolves the Island and Local banks for the UI from the current selection.
///
/// Kept separate from <see cref="ItemSlotBinding"/> so all six slot objects share one
/// lookup rather than each walking the scene, and so the selection plumbing lives in one
/// place if it later moves off <see cref="BuildingSelections"/>.
/// </summary>
public static class ItemSlotBindingSource
{
    private static Building selectedBuilding;

    /// <summary>
    /// The building the Items tab is currently showing. The panel sets this when the
    /// selection changes; the Local slots follow it.
    /// </summary>
    public static Building SelectedBuilding
    {
        get => selectedBuilding;
        set => selectedBuilding = value;
    }

    public static ItemSocketBank Resolve(ItemSocketScope scope) =>
        scope == ItemSocketScope.Island ? (ItemSocketBank)ResolveIsland() : ResolveLocal();

    /// <summary>The island-wide bank of the selected building's island.</summary>
    public static IslandItemSockets ResolveIsland()
    {
        Island island = ResolveIslandRoot();
        if (island == null) return null;

        IslandItemSockets sockets = island.GetComponent<IslandItemSockets>();
        if (sockets == null) sockets = island.GetComponentInChildren<IslandItemSockets>(true);
        return sockets;
    }

    /// <summary>The selected building's own bank.</summary>
    public static WarehouseSockets ResolveLocal()
    {
        if (selectedBuilding == null) return null;
        return selectedBuilding.GetComponent<WarehouseSockets>();
    }

    public static Island ResolveIslandRoot()
    {
        if (selectedBuilding == null) return null;
        return selectedBuilding.GetComponentInParent<Island>();
    }
}
