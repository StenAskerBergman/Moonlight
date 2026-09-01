using UnityEngine;

/// <summary>
/// One end of a transfer. Wraps the three stores that can hold items — the island's
/// commodity <see cref="Inventory"/>, its <see cref="IslandItemStorage"/> pool of
/// slottable items, and a vessel's <see cref="UnitInventory"/> — behind a common
/// take/give pair, so <see cref="ItemMover"/> does not need an overload per pairing.
/// </summary>
public struct ItemEndpoint
{
    private readonly Inventory inventory;
    private readonly UnitInventory unitInventory;
    private readonly IslandItemStorage itemPool;
    private readonly string label;

    private ItemEndpoint(Inventory inventory, UnitInventory unitInventory, IslandItemStorage itemPool, string label)
    {
        this.inventory = inventory;
        this.unitInventory = unitInventory;
        this.itemPool = itemPool;
        this.label = label;
    }

    public static ItemEndpoint From(Inventory inventory) =>
        new ItemEndpoint(inventory, null, null, inventory != null ? inventory.name : "inventory");

    public static ItemEndpoint From(UnitInventory unitInventory) =>
        new ItemEndpoint(null, unitInventory, null, unitInventory != null ? unitInventory.name : "vessel");

    public static ItemEndpoint From(IslandItemStorage itemPool) =>
        new ItemEndpoint(null, null, itemPool, itemPool != null ? itemPool.name : "island items");

    /// <summary>Picks a unit's item store, preferring its slot-based UnitInventory.</summary>
    public static ItemEndpoint From(Unit unit)
    {
        if (unit == null) return default(ItemEndpoint);
        if (unit.unitInventory != null) return From(unit.unitInventory);
        if (unit.inventory != null) return From(unit.inventory);
        return default(ItemEndpoint);
    }

    public bool IsValid => inventory != null || unitInventory != null || itemPool != null;

    public string Label => string.IsNullOrEmpty(label) ? "store" : label;

    public int GetAmount(ItemData item)
    {
        if (item == null) return 0;
        if (inventory != null) return inventory.GetItemAmount(item);
        if (itemPool != null) return itemPool.GetAmount(item);

        if (unitInventory != null)
        {
            var all = unitInventory.GetAllItems();
            int amount;
            return all != null && all.TryGetValue(item, out amount) ? amount : 0;
        }

        return 0;
    }

    public bool CanTake(ItemData item, int quantity)
    {
        if (item == null || quantity <= 0) return false;
        if (inventory != null) return inventory.CanRemove(item, quantity);
        return GetAmount(item) >= quantity;
    }

    public bool CanGive(ItemData item, int quantity)
    {
        if (item == null || quantity <= 0) return false;
        if (inventory != null) return inventory.CanAdd(item, quantity);

        // UnitInventory and the island item pool report capacity only on the attempt
        // itself, so an optimistic yes here is corrected by the rollback in ItemMover.
        return true;
    }

    public bool Take(ItemData item, int quantity)
    {
        if (item == null || quantity <= 0) return false;
        if (inventory != null) return inventory.RemoveItem(item, quantity);
        if (unitInventory != null) return unitInventory.RemoveItem(item, quantity);
        if (itemPool != null) return itemPool.Remove(item, quantity);
        return false;
    }

    public bool Give(ItemData item, int quantity)
    {
        if (item == null || quantity <= 0) return false;

        if (inventory != null)
        {
            if (!inventory.CanAdd(item, quantity)) return false;
            inventory.AddItem(item, quantity);
            return true;
        }

        if (unitInventory != null) return unitInventory.AddItem(item, quantity, "ItemMover");
        if (itemPool != null) { itemPool.Add(item, quantity); return true; }
        return false;
    }
}

/// <summary>
/// Every way an item changes hands: between slots, in and out of the island pool, and
/// between stores — one vessel to another, or a vessel to the island it is docked at.
///
/// Nothing here bypasses the activation lock. An activated item is pinned in its slot,
/// so every slot-to-anywhere move asks <see cref="ItemActivationRules.CanMove"/> first.
/// </summary>
public static class ItemMover
{
    #region Slot to slot

    /// <summary>
    /// Move an item from one slot to another. The banks may be the same one (reordering
    /// slots), two banks on one building, or banks on different buildings entirely —
    /// which is how an item moves between buildings.
    /// </summary>
    public static bool TryMoveBetweenSlots(
        ItemSocketBank fromBank, int fromIndex,
        ItemSocketBank toBank, int toIndex,
        out string reason)
    {
        if (fromBank == null || toBank == null) { reason = "Missing item bank."; return false; }
        if (fromBank == toBank && fromIndex == toIndex) { reason = null; return true; }

        if (!ItemActivationRules.CanMove(fromBank, fromIndex, out reason)) return false;

        ItemSlotState source = fromBank.GetSlot(fromIndex);
        ItemSlotState target = toBank.GetSlot(toIndex);

        if (target == null) { reason = "Target slot " + toIndex + " does not exist."; return false; }
        if (!toBank.IsSlotUnlocked(toIndex)) { reason = "That slot is not unlocked yet."; return false; }
        if (target.IsOccupied) { reason = "That slot is already occupied."; return false; }

        ItemData item = source.Item;
        if (!ItemActivationRules.FitsScope(item, toBank.Scope, out reason)) return false;

        source.Clear();
        target.SetItem(item);

        fromBank.NotifyChanged();
        if (toBank != fromBank) toBank.NotifyChanged();

        reason = null;
        return true;
    }

