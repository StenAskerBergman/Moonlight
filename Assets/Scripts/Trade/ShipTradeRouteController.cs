using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Runtime execution state of an automated trading ship.
/// </summary>
public enum TradeRouteState
{
    Idle,
    Travelling,
    WaitingForDock,
    Trading,
    WaitingForCargoCondition,
    Advancing
}

/// <summary>
/// Independent runtime executor attached to a ship assigned to a TradingRoute.
/// Executes the Anno 2070 logistics state machine:
/// TravelToStation -> WaitForDock -> ExecuteCargoTargets -> AdvanceStation -> TravelToNextStation.
/// Implements IAutonomousBehaviorSource so SelectedUnitOrderVisualizer can display its route.
/// </summary>
[RequireComponent(typeof(Unit))]
public class ShipTradeRouteController : MonoBehaviour, IAutonomousBehaviorSource
{
    [Header("State Tracking")]
    [SerializeField] private string assignedRouteId;
    [SerializeField] private int currentStationIndex = 0;
    [SerializeField] private TradeRouteState currentState = TradeRouteState.Idle;
    [SerializeField] private float tradeDwellTime = 1.5f; // Visual pause while loading/unloading
    [SerializeField] private bool isPausedByPlayer = false;

    private Unit unit;
    private NavalUnit navalUnit;
    private NavMeshAgent agent;
    private UnitInventory unitInventory;
    private UnitMovement unitMovement;

    private TradePort currentTargetPort;
    private float stateTimer = 0f;
    private float smartCheckTimer = 0f;
    private Vector3 currentDestination;

    public string AssignedRouteId => assignedRouteId;
    public int CurrentStationIndex => currentStationIndex;
    public TradeRouteState CurrentState => currentState;
    public TradePort CurrentTargetPort => currentTargetPort;
    public bool IsPaused => isPausedByPlayer;

    #region IAutonomousBehaviorSource Implementation

    public string SourceName
    {
        get
        {
            var r = TradingRouteManager.Instance != null ? TradingRouteManager.Instance.GetRoute(assignedRouteId) : null;
            return r != null ? $"Trade Route: {r.name}" : "Trade Route";
        }
    }

    public string CurrentActionDescription
    {
        get
        {
            if (isPausedByPlayer) return "Paused (Player Order)";

            var r = TradingRouteManager.Instance != null ? TradingRouteManager.Instance.GetRoute(assignedRouteId) : null;
            string stationName = (r != null && currentStationIndex < r.stations.Count) ? r.stations[currentStationIndex].stationName : "Station";
            switch (currentState)
            {
                case TradeRouteState.Travelling: return $"Sailing to {stationName}";
                case TradeRouteState.WaitingForDock:
                    int queueRank = currentTargetPort != null ? currentTargetPort.GetQueueIndex(this) + 1 : 0;
                    string rankStr = queueRank > 0 ? $" (Queue #{queueRank})" : "";
                    return $"Waiting for dock at {stationName}{rankStr}";
                case TradeRouteState.Trading: return $"Trading cargo at {stationName}";
                case TradeRouteState.WaitingForCargoCondition: return $"Waiting for cargo at {stationName}";
                case TradeRouteState.Advancing: return $"Departing {stationName}";
                default: return "Idle";
            }
        }
    }

    public bool IsActive => !string.IsNullOrEmpty(assignedRouteId) && !isPausedByPlayer;

    public IReadOnlyList<Vector3> GetAutonomousWaypoints()
    {
        var list = new List<Vector3>();
        var r = TradingRouteManager.Instance != null ? TradingRouteManager.Instance.GetRoute(assignedRouteId) : null;
        if (r != null && r.stations != null)
        {
            foreach (var st in r.stations)
            {
                var isl = ResolveIsland(st);
                if (isl != null)
                {
                    var port = TradePort.ResolveForIsland(isl);
                    list.Add(port != null ? port.GetApproachPoint(agent) : isl.bounds.center);
                }
            }
        }
        return list;
    }

    public IReadOnlyList<string> GetAutonomousWaypointLabels()
    {
        var list = new List<string>();
        var r = TradingRouteManager.Instance != null ? TradingRouteManager.Instance.GetRoute(assignedRouteId) : null;
        if (r != null && r.stations != null)
        {
            foreach (var st in r.stations)
            {
                list.Add(st.stationName);
            }
        }
        return list;
    }

