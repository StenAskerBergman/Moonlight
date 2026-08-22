using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns and dispatches Truck units for ground-based inter-building resource
/// transport. Listens for BuildingOutput.OnOutputReady and routes an idle (or
/// freshly spawned) truck to ferry the output from the producing building to a
/// consuming building on the same island.
/// </summary>
public class TransportManager : MonoBehaviour
{
    public static TransportManager Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private GameObject truckPrefab; // TODO: assign the Truck prefab (Assets/Prefabs/Units Prefabs/Characters/Transport/Trucks) in Inspector
    [SerializeField] private int maxTrucksPerIsland = 3; // TODO: currently enforced as a single global cap — revisit once trucks are tracked per Island

    private readonly List<Truck> _activeTrucks = new List<Truck>();
    private readonly Queue<Truck> _idleTrucks = new Queue<Truck>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        BuildingOutput.OnOutputReady += OnOutputReady;
        Truck.OnTruckDelivered += OnTruckDelivered;
    }

    private void OnDisable()
    {
        BuildingOutput.OnOutputReady -= OnOutputReady;
        Truck.OnTruckDelivered -= OnTruckDelivered;
    }

    private void OnOutputReady(Building producer, ItemEnums.ResourceType resource, int amount)
    {
        if (producer == null || amount <= 0) return;

        Building consumer = FindConsumer(producer, resource);
        if (consumer == null) return; // No building on the island currently wants this resource.

        if (!TryGetPath(producer, consumer, out List<Cell> path))
        {
            Debug.LogWarning($"TransportManager: no road path between '{producer.name}' and '{consumer.name}' " +
                              $"— {resource} output left pending for pickup.");
            return;
        }

        Truck truck = GetOrSpawnTruck(producer.transform.position);
        if (truck == null) return;

        truck.AssignRoute(producer, consumer, path);
    }

    /// <summary>
    /// Finds a building on the same island as the producer that accepts the given
    /// resource type. Building.Resources is not populated by any producer/consumer
    /// wiring yet, so this will find nothing until that lands — it fails safe
    /// (returns null) rather than throwing.
    /// </summary>
    private Building FindConsumer(Building producer, ItemEnums.ResourceType resource)
    {
        Island island = producer.GetComponentInParent<Island>();
        if (island == null) return null;

        // Island.buildings is not populated at placement time yet (see
        // docs/superpowers/specs/2026-08-20-settlement-anchor-buildable-area-design.md
        // section 6.6) — placed buildings are children of the island transform
        // instead, which is the invariant the rest of the codebase (e.g.
        // Island.GetPlayerBaseInventory) already relies on.
        foreach (Building candidate in island.GetComponentsInChildren<Building>())
        {
            if (candidate != null && candidate != producer && candidate.Resources.Contains(resource))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a drivable road path between two buildings.
    /// TODO: wire once RoadNetwork.cs is in place. Once it exists, this should:
    ///   1. Resolve each Building to its occupying Cell (no Building -> Cell lookup
    ///      exists yet either — Cell only tracks occupyingBuilding, not the reverse;
    ///      that lookup will need to be added alongside RoadNetwork, e.g. on MapGrid).
    ///   2. Check RoadNetwork.Instance.HasRoadAccess(fromCell) && HasRoadAccess(toCell).
    ///   3. Return RoadNetwork.Instance.GetPath(fromCell, toCell).
    /// Until then this always fails so callers degrade gracefully (pending output is
    /// simply left uncollected) instead of referencing a type that doesn't exist.
    /// </summary>
    private bool TryGetPath(Building from, Building to, out List<Cell> path)
    {
        path = null;
        return false;
    }

    private Truck GetOrSpawnTruck(Vector3 spawnPosition)
    {
        if (_idleTrucks.Count > 0)
        {
            return _idleTrucks.Dequeue();
        }

        if (_activeTrucks.Count < maxTrucksPerIsland)
        {
            return SpawnTruck(spawnPosition);
        }

        return null; // At capacity — this delivery waits for a truck to free up.
    }

    public Truck SpawnTruck(Vector3 position)
    {
        if (truckPrefab == null)
        {
            Debug.LogError("TransportManager: truckPrefab is not assigned.");
            return null;
        }

        GameObject go = Instantiate(truckPrefab, position, Quaternion.identity);
        Truck truck = go.GetComponent<Truck>();
        if (truck == null)
        {
            Debug.LogError($"TransportManager: truckPrefab '{truckPrefab.name}' lacks a Truck component.");
            Destroy(go);
            return null;
        }

        _activeTrucks.Add(truck);
        return truck;
    }

    private void OnTruckDelivered(Truck truck)
    {
        if (truck == null) return;
        _idleTrucks.Enqueue(truck);
    }
}
