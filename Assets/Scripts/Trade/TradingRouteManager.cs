using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Authoritative manager and repository for all trading routes in the match.
/// Owns route configuration, CRUD operations, ship assignments, and persistence hooks.
/// </summary>
public class TradingRouteManager : MonoBehaviour
{
    private static TradingRouteManager _instance;
    public static TradingRouteManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TradingRouteManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("TradingRouteManager");
                    _instance = go.AddComponent<TradingRouteManager>();
                    if (Application.isPlaying)
                    {
                        DontDestroyOnLoad(go);
                    }
                }
            }
            return _instance;
        }
    }

    [SerializeField] private List<TradingRoute> routes = new List<TradingRoute>();
    private TradingRoute selectedRoute;

    public IReadOnlyList<TradingRoute> Routes => routes;
    public TradingRoute SelectedRoute => selectedRoute;

    // Events
    public event Action OnRoutesChanged;
    public event Action<TradingRoute> OnRouteSelected;
    public event Action<TradingRoute> OnRouteUpdated;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Start()
    {
        // Reconcile on start
        ReconcileDestroyedShips();
    }

    private void Update()
    {
        // Periodically cleanse destroyed ships
        if (Time.frameCount % 120 == 0)
        {
            ReconcileDestroyedShips();
        }
    }

    /// <summary>
    /// Creates a valid empty route with a sensible default name and immediately selects it.
    /// </summary>
    public TradingRoute CreateRoute(string customName = null)
    {
        string routeName = customName;
        if (string.IsNullOrWhiteSpace(routeName))
        {
            int number = routes.Count + 1;
            routeName = $"Trading Route {number}";
            while (routes.Exists(r => r.name.Equals(routeName, StringComparison.OrdinalIgnoreCase)))
            {
                number++;
                routeName = $"Trading Route {number}";
            }
        }

        var newRoute = new TradingRoute(routeName);
        routes.Add(newRoute);
        SelectRoute(newRoute.id);
        NotifyRoutesChanged();
        return newRoute;
    }

    /// <summary>
    /// Deletes a route, halts and detaches any assigned ships cleanly.
    /// </summary>
    public bool DeleteRoute(string routeId)
    {
        if (string.IsNullOrEmpty(routeId)) return false;

        TradingRoute route = routes.Find(r => r.id == routeId);
        if (route == null) return false;

        // Unassign all ships executing this route
        var shipIds = new List<string>(route.assignedShipIds);
        foreach (string shipId in shipIds)
        {
            Unit ship = FindUnitById(shipId);
            if (ship != null)
            {
                var controller = ship.GetComponent<ShipTradeRouteController>();
                if (controller != null)
                {
                    controller.StopRoute();
                }
            }
        }

        bool wasSelected = selectedRoute == route;
        routes.Remove(route);

        if (wasSelected)
        {
            selectedRoute = routes.Count > 0 ? routes[0] : null;
            OnRouteSelected?.Invoke(selectedRoute);
        }

        NotifyRoutesChanged();
        return true;
    }

    public TradingRoute GetRoute(string routeId)
    {
        if (string.IsNullOrEmpty(routeId)) return null;
        return routes.Find(r => r.id == routeId);
    }

    public void SelectRoute(string routeId)
    {
        selectedRoute = GetRoute(routeId);
        OnRouteSelected?.Invoke(selectedRoute);
    }

    public void SelectRoute(TradingRoute route)
    {
        selectedRoute = route;
        OnRouteSelected?.Invoke(selectedRoute);
    }

    /// <summary>
    /// Assigns a ship to a specific route. Enforces one authoritative active trading route per ship.
    /// </summary>
    public bool AssignShip(string routeId, Unit ship)
    {
        if (ship == null) return false;
        TradingRoute route = GetRoute(routeId);
        if (route == null) return false;

        string shipId = ship.ID;

        // Unassign from any prior route
        foreach (var otherRoute in routes)
        {
            if (otherRoute.assignedShipIds.Contains(shipId))
            {
                otherRoute.UnassignShip(shipId);
            }
        }

        if (route.AssignShip(shipId))
        {
            var controller = ship.GetComponent<ShipTradeRouteController>();
            if (controller == null)
            {
                controller = ship.gameObject.AddComponent<ShipTradeRouteController>();
            }
            controller.SetRoute(route.id);

            NotifyRouteUpdated(route);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes a ship from its assigned route, safely halting its execution without deleting the route.
    /// </summary>
    public bool UnassignShip(Unit ship)
    {
        if (ship == null) return false;
        string shipId = ship.ID;

        var controller = ship.GetComponent<ShipTradeRouteController>();
        if (controller != null)
        {
            controller.StopRoute();
        }

        bool changed = false;
        foreach (var route in routes)
        {
            if (route.UnassignShip(shipId))
            {
                changed = true;
                NotifyRouteUpdated(route);
            }
        }

        return changed;
    }

    public TradingRoute GetAssignedRouteForShip(string shipId)
    {
        if (string.IsNullOrEmpty(shipId)) return null;
        return routes.Find(r => r.assignedShipIds.Contains(shipId));
    }

    /// <summary>
    /// Resumes a ship's paused trade route.
    /// </summary>
    public bool ResumeShipRoute(Unit ship)
    {
        if (ship == null) return false;
        var controller = ship.GetComponent<ShipTradeRouteController>();
        if (controller != null && controller.IsPaused)
        {
            controller.ResumeRoute();
            TradingRoute route = GetAssignedRouteForShip(ship.ID);
            if (route != null) NotifyRouteUpdated(route);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Authoritatively resolves an Island reference from a TradeRouteStation.
    /// </summary>
    public static Island ResolveIsland(TradeRouteStation station)
    {
        if (station == null) return null;

        if (IslandManager.instance != null && IslandManager.instance.islands != null)
        {
            // 1. By Island ID
            if (!string.IsNullOrEmpty(station.islandId))
            {
                var match = IslandManager.instance.islands.Find(i => i != null && i.ID == station.islandId);
                if (match != null) return match;
            }

            // 2. By numeric id
            if (station.islandIndex > 0)
            {
                var match = IslandManager.instance.GetIsland(station.islandIndex);
                if (match != null) return match;
            }

            // 3. By name
            if (!string.IsNullOrEmpty(station.stationName))
            {
                var match = IslandManager.instance.GetIslandByName(station.stationName);
                if (match != null) return match;
            }
        }

        return null;
    }

    /// <summary>
    /// Safely purges destroyed or invalid ship references across all routes.
    /// </summary>
    public void ReconcileDestroyedShips()
    {
        bool changed = false;
        foreach (var route in routes)
        {
            for (int i = route.assignedShipIds.Count - 1; i >= 0; i--)
            {
                string shipId = route.assignedShipIds[i];
                Unit ship = FindUnitById(shipId);
                if (ship == null)
                {
                    route.assignedShipIds.RemoveAt(i);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            NotifyRoutesChanged();
        }
    }

    public void NotifyRoutesChanged()
    {
        OnRoutesChanged?.Invoke();
    }

    public void NotifyRouteUpdated(TradingRoute route)
    {
        if (route == null) return;
        OnRouteUpdated?.Invoke(route);
        if (selectedRoute == route)
        {
            OnRouteSelected?.Invoke(selectedRoute);
        }
    }

    public Unit FindUnitById(string unitId)
    {
        if (string.IsNullOrEmpty(unitId) || UnitSelections.Instance == null) return null;
        if (UnitSelections.Instance.unitList == null) return null;

        foreach (var unit in UnitSelections.Instance.unitList)
        {
            if (unit != null && unit.ID == unitId)
            {
                return unit;
            }
        }
        return null;
    }

    #region Persistence

    public string ExportToJson()
    {
        var collection = TradingRouteCollectionDTO.FromRoutes(routes);
        return JsonUtility.ToJson(collection, prettyPrint: true);
    }

    public void ImportFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            var collection = JsonUtility.FromJson<TradingRouteCollectionDTO>(json);
            if (collection != null)
            {
                routes = collection.ToRoutes();
                selectedRoute = routes.Count > 0 ? routes[0] : null;

                // Re-bind active ship controllers
                foreach (var route in routes)
                {
                    foreach (var shipId in route.assignedShipIds)
                    {
                        Unit ship = FindUnitById(shipId);
                        if (ship != null)
                        {
                            var controller = ship.GetComponent<ShipTradeRouteController>();
                            if (controller == null)
                            {
                                controller = ship.gameObject.AddComponent<ShipTradeRouteController>();
                            }
                            controller.SetRoute(route.id);
                        }
                    }
                }

                NotifyRoutesChanged();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TradingRouteManager] Failed to import routes from JSON: {ex.Message}");
        }
    }

    public void SaveToFile(string filePath = null)
    {
        string path = filePath ?? Path.Combine(Application.persistentDataPath, "TradingRoutes.json");
        string json = ExportToJson();
        File.WriteAllText(path, json);
        Debug.Log($"[TradingRouteManager] Saved {routes.Count} routes to {path}");
    }

    public void LoadFromFile(string filePath = null)
    {
        string path = filePath ?? Path.Combine(Application.persistentDataPath, "TradingRoutes.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            ImportFromJson(json);
            Debug.Log($"[TradingRouteManager] Loaded routes from {path}");
        }
    }

    #endregion
}