    public void OnPlayerManualOverride()
    {
        if (unit == null) unit = GetComponent<Unit>();
        string uName = unit != null ? unit.displayName : name;
        Debug.Log($"<color=yellow>[TradeRoute] {uName} manually redirected by player - pausing route {assignedRouteId}.</color>");
        PauseRoute();
    }

    public void PauseRoute()
    {
        isPausedByPlayer = true;
        if (currentTargetPort != null)
        {
            currentTargetPort.ReleaseDock(this);
            currentTargetPort = null;
        }
        currentState = TradeRouteState.Idle;
    }

    public void ResumeRoute()
    {
        if (!string.IsNullOrEmpty(assignedRouteId))
        {
            isPausedByPlayer = false;
            currentState = TradeRouteState.Idle;
            if (unit != null && unit.displayName != null)
            {
                Debug.Log($"<color=green>[TradeRoute] {unit.displayName} resumed trade route {assignedRouteId}.</color>");
            }
        }
    }

    #endregion

    private void Awake()
    {
        unit = GetComponent<Unit>();
        navalUnit = GetComponent<NavalUnit>();
        agent = GetComponent<NavMeshAgent>();
        unitInventory = GetComponent<UnitInventory>();
        unitMovement = GetComponent<UnitMovement>();
    }

    private void Start()
    {
        if (unitInventory == null) unitInventory = GetComponent<UnitInventory>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        if (unit != null && unit.CommandExecutor != null)
        {
            unit.CommandExecutor.RegisterAutonomousSource(this);
        }
    }

    private void Update()
    {
        if (string.IsNullOrEmpty(assignedRouteId) || isPausedByPlayer)
        {
            if (string.IsNullOrEmpty(assignedRouteId)) currentState = TradeRouteState.Idle;
            return;
        }

        TradingRoute route = TradingRouteManager.Instance.GetRoute(assignedRouteId);
        if (route == null)
        {
            StopRoute();
            return;
        }

        if (route.stations == null || route.stations.Count == 0)
        {
            currentState = TradeRouteState.Idle;
            return;
        }

        // Clamp station index in case stations were removed while active
        if (currentStationIndex >= route.stations.Count)
        {
            currentStationIndex = 0;
        }

        TradeRouteStation station = route.stations[currentStationIndex];
        Island targetIsland = ResolveIsland(station);
        if (targetIsland == null)
        {
            Debug.LogWarning($"[TradeRoute] Station '{station.stationName}' references an unresolved island. Advancing to next station.");
            AdvanceToNextStation(route);
            return;
        }

        currentTargetPort = TradePort.ResolveForIsland(targetIsland);
        if (currentTargetPort == null || !currentTargetPort.IsOperational)
        {
            Debug.LogWarning($"[TradeRoute] Station '{station.stationName}' has no operational harbor infrastructure. Advancing to next station.");
            AdvanceToNextStation(route);
            return;
        }

        // State Machine Loop
        switch (currentState)
        {
            case TradeRouteState.Idle:
                BeginTravelToStation(currentTargetPort);
                break;

            case TradeRouteState.Travelling:
                UpdateTravelling(route, currentTargetPort);
                break;

            case TradeRouteState.WaitingForDock:
                UpdateWaitingForDock(currentTargetPort);
                break;

            case TradeRouteState.Trading:
                UpdateTrading(route, station, currentTargetPort);
                break;

            case TradeRouteState.WaitingForCargoCondition:
                UpdateWaitingForCargo(route, station, currentTargetPort);
                break;

            case TradeRouteState.Advancing:
                AdvanceToNextStation(route);
                break;
        }
    }

    #region State Logic

    private void BeginTravelToStation(TradePort port)
    {
        if (port == null || agent == null) return;

        currentDestination = port.GetApproachPoint(agent);
        SteerToPosition(currentDestination, arrivalTolerance: port.DockingDistance, description: $"Sail to {port.Island?.islandName ?? "Port"}");
        currentState = TradeRouteState.Travelling;
    }

