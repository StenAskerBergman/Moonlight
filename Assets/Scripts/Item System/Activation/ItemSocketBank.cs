using System;
using UnityEngine;

/// <summary>
/// A bank of item slots that can hold, activate and expire items.
///
/// Two banks exist per island view: <see cref="IslandItemSockets"/> on the Island, whose
/// activated items affect every building on it, and <see cref="WarehouseSockets"/> on a
/// building, whose activated items affect only that building. Both share this logic;
/// only the scope and how many slots are unlocked differ.
///
/// An activated slot is locked: the item in it cannot be moved, unsocketed or transferred
/// until it is deactivated, expires, or is consumed.
/// </summary>
public abstract class ItemSocketBank : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Runtime contents of each slot. Leave empty in the prefab; the bank sizes itself on Awake.")]
    private ItemSlotState[] slots = new ItemSlotState[0];

    /// <summary>Raised when a slot's contents or activation state change.</summary>
    public event Action SocketsChanged;

    /// <summary>Raised when an activation ends on its own — expired or fully consumed.</summary>
    public event Action<int, ItemData, ItemSlotTickResult> ActivationEnded;

    /// <summary>Which bank this is, and therefore what an activated item here affects.</summary>
    public abstract ItemSocketScope Scope { get; }

    /// <summary>Total slots drawn in the UI, including ones not yet unlocked.</summary>
    public virtual int SlotCount => 3;

    /// <summary>How many of those slots currently accept an item. The rest render locked.</summary>
    public virtual int UnlockedSlotCount => SlotCount;

    protected virtual void Awake() => ResizeSlots();

    protected virtual void Update() => TickSlots(Time.deltaTime);

    #region Queries

    public ItemSlotState GetSlot(int slotIndex) =>
        slotIndex >= 0 && slotIndex < slots.Length ? slots[slotIndex] : null;

    public ItemData GetSocketedItem(int slotIndex)
    {
        ItemSlotState slot = GetSlot(slotIndex);
        return slot != null ? slot.Item : null;
    }

    public bool IsSocketEmpty(int slotIndex)
    {
        ItemSlotState slot = GetSlot(slotIndex);
        return slot == null || slot.IsEmpty;
    }

    public bool IsSlotUnlocked(int slotIndex) => slotIndex >= 0 && slotIndex < UnlockedSlotCount;

    /// <summary>True while the slot holds an activated item, which pins it in place.</summary>
    public bool IsSlotLocked(int slotIndex)
    {
        ItemSlotState slot = GetSlot(slotIndex);
        return slot != null && slot.IsLocked;
    }

    public bool IsSlotActivated(int slotIndex)
    {
        ItemSlotState slot = GetSlot(slotIndex);
        return slot != null && slot.IsActivated;
    }

    /// <summary>Index of the first empty unlocked slot, or -1 when the bank is full.</summary>
    public int FirstFreeSlot()
    {
        for (int i = 0; i < UnlockedSlotCount && i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].IsEmpty) return i;
        }
        return -1;
    }

    /// <summary>The island this bank belongs to, whether it sits on the Island or on a building under it.</summary>
    public Island OwningIsland => GetComponentInParent<Island>();

    /// <summary>Island-wide pool that items move to and from when they leave or enter a slot.</summary>
    public IslandItemStorage ResolveItemPool()
    {
        Island island = OwningIsland;
        if (island == null) return null;
        return island.Items != null ? island.Items : island.GetComponent<IslandItemStorage>();
    }

    #endregion

    #region Move in / out

    /// <summary>
    /// Move an item out of the island pool and into a slot. Fails when the slot is taken,
    /// still locked behind the owner's level, or the island has none of that item.
    /// </summary>
    public bool TrySocket(int slotIndex, ItemData item, IslandItemStorage islandItems) =>
        TrySocket(slotIndex, item, islandItems, out _);

    public bool TrySocket(int slotIndex, ItemData item, IslandItemStorage islandItems, out string reason)
    {
        if (item == null) { reason = "No item to slot."; return false; }
        if (islandItems == null) { reason = "This island has no item storage."; return false; }

        ItemSlotState slot = GetSlot(slotIndex);
        if (slot == null) { reason = "Slot " + slotIndex + " does not exist."; return false; }
        if (!IsSlotUnlocked(slotIndex)) { reason = DescribeLockedSlot(slotIndex); return false; }
        if (slot.IsOccupied) { reason = "That slot is already occupied."; return false; }

        if (!ItemActivationRules.FitsScope(item, Scope, out reason)) return false;
        if (!islandItems.Remove(item, 1)) { reason = "No '" + DescribeItem(item) + "' in island storage."; return false; }

        slot.SetItem(item);
        ItemCatalog.Register(item);
        RaiseSocketsChanged();

        reason = null;
        return true;
    }

    /// <summary>Pull an item back out of a slot and return it to the island pool.</summary>
    public bool TryUnsocket(int slotIndex, IslandItemStorage islandItems) =>
        TryUnsocket(slotIndex, islandItems, out _);

    public bool TryUnsocket(int slotIndex, IslandItemStorage islandItems, out string reason)
    {
        if (islandItems == null) { reason = "This island has no item storage."; return false; }

        ItemSlotState slot = GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty) { reason = "That slot is empty."; return false; }

        // The whole point of the lock: an item in use stays where it is.
        if (slot.IsLocked)
        {
            reason = "'" + DescribeItem(slot.Item) + "' is active. Deactivate it before moving it.";
            return false;
        }

        ItemData item = slot.Item;
        slot.Clear();
        islandItems.Add(item, 1);
        RaiseSocketsChanged();

        reason = null;
        return true;
    }

    #endregion

    #region Activate / cancel / consume

    /// <summary>Switch on the item in a slot, locking it in place.</summary>
    public bool TryActivate(int slotIndex, out string reason)
    {
        if (!ItemActivationRules.CanActivate(this, slotIndex, out reason)) return false;

        GetSlot(slotIndex).BeginActivation();
        RaiseSocketsChanged();

        reason = null;
        return true;
    }

    /// <summary>
    /// Cancel an activation early. The item stays in the slot and is movable again;
    /// charges already spent are not refunded.
    /// </summary>
    public bool TryDeactivate(int slotIndex, out string reason)
    {
        ItemSlotState slot = GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty) { reason = "That slot is empty."; return false; }
        if (!slot.IsActivated) { reason = "That item is not active."; return false; }

        slot.EndActivation();

        // A consumable with nothing left is spent, not stored.
        if (slot.Item.activation != null && slot.Item.activation.IsConsumable && slot.RemainingCharges <= 0)
        {
            DestroyItemInSlot(slotIndex, ItemSlotTickResult.Consumed);
        }
        else
        {
            RaiseSocketsChanged();
        }

        reason = null;
        return true;
    }

    /// <summary>Spend charges on a consumable now. Destroys the item when the last charge goes.</summary>
    public bool TryConsume(int slotIndex, int charges, out string reason)
    {
        ItemSlotState slot = GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty) { reason = "That slot is empty."; return false; }

        ItemActivationProfile profile = slot.Item.activation;
        if (profile == null || !profile.IsConsumable)
        {
            reason = "'" + DescribeItem(slot.Item) + "' is not consumable.";
            return false;
        }

        if (!slot.SpendCharges(charges))
        {
            reason = "'" + DescribeItem(slot.Item) + "' has only " + slot.RemainingCharges + " charge(s) left.";
            return false;
        }

        if (slot.RemainingCharges <= 0) DestroyItemInSlot(slotIndex, ItemSlotTickResult.Consumed);
        else RaiseSocketsChanged();

        reason = null;
        return true;
    }

    #endregion

    #region Ticking

    /// <summary>
    /// Runs durations and charge burn, and re-checks that each activation is still legal.
    /// An activation whose preconditions have gone away is cancelled rather than left
    /// running on stale state.
    /// </summary>
    protected void TickSlots(float deltaSeconds)
    {
        if (slots == null || deltaSeconds <= 0f) return;

        bool changed = false;

        for (int i = 0; i < slots.Length; i++)
        {
            ItemSlotState slot = slots[i];
            if (slot == null || !slot.IsActivated) continue;

            string reason;
            if (!ItemActivationRules.IsStillValid(this, i, out reason))
            {
                Debug.Log("Activation of '" + DescribeItem(slot.Item) + "' on '" + name + "' was cancelled: " + reason, this);
                slot.EndActivation();
                changed = true;
                continue;
            }

            ItemSlotTickResult result = slot.Tick(deltaSeconds);
            if (result == ItemSlotTickResult.None) continue;

            ItemData item = slot.Item;

            if (result == ItemSlotTickResult.Consumed)
            {
                // DestroyItemInSlot raises the change event itself.
                DestroyItemInSlot(i, result);
            }
            else
            {
                slot.EndActivation();
                if (ActivationEnded != null) ActivationEnded(i, item, result);
                changed = true;
            }
        }

        if (changed) RaiseSocketsChanged();
    }

    private void DestroyItemInSlot(int slotIndex, ItemSlotTickResult cause)
    {
        ItemSlotState slot = GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty) return;

        ItemData item = slot.Item;
        slot.Clear();

        if (ActivationEnded != null) ActivationEnded(slotIndex, item, cause);
        RaiseSocketsChanged();
    }

    #endregion

    #region Save / load

    public ItemSocketBankSaveData Capture()
    {
        ItemSlotSaveData[] captured = new ItemSlotSaveData[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            captured[i] = slots[i] != null ? slots[i].Capture() : default(ItemSlotSaveData);
        }

        return new ItemSocketBankSaveData { scope = Scope, slots = captured };
    }

    public void Restore(ItemSocketBankSaveData data)
    {
        ResizeSlots();

        if (data.slots == null) return;

        int count = Mathf.Min(data.slots.Length, slots.Length);
        for (int i = 0; i < count; i++)
        {
            slots[i].Restore(data.slots[i]);
        }

        RaiseSocketsChanged();
    }

    #endregion

    protected void RaiseSocketsChanged()
    {
        if (SocketsChanged != null) SocketsChanged();
    }

    /// <summary>
    /// Announce that a slot changed. For callers outside the bank — <see cref="ItemMover"/>
    /// moves items between banks and has to tell both of them afterwards.
    /// </summary>
    public void NotifyChanged() => RaiseSocketsChanged();

    /// <summary>Message shown when a slot exists in the UI but is not unlocked yet.</summary>
    protected virtual string DescribeLockedSlot(int slotIndex) => "That slot is not unlocked yet.";

    protected static string DescribeItem(ItemData item)
    {
        if (item == null) return "item";
        return string.IsNullOrEmpty(item.displayName) ? item.name : item.displayName;
    }

    /// <summary>
    /// Grows or shrinks the backing array to <see cref="SlotCount"/>, keeping what is in
    /// the slots that survive. Callers that shrink must empty the removed slots first.
    /// </summary>
    protected void ResizeSlots()
    {
        int required = Mathf.Max(0, SlotCount);

        if (slots == null) slots = new ItemSlotState[0];
        if (slots.Length != required)
        {
            ItemSlotState[] resized = new ItemSlotState[required];
            int copy = Mathf.Min(required, slots.Length);
            Array.Copy(slots, resized, copy);
            slots = resized;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) slots[i] = new ItemSlotState();
        }
    }

    /// <summary>Empties every slot back into the island pool. Used when the owner is destroyed.</summary>
    protected void ReturnAllToPool()
    {
        if (slots == null) return;

        IslandItemStorage pool = ResolveItemPool();
        if (pool == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            ItemSlotState slot = slots[i];
            if (slot == null || slot.IsEmpty) continue;

            ItemData item = slot.Item;
            slot.Clear();
            pool.Add(item, 1);
        }
    }
}
