using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandStorageManager : StorageManager
{
    public IslandStorage islandStorage { get; private set; } // Making it publicly readable but only settable within the class.

    public IslandStorageManager(Storage storage) : base(storage)
    {
        this.islandStorage = islandStorage;
    }

    // Unity Constructor
    private void Awake()
    {
        islandStorage = GetComponent<IslandStorage>();
        if (!islandStorage)
        {
            islandStorage = gameObject.AddComponent<IslandStorage>();
        }
        if (!storage)
        {
            storage = islandStorage;
        }
    }
    /// <summary>
    /// Returns the quantity of this commodity legally available for export,
    /// strictly respecting the island's protected minimum stock (IslandTradeRules.MinStockToRetain).
    /// </summary>
    public int GetAvailableForExport(ItemData item)
    {
        if (item == null) return 0;
        int currentStock = GetItemQuantity(item);
        IslandTradeRules rules = GetComponent<IslandTradeRules>();
        int reserve = (rules != null) ? Mathf.Max(0, rules.GetRule(item).MinStockToRetain) : 0;
        return Mathf.Max(0, currentStock - reserve);
    }

    /// <summary>
    /// Returns the remaining storage capacity for this item on the island.
    /// </summary>
    public int GetRemainingCapacity(ItemData item)
    {
        if (item == null) return 0;
        int limit = GetCapacityLimit();
        if (limit <= 0) return 9999;
        int currentStock = GetItemQuantity(item);
        return Mathf.Max(0, limit - currentStock);
    }
}