    private void UpdateTravelling(TradingRoute route, TradePort port)
    {
        if (port == null || !port.IsOperational || agent == null)
        {
            AdvanceToNextStation(route);
            return;
        }

        float distance = Vector3.Distance(transform.position, currentDestination);
        bool hasArrived = distance <= port.DockingDistance;

        if (!hasArrived && agent.enabled && agent.isOnNavMesh && !agent.pathPending)
        {
            if (agent.remainingDistance <= port.DockingDistance + 1f && agent.remainingDistance > 0f)
            {
                hasArrived = true;
            }
        }

        if (hasArrived)
        {
            currentState = TradeRouteState.WaitingForDock;
            if (port.RequestDock(this))
            {
                OnDockGranted(port);
            }
            else
            {
                // Steer towards designated queue waiting position
                int queueIdx = port.GetQueueIndex(this);
                Vector3 waitingPoint = port.GetWaitingPoint(queueIdx, agent);
                SteerToPosition(waitingPoint, arrivalTolerance: 5f, description: "Wait in harbor queue");
            }
        }
    }

    public void OnDockGranted(TradePort port)
    {
        currentTargetPort = port;
        currentState = TradeRouteState.Trading;
        stateTimer = tradeDwellTime;

        // Steer to assigned lateral berth position at the dock
        int slotIdx = port.GetDockedSlotIndex(this);
        Vector3 berthPoint = port.GetBerthPoint(slotIdx, agent);
        SteerToPosition(berthPoint, arrivalTolerance: 4f, description: "Dock at harbor berth");
    }

    private void UpdateWaitingForDock(TradePort port)
    {
        if (port == null || !port.IsOperational)
        {
            TradingRoute route = TradingRouteManager.Instance.GetRoute(assignedRouteId);
            if (route != null) AdvanceToNextStation(route);
            return;
        }

        if (port.RequestDock(this))
        {
            OnDockGranted(port);
            return;
        }

        // Periodically verify ship is holding its designated queue position
        int queueIdx = port.GetQueueIndex(this);
        Vector3 waitingPoint = port.GetWaitingPoint(queueIdx, agent);
        if (Vector3.Distance(transform.position, waitingPoint) > 6f)
        {
            SteerToPosition(waitingPoint, arrivalTolerance: 5f, description: "Hold queue position");
        }
    }

