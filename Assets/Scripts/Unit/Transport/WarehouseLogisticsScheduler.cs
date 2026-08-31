using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Warehouse-local output pickup module. Owns this warehouse's assigned
/// producers, pickup queue, road-drone pool, retries, and dispatch decisions.
/// </summary>
[DisallowMultipleComponent]
public sealed class WarehouseLogisticsScheduler : MonoBehaviour
{
    [Header("Warehouse Fleet")]
    [SerializeField, Range(1, 3)] private int warehouseLevel = 1;
    [SerializeField] private GameObject roadDronePrefab;
    [SerializeField, Min(1)] private int droneCargoCapacity = 15;
    [SerializeField, Min(0)] private int retryLimit = 2;
    [Tooltip("Seconds a failed job waits before it is eligible for dispatch again. " +
             "Without a delay every retry is consumed in the frame the first attempt failed.")]
    [SerializeField, Min(0f)] private float retryDelaySeconds = 2f;
    [SerializeField, Min(0.1f)] private float assignmentRefreshSeconds = 1f;

    private readonly Queue<PickupJob> jobs = new Queue<PickupJob>();
    private readonly List<Truck> drones = new List<Truck>();
    private readonly Queue<Truck> idleDrones = new Queue<Truck>();
    private readonly Dictionary<Building, PickupJob> activeByProducer = new Dictionary<Building, PickupJob>();
    private readonly List<PickupJob> retryingJobs = new List<PickupJob>();
    private readonly Queue<DeliveryJob> deliveries = new Queue<DeliveryJob>();
    private readonly HashSet<Building> activeDeliveries = new HashSet<Building>();

    private InfluenceZone influence;
    private Building warehouseBuilding;
    private Island island;
    private IslandResourceStorage sharedStorage;
    private float nextAssignmentRefresh;
    private bool warnedAboutMissingInfluence;

    public int WarehouseId => warehouseBuilding != null && warehouseBuilding.BuildingId != 0
        ? warehouseBuilding.BuildingId
        : GetInstanceID();
    public int FleetCapacity => Mathf.Clamp(warehouseLevel, 1, 3);
    public int QueuedJobCount => jobs.Count;

    private void Awake()
    {
        influence = GetComponent<InfluenceZone>();
        warehouseBuilding = GetComponent<Building>();
        island = GetComponentInParent<Island>();
        if (island != null)
        {
            sharedStorage = island.GetComponent<IslandResourceStorage>();
            if (sharedStorage == null) sharedStorage = island.gameObject.AddComponent<IslandResourceStorage>();
        }
    }

    private void OnEnable()
    {
        WarehouseAssignmentRegistry.Register(this);
        BuildingOutput.OnOutputReady += HandleOutputReady;
        RoadPlacer.OnRoadPlaced += HandleRoadChanged;
        RoadPlacer.OnRoadRemoved += HandleRoadChanged;
    }

    private void OnDisable()
    {
        WarehouseAssignmentRegistry.Unregister(this);
        BuildingOutput.OnOutputReady -= HandleOutputReady;
        RoadPlacer.OnRoadPlaced -= HandleRoadChanged;
        RoadPlacer.OnRoadRemoved -= HandleRoadChanged;
    }

    private void Update()
    {
        if (Time.time >= nextAssignmentRefresh)
        {
            nextAssignmentRefresh = Time.time + assignmentRefreshSeconds;
            DiscoverReadyProducers();
            DiscoverSupplyRequests();
        }
        PromoteDueRetries();
        DispatchQueuedJobs();
        DispatchDeliveries();
    }

    // The zone is resolved lazily rather than cached once in Awake. Depot adds this
    // scheduler before an InfluenceZone necessarily exists, and a permanently null
    // zone makes Covers() always false, which silently disables the whole warehouse.
    public bool Covers(Vector3 worldPosition)
    {
        if (influence == null) influence = GetComponent<InfluenceZone>();
        if (influence == null)
        {
            if (!warnedAboutMissingInfluence)
            {
                warnedAboutMissingInfluence = true;
                Debug.LogWarning(
                    $"WarehouseLogisticsScheduler on '{name}' has no InfluenceZone, so it covers no producers " +
                    "and will never dispatch a pickup. Add an InfluenceZone to this building.",
                    this);
            }
            return false;
        }
        return influence.ContainsPoint(worldPosition);
    }
    public bool BelongsTo(Island candidate) => island != null && island == candidate;
    public bool HasImmediatelyAvailableDrone => idleDrones.Count > 0 || drones.Count < FleetCapacity;

