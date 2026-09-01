using UnityEngine;

/// <summary>
/// The island's three item slots. An item activated here applies to every building on
/// the island, as opposed to <see cref="WarehouseSockets"/>, whose slots apply only to
/// the building holding them.
///
/// Sits on the Island alongside <see cref="IslandItemStorage"/>. The Items tab draws
/// this bank as the "Island" row and the selected building's bank as the "Local" row.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(IslandItemStorage))]
public sealed class IslandItemSockets : ItemSocketBank
{
    public const int IslandSlotCount = 3;

    [SerializeField, Range(0, IslandSlotCount)]
    [Tooltip("How many of the three island slots are usable. Raise this as the island develops; slots above it render locked.")]
    private int unlockedSlots = IslandSlotCount;

    public override ItemSocketScope Scope => ItemSocketScope.Island;

    public override int SlotCount => IslandSlotCount;

    public override int UnlockedSlotCount => Mathf.Clamp(unlockedSlots, 0, IslandSlotCount);

    /// <summary>
    /// Changes how many island slots are usable. Anything sitting in a slot that is being
    /// locked away is returned to the island pool first, so nothing is stranded — except
    /// an activated item, which pins the change until it is switched off.
    /// </summary>
    public bool TrySetUnlockedSlots(int count, out string reason)
    {
        count = Mathf.Clamp(count, 0, IslandSlotCount);
        if (count == UnlockedSlotCount) { reason = null; return true; }

        if (count < UnlockedSlotCount)
        {
            IslandItemStorage pool = ResolveItemPool();

            for (int i = count; i < UnlockedSlotCount; i++)
            {
                if (IsSocketEmpty(i)) continue;

                if (IsSlotLocked(i))
                {
                    reason = "Island slot " + (i + 1) + " holds an active item. Deactivate it first.";
                    return false;
                }

                if (!TryUnsocket(i, pool, out reason)) return false;
            }
        }

        unlockedSlots = count;
        RaiseSocketsChanged();

        reason = null;
        return true;
    }

    private void OnValidate() => unlockedSlots = Mathf.Clamp(unlockedSlots, 0, IslandSlotCount);

    protected override string DescribeLockedSlot(int slotIndex) =>
        "Island slot " + (slotIndex + 1) + " is not unlocked yet.";

    private void OnDestroy() => ReturnAllToPool();
}