    private void UpdateTrading(TradingRoute route, TradeRouteStation station, TradePort port)
    {
        if (port == null || !port.IsOperational)
        {
            AdvanceToNextStation(route);
            return;
        }

        ExecuteStationCargoTargets(station, port);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            if (route.mode == TradeRouteMode.Smart && !AreCargoTargetsSatisfied(station))
            {
                currentState = TradeRouteState.WaitingForCargoCondition;
                smartCheckTimer = 2.0f;
                return;
            }

            if (route.mode == TradeRouteMode.OneTime && currentStationIndex >= route.stations.Count - 1)
            {
                port.ReleaseDock(this);
                StopRoute();
                if (TradingRouteManager.Instance != null && unit != null)
                {
                    TradingRouteManager.Instance.UnassignShip(unit);
                }
                return;
            }

            port.ReleaseDock(this);
            currentState = TradeRouteState.Advancing;
        }
    }

    private void UpdateWaitingForCargo(TradingRoute route, TradeRouteStation station, TradePort port)
    {
        if (port == null || !port.IsOperational)
        {
            AdvanceToNextStation(route);
            return;
        }

        // If player changed route mode to Continuous, release dock and advance immediately
        if (route.mode != TradeRouteMode.Smart)
        {
            port.ReleaseDock(this);
            currentState = TradeRouteState.Advancing;
            return;
        }

        // If cargo targets became satisfied (e.g. player edited desired amounts), depart immediately
        if (AreCargoTargetsSatisfied(station))
        {
            port.ReleaseDock(this);
            currentState = TradeRouteState.Advancing;
            return;
        }

        smartCheckTimer -= Time.deltaTime;
        if (smartCheckTimer <= 0f)
        {
            smartCheckTimer = 2.0f;
            ExecuteStationCargoTargets(station, port);

            if (AreCargoTargetsSatisfied(station))
            {
                port.ReleaseDock(this);
                currentState = TradeRouteState.Advancing;
            }
        }
    }

    private void AdvanceToNextStation(TradingRoute route)
    {
        if (currentTargetPort != null)
        {
            currentTargetPort.ReleaseDock(this);
            currentTargetPort = null;
        }

        currentStationIndex = (currentStationIndex + 1) % route.stations.Count;

        if (route.stations.Count > 0)
        {
            TradeRouteStation nextStation = route.stations[currentStationIndex];
            Island nextIsland = ResolveIsland(nextStation);
            if (nextIsland != null)
            {
                TradePort nextPort = TradePort.ResolveForIsland(nextIsland);
                if (nextPort != null && nextPort.IsOperational)
                {
                    BeginTravelToStation(nextPort);
                    return;
                }
            }
        }

        currentState = TradeRouteState.Idle;
    }

    private void SteerToPosition(Vector3 destination, float arrivalTolerance, string description)
    {
        currentDestination = destination;
        if (unit != null && unit.CommandExecutor != null)
        {
            unit.CommandExecutor.IssueCommand(
                new MoveCommand(destination, arrivalTolerance: arrivalTolerance, description: description),
                queue: false,
                isPlayerOrder: false
            );
        }
        else if (unitMovement != null)
        {
            unitMovement.SetDirectDestination(destination);
        }
    }

    #endregion

    #region Cargo Execution

    private void ExecuteStationCargoTargets(TradeRouteStation station, TradePort port)
    {
        if (station == null || port == null || unit == null) return;
        if (station.cargoTargets == null || station.cargoTargets.Count == 0) return;

        // Pass 1: ALL UNLOADS FIRST to free up cargo hold slots and capacity
        foreach (var target in station.cargoTargets)
        {
            if (target == null || target.item == null) continue;

            int currentShipAmount = unitInventory != null ? unitInventory.GetItemQuantity(target.item) : 0;
            int desiredAmount = target.desiredShipAmount;

            if (currentShipAmount > desiredAmount)
            {
                int unloaded = port.ExecuteUnload(unit, target.item, desiredAmount);
                if (unloaded > 0)
                {
                    Debug.Log($"<color=cyan>[TradeRoute] {unit.displayName} unloaded {unloaded}x {target.item.displayName} at {station.stationName}.</color>");
                }
            }
        }

        // Pass 2: ALL LOADS AFTERWARDS into freed capacity
        foreach (var target in station.cargoTargets)
        {
            if (target == null || target.item == null) continue;

            int currentShipAmount = unitInventory != null ? unitInventory.GetItemQuantity(target.item) : 0;
            int desiredAmount = target.desiredShipAmount;

            if (currentShipAmount < desiredAmount)
            {
                int loaded = port.ExecuteLoad(unit, target.item, desiredAmount);
                if (loaded > 0)
                {
                    Debug.Log($"<color=cyan>[TradeRoute] {unit.displayName} loaded {loaded}x {target.item.displayName} at {station.stationName}.</color>");
                }
            }
        }
    }

    private bool AreCargoTargetsSatisfied(TradeRouteStation station)
    {
        if (station == null || unitInventory == null) return true;
        if (station.cargoTargets == null || station.cargoTargets.Count == 0) return true;

        foreach (var target in station.cargoTargets)
        {
            if (target == null || target.item == null) continue;
            int currentShipAmount = unitInventory.GetItemQuantity(target.item);
            if (currentShipAmount != target.desiredShipAmount)
            {
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Public Management & Helpers

    public void SetRoute(string routeId)
    {
        if (assignedRouteId != routeId)
        {
            if (currentTargetPort != null)
            {
                currentTargetPort.ReleaseDock(this);
            }
            assignedRouteId = routeId;
            currentStationIndex = 0;
            isPausedByPlayer = false;
            currentState = TradeRouteState.Idle;
        }
    }

    public void StopRoute()
    {
        if (currentTargetPort != null)
        {
            currentTargetPort.ReleaseDock(this);
            currentTargetPort = null;
        }

        assignedRouteId = null;
        isPausedByPlayer = false;
        currentState = TradeRouteState.Idle;

        if (unit != null && unit.CommandExecutor != null)
        {
            unit.CommandExecutor.ClearCommands();
        }
        else if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private Island ResolveIsland(TradeRouteStation station)
    {
        return TradingRouteManager.ResolveIsland(station);
    }

    private void OnDestroy()
    {
        if (currentTargetPort != null)
        {
            currentTargetPort.ReleaseDock(this);
        }

        if (unit != null && unit.CommandExecutor != null)
        {
            unit.CommandExecutor.UnregisterAutonomousSource(this);
        }

        if (TradingRouteManager.Instance != null && unit != null)
        {
            TradingRouteManager.Instance.UnassignShip(unit);
        }
    }

    #endregion
}