    public bool TryQueuePriorityPickup(Building producer)
    {
        if (producer == null || WarehouseAssignmentRegistry.IsPickupActive(producer) || !HasImmediatelyAvailableDrone) return false;
        BuildingOutput output = producer.GetComponent<BuildingOutput>();
        if (output == null || output.AvailableAmount <= 0) return false;
        if (!TryBuildRoadRoute(producer, out _)) return false;
        if (!output.TryReservePickup(droneCargoCapacity, out Dictionary<ItemEnums.ResourceType, int> cargo, true)) return false;

        var job = new PickupJob(producer, this, cargo, true);
        activeByProducer.Add(producer, job);
        WarehouseAssignmentRegistry.MarkPickupActive(producer);
        jobs.Enqueue(job);
        return true;
    }

    private void HandleOutputReady(Building producer, ItemEnums.ResourceType resource, int amount)
    {
        TryQueueAutomaticPickup(producer);
    }

    private void HandleRoadChanged(Cell changedCell)
    {
        DiscoverReadyProducers();
    }

    private void DiscoverReadyProducers()
    {
        if (island == null) return;
        foreach (BuildingOutput output in island.GetComponentsInChildren<BuildingOutput>())
        {
            if (output != null) TryQueueAutomaticPickup(output.GetComponent<Building>());
        }
    }

    private void DiscoverSupplyRequests()
    {
        if (island == null || sharedStorage == null) return;
        foreach (BuildingSupply supply in island.GetComponentsInChildren<BuildingSupply>())
        {
            Building consumer = supply != null ? supply.GetComponent<Building>() : null;
            if (consumer == null || activeDeliveries.Contains(consumer) || WarehouseAssignmentRegistry.Resolve(consumer) != this) continue;
            if (!supply.TryGetNextDeliveryRequest(droneCargoCapacity, out ItemEnums.ResourceType resource, out int requested)) continue;
            if (!TryBuildRoadRoute(consumer, out _)) continue;
            if (!sharedStorage.TryReserve(resource, requested, out int reserved)) continue;
            activeDeliveries.Add(consumer);
            deliveries.Enqueue(new DeliveryJob(consumer, this, resource, reserved));
        }
    }

    private void DispatchDeliveries()
    {
        while (deliveries.Count > 0)
        {
            Truck drone = GetOrCreateDrone();
            if (drone == null) return;
            DeliveryJob job = deliveries.Dequeue();
            if (job.Consumer == null || !TryBuildRoadRoute(job.Consumer, out List<Cell> route))
            {
                sharedStorage?.ReleaseReservation(job.Resource, job.Amount);
                activeDeliveries.Remove(job.Consumer);
                idleDrones.Enqueue(drone);
                continue;
            }
            drone.AssignDeliveryJob(job, route, sharedStorage, HandleDeliveryFinished);
        }
    }

    private void HandleDeliveryFinished(Truck drone, DeliveryJob job, bool succeeded)
    {
        if (!succeeded && job != null) sharedStorage?.ReleaseReservation(job.Resource, job.Amount);
        if (job != null) activeDeliveries.Remove(job.Consumer);
        if (drone != null) idleDrones.Enqueue(drone);
    }

    private bool TryQueueAutomaticPickup(Building producer)
    {
        if (producer == null || activeByProducer.ContainsKey(producer)) return false;
        BuildingOutput output = producer.GetComponent<BuildingOutput>();
        if (output == null || !output.IsPickupReady) return false;
        if (WarehouseAssignmentRegistry.Resolve(producer) != this) return false;
        if (!TryBuildRoadRoute(producer, out List<Cell> route)) return false;
        if (!output.TryReservePickup(droneCargoCapacity, out Dictionary<ItemEnums.ResourceType, int> cargo)) return false;

        var job = new PickupJob(producer, this, cargo, false);
        activeByProducer.Add(producer, job);
        WarehouseAssignmentRegistry.MarkPickupActive(producer);
        jobs.Enqueue(job);
        return true;
    }

