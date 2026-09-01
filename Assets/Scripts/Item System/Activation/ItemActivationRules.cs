using UnityEngine;

/// <summary>
/// The one place that decides whether an item may be activated, may stay activated, or
/// may be moved.
///
/// <see cref="CanActivate"/> is the up-front check run when the player presses activate.
/// <see cref="IsStillValid"/> is the same question asked again every frame by
/// <see cref="ItemSocketBank"/>, so an activation whose preconditions have since gone
/// away — the island lost the population that unlocked it, the holding building was
/// demolished — is cancelled instead of quietly continuing to apply.
/// </summary>
public static class ItemActivationRules
{
    /// <summary>Whether this bank's scope accepts the item at all.</summary>
    public static bool FitsScope(ItemData item, ItemSocketScope scope, out string reason)
    {
        if (item == null) { reason = "No item."; return false; }

        if (!item.isSocketable)
        {
            reason = "'" + Describe(item) + "' is a cargo commodity, not a slottable item.";
            return false;
        }

        ItemActivationProfile profile = item.activation;
        if (profile == null) { reason = null; return true; }

        if (!profile.Allows(scope))
        {
            string where = scope == ItemSocketScope.Island ? "Island" : "Local";
            reason = "'" + Describe(item) + "' cannot go in a " + where + " slot.";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// Up-front verification: may the item in this slot be switched on right now?
    /// </summary>
    public static bool CanActivate(ItemSocketBank bank, int slotIndex, out string reason)
    {
        if (bank == null) { reason = "No item bank."; return false; }

        ItemSlotState slot = bank.GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty) { reason = "That slot is empty."; return false; }
        if (slot.IsActivated) { reason = "'" + Describe(slot.Item) + "' is already active."; return false; }
        if (!bank.IsSlotUnlocked(slotIndex)) { reason = "That slot is not unlocked yet."; return false; }

        if (!FitsScope(slot.Item, bank.Scope, out reason)) return false;
        if (!MeetsUnlock(bank, slot.Item, out reason)) return false;
        if (!PassesUniqueness(bank, slotIndex, out reason)) return false;

        ItemActivationProfile profile = slot.Item.activation;
        if (profile != null && profile.IsConsumable && slot.RemainingCharges <= 0)
        {
            reason = "'" + Describe(slot.Item) + "' has no charges left.";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// The recurring check. Same conditions as <see cref="CanActivate"/> minus the ones
    /// that only make sense before switching on (already-active, has-charges — the tick
    /// loop handles charge exhaustion itself).
    /// </summary>
    public static bool IsStillValid(ItemSocketBank bank, int slotIndex, out string reason)
    {
        if (bank == null) { reason = "The item bank no longer exists."; return false; }

        ItemSlotState slot = bank.GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty) { reason = "The slot is empty."; return false; }

        // A downgrade can pull the slot back below the unlocked count while something runs in it.
        if (!bank.IsSlotUnlocked(slotIndex)) { reason = "The slot is no longer unlocked."; return false; }

        if (!FitsScope(slot.Item, bank.Scope, out reason)) return false;
        if (!MeetsUnlock(bank, slot.Item, out reason)) return false;
        if (!PassesUniqueness(bank, slotIndex, out reason)) return false;

        reason = null;
        return true;
    }

    /// <summary>
    /// Whether the item in a slot may be moved, unsocketed or transferred. This is the
    /// rule the whole feature turns on: an activated item is pinned until it is switched
    /// off, expires, or is consumed.
    /// </summary>
    public static bool CanMove(ItemSocketBank bank, int slotIndex, out string reason)
    {
        if (bank == null) { reason = "No item bank."; return false; }

        ItemSlotState slot = bank.GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty) { reason = "That slot is empty."; return false; }

        if (slot.IsLocked)
        {
            reason = "'" + Describe(slot.Item) + "' is active. Deactivate it before moving it.";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>Population gate, shared with the rest of the warehouse panel.</summary>
    private static bool MeetsUnlock(ItemSocketBank bank, ItemData item, out string reason)
    {
        Island island = bank.OwningIsland;
        IslandPopulation population = island != null
            ? (island.Population != null ? island.Population : island.GetComponent<IslandPopulation>())
            : null;

        // No population component means an unpopulated or test island; only ungated items pass.
        if (population == null)
        {
            if (item.unlock.IsUngated) { reason = null; return true; }

            reason = "'" + Describe(item) + "' needs a population this island does not track.";
            return false;
        }

        if (!population.IsUnlocked(item.unlock))
        {
            reason = "'" + Describe(item) + "' is not unlocked on this island yet.";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// Key characters are people: the same one cannot be active in two places on an
    /// island at once. Unique items follow the same rule.
    /// </summary>
    private static bool PassesUniqueness(ItemSocketBank bank, int slotIndex, out string reason)
    {
        ItemSlotState slot = bank.GetSlot(slotIndex);
        ItemData item = slot.Item;

        bool profileIsKeyCharacter = item.activation != null && item.activation.IsKeyCharacter;
        if (!profileIsKeyCharacter && !item.isUnique) { reason = null; return true; }

        Island island = bank.OwningIsland;
        if (island == null) { reason = null; return true; }

        foreach (ItemSocketBank other in island.GetComponentsInChildren<ItemSocketBank>(true))
        {
            for (int i = 0; i < other.SlotCount; i++)
            {
                if (other == bank && i == slotIndex) continue;

                ItemSlotState candidate = other.GetSlot(i);
                if (candidate == null || !candidate.IsActivated) continue;
                if (candidate.Item != item) continue;

                string what = profileIsKeyCharacter ? "is already deployed" : "is already active";
                reason = "'" + Describe(item) + "' " + what + " elsewhere on this island.";
                return false;
            }
        }

        reason = null;
        return true;
    }

    private static string Describe(ItemData item)
    {
        if (item == null) return "item";
        return string.IsNullOrEmpty(item.displayName) ? item.name : item.displayName;
    }
}
