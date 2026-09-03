using System;
using System.Collections.Generic;
using UnityEngine;

public class IslandStorage : Storage
{
    [Header("Settlement goods capacity")]
    [SerializeField, Min(1)] private int baseCapacityPerGood = 50;
    [SerializeField, Min(0)] private int structureCapacity;
    [SerializeField, Min(0)] private int enhancementCapacity;

    private readonly Dictionary<ItemData, int> reserved = new Dictionary<ItemData, int>();

    public event Action CapacityChanged;

    public int CapacityPerGood => Mathf.Max(1, baseCapacityPerGood + structureCapacity + enhancementCapacity);

    public override int GetCapacityLimit() => CapacityPerGood;

    public override bool CanAddItem(ItemData itemData, int quantity)
    {
        return itemData != null && quantity > 0 && GetItemQuantity(itemData) + quantity <= CapacityPerGood;
    }

    public int GetRemainingCapacity(ItemData itemData)
    {
        if (itemData == null) return 0;
        return Mathf.Max(0, CapacityPerGood - GetItemQuantity(itemData));
    }

    public int GetAvailableAmount(ItemData itemData)
    {
        if (itemData == null) return 0;
        reserved.TryGetValue(itemData, out int held);
        return Mathf.Max(0, GetItemQuantity(itemData) - held);
    }

    public bool TryReserve(ItemData itemData, int requested, out int amount)
    {
        amount = Mathf.Min(Mathf.Max(0, requested), GetAvailableAmount(itemData));
        if (amount <= 0) return false;
        reserved[itemData] = GetReservedAmount(itemData) + amount;
        return true;
    }

    public bool HasReservation(ItemData itemData, int amount)
    {
        return itemData != null && amount > 0 && GetReservedAmount(itemData) >= amount;
    }

    public bool CommitReservation(ItemData itemData, int amount)
    {
        if (!HasReservation(itemData, amount) || GetItemQuantity(itemData) < amount) return false;
        if (!RemoveItem(itemData, amount)) return false;
        ReduceReservation(itemData, amount);
        return true;
    }

    public void ReleaseReservation(ItemData itemData, int amount)
    {
        if (itemData == null || amount <= 0) return;
        ReduceReservation(itemData, amount);
    }

    public void AddStructureCapacity(int amount)
    {
        if (amount <= 0) return;
        structureCapacity += amount;
        CapacityChanged?.Invoke();
    }

    public void RemoveStructureCapacity(int amount)
    {
        if (amount <= 0) return;
        structureCapacity = Mathf.Max(0, structureCapacity - amount);
        CapacityChanged?.Invoke();
    }

    public void AddEnhancementCapacity(int amount)
    {
        if (amount == 0) return;
        enhancementCapacity = Mathf.Max(0, enhancementCapacity + amount);
        CapacityChanged?.Invoke();
    }

    private int GetReservedAmount(ItemData itemData)
    {
        return itemData != null && reserved.TryGetValue(itemData, out int amount) ? amount : 0;
    }

    private void ReduceReservation(ItemData itemData, int amount)
    {
        if (!reserved.TryGetValue(itemData, out int held)) return;
        held -= amount;
        if (held <= 0) reserved.Remove(itemData);
        else reserved[itemData] = held;
    }

    public override void AddItem(ItemData itemData, int quantity)
    {
        TryAddItem(itemData, quantity);
    }
}
