using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resource-count view of an island's shared stockpile. This is intentionally
/// independent of warehouse ownership; every warehouse deposits into the same
/// storage. A future inventory UI can read Entries without changing logistics.
/// </summary>
[DisallowMultipleComponent]
public sealed class IslandResourceStorage : MonoBehaviour
{
    [System.Serializable]
    public struct ResourceEntry
    {
        public ItemEnums.ResourceType resource;
        public int amount;
    }

    [SerializeField] private List<ResourceEntry> entries = new List<ResourceEntry>();
    private readonly Dictionary<ItemEnums.ResourceType, int> amounts = new Dictionary<ItemEnums.ResourceType, int>();
    private readonly Dictionary<ItemEnums.ResourceType, int> reserved = new Dictionary<ItemEnums.ResourceType, int>();

    public IReadOnlyList<ResourceEntry> Entries => entries;

    private void Awake()
    {
        amounts.Clear();
        foreach (ResourceEntry entry in entries)
        {
            if (entry.amount > 0) amounts[entry.resource] = entry.amount;
        }
        RefreshInspectorEntries();
    }

    public int GetAmount(ItemEnums.ResourceType resource) => amounts.TryGetValue(resource, out int amount) ? amount : 0;
    public int GetAvailableAmount(ItemEnums.ResourceType resource)
    {
        reserved.TryGetValue(resource, out int held);
        return Mathf.Max(0, GetAmount(resource) - held);
    }

    public bool TryReserve(ItemEnums.ResourceType resource, int requested, out int reservation)
    {
        reservation = Mathf.Min(Mathf.Max(0, requested), GetAvailableAmount(resource));
        if (reservation <= 0) return false;
        reserved[resource] = GetAmountReserved(resource) + reservation;
        return true;
    }

    public bool CommitReservation(ItemEnums.ResourceType resource, int reservation)
    {
        if (reservation <= 0 || GetAmountReserved(resource) < reservation || GetAmount(resource) < reservation) return false;
        amounts[resource] -= reservation;
        reserved[resource] -= reservation;
        if (amounts[resource] <= 0) amounts.Remove(resource);
        if (reserved[resource] <= 0) reserved.Remove(resource);
        RefreshInspectorEntries();
        return true;
    }

    public void ReleaseReservation(ItemEnums.ResourceType resource, int reservation)
    {
        if (reservation <= 0 || !reserved.TryGetValue(resource, out int held)) return;
        held -= reservation;
        if (held <= 0) reserved.Remove(resource);
        else reserved[resource] = held;
    }

    private int GetAmountReserved(ItemEnums.ResourceType resource) => reserved.TryGetValue(resource, out int amount) ? amount : 0;

    public void Add(IReadOnlyDictionary<ItemEnums.ResourceType, int> cargo)
    {
        if (cargo == null) return;
        foreach (var entry in cargo)
        {
            if (entry.Value <= 0) continue;
            amounts[entry.Key] = GetAmount(entry.Key) + entry.Value;
        }
        RefreshInspectorEntries();
    }

    private void RefreshInspectorEntries()
    {
        entries.Clear();
        foreach (var entry in amounts)
        {
            entries.Add(new ResourceEntry { resource = entry.Key, amount = entry.Value });
        }
        entries.Sort((a, b) => a.resource.CompareTo(b.resource));
    }
}
