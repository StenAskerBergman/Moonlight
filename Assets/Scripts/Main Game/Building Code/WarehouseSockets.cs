using UnityEngine;

/// <summary>
/// The Local item bank on a warehouse / Port Authority, plus its trade-slot allowance.
///
/// Three slots are always drawn; the warehouse level decides how many of them are
/// unlocked (1 / 2 / 3). An item activated here affects only this building — the
/// island-wide equivalent is <see cref="IslandItemSockets"/>.
///
/// Level also caps how many active buy/sell rules the island may run through this
/// warehouse (2 / 4 / 8). Sits alongside <see cref="Depot"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class WarehouseSockets : ItemSocketBank
{
    public const int MaxLevel = 3;

    /// <summary>Local banks always show three slots; the level gates how many are usable.</summary>
    public const int LocalSlotCount = 3;

    private static readonly int[] TradeSlotsByLevel = { 2, 4, 8 };
    private static readonly int[] UnlockedSocketsByLevel = { 1, 2, 3 };

    [SerializeField, Range(1, MaxLevel)]
    [Tooltip("1 = 2 trade slots / 1 unlocked item slot, 2 = 4 / 2, 3 = 8 / 3.")]
    private int level = 1;

    public override ItemSocketScope Scope => ItemSocketScope.Local;

    public override int SlotCount => LocalSlotCount;

    public override int UnlockedSlotCount => UnlockedSocketsByLevel[Mathf.Clamp(level, 1, MaxLevel) - 1];

    public int Level => level;

    public int TradeSlots => TradeSlotsByLevel[Mathf.Clamp(level, 1, MaxLevel) - 1];

    /// <summary>
    /// Slots drawn in the Items tab. Always three; use <see cref="ItemSocketBank.UnlockedSlotCount"/>
    /// to tell which of them accept an item at this level.
    /// </summary>
    public int SocketCount => SlotCount;

    private void OnValidate() => level = Mathf.Clamp(level, 1, MaxLevel);

    /// <summary>
    /// Change the warehouse level. A downgrade returns anything in the slots it locks away
    /// to the island pool; an activated item there blocks the downgrade until it is
    /// switched off, rather than being silently torn out of use.
    /// </summary>
    public bool TrySetLevel(int newLevel, out string reason)
    {
        newLevel = Mathf.Clamp(newLevel, 1, MaxLevel);
        if (newLevel == level) { reason = null; return true; }

        int newUnlocked = UnlockedSocketsByLevel[newLevel - 1];

        if (newUnlocked < UnlockedSlotCount)
        {
            IslandItemStorage pool = ResolveItemPool();

            for (int i = newUnlocked; i < UnlockedSlotCount; i++)
            {
                if (IsSocketEmpty(i)) continue;

                if (IsSlotLocked(i))
                {
                    reason = "Local slot " + (i + 1) + " holds an active item. Deactivate it before downgrading.";
                    return false;
                }

                if (!TryUnsocket(i, pool, out reason)) return false;
            }
        }

        level = newLevel;
        RaiseSocketsChanged();

        reason = null;
        return true;
    }

    /// <summary>Back-compatible level setter. Logs and leaves the level alone when a downgrade is blocked.</summary>
    public void SetLevel(int newLevel)
    {
        if (TrySetLevel(newLevel, out string reason)) return;
        Debug.LogWarning("Could not set '" + name + "' to level " + newLevel + ": " + reason, this);
    }

    protected override string DescribeLockedSlot(int slotIndex) =>
        "Local slot " + (slotIndex + 1) + " unlocks at warehouse level " + (slotIndex + 1) + ".";

    // When the warehouse is demolished, hand everything in its slots back to the island
    // pool so nothing is permanently lost.
    private void OnDestroy() => ReturnAllToPool();
}
