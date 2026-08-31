using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Ground-vehicle transport unit. Drives a producer -> consumer route along a
/// road path (a list of Cells), loading a producing Building's output and
/// depositing it into a consuming Building's inventory.
///
/// Routes are assigned externally by TransportManager; this component only
/// drives the state machine once AssignRoute() has been called.
///
/// TODO: Assign truck models in the Inspector — the source meshes live at:
///   Assets/Resources/Blender/EmptyTruck.fbx -> emptyTruckModel
///   Assets/Resources/Blender/truck.fbx      -> fullTruckModel
/// TODO: Once a Landcraft NavMeshMovementProfile asset exists (see
/// NavMeshMovementProfile.cs) and its agent type is baked, switch MoveAlongPath()
/// to agent.SetDestination()-driven movement instead of manual transform
/// interpolation, and assign that profile's agentTypeID to this prefab's agent.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Truck : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private GameObject emptyTruckModel; // TODO: assign EmptyTruck.fbx model in Inspector
    [SerializeField] private GameObject fullTruckModel;  // TODO: assign truck.fbx model in Inspector

    public enum TruckState { Idle, DrivingToPickup, Loading, DrivingToDropoff, Unloading }
    public TruckState CurrentState { get; private set; } = TruckState.Idle;

    public Building PickupBuilding { get; private set; }
    public Building DropoffBuilding { get; private set; }
    private List<Cell> _routePath;
    private int _pathIndex;

    [Header("Config")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float loadUnloadTime = 1.5f;
    [SerializeField] private int cargoCapacity = 15;

    // Cargo capacity is enforced through Transport (AddResource/RemoveResource).
    // Transport exposes no enumeration API, so a local mirror is kept for Unload
    // (which needs to know exactly what to deposit) and for HasCargo.
    private Transport _transport;
    private readonly Dictionary<ItemEnums.ResourceType, int> _cargo = new Dictionary<ItemEnums.ResourceType, int>();
    public bool HasCargo => _cargo.Count > 0 && _cargo.Values.Any(v => v > 0);

    // Optional — present only if the Truck prefab also carries a Unit component
    // (e.g. for selection/inspection). Delivery events are only published when set.
    private Unit _unit;
    private PickupJob _pickupJob;
    private IslandResourceStorage _sharedStorage;
    private Action<Truck, PickupJob, bool> _pickupCompletion;
    private DeliveryJob _deliveryJob;
    private Action<Truck, DeliveryJob, bool> _deliveryCompletion;

    public static event Action<Truck> OnTruckArrived;
    public static event Action<Truck> OnTruckDelivered;

    private void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        _unit = GetComponent<Unit>();
        _transport = new Transport(cargoCapacity);
        SetModel(empty: true);
    }

    /// <summary>
    /// Assigns a pickup/dropoff pair and the road path between them, then starts
    /// driving. Called by TransportManager once a route has been resolved.
    /// </summary>
    public void AssignRoute(Building pickup, Building dropoff, List<Cell> path)
    {
        if (pickup == null || dropoff == null || path == null || path.Count == 0)
        {
            Debug.LogWarning($"Truck '{name}': AssignRoute called with invalid pickup/dropoff/path.");
            return;
        }

        PickupBuilding = pickup;
        DropoffBuilding = dropoff;
        _routePath = path;
        _pathIndex = 0;

        SetModel(empty: true);
        CurrentState = TruckState.DrivingToPickup;
    }

    public void AssignPickupJob(PickupJob job, List<Cell> path, IslandResourceStorage sharedStorage,
        Action<Truck, PickupJob, bool> completion)
    {
        if (job == null || job.Producer == null || path == null || path.Count == 0 || sharedStorage == null)
        {
            completion?.Invoke(this, job, false);
            return;
        }

        _pickupJob = job;
        _sharedStorage = sharedStorage;
        _pickupCompletion = completion;
        PickupBuilding = job.Producer;
        DropoffBuilding = null;
        _routePath = path;
        _pathIndex = 0;
        SetModel(empty: true);
        CurrentState = TruckState.DrivingToPickup;
    }

    public void AssignDeliveryJob(DeliveryJob job, List<Cell> path, IslandResourceStorage sharedStorage,
        Action<Truck, DeliveryJob, bool> completion)
    {
        if (job == null || job.Consumer == null || path == null || path.Count == 0 || sharedStorage == null ||
            !sharedStorage.CommitReservation(job.Resource, job.Amount) || !_transport.AddResource(job.Resource, job.Amount))
        {
            completion?.Invoke(this, job, false);
            return;
        }

        _deliveryJob = job;
        _deliveryCompletion = completion;
        _sharedStorage = sharedStorage;
        _cargo[job.Resource] = job.Amount;
        PickupBuilding = null;
        DropoffBuilding = job.Consumer;
        _routePath = path;
        _pathIndex = 0;
        SetModel(empty: false);
        CurrentState = TruckState.DrivingToDropoff;
    }

    private void Update()
    {
        switch (CurrentState)
        {
            case TruckState.DrivingToPickup:
            case TruckState.DrivingToDropoff:
                MoveAlongPath();
                break;
        }
    }

    private void MoveAlongPath()
    {
        if (_routePath == null || _routePath.Count == 0)
        {
            HandleArrival();
            return;
        }

        Vector3 target = _routePath[_pathIndex].position;
        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) < 0.1f)
        {
            _pathIndex++;
            if (_pathIndex >= _routePath.Count)
            {
                HandleArrival();
            }
        }
    }

    private void HandleArrival()
    {
        OnTruckArrived?.Invoke(this);

        switch (CurrentState)
        {
            case TruckState.DrivingToPickup:
                CurrentState = TruckState.Loading;
                StartCoroutine(LoadCargo());
                break;

            case TruckState.DrivingToDropoff:
                CurrentState = TruckState.Unloading;
                StartCoroutine(UnloadCargo());
                break;
        }
    }

    private IEnumerator LoadCargo()
    {
        yield return new WaitForSeconds(loadUnloadTime);

        BuildingOutput output = PickupBuilding != null ? PickupBuilding.GetComponent<BuildingOutput>() : null;
        if (_pickupJob != null)
        {
            _pickupJob.SetState(PickupJob.JobState.Loading);
            if (output == null || !output.CommitReservation(_pickupJob.Cargo))
            {
                CompletePickupJob(false);
                yield break;
            }

            foreach (var entry in _pickupJob.Cargo)
            {
                if (!_transport.AddResource(entry.Key, entry.Value))
                {
                    CompletePickupJob(false);
                    yield break;
                }
                _cargo[entry.Key] = entry.Value;
            }
        }
        else if (output != null)
        {
            Dictionary<ItemEnums.ResourceType, int> collected = output.CollectOutput();
            foreach (var entry in collected)
            {
                if (entry.Value <= 0) continue;

                if (_transport.AddResource(entry.Key, entry.Value))
                {
                    if (_cargo.ContainsKey(entry.Key)) _cargo[entry.Key] += entry.Value;
                    else _cargo[entry.Key] = entry.Value;
                }
                else
                {
                    Debug.LogWarning($"Truck '{name}': cargo capacity ({cargoCapacity}) exceeded loading " +
                                      $"{entry.Value} of {entry.Key} from '{PickupBuilding.name}' — excess left unloaded.");
                }
            }
        }
        else
        {
            Debug.LogWarning($"Truck '{name}': pickup building '{PickupBuilding?.name}' has no BuildingOutput component.");
        }

        SetModel(empty: !HasCargo);

        // Drive back to the dropoff along the reverse route. Copy rather than
        // mutate in place — the path list may be shared/cached by the caller.
        var reversed = new List<Cell>(_routePath);
        reversed.Reverse();
        _routePath = reversed;
        _pathIndex = 0;

        _pickupJob?.SetState(PickupJob.JobState.TravelingToWarehouse);
        CurrentState = TruckState.DrivingToDropoff;
    }

    private IEnumerator UnloadCargo()
    {
        yield return new WaitForSeconds(loadUnloadTime);

        if (_deliveryJob != null)
        {
            BuildingSupply supply = DropoffBuilding != null ? DropoffBuilding.GetComponent<BuildingSupply>() : null;
            int accepted = supply != null ? supply.ReceiveSupply(_deliveryJob.Resource, _deliveryJob.Amount) : 0;
            if (accepted != _deliveryJob.Amount)
            {
                if (accepted > 0)
                {
                    _transport.RemoveResource(_deliveryJob.Resource, accepted);
                    _cargo[_deliveryJob.Resource] -= accepted;
                    if (_cargo[_deliveryJob.Resource] <= 0) _cargo.Remove(_deliveryJob.Resource);
                }
                CompleteDeliveryJob(false);
                yield break;
            }
            _transport.RemoveResource(_deliveryJob.Resource, _deliveryJob.Amount);
        }
        else if (_pickupJob != null && _sharedStorage != null)
        {
            _pickupJob.SetState(PickupJob.JobState.Unloading);
            _sharedStorage.Add(_cargo);
            foreach (var entry in _cargo) _transport.RemoveResource(entry.Key, entry.Value);
        }
        else
        {
            BuildingInventory dropoffInventory = DropoffBuilding != null ? DropoffBuilding.buildingInventory : null;
            if (dropoffInventory != null)
            {
                foreach (var entry in _cargo)
                {
                    for (int i = 0; i < entry.Value; i++) dropoffInventory.AddResourceToBuilding(entry.Key);
                    _transport.RemoveResource(entry.Key, entry.Value);
                }
            }
            else Debug.LogWarning($"Truck '{name}': dropoff building '{DropoffBuilding?.name}' has no BuildingInventory component.");
        }

        _cargo.Clear();
        SetModel(empty: true);

        if (_unit != null)
        {
            GameEventBus.Publish(new OnUnitDelivered(_unit));
        }

        if (_deliveryJob != null)
        {
            CompleteDeliveryJob(true);
            yield break;
        }

        if (_pickupJob != null)
        {
            CompletePickupJob(true);
            yield break;
        }

        OnTruckDelivered?.Invoke(this);

        PickupBuilding = null;
        DropoffBuilding = null;
        _routePath = null;
        _pathIndex = 0;
        CurrentState = TruckState.Idle;
    }

    private void CompletePickupJob(bool succeeded)
    {
        PickupJob job = _pickupJob;
        Action<Truck, PickupJob, bool> completion = _pickupCompletion;
        _pickupJob = null;
        _sharedStorage = null;
        _pickupCompletion = null;
        _cargo.Clear();
        PickupBuilding = null;
        DropoffBuilding = null;
        _routePath = null;
        _pathIndex = 0;
        SetModel(empty: true);
        CurrentState = TruckState.Idle;
        completion?.Invoke(this, job, succeeded);
    }

    private void CompleteDeliveryJob(bool succeeded)
    {
        DeliveryJob job = _deliveryJob;
        Action<Truck, DeliveryJob, bool> completion = _deliveryCompletion;
        if (!succeeded && _sharedStorage != null && _cargo.Count > 0)
        {
            _sharedStorage.Add(_cargo);
            foreach (var entry in _cargo) _transport.RemoveResource(entry.Key, entry.Value);
        }
        _deliveryJob = null;
        _deliveryCompletion = null;
        _sharedStorage = null;
        _cargo.Clear();
        PickupBuilding = null;
        DropoffBuilding = null;
        _routePath = null;
        _pathIndex = 0;
        SetModel(empty: true);
        CurrentState = TruckState.Idle;
        completion?.Invoke(this, job, succeeded);
    }

    private void SetModel(bool empty)
    {
        if (emptyTruckModel != null) emptyTruckModel.SetActive(empty);
        if (fullTruckModel != null) fullTruckModel.SetActive(!empty);
    }
}
