using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Authoritative maritime trade port / docking authority for an island or harbor.
/// Governs ship docking throughput, water approach positions, island export reserves,
/// and authorized inventory transfers.
/// </summary>
public class TradePort : MonoBehaviour
{
    [Header("Port Capacity & Throughput")]
    [Tooltip("Maximum number of ships that may simultaneously dock and trade.")]
    [SerializeField] private int maxDockingSlots = 2;

    [Tooltip("Radius around the approach point considered within docking range.")]
    [SerializeField] private float dockingDistance = 15f;

    [Header("References")]
    [SerializeField] private Island island;
    [SerializeField] private Building harborBuilding;

    private readonly List<ShipTradeRouteController> activeDockedShips = new List<ShipTradeRouteController>();
    private readonly List<ShipTradeRouteController> waitingQueue = new List<ShipTradeRouteController>();

    private Vector3? cachedApproachPoint;

    public Island Island
    {
        get
        {
            if (island == null) island = GetComponentInParent<Island>();
            return island;
        }
        set => island = value;
    }

    public Building HarborBuilding
    {
        get => harborBuilding;
        set
        {
            harborBuilding = value;
            cachedApproachPoint = null;
        }
    }

    /// <summary>
    /// Dynamic throughput capacity: scales based on operational harbor facilities on this island.
    /// </summary>
    public int MaxDockingSlots
    {
        get
        {
            int baseSlots = Mathf.Max(1, maxDockingSlots);
            if (Island != null && Island.buildings != null)
            {
                int harborCount = 0;
                foreach (var b in Island.buildings)
                {
                    if (b != null && b.gameObject.activeInHierarchy && b.CurrentState != BuildingEnums.BuildingState.Destroyed)
                    {
                        var props = b.GetComponent<BuildingProperties>();
                        if (props != null && InfluenceManager.IsHarborBuilding(props))
                        {
                            harborCount++;
                        }
                    }
                }
                if (harborCount > 1)
                {
                    baseSlots += (harborCount - 1) * 2;
                }
            }
            return baseSlots;
        }
    }

    public int ActiveDockedCount => activeDockedShips.Count;
    public int WaitingQueueCount => waitingQueue.Count;
    public float DockingDistance => dockingDistance;

    /// <summary>
    /// Returns true if this port and its backing island and harbor infrastructure are active and operational.
    /// </summary>
    public bool IsOperational
    {
        get
        {
            if (Island == null || !Island.gameObject.activeInHierarchy) return false;
            if (IslandInventory == null) return false;

            if (harborBuilding != null)
            {
                if (!harborBuilding.gameObject.activeInHierarchy) return false;
                if (harborBuilding.CurrentState == BuildingEnums.BuildingState.Destroyed) return false;
                return true;
            }

            return HasOperationalHarborOnIsland(Island);
        }
    }

    private void Awake()
    {
        if (island == null) island = GetComponentInParent<Island>();
    }

    private void Update()
    {
        // Reconcile any destroyed or inactive ships
        activeDockedShips.RemoveAll(s => s == null || !s.isActiveAndEnabled);
        waitingQueue.RemoveAll(s => s == null || !s.isActiveAndEnabled);

        // Process waiting queue if slots opened up
        int maxSlots = MaxDockingSlots;
        while (activeDockedShips.Count < maxSlots && waitingQueue.Count > 0)
        {
            var nextShip = waitingQueue[0];
            waitingQueue.RemoveAt(0);

            if (nextShip != null && nextShip.isActiveAndEnabled && nextShip.CurrentState == TradeRouteState.WaitingForDock)
            {
                activeDockedShips.Add(nextShip);
                nextShip.OnDockGranted(this);
            }
        }
    }

    public void SetHarbor(Island targetIsland, Building building)
    {
        island = targetIsland;
        harborBuilding = building;
        cachedApproachPoint = null;
    }

    #region Docking Throughput

    /// <summary>
    /// Requests a docking slot. Returns true if granted immediately, false if queued.
    /// </summary>
    public bool RequestDock(ShipTradeRouteController ship)
    {
        if (ship == null) return false;

        // Clean stale references
        activeDockedShips.RemoveAll(s => s == null || !s.isActiveAndEnabled);
        waitingQueue.RemoveAll(s => s == null || !s.isActiveAndEnabled);

        if (activeDockedShips.Contains(ship))
        {
            return true;
        }

        if (activeDockedShips.Count < MaxDockingSlots)
        {
            waitingQueue.Remove(ship);
            activeDockedShips.Add(ship);
            return true;
        }

        if (!waitingQueue.Contains(ship))
        {
            waitingQueue.Add(ship);
        }

        return false;
    }

