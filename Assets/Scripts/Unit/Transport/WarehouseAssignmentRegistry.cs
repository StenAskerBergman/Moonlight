using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Island-level warehouse assignment only. Scheduling remains local to each
/// warehouse. Existing assignments remain sticky while coverage is valid.
/// </summary>
public static class WarehouseAssignmentRegistry
{
    private static readonly List<WarehouseLogisticsScheduler> warehouses = new List<WarehouseLogisticsScheduler>();
    private static readonly Dictionary<Building, WarehouseLogisticsScheduler> assignments = new Dictionary<Building, WarehouseLogisticsScheduler>();
    private static readonly HashSet<Building> activePickups = new HashSet<Building>();

    public static void Register(WarehouseLogisticsScheduler warehouse)
    {
        if (warehouse != null && !warehouses.Contains(warehouse)) warehouses.Add(warehouse);
    }

    public static void Unregister(WarehouseLogisticsScheduler warehouse)
    {
        warehouses.Remove(warehouse);
        var affected = new List<Building>();
        foreach (var entry in assignments)
        {
            if (entry.Value == warehouse) affected.Add(entry.Key);
        }
        foreach (Building building in affected)
        {
            if (!activePickups.Contains(building)) assignments.Remove(building);
        }
    }

    public static WarehouseLogisticsScheduler Resolve(Building producer)
    {
        if (producer == null) return null;
        if (assignments.TryGetValue(producer, out WarehouseLogisticsScheduler current))
        {
            if (activePickups.Contains(producer) || (current != null && current.Covers(producer.transform.position))) return current;
            assignments.Remove(producer);
        }

        Island island = producer.GetComponentInParent<Island>();
        WarehouseLogisticsScheduler best = null;
        float bestDistance = float.PositiveInfinity;
        int bestId = int.MaxValue;

        warehouses.RemoveAll(warehouse => warehouse == null);
        foreach (WarehouseLogisticsScheduler warehouse in warehouses)
        {
            if (!warehouse.BelongsTo(island) || !warehouse.Covers(producer.transform.position)) continue;
            float distance = (warehouse.transform.position - producer.transform.position).sqrMagnitude;
            int id = warehouse.WarehouseId;
            if (distance < bestDistance || (Mathf.Approximately(distance, bestDistance) && id < bestId))
            {
                best = warehouse;
                bestDistance = distance;
                bestId = id;
            }
        }

        if (best != null) assignments[producer] = best;
        return best;
    }

    public static void MarkPickupActive(Building producer) => activePickups.Add(producer);
    public static bool IsPickupActive(Building producer) => producer != null && activePickups.Contains(producer);

    public static void MarkPickupFinished(Building producer)
    {
        activePickups.Remove(producer);
        if (producer == null) return;
        if (assignments.TryGetValue(producer, out WarehouseLogisticsScheduler current) &&
            (current == null || !current.Covers(producer.transform.position)))
        {
            assignments.Remove(producer);
            Resolve(producer);
        }
    }
}
