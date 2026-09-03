using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Demolishes one building and refunds part of what it cost.
///
/// Placement scatters its bookkeeping across several systems - grid cells get an
/// occupying building, the island keeps a buildings list, the InfluenceManager keeps a
/// zone, the Bank keeps a revenue/expense row. Destroying the GameObject alone would
/// leave every one of those pointing at a dead object, so this reverses each of them in
/// the same order placement set them up.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Building))]
public sealed class BuildingDemolition : MonoBehaviour
{
    [Tooltip("Fraction of the building's item cost returned to the island stockpile. 0.5 = half back.")]
    [SerializeField, Range(0f, 1f)] private float itemRefundFraction = 0.5f;

    [Tooltip("Fraction of the building's currency price returned to the Bank.")]
    [SerializeField, Range(0f, 1f)] private float currencyRefundFraction = 0.5f;

    [Tooltip("Off for buildings that must never be removed by the player, e.g. a scripted objective.")]
    [SerializeField] private bool demolishable = true;

    public bool Demolishable => demolishable;

    /// <summary>
    /// What demolishing would return, without doing it. Used by the confirmation UI so
    /// the player can see the refund before committing.
    /// </summary>
    public Dictionary<ItemData, int> PreviewItemRefund()
    {
        Dictionary<ItemData, int> refund = new Dictionary<ItemData, int>();

        CostData costData = GetCostData();
        if (costData == null) return refund;

        Dictionary<ItemData, int> costs = costData.GetCostItemsDictionary();
        if (costs == null) return refund;

        foreach (KeyValuePair<ItemData, int> entry in costs)
        {
            if (entry.Key == null || entry.Value <= 0) continue;

            // Floor, so a refund fraction can never hand back more than was paid.
            int amount = Mathf.FloorToInt(entry.Value * itemRefundFraction);
            if (amount > 0) refund[entry.Key] = amount;
        }

        return refund;
    }

    public int PreviewCurrencyRefund()
    {
        CostData costData = GetCostData();
        return costData == null ? 0 : Mathf.FloorToInt(costData.price * currencyRefundFraction);
    }

    /// <summary>
    /// Tears the building down and pays the refund. Returns false when the building
    /// refused to be demolished, so a caller marking several can report what survived.
    /// </summary>
    public bool Demolish()
    {
        if (!demolishable)
        {
            Debug.Log($"'{name}' is flagged as not demolishable.", this);
            return false;
        }

        Building building = GetComponent<Building>();
        Island island = GetComponentInParent<Island>();

        PayItemRefund(island);
        PayCurrencyRefund();

        ReleaseGridCells(building, island);
        UnregisterInfluence(island);
        RemoveFromIsland(building, island);
        ClearSelection(building);

        // Components that own external state clean themselves up in OnDestroy - e.g.
        // WarehouseSockets returns its socketed items to the island pool.
        Destroy(gameObject);
        return true;
    }

    #region Refund

    private void PayItemRefund(Island island)
    {
        Dictionary<ItemData, int> refund = PreviewItemRefund();
        if (refund.Count == 0) return;

        Inventory stockpile = island != null ? island.GetComponent<Inventory>() : null;
        if (stockpile == null) return;

        foreach (KeyValuePair<ItemData, int> entry in refund)
        {
            // Respect capacity: a full stockpile drops the overflow rather than
            // silently exceeding the storage limit.
            if (stockpile.CanAdd(entry.Key, entry.Value)) stockpile.AddItem(entry.Key, entry.Value);
        }
    }

    private void PayCurrencyRefund()
    {
        Bank bank = FindObjectOfType<Bank>();
        if (bank == null) return;

        // Off the books first: a demolished building must stop drawing upkeep and
        // stop paying revenue whether or not anything is refunded for it.
        bank.UntrackBuilding(GetComponent<BuildingCost>());

        int refund = PreviewCurrencyRefund();
        if (refund > 0) bank.AddIncome(refund);
    }

    private CostData GetCostData()
    {
        BuildingCost cost = GetComponent<BuildingCost>();
        return cost != null ? cost.costData : null;
    }

    #endregion

    #region Unwind placement

    // Cells are matched by the building they point at rather than by recomputing the
    // footprint: a building that was rotated or whose size changed after placement would
    // otherwise leave cells reserved forever.
    private void ReleaseGridCells(Building building, Island island)
    {
        if (building == null) return;

        GridSystem grid = null;

        BuildingProperties properties = GetComponent<BuildingProperties>();
        if (properties != null) grid = properties.gridSystem;

        if (grid == null && island != null)
        {
            grid = island.GetComponent<GridSystem>() ?? island.GetComponentInChildren<GridSystem>();
        }

        if (grid == null) return;

        for (int x = 0; x < grid.gridSize; x++)
        {
            for (int z = 0; z < grid.gridSize; z++)
            {
                Cell cell = grid.GetCell(x, z);
                if (cell != null && cell.occupyingBuilding == building) cell.ReleaseCell();
            }
        }
    }

    private void UnregisterInfluence(Island island)
    {
        InfluenceZone zone = GetComponent<InfluenceZone>();
        if (zone == null) return;

        InfluenceManager influenceManager = island != null
            ? (island.islandObject != null
                ? island.islandObject.GetComponent<InfluenceManager>()
                : island.GetComponent<InfluenceManager>())
            : null;

        if (influenceManager != null) influenceManager.UnregisterZone(zone);
    }

    private void RemoveFromIsland(Building building, Island island)
    {
        if (island == null || building == null || island.buildings == null) return;
        island.buildings.Remove(building);
    }

    private static void ClearSelection(Building building)
    {
        if (BuildingSelections.Instance == null) return;
        if (BuildingSelections.Instance.SelectedBuilding != building) return;

        // Otherwise the HUD keeps showing a panel for a building that no longer exists.
        BuildingSelections.Instance.DeselectAll();
    }

    #endregion
}