    private void DispatchQueuedJobs()
    {
        while (jobs.Count > 0)
        {
            Truck drone = GetOrCreateDrone();
            if (drone == null) return;

            PickupJob job = jobs.Dequeue();
            if (job.Producer == null || !TryBuildRoadRoute(job.Producer, out List<Cell> route))
            {
                HandleJobFailure(drone, job);
                continue;
            }

            job.SetState(PickupJob.JobState.TravelingToPickup);
            drone.AssignPickupJob(job, route, sharedStorage, HandleDroneFinished);
        }
    }

    private Truck GetOrCreateDrone()
    {
        while (idleDrones.Count > 0)
        {
            Truck idle = idleDrones.Dequeue();
            if (idle != null) return idle;
        }
        drones.RemoveAll(drone => drone == null);
        if (drones.Count >= FleetCapacity) return null;

        GameObject prefab = roadDronePrefab != null
            ? roadDronePrefab
            : TransportManager.Instance != null ? TransportManager.Instance.TruckPrefab : null;
        GameObject instance;
        if (prefab != null)
        {
            instance = Instantiate(prefab, transform.position, transform.rotation, transform);
        }
        else
        {
            instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.name = "Road Logistics Drone";
            instance.transform.SetParent(transform, true);
            instance.transform.SetPositionAndRotation(transform.position, transform.rotation);
            instance.transform.localScale = new Vector3(0.8f, 0.5f, 1.2f);
            instance.AddComponent<Truck>();
        }
        Truck truck = instance.GetComponent<Truck>();
        if (truck == null)
        {
            Destroy(instance);
            return null;
        }
        drones.Add(truck);
        return truck;
    }

    private bool TryBuildRoadRoute(Building producer, out List<Cell> route)
    {
        route = null;
        if (producer == null || warehouseBuilding == null || RoadNetwork.Instance == null) return false;
        GridSystem producerGrid = producer.GetComponentInParent<GridSystem>();
        GridSystem warehouseGrid = warehouseBuilding.GetComponentInParent<GridSystem>();
        if (producerGrid == null || warehouseGrid == null) return false;
        Cell producerCell = producerGrid.GetCellAtWorldPosition(producer.transform.position);
        Cell warehouseCell = warehouseGrid.GetCellAtWorldPosition(transform.position);
        route = RoadNetwork.Instance.GetRouteBetween(warehouseCell, producerCell);
        return route != null && route.Count > 0;
    }

    private void HandleDroneFinished(Truck drone, PickupJob job, bool succeeded)
    {
        if (succeeded)
        {
            job.SetState(PickupJob.JobState.Completed);
            FinishJob(job);
            if (drone != null) idleDrones.Enqueue(drone);
            return;
        }
        HandleJobFailure(drone, job);
    }

    private void HandleJobFailure(Truck drone, PickupJob job)
    {
        if (drone != null) idleDrones.Enqueue(drone);
        if (job != null && job.Producer != null && job.RegisterRetry() <= retryLimit)
        {
            // Park the job outside `jobs` until its delay elapses. Re-enqueuing here
            // let DispatchQueuedJobs' loop dequeue it again on the same iteration,
            // which spent every retry in the frame the first attempt failed.
            job.SetState(PickupJob.JobState.Queued);
            job.SetEarliestDispatchTime(Time.time + retryDelaySeconds);
            retryingJobs.Add(job);
            return;
        }

        if (job != null)
        {
            BuildingOutput output = job.Producer != null ? job.Producer.GetComponent<BuildingOutput>() : null;
            output?.ReleaseReservation(job.Cargo);
            job.SetState(PickupJob.JobState.Failed);
            FinishJob(job);
        }
    }

    private void FinishJob(PickupJob job)
    {
        if (job == null) return;

        // Compare against a real null, not Unity's overloaded ==. A destroyed
        // producer is still a valid dictionary key, and skipping it here left the
        // entry in activeByProducer and in the registry's active-pickup set forever.
        Building producer = job.Producer;
        if (ReferenceEquals(producer, null)) return;

        activeByProducer.Remove(producer);
        WarehouseAssignmentRegistry.MarkPickupFinished(producer);
    }

    private void PromoteDueRetries()
    {
        for (int index = retryingJobs.Count - 1; index >= 0; index--)
        {
            PickupJob job = retryingJobs[index];
            if (job == null)
            {
                retryingJobs.RemoveAt(index);
                continue;
            }
            if (Time.time < job.EarliestDispatchTime) continue;

            retryingJobs.RemoveAt(index);
            jobs.Enqueue(job);
        }
    }
}
