using UnityEngine;

/// <summary>
/// Everything a warehouse panel tab needs to render, resolved once per selection by
/// <see cref="WarehousePanelUI"/>. The three datasets stay separate on purpose:
/// commodities live in <see cref="Goods"/>, socketable modifiers in <see cref="Items"/>,
/// and buy/sell orders in <see cref="TradeRules"/>. No tab derives its contents from
/// another tab's data.
/// </summary>
public sealed class WarehousePanelContext
{
    public Building Building { get; }
    public Island Island { get; }

    /// <summary>Island-wide commodity stockpile. Goods tab only.</summary>
    public Inventory Goods { get; }

    /// <summary>Island-wide pool of unsocketed modifiers. Items tab only.</summary>
    public IslandItemStorage Items { get; }

    /// <summary>Island-wide passive buy/sell orders. Trade tab only.</summary>
    public IslandTradeRules TradeRules { get; }

    /// <summary>Population counts backing the tier tabs' unlock states.</summary>
    public IslandPopulation Population { get; }

    /// <summary>Sockets and trade-slot allowance of the selected warehouse.</summary>
    public WarehouseSockets Sockets { get; }

    public Enums.Faction Faction =>
        Population != null ? Population.Faction : Enums.Faction.Tyc;

    public WarehousePanelContext(Building building, Island island)
    {
        Building = building;
        Island = island;

        if (island != null)
        {
            // Island.Initialize resolves these; fall back to GetComponent for an island
            // that hasn't been initialised yet (or was placed by hand in a test scene).
            Goods = island.GetComponent<Inventory>();

            Items = island.Items != null
                ? island.Items
                : island.GetComponent<IslandItemStorage>();

            Population = island.Population != null
                ? island.Population
                : island.GetComponent<IslandPopulation>();

            TradeRules = island.TradeRules != null
                ? island.TradeRules
                : island.GetComponent<IslandTradeRules>();
        }

        if (building != null)
        {
            Sockets = building.GetComponent<WarehouseSockets>();
        }
    }

    public int GetPopulation(Enums.Faction faction, PopulationClass populationClass) =>
        Population != null ? Population.GetPopulation(faction, populationClass) : 0;

    public bool IsUnlocked(PopulationUnlock unlock) =>
        Population == null ? unlock.IsUngated : Population.IsUnlocked(unlock);
}