    /// <summary>Take an item out of the island pool and put it in a slot.</summary>
    public static bool TryMoveFromPool(ItemSocketBank bank, int slotIndex, ItemData item, out string reason)
    {
        if (bank == null) { reason = "No item bank."; return false; }
        return bank.TrySocket(slotIndex, item, bank.ResolveItemPool(), out reason);
    }

    /// <summary>Return the item in a slot to the island pool.</summary>
    public static bool TryMoveToPool(ItemSocketBank bank, int slotIndex, out string reason)
    {
        if (bank == null) { reason = "No item bank."; return false; }
        return bank.TryUnsocket(slotIndex, bank.ResolveItemPool(), out reason);
    }

    /// <summary>Drop an item into the first free unlocked slot of a bank.</summary>
    public static bool TryMoveToFirstFreeSlot(ItemSocketBank bank, ItemData item, out string reason)
    {
        if (bank == null) { reason = "No item bank."; return false; }

        int free = bank.FirstFreeSlot();
        if (free < 0) { reason = "No free slot on " + bank.name + "."; return false; }

        return bank.TrySocket(free, item, bank.ResolveItemPool(), out reason);
    }

    #endregion

    #region Store to store

    /// <summary>
    /// Move goods or items between two stores — vessel to vessel, vessel to island, or
    /// between two stores on the same island. Rolls the take back if the give fails, so a
    /// full destination never eats the cargo.
    /// </summary>
    public static bool TryTransfer(ItemEndpoint from, ItemEndpoint to, ItemData item, int quantity, out string reason)
    {
        if (!from.IsValid || !to.IsValid) { reason = "One side of the transfer has no store."; return false; }
        if (item == null) { reason = "No item to transfer."; return false; }
        if (quantity <= 0) { reason = "Nothing to transfer."; return false; }

        int available = from.GetAmount(item);
        if (available < quantity)
        {
            reason = from.Label + " has only " + available + " of " + Describe(item) + ".";
            return false;
        }

        if (!from.CanTake(item, quantity)) { reason = from.Label + " will not release that."; return false; }
        if (!to.CanGive(item, quantity)) { reason = to.Label + " has no room for that."; return false; }

        if (!from.Take(item, quantity)) { reason = "Could not take from " + from.Label + "."; return false; }

        if (!to.Give(item, quantity))
        {
            // Put it back rather than destroying it.
            from.Give(item, quantity);
            reason = to.Label + " has no room for that.";
            return false;
        }

        ItemCatalog.Register(item);
        reason = null;
        return true;
    }

    /// <summary>Transfer everything of one item that the source holds.</summary>
    public static bool TryTransferAll(ItemEndpoint from, ItemEndpoint to, ItemData item, out string reason)
    {
        int amount = from.IsValid ? from.GetAmount(item) : 0;
        if (amount <= 0) { reason = "Nothing to transfer."; return false; }

        return TryTransfer(from, to, item, amount, out reason);
    }

    /// <summary>
    /// Move an item from a slot into a store — loading a vessel from a warehouse slot,
    /// for instance. Blocked while the item is activated.
    /// </summary>
    public static bool TryTransferFromSlot(ItemSocketBank bank, int slotIndex, ItemEndpoint to, out string reason)
    {
        if (bank == null) { reason = "No item bank."; return false; }
        if (!to.IsValid) { reason = "Nowhere to transfer to."; return false; }

        if (!ItemActivationRules.CanMove(bank, slotIndex, out reason)) return false;

        ItemSlotState slot = bank.GetSlot(slotIndex);
        ItemData item = slot.Item;

        if (!to.CanGive(item, 1)) { reason = to.Label + " has no room for that."; return false; }

        slot.Clear();

        if (!to.Give(item, 1))
        {
            slot.SetItem(item);
            reason = to.Label + " has no room for that.";
            return false;
        }

        bank.NotifyChanged();

        reason = null;
        return true;
    }

    /// <summary>Move an item out of a store and into a bank's first free slot.</summary>
    public static bool TryTransferToSlot(ItemEndpoint from, ItemSocketBank bank, int slotIndex, ItemData item, out string reason)
    {
        if (bank == null) { reason = "No item bank."; return false; }
        if (!from.IsValid) { reason = "Nowhere to transfer from."; return false; }
        if (item == null) { reason = "No item to transfer."; return false; }

        ItemSlotState slot = bank.GetSlot(slotIndex);
        if (slot == null) { reason = "Slot " + slotIndex + " does not exist."; return false; }
        if (!bank.IsSlotUnlocked(slotIndex)) { reason = "That slot is not unlocked yet."; return false; }
        if (slot.IsOccupied) { reason = "That slot is already occupied."; return false; }

        if (!ItemActivationRules.FitsScope(item, bank.Scope, out reason)) return false;
        if (!from.Take(item, 1)) { reason = from.Label + " has none of that."; return false; }

        slot.SetItem(item);
        ItemCatalog.Register(item);
        bank.NotifyChanged();

        reason = null;
        return true;
    }

    #endregion

    private static string Describe(ItemData item)
    {
        if (item == null) return "item";
        return string.IsNullOrEmpty(item.displayName) ? item.name : item.displayName;
    }
}
