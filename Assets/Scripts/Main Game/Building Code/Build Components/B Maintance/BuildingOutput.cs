using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BuildingOutput : MonoBehaviour
{
    private Building building;

    [Header("Local Output Storage")]
    [SerializeField, Min(1)] private int outputCapacity = 30;
    [SerializeField, Min(1)] private int pickupLoadSize = 15;

    public Dictionary<ItemEnums.ResourceType, int> PendingOutput { get; private set; } = new Dictionary<ItemEnums.ResourceType, int>();
    private readonly Dictionary<ItemEnums.ResourceType, int> reservedOutput = new Dictionary<ItemEnums.ResourceType, int>();
    private readonly Dictionary<ItemEnums.ResourceType, ItemData> itemDefinitions = new Dictionary<ItemEnums.ResourceType, ItemData>();

    public int OutputCapacity => outputCapacity;
    public int PickupLoadSize => pickupLoadSize;
    public int StoredAmount => PendingOutput.Values.Sum();
    public int ReservedAmount => reservedOutput.Values.Sum();
    public int AvailableAmount => Mathf.Max(0, StoredAmount - ReservedAmount);
    public bool IsFull => StoredAmount >= outputCapacity;
    public bool IsPickupReady => AvailableAmount >= pickupLoadSize || (IsFull && AvailableAmount > 0);

    public static event Action<Building, ItemEnums.ResourceType, int> OnOutputReady;

    private void Awake()
    {
        building = GetComponent<Building>();
    }

    public void AddOutput(ItemEnums.ResourceType resource, int amount)
    {
        if (amount <= 0) return;
        if (building == null || building.CurrentState != BuildingEnums.BuildingState.Active) return;

        int accepted = Mathf.Min(amount, Mathf.Max(0, outputCapacity - StoredAmount));
        if (accepted <= 0) return;

        if (PendingOutput.ContainsKey(resource))
        {
            PendingOutput[resource] += accepted;
        }
        else
        {
            PendingOutput[resource] = accepted;
        }

        OnOutputReady?.Invoke(building, resource, accepted);
    }

    public void RegisterItemDefinition(ItemEnums.ResourceType resource, ItemData item)
    {
        if (item == null || resource == ItemEnums.ResourceType.None) return;
        if (item.HasResourceType && item.ResourceType != resource)
        {
            Debug.LogError($"{name}: '{item.name}' maps to {item.ResourceType}, not {resource}.", item);
            return;
        }
        itemDefinitions[resource] = item;
        ItemCatalog.Register(item);
    }

    public ItemData GetItemDefinition(ItemEnums.ResourceType resource)
    {
        itemDefinitions.TryGetValue(resource, out ItemData item);
        return item;
    }

    public bool TryReservePickup(int capacity, out Dictionary<ItemEnums.ResourceType, int> reservation, bool allowPartial = false)
    {
        reservation = new Dictionary<ItemEnums.ResourceType, int>();
        int remaining = Mathf.Max(0, capacity);
        if (remaining == 0 || (!allowPartial && !IsPickupReady) || AvailableAmount <= 0) return false;

        foreach (var entry in PendingOutput)
        {
            reservedOutput.TryGetValue(entry.Key, out int alreadyReserved);
            int available = Mathf.Max(0, entry.Value - alreadyReserved);
            int amount = Mathf.Min(available, remaining);
            if (amount <= 0) continue;

            reservation[entry.Key] = amount;
            reservedOutput[entry.Key] = alreadyReserved + amount;
            remaining -= amount;
            if (remaining == 0) break;
        }

        return reservation.Count > 0;
    }

    public bool CommitReservation(IReadOnlyDictionary<ItemEnums.ResourceType, int> reservation)
    {
        if (!ReservationMatches(reservation)) return false;

        foreach (var entry in reservation)
        {
            PendingOutput[entry.Key] -= entry.Value;
            reservedOutput[entry.Key] -= entry.Value;
            if (PendingOutput[entry.Key] <= 0) PendingOutput.Remove(entry.Key);
            if (reservedOutput[entry.Key] <= 0) reservedOutput.Remove(entry.Key);
        }

        return true;
    }

    public void ReleaseReservation(IReadOnlyDictionary<ItemEnums.ResourceType, int> reservation)
    {
        if (reservation == null) return;
        foreach (var entry in reservation)
        {
            if (!reservedOutput.TryGetValue(entry.Key, out int amount)) continue;
            amount -= entry.Value;
            if (amount <= 0) reservedOutput.Remove(entry.Key);
            else reservedOutput[entry.Key] = amount;
        }
    }

    public bool ReservationMatches(IReadOnlyDictionary<ItemEnums.ResourceType, int> reservation)
    {
        if (reservation == null || reservation.Count == 0) return false;
        foreach (var entry in reservation)
        {
            if (entry.Value <= 0 || !PendingOutput.TryGetValue(entry.Key, out int stored) || stored < entry.Value) return false;
            if (!reservedOutput.TryGetValue(entry.Key, out int reserved) || reserved < entry.Value) return false;
        }
        return true;
    }

    public Dictionary<ItemEnums.ResourceType, int> CollectOutput()
    {
        Dictionary<ItemEnums.ResourceType, int> collected = new Dictionary<ItemEnums.ResourceType, int>();
        foreach (var entry in PendingOutput)
        {
            reservedOutput.TryGetValue(entry.Key, out int reserved);
            int available = Mathf.Max(0, entry.Value - reserved);
            if (available > 0) collected[entry.Key] = available;
        }
        foreach (var entry in collected)
        {
            PendingOutput[entry.Key] -= entry.Value;
            if (PendingOutput[entry.Key] <= 0) PendingOutput.Remove(entry.Key);
        }
        return collected;
    }
}
