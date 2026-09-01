using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Island-wide pool of socketable items — warehouse upgrades, seeds, building and
/// vehicle modifiers. Deliberately a separate stockpile from <see cref="Inventory"/>:
/// these are not cargo commodities and must never be inferred from the goods list.
///
/// Holds only unsocketed stock. Once an item is slotted into a warehouse it moves into
/// that building's <see cref="WarehouseSockets"/> and leaves this pool.
/// </summary>
[DisallowMultipleComponent]
public sealed class IslandItemStorage : MonoBehaviour
{
    [Serializable]
    private struct StartingItem
    {
        public ItemData item;
        [Min(1)] public int amount;
    }

    [Tooltip("Items present on this island at start. Each must have 'Is Socketable' ticked on its ItemData.")]
    [SerializeField] private List<StartingItem> startingItems = new List<StartingItem>();

    private readonly Dictionary<ItemData, int> stock = new Dictionary<ItemData, int>();

    public event Action ItemsChanged;

    private void Awake()
    {
        foreach (StartingItem entry in startingItems)
        {
            if (entry.item == null || entry.amount <= 0) continue;
            Add(entry.item, entry.amount);
        }
    }

    public IReadOnlyDictionary<ItemData, int> GetAllItems() => stock;

    public int GetAmount(ItemData item) =>
        item != null && stock.TryGetValue(item, out int amount) ? amount : 0;

    public void Add(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return;

        if (!item.isSocketable)
        {
            Debug.LogWarning(
                $"'{item.name}' was added to IslandItemStorage but is not marked socketable. " +
                "Commodities belong in Inventory; tick 'Is Socketable' on the ItemData if this is a modifier.",
                this);
        }

        stock[item] = GetAmount(item) + amount;
        ItemsChanged?.Invoke();
    }

    public bool Remove(ItemData item, int amount)
    {
        if (item == null || amount <= 0 || GetAmount(item) < amount) return false;

        int remaining = stock[item] - amount;
        if (remaining > 0) stock[item] = remaining;
        else stock.Remove(item);

        ItemsChanged?.Invoke();
        return true;
    }
}