    /// <summary>
    /// Releases a docking slot when a ship completes trade or departs.
    /// </summary>
    public void ReleaseDock(ShipTradeRouteController ship)
    {
        if (ship == null) return;

        activeDockedShips.Remove(ship);
        waitingQueue.Remove(ship);
    }

    public int GetDockedSlotIndex(ShipTradeRouteController ship)
    {
        if (ship == null) return -1;
        return activeDockedShips.IndexOf(ship);
    }

    public int GetQueueIndex(ShipTradeRouteController ship)
    {
        if (ship == null) return -1;
        return waitingQueue.IndexOf(ship);
    }

    public bool IsDocked(ShipTradeRouteController ship)
    {
        return ship != null && activeDockedShips.Contains(ship);
    }

    public bool IsWaiting(ShipTradeRouteController ship)
    {
        return ship != null && waitingQueue.Contains(ship);
    }

    #endregion

    #region Spatial Coordinates (Approach, Berths & Waiting Anchors)

    /// <summary>
    /// Calculates or returns the optimal navigable water approach point near this port.
    /// </summary>
    public Vector3 GetApproachPoint(NavMeshAgent shipAgent = null)
    {
        if (cachedApproachPoint.HasValue)
        {
            return cachedApproachPoint.Value;
        }

        Vector3 searchCenter = transform.position;
        if (harborBuilding != null)
        {
            // Center slightly seaward of the harbor building
            Vector3 fwd = harborBuilding.transform.forward;
            fwd.y = 0f;
            searchCenter = harborBuilding.transform.position + (fwd.normalized * 8f);
        }
        else if (Island != null)
        {
            var depot = Island.GetComponentInChildren<Depot>();
            if (depot != null)
            {
                searchCenter = depot.transform.position;
            }
            else
            {
                searchCenter = Island.bounds.center;
            }
        }

        int agentTypeId = shipAgent != null ? shipAgent.agentTypeID : 0;
        int areaMask = shipAgent != null ? shipAgent.areaMask : -1;

        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agentTypeId,
            areaMask = areaMask
        };

        float[] sampleRadii = { 15f, 30f, 60f, 120f, 250f, 500f };
        foreach (float radius in sampleRadii)
        {
            if (NavMesh.SamplePosition(searchCenter, out NavMeshHit hit, radius, filter))
            {
                cachedApproachPoint = hit.position;
                return hit.position;
            }
        }

