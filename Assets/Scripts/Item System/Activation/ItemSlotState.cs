using System;
using UnityEngine;

/// <summary>
/// Runtime state of one item slot: what is in it, whether it is switched on, and how
/// much life the activation has left.
///
/// "Slot occupied" is not a separate flag — a slot is occupied when it holds an item,
/// and locked (immovable) when that item is activated. Everything here round-trips
/// through <see cref="ItemSlotSaveData"/>.
/// </summary>
[Serializable]
public sealed class ItemSlotState
{
    [SerializeField] private ItemData item;
    [SerializeField] private bool activated;
    [SerializeField] private float remainingSeconds;
    [SerializeField] private int remainingCharges;

    /// <summary>Seconds carried toward the next charge, so partial ticks are not lost.</summary>
    [SerializeField] private float chargeProgressSeconds;

    public ItemData Item => item;
    public bool IsEmpty => item == null;
    public bool IsOccupied => item != null;
    public bool IsActivated => activated;

    /// <summary>An activated item cannot be moved, transferred or unsocketed until it is switched off or destroyed.</summary>
    public bool IsLocked => activated && item != null;

    /// <summary>Seconds left on a timed activation. 0 on an untimed one — check <see cref="HasTimer"/> first.</summary>
    public float RemainingSeconds => remainingSeconds;

    public bool HasTimer => activated && item != null && item.activation != null && item.activation.HasDuration;

    public int RemainingCharges => remainingCharges;

    public void SetItem(ItemData newItem)
    {
        item = newItem;
        activated = false;
        remainingSeconds = 0f;
        chargeProgressSeconds = 0f;
        remainingCharges = newItem != null && newItem.activation != null ? newItem.activation.charges : 0;
    }

    public void Clear()
    {
        item = null;
        activated = false;
        remainingSeconds = 0f;
        chargeProgressSeconds = 0f;
        remainingCharges = 0;
    }

    public void BeginActivation()
    {
        if (item == null) return;

        activated = true;
        chargeProgressSeconds = 0f;

        ItemActivationProfile profile = item.activation;
        if (profile == null) return;

        remainingSeconds = profile.HasDuration ? profile.durationSeconds : 0f;

        // A consumable with no per-charge rate spends one charge up front rather than
        // over time, so a single-use item is used by the act of switching it on.
        if (profile.IsConsumable && profile.secondsPerCharge <= 0f && remainingCharges > 0)
        {
            remainingCharges--;
        }
    }

    public void EndActivation()
    {
        activated = false;
        remainingSeconds = 0f;
        chargeProgressSeconds = 0f;
    }

    /// <summary>
    /// Advances the timer and charge burn. Returns what the bank should do next.
    /// </summary>
    public ItemSlotTickResult Tick(float deltaSeconds)
    {
        if (!activated || item == null || deltaSeconds <= 0f) return ItemSlotTickResult.None;

        ItemActivationProfile profile = item.activation;
        if (profile == null) return ItemSlotTickResult.None;

        if (profile.IsConsumable && profile.secondsPerCharge > 0f)
        {
            chargeProgressSeconds += deltaSeconds;
            while (chargeProgressSeconds >= profile.secondsPerCharge && remainingCharges > 0)
            {
                chargeProgressSeconds -= profile.secondsPerCharge;
                remainingCharges--;
            }

            if (remainingCharges <= 0) return ItemSlotTickResult.Consumed;
        }

        if (profile.HasDuration)
        {
            remainingSeconds -= deltaSeconds;
            if (remainingSeconds <= 0f)
            {
                remainingSeconds = 0f;
                return profile.IsConsumable ? ItemSlotTickResult.Consumed : ItemSlotTickResult.Expired;
            }
        }

        return ItemSlotTickResult.None;
    }

    /// <summary>Spend charges outside the timer, e.g. an explicit "use it now" action.</summary>
    public bool SpendCharges(int amount)
    {
        if (amount <= 0 || remainingCharges < amount) return false;

        remainingCharges -= amount;
        return true;
    }

    public ItemSlotSaveData Capture()
    {
        return new ItemSlotSaveData
        {
            itemId = item != null ? item.Id.FullId : string.Empty,
            activated = activated,
            remainingSeconds = remainingSeconds,
            remainingCharges = remainingCharges,
            chargeProgressSeconds = chargeProgressSeconds,
        };
    }

    public void Restore(ItemSlotSaveData data)
    {
        item = string.IsNullOrEmpty(data.itemId) ? null : ItemCatalog.Resolve(data.itemId);
        activated = item != null && data.activated;
        remainingSeconds = data.remainingSeconds;
        remainingCharges = data.remainingCharges;
        chargeProgressSeconds = data.chargeProgressSeconds;
    }
}

/// <summary>What a slot's timer wants the owning bank to do this frame.</summary>
public enum ItemSlotTickResult
{
    /// <summary>Still running.</summary>
    None = 0,

    /// <summary>Duration ran out on a non-consumable; switch it off but keep the item.</summary>
    Expired = 1,

    /// <summary>Charges ran out, or a consumable's duration ended; destroy the item.</summary>
    Consumed = 2,
}

/// <summary>Save payload for one slot. Items are stored by namespaced id, not by asset reference.</summary>
[Serializable]
public struct ItemSlotSaveData
{
    public string itemId;
    public bool activated;
    public float remainingSeconds;
    public int remainingCharges;
    public float chargeProgressSeconds;
}

/// <summary>Save payload for a whole bank of slots.</summary>
[Serializable]
public struct ItemSocketBankSaveData
{
    public ItemSocketScope scope;
    public ItemSlotSaveData[] slots;
}
