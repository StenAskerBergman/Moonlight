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
        if (output != null)
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

        CurrentState = TruckState.DrivingToDropoff;
    }

    private IEnumerator UnloadCargo()
    {
        yield return new WaitForSeconds(loadUnloadTime);

        BuildingInventory dropoffInventory = DropoffBuilding != null ? DropoffBuilding.buildingInventory : null;
        if (dropoffInventory != null)
        {
            foreach (var entry in _cargo)
            {
                for (int i = 0; i < entry.Value; i++)
                {
                    dropoffInventory.AddResourceToBuilding(entry.Key);
                }
                _transport.RemoveResource(entry.Key, entry.Value);
            }
        }
        else
        {
            Debug.LogWarning($"Truck '{name}': dropoff building '{DropoffBuilding?.name}' has no BuildingInventory component.");
        }

        _cargo.Clear();
        SetModel(empty: true);

        if (_unit != null)
        {
            GameEventBus.Publish(new OnUnitDelivered(_unit));
        }

        OnTruckDelivered?.Invoke(this);

        PickupBuilding = null;
        DropoffBuilding = null;
        _routePath = null;
        _pathIndex = 0;
        CurrentState = TruckState.Idle;
    }

    private void SetModel(bool empty)
    {
        if (emptyTruckModel != null) emptyTruckModel.SetActive(empty);
        if (fullTruckModel != null) fullTruckModel.SetActive(!empty);
    }
}