        Vector3 fallback = new Vector3(searchCenter.x, 0f, searchCenter.z);
        cachedApproachPoint = fallback;
        return fallback;
    }

    /// <summary>
    /// Calculates a distinct lateral docking berth position for a specific dock slot.
    /// Prevents multiple concurrently docked ships from overlapping at the exact same world coordinate.
    /// </summary>
    public Vector3 GetBerthPoint(int slotIndex, NavMeshAgent shipAgent = null)
    {
        Vector3 basePoint = GetApproachPoint(shipAgent);
        if (slotIndex <= 0) return basePoint;

        Vector3 seawardDir = GetSeawardDirection();
        Vector3 lateralDir = Vector3.Cross(Vector3.up, seawardDir).normalized;

        // Alternate berths laterally: slot 1 -> +8m, slot 2 -> -8m, slot 3 -> +16m, etc.
        float offsetDistance = ((slotIndex + 1) / 2) * 8f;
        float sign = (slotIndex % 2 == 1) ? 1f : -1f;
        Vector3 candidate = basePoint + lateralDir * (offsetDistance * sign);

        int agentTypeId = shipAgent != null ? shipAgent.agentTypeID : 0;
        int areaMask = shipAgent != null ? shipAgent.areaMask : -1;
        var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = areaMask };

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 15f, filter))
        {
            return hit.position;
        }

        return basePoint;
    }

    /// <summary>
    /// Calculates an offshore stand-off waiting anchor for ships waiting in the harbor throughput queue.
    /// Staggers ships based on their queue index so they wait in an orderly manner out at sea.
    /// </summary>
    public Vector3 GetWaitingPoint(int queueIndex, NavMeshAgent shipAgent = null)
    {
        Vector3 basePoint = GetApproachPoint(shipAgent);
        Vector3 seawardDir = GetSeawardDirection();
        Vector3 lateralDir = Vector3.Cross(Vector3.up, seawardDir).normalized;

        float distance = dockingDistance + 12f + (Mathf.Max(0, queueIndex) * 14f);
        float stagger = ((queueIndex % 2 == 0) ? 1f : -1f) * 6f;

        Vector3 candidate = basePoint + (seawardDir * distance) + (lateralDir * stagger);

        int agentTypeId = shipAgent != null ? shipAgent.agentTypeID : 0;
        int areaMask = shipAgent != null ? shipAgent.areaMask : -1;
        var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = areaMask };

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 20f, filter))
        {
            return hit.position;
        }

        return candidate;
    }

    private Vector3 GetSeawardDirection()
    {
        if (harborBuilding != null)
        {
            Vector3 fwd = harborBuilding.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.01f) return fwd.normalized;
        }

        if (Island != null && cachedApproachPoint.HasValue)
        {
            Vector3 outFromCenter = cachedApproachPoint.Value - Island.bounds.center;
            outFromCenter.y = 0f;
            if (outFromCenter.sqrMagnitude > 0.01f) return outFromCenter.normalized;
        }

        return Vector3.forward;
    }

    #endregion

    #region Authorized Island Storage Queries & Transfers

    public Inventory IslandInventory
    {
        get
        {
            if (Island == null) return null;
            return Island.GetComponent<Inventory>();
        }
    }

    public IslandTradeRules TradeRules
    {
        get
        {
            if (Island == null) return null;
            return Island.TradeRules;
        }
    }

    public int GetAvailableStock(ItemData item)
    {
        if (item == null || IslandInventory == null) return 0;
        return IslandInventory.GetItemAmount(item);
    }

    public int GetMinimumReserve(ItemData item)
    {
        if (item == null || TradeRules == null) return 0;
        var rule = TradeRules.GetRule(item);
        return rule != null ? Mathf.Max(0, rule.MinStockToRetain) : 0;
    }

    /// <summary>
    /// Returns the maximum amount of goods legally permitted to be exported from this island,
    /// strictly respecting the island's protected minimum reserve (IslandTradeRules.MinStockToRetain).
    /// </summary>
    public int GetAvailableForExport(ItemData item)
    {
        if (item == null || IslandInventory == null) return 0;
        return IslandInventory.GetAvailableForExport(item);
    }

    /// <summary>
    /// Returns the remaining capacity the island storage can accept for this item.
    /// </summary>
    public int GetCapacityForImport(ItemData item)
    {
        if (item == null || IslandInventory == null) return 0;
        return IslandInventory.GetRemainingCapacity(item);
    }

    /// <summary>
    /// Executes an authorized load transfer from the island storage into the ship's inventory.
    /// Calculates: Load = min(target - current, availableForExport, shipCapacityLeft).
    /// Returns the actual amount loaded.
    /// </summary>
    public int ExecuteLoad(Unit shipUnit, ItemData item, int targetAmount)
    {
        if (shipUnit == null || item == null) return 0;
        if (IslandInventory == null) return 0;

        var shipInventory = shipUnit.GetComponent<UnitInventory>();
        if (shipInventory == null) return 0;

        int currentShipAmount = shipInventory.GetItemQuantity(item);
        if (currentShipAmount >= targetAmount) return 0;

        int availableForExport = GetAvailableForExport(item);
        if (availableForExport <= 0) return 0;

        int shipCapacityLeft = shipInventory.GetRemainingCapacity(item);
        if (shipCapacityLeft <= 0) return 0;

        int loadAmount = Mathf.Min(
            targetAmount - currentShipAmount,
            availableForExport,
            shipCapacityLeft
        );

        if (loadAmount <= 0) return 0;

        if (!IslandInventory.CanRemove(item, loadAmount))
        {
            int islandHeld = IslandInventory.GetItemAmount(item);
            int safeReserve = GetMinimumReserve(item);
            loadAmount = Mathf.Min(loadAmount, Mathf.Max(0, islandHeld - safeReserve));
            if (loadAmount <= 0) return 0;
        }

        if (shipInventory.AddItem(item, loadAmount, "TradePort Load"))
        {
            IslandInventory.RemoveItem(item, loadAmount);
            return loadAmount;
        }

        return 0;
    }

    /// <summary>
    /// Executes an authorized unload transfer from the ship's inventory into the island storage.
    /// Calculates: Unload = min(current - target, availableDestinationCapacity).
    /// Returns the actual amount unloaded.
    /// </summary>
    public int ExecuteUnload(Unit shipUnit, ItemData item, int targetAmount)
    {
        if (shipUnit == null || item == null) return 0;
        if (IslandInventory == null) return 0;

        var shipInventory = shipUnit.GetComponent<UnitInventory>();
        if (shipInventory == null) return 0;

        int currentShipAmount = shipInventory.GetItemQuantity(item);
        if (currentShipAmount <= targetAmount) return 0;

        int destinationCapacityLeft = GetCapacityForImport(item);
        if (destinationCapacityLeft <= 0) return 0;

        int unloadAmount = Mathf.Min(
            currentShipAmount - targetAmount,
            destinationCapacityLeft
        );

        if (unloadAmount <= 0) return 0;

        if (!IslandInventory.CanAdd(item, unloadAmount))
        {
            return 0;
        }

        if (shipInventory.RemoveItem(item, unloadAmount))
        {
            IslandInventory.AddItem(item, unloadAmount);
            return unloadAmount;
        }

        return 0;
    }

    #endregion

    #region Authoritative Harbor & Port Resolution

    public static bool HasOperationalHarborOnIsland(Island targetIsland)
    {
        if (targetIsland == null || !targetIsland.gameObject.activeInHierarchy) return false;

        if (targetIsland.buildings != null)
        {
            foreach (var b in targetIsland.buildings)
            {
                if (b == null || !b.gameObject.activeInHierarchy) continue;
                if (b.CurrentState == BuildingEnums.BuildingState.Destroyed) continue;
                var props = b.GetComponent<BuildingProperties>();
                if (props != null && InfluenceManager.IsHarborBuilding(props))
                {
                    return true;
                }
            }
        }

        var depot = targetIsland.GetComponentInChildren<Depot>();
        if (depot != null && depot.gameObject.activeInHierarchy)
        {
            var b = depot.GetComponent<Building>();
            if (b == null || b.CurrentState != BuildingEnums.BuildingState.Destroyed)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves the operational TradePort authority for an island via its harbor buildings or depot.
    /// If the island lacks an operational harbor/depot, returns null unless createFallback is explicitly requested.
    /// </summary>
    public static TradePort ResolveForIsland(Island targetIsland, bool createFallback = false)
    {
        if (targetIsland == null || !targetIsland.gameObject.activeInHierarchy) return null;

        // 1. Check existing building registry on the island
        if (targetIsland.buildings != null)
        {
            foreach (var b in targetIsland.buildings)
            {
                if (b == null || !b.gameObject.activeInHierarchy) continue;
                if (b.CurrentState == BuildingEnums.BuildingState.Destroyed) continue;

                var props = b.GetComponent<BuildingProperties>();
                if (props != null && InfluenceManager.IsHarborBuilding(props))
                {
                    var port = b.GetComponent<TradePort>();
                    if (port == null)
                    {
                        port = b.gameObject.AddComponent<TradePort>();
                        port.SetHarbor(targetIsland, b);
                    }
                    else if (port.HarborBuilding != b)
                    {
                        port.SetHarbor(targetIsland, b);
                    }
                    return port;
                }
            }
        }

        // 2. Check any existing TradePort in island children
        var existingPorts = targetIsland.GetComponentsInChildren<TradePort>();
        foreach (var p in existingPorts)
        {
            if (p != null && p.IsOperational) return p;
        }

        // 3. Check any active Depot on island
        var depots = targetIsland.GetComponentsInChildren<Depot>();
        foreach (var depot in depots)
        {
            if (depot == null || !depot.gameObject.activeInHierarchy) continue;
            var b = depot.GetComponent<Building>();
            if (b != null && b.CurrentState == BuildingEnums.BuildingState.Destroyed) continue;

            var port = depot.GetComponent<TradePort>();
            if (port == null)
            {
                port = depot.gameObject.AddComponent<TradePort>();
                port.SetHarbor(targetIsland, b);
            }
            return port;
        }

        // 4. Fallback: only attach to island root if explicitly allowed
        if (createFallback)
        {
            var rootPort = targetIsland.GetComponent<TradePort>();
            if (rootPort == null)
            {
                rootPort = targetIsland.gameObject.AddComponent<TradePort>();
                rootPort.SetHarbor(targetIsland, null);
            }
            return rootPort;
        }

        return null;
    }

    #endregion
}
