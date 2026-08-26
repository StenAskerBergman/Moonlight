using System.Collections.Generic;
using System.Linq;
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

    [Header("On-Demand Ordering")]
    [Tooltip("Minimum seconds between manual truck requests for the same building.")]
    [SerializeField] private float manualOrderCooldown = 30f;

    private readonly List<Truck> _activeTrucks = new List<Truck>();
    private readonly Queue<Truck> _idleTrucks = new Queue<Truck>();
    private readonly Dictionary<Building, float> _lastManualOrderTime = new Dictionary<Building, float>();

    public GameObject TruckPrefab => truckPrefab;

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
        Truck.OnTruckDelivered += OnTruckDelivered;
    }

    private void OnDisable()
    {
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
    /// Whether RequestTruck(building) would be allowed to dispatch right now —
    /// i.e. the per-building manual-order cooldown has elapsed. Used by HUD to
    /// gate/gray out the "Order Truck" button.
    /// </summary>
    public bool CanRequestTruck(Building building)
    {
        if (building == null) return false;
        if (!_lastManualOrderTime.TryGetValue(building, out float lastTime)) return true;
        return Time.time - lastTime >= manualOrderCooldown;
    }

    /// <summary>Seconds remaining before RequestTruck(building) is allowed again, or 0 if ready.</summary>
    public float GetCooldownRemaining(Building building)
    {
        if (building == null) return 0f;
        if (!_lastManualOrderTime.TryGetValue(building, out float lastTime)) return 0f;
        return Mathf.Max(0f, manualOrderCooldown - (Time.time - lastTime));
    }

    /// <summary>
    /// Manually dispatches a truck to collect from <paramref name="building"/> right
    /// now, using the same consumer/path resolution as the automatic OnOutputReady
    /// path. Exists for special circumstances where a player wants to force a pickup
    /// — e.g. output piled up because no truck/route was available when it was
    /// produced. Gated by manualOrderCooldown so it can't be spammed.
    /// Returns true if a truck was dispatched.
    /// </summary>
    public bool RequestTruck(Building building)
    {
        if (building == null) return false;

        if (!CanRequestTruck(building))
        {
            Debug.Log($"TransportManager: '{building.name}' truck request is on cooldown " +
                      $"({GetCooldownRemaining(building):F1}s remaining).");
            return false;
        }

        BuildingOutput output = building.GetComponent<BuildingOutput>();
        if (output == null || output.AvailableAmount <= 0)
        {
            Debug.Log($"TransportManager: '{building.name}' has no pending output to collect.");
            return false;
        }

        Island island = building.GetComponentInParent<Island>();
        WarehouseLogisticsScheduler[] schedulers = FindObjectsOfType<WarehouseLogisticsScheduler>();
        foreach (WarehouseLogisticsScheduler scheduler in schedulers
                     .Where(candidate => candidate != null && candidate.BelongsTo(island) && candidate.HasImmediatelyAvailableDrone)
                     .OrderBy(candidate => (candidate.transform.position - building.transform.position).sqrMagnitude))
        {
            if (!scheduler.TryQueuePriorityPickup(building)) continue;
            _lastManualOrderTime[building] = Time.time;
            return true;
        }

        Debug.LogWarning($"TransportManager: no free road drone can reach '{building.name}'. " +
                         "An island-owned airborne priority adapter is not configured.");
        return false;
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
    /// Resolves a drivable road path between two buildings: each building is mapped to
    /// the grid cell it stands on, then RoadNetwork routes between the road cells
    /// serving them. Fails safe — returns false, leaving the output pending — when
    /// there is no RoadNetwork in the scene, when a building cannot be resolved to a
    /// cell, or when no connected stretch of road joins the two.
    /// </summary>
    private bool TryGetPath(Building from, Building to, out List<Cell> path)
    {
        path = null;

        if (RoadNetwork.Instance == null) return false;

        Cell fromCell = ResolveBuildingCell(from);
        Cell toCell = ResolveBuildingCell(to);
        if (fromCell == null || toCell == null) return false;

        path = RoadNetwork.Instance.GetRouteBetween(fromCell, toCell);
        return path != null && path.Count > 0;
    }

    /// <summary>
    /// Maps a placed building to the grid cell beneath it, via the GridSystem on the
    /// island it belongs to — the same lookup Bank uses when registering a newly
    /// placed building.
    /// </summary>
    private Cell ResolveBuildingCell(Building building)
    {
        if (building == null) return null;

        GridSystem gridSystem = building.GetComponentInParent<GridSystem>();
        if (gridSystem == null) return null;

        return gridSystem.GetCellAtWorldPosition(building.transform.position);
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
