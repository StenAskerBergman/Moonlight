using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Compatibility adapter between the ResourceType-based production pipeline and the
/// settlement's authoritative ItemData inventory. It owns reservations and mappings,
/// never a second set of goods quantities.
/// </summary>
[DisallowMultipleComponent]
public sealed class IslandResourceStorage : MonoBehaviour
{
    [System.Serializable]
    public struct ResourceEntry
    {
        public ItemEnums.ResourceType resource;
        public ItemData item;
        public int amount;
    }

    [SerializeField] private List<ResourceEntry> entries = new List<ResourceEntry>();

    private readonly Dictionary<ItemEnums.ResourceType, ItemData> itemByResource =
        new Dictionary<ItemEnums.ResourceType, ItemData>();
    private readonly Dictionary<ItemEnums.ResourceType, int> reserved =
        new Dictionary<ItemEnums.ResourceType, int>();

    private Inventory goods;

    public IReadOnlyList<ResourceEntry> Entries => entries;

    private void Awake()
    {
        goods = GetComponent<Inventory>();
        if (goods == null)
        {
            Debug.LogError($"{name}: IslandResourceStorage requires the island's authoritative Inventory.", this);
            return;
        }

        foreach (ResourceEntry entry in entries)
        {
            if (entry.item != null) RegisterItemDefinition(entry.resource, entry.item);
        }

        RegisterMappingsFromStockpile();
        RefreshInspectorEntries();
    }

    private void OnEnable()
    {
        if (goods == null) goods = GetComponent<Inventory>();
        if (goods != null) goods.OnInventoryChanged += HandleGoodsChanged;
    }

    private void OnDisable()
    {
        if (goods != null) goods.OnInventoryChanged -= HandleGoodsChanged;
        reserved.Clear();
    }

    public bool RegisterItemDefinition(ItemEnums.ResourceType resource, ItemData item)
    {
        if (resource == ItemEnums.ResourceType.None || item == null) return false;
        if (item.HasResourceType && item.ResourceType != resource)
        {
            Debug.LogError($"{name}: '{item.name}' maps to {item.ResourceType}, not {resource}.", item);
            return false;
        }

        if (itemByResource.TryGetValue(resource, out ItemData existing) && existing != null && existing != item)
        {
            Debug.LogError($"{name}: both '{existing.name}' and '{item.name}' map to {resource}.", this);
            return false;
        }

        itemByResource[resource] = item;
        ItemCatalog.Register(item);
        RefreshInspectorEntries();
        return true;
    }

    public bool TryGetItemDefinition(ItemEnums.ResourceType resource, out ItemData item)
    {
        if (itemByResource.TryGetValue(resource, out item) && item != null) return true;

        RegisterMappingsFromStockpile();
        return itemByResource.TryGetValue(resource, out item) && item != null;
    }

    public int GetAmount(ItemEnums.ResourceType resource)
    {
        return goods != null && TryGetItemDefinition(resource, out ItemData item)
            ? goods.GetItemAmount(item)
            : 0;
    }

    public int GetAvailableAmount(ItemEnums.ResourceType resource)
    {
        return Mathf.Max(0, GetAmount(resource) - GetReservedAmount(resource));
    }

    public bool TryReserve(ItemEnums.ResourceType resource, int requested, out int reservation)
    {
        reservation = Mathf.Min(Mathf.Max(0, requested), GetAvailableAmount(resource));
        if (reservation <= 0) return false;
        reserved[resource] = GetReservedAmount(resource) + reservation;
        return true;
    }

    public bool HasReservation(ItemEnums.ResourceType resource, int reservation)
    {
        return reservation > 0 && GetReservedAmount(resource) >= reservation && GetAmount(resource) >= reservation;
    }

    public bool CommitReservation(ItemEnums.ResourceType resource, int reservation)
    {
        if (!HasReservation(resource, reservation) ||
            !TryGetItemDefinition(resource, out ItemData item) ||
            goods == null ||
            !goods.RemoveItem(item, reservation))
        {
            return false;
        }

        ReduceReservation(resource, reservation);
        return true;
    }

    public void ReleaseReservation(ItemEnums.ResourceType resource, int reservation)
    {
        if (reservation <= 0) return;
        ReduceReservation(resource, reservation);
    }

    public bool CanAdd(IReadOnlyDictionary<ItemEnums.ResourceType, int> cargo)
    {
        if (goods == null || cargo == null || cargo.Count == 0) return false;
        foreach (KeyValuePair<ItemEnums.ResourceType, int> entry in cargo)
        {
            if (entry.Value <= 0 ||
                !TryGetItemDefinition(entry.Key, out ItemData item) ||
                !goods.CanAdd(item, entry.Value))
            {
                return false;
            }
        }
        return true;
    }

    public bool TryAdd(IReadOnlyDictionary<ItemEnums.ResourceType, int> cargo)
    {
        if (!TryTranslate(cargo, out Dictionary<ItemData, int> translated)) return false;
        return goods != null && goods.TryAddItems(translated);
    }

    public bool TryRemove(IReadOnlyDictionary<ItemEnums.ResourceType, int> cargo)
    {
        if (!TryTranslate(cargo, out Dictionary<ItemData, int> translated)) return false;
        return goods != null && goods.TryRemoveItems(translated);
    }

    // Compatibility for older callers. New transfer code must observe TryAdd's result.
    public void Add(IReadOnlyDictionary<ItemEnums.ResourceType, int> cargo)
    {
        if (!TryAdd(cargo))
        {
            Debug.LogWarning($"{name}: settlement stockpile rejected logistics cargo.", this);
        }
    }

    private bool TryTranslate(
        IReadOnlyDictionary<ItemEnums.ResourceType, int> cargo,
        out Dictionary<ItemData, int> translated)
    {
        translated = new Dictionary<ItemData, int>();
        if (cargo == null || cargo.Count == 0) return false;

        foreach (KeyValuePair<ItemEnums.ResourceType, int> entry in cargo)
        {
            if (entry.Value <= 0 || !TryGetItemDefinition(entry.Key, out ItemData item)) return false;
            translated.TryGetValue(item, out int current);
            translated[item] = current + entry.Value;
        }
        return true;
    }

    private void RegisterMappingsFromStockpile()
    {
        if (goods == null) return;
        foreach (KeyValuePair<ItemData, int> entry in goods.GetAllItems())
        {
            ItemData item = entry.Key;
            if (item != null && item.HasResourceType)
            {
                RegisterItemDefinition(item.ResourceType, item);
            }
        }
    }

    private int GetReservedAmount(ItemEnums.ResourceType resource)
    {
        return reserved.TryGetValue(resource, out int amount) ? amount : 0;
    }

    private void ReduceReservation(ItemEnums.ResourceType resource, int amount)
    {
        if (!reserved.TryGetValue(resource, out int held)) return;
        held -= amount;
        if (held <= 0) reserved.Remove(resource);
        else reserved[resource] = held;
    }

    private void HandleGoodsChanged()
    {
        RegisterMappingsFromStockpile();
        RefreshInspectorEntries();
    }

    private void RefreshInspectorEntries()
    {
        entries.Clear();
        foreach (KeyValuePair<ItemEnums.ResourceType, ItemData> mapping in itemByResource)
        {
            entries.Add(new ResourceEntry
            {
                resource = mapping.Key,
                item = mapping.Value,
                amount = goods != null ? goods.GetItemAmount(mapping.Value) : 0
            });
        }
        entries.Sort((a, b) => a.resource.CompareTo(b.resource));
    }
}
