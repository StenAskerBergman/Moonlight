using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves a namespaced item id back to its <see cref="ItemData"/> asset, so slot save
/// data can reference items by id instead of by asset reference.
///
/// Builds its index from everything under a Resources folder on first use. Assets kept
/// outside Resources will not be found that way, so anything that already holds an
/// ItemData reference (island stock, starter items) registers it via
/// <see cref="Register"/>, and the index picks it up from there.
/// </summary>
public static class ItemCatalog
{
    private static readonly Dictionary<string, ItemData> byId = new Dictionary<string, ItemData>();
    private static bool scannedResources;

    /// <summary>Adds an item to the index. Safe to call repeatedly with the same asset.</summary>
    public static void Register(ItemData item)
    {
        if (item == null) return;

        string id = item.Id.FullId;
        if (string.IsNullOrEmpty(id)) return;

        if (byId.TryGetValue(id, out ItemData existing) && existing != null && existing != item)
        {
            Debug.LogWarning(
                $"Two ItemData assets share the id '{id}': '{existing.name}' and '{item.name}'. " +
                "Saved slots referencing this id will resolve to whichever loaded first.", item);
            return;
        }

        byId[id] = item;
    }

    public static void RegisterAll(IEnumerable<ItemData> items)
    {
        if (items == null) return;
        foreach (ItemData item in items) Register(item);
    }

    /// <summary>Looks up an item by its full namespaced id, or null when nothing matches.</summary>
    public static ItemData Resolve(string fullId)
    {
        if (string.IsNullOrEmpty(fullId)) return null;

        EnsureResourcesScanned();

        if (byId.TryGetValue(fullId, out ItemData item) && item != null) return item;

        Debug.LogWarning(
            $"No ItemData found for id '{fullId}'. The slot holding it will load empty. " +
            "Move the asset under a Resources folder, or register it at startup via ItemCatalog.Register.");
        return null;
    }

    public static ItemData Resolve(Identifier id) => Resolve(id.FullId);

    /// <summary>Drops the index. Editor domain reloads and test teardown want this.</summary>
    public static void Clear()
    {
        byId.Clear();
        scannedResources = false;
    }

    private static void EnsureResourcesScanned()
    {
        if (scannedResources) return;
        scannedResources = true;

        foreach (ItemData item in Resources.LoadAll<ItemData>(string.Empty))
        {
            Register(item);
        }
    }
}
