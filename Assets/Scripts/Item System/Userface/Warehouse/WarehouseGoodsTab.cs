using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Goods tab: the island-wide stockpile of normal cargo commodities — fish, tools,
/// concrete, building modules, production inputs. The selected tier tab filters which
/// goods are listed; goods whose population requirement isn't met yet are shown locked
/// rather than hidden, so the next band is visible.
///
/// Reads <see cref="Inventory"/> only. Socketable items and trade orders are separate
/// datasets and never appear here.
/// </summary>
public sealed class WarehouseGoodsTab : WarehousePanelTab
{
    [Tooltip("Every commodity that can appear in the grid. Goods absent from the island's stock still render (at 0) when their tier tab is active, matching Anno's fixed goods layout.")]
    [SerializeField] private List<ItemData> knownGoods = new List<ItemData>();

    public override string TabLabel => "GOODS";

    private Inventory boundInventory;

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    public override void Rebuild()
    {
        Subscribe();

        if (Context == null || Context.Goods == null)
        {
            HideUnusedSlots(0);
            return;
        }

        Inventory inventory = Context.Goods;
        IReadOnlyDictionary<ItemData, int> stock = inventory.GetAllItems();
        int capacity = inventory.GetCapacityLimit();
        int currentPopulation = ActiveTierPopulation;

        int used = 0;

        foreach (ItemData good in EnumerateGoodsForActiveTab(stock))
        {
            WarehouseSlotView slot = TakeSlot(used, null);
            if (slot == null) break;

            if (Context.IsUnlocked(good.unlock))
            {
                stock.TryGetValue(good, out int amount);
                slot.SetGood(good, amount, capacity);
            }
            else
            {
                slot.SetLockedGood(good, good.unlock, currentPopulation);
            }

            used++;
        }

        HideUnusedSlots(used);
    }

    // Union of the authored goods list and whatever the island actually holds, so a
    // commodity that arrives without being registered in knownGoods still shows up
    // rather than silently vanishing from the player's stockpile view.
    private IEnumerable<ItemData> EnumerateGoodsForActiveTab(IReadOnlyDictionary<ItemData, int> stock)
    {
        HashSet<ItemData> seen = new HashSet<ItemData>();

        foreach (ItemData good in knownGoods)
        {
            if (good == null || good.isSocketable) continue;
            if (!seen.Add(good)) continue;
            if (!ListsOnActiveTier(good.unlock)) continue;
            yield return good;
        }

        foreach (KeyValuePair<ItemData, int> entry in stock)
        {
            ItemData good = entry.Key;
            if (good == null || good.isSocketable) continue;
            if (!seen.Add(good)) continue;
            if (!ListsOnActiveTier(good.unlock)) continue;
            yield return good;
        }
    }

    private void Subscribe()
    {
        Inventory inventory = Context?.Goods;
        if (boundInventory == inventory) return;

        Unsubscribe();
        boundInventory = inventory;

        if (boundInventory != null)
        {
            boundInventory.OnInventoryChanged += OnInventoryChanged;
        }
    }

    private void Unsubscribe()
    {
        if (boundInventory == null) return;

        boundInventory.OnInventoryChanged -= OnInventoryChanged;
        boundInventory = null;
    }

    // Stock changes constantly from production and logistics, so the grid refreshes
    // off the inventory event rather than polling every frame.
    private void OnInventoryChanged()
    {
        if (isActiveAndEnabled) Rebuild();
    }
}
