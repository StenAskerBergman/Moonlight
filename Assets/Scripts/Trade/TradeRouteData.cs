using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Operational mode for a trading route.
/// </summary>
public enum TradeRouteMode
{
    /// <summary>
    /// Perform as much of the requested trade as currently possible and leave immediately.
    /// Missing goods or full storage will never deadlock the route.
    /// </summary>
    Continuous = 0,

    /// <summary>
    /// Wait at the station until all configured cargo targets are fully satisfied before departing.
    /// </summary>
    Smart = 1,

    /// <summary>
    /// Execute the sequence of stations once and stop upon completion of the final station.
    /// </summary>
    OneTime = 2
}

/// <summary>
/// Cargo operation type implied by comparing current ship stock against target amount.
/// </summary>
public enum TradeCargoOperation
{
    None = 0,
    Load = 1,
    Unload = 2
}

/// <summary>
/// Desired post-station cargo state for a specific commodity on the ship.
/// In Anno 2070 style: target = 60 means after visiting this station, the ship should hold 60.
/// If the ship currently has 10, it loads 50. If the ship has 80, it unloads 20.
/// </summary>
[Serializable]
public class TradeCargoTarget
{
    public ItemData item;
    public int desiredShipAmount;

    public TradeCargoTarget()
    {
        desiredShipAmount = 0;
    }

    public TradeCargoTarget(ItemData item, int desiredShipAmount)
    {
        this.item = item;
        this.desiredShipAmount = Mathf.Max(0, desiredShipAmount);
    }

    public TradeCargoOperation GetOperation(int currentAmount)
    {
        if (currentAmount < desiredShipAmount) return TradeCargoOperation.Load;
        if (currentAmount > desiredShipAmount) return TradeCargoOperation.Unload;
        return TradeCargoOperation.None;
    }

    public int GetPlannedTransferAmount(int currentAmount)
    {
        return Mathf.Abs(desiredShipAmount - currentAmount);
    }

    public TradeCargoTarget Clone()
    {
        return new TradeCargoTarget(item, desiredShipAmount);
    }
}

/// <summary>
/// A station along a trading route, representing a specific island/harbor call.
/// </summary>
[Serializable]
public class TradeRouteStation
{
    public string id = Guid.NewGuid().ToString();
    public string islandId;
    public int islandIndex;
    public string stationName = "Unnamed Station";
    public List<TradeCargoTarget> cargoTargets = new List<TradeCargoTarget>();

    public TradeRouteStation()
    {
        id = Guid.NewGuid().ToString();
    }

    public TradeRouteStation(Island island)
    {
        id = Guid.NewGuid().ToString();
        if (island != null)
        {
            islandId = island.ID;
            islandIndex = island.id;
            stationName = !string.IsNullOrWhiteSpace(island.islandName) ? island.islandName : island.name;
        }
    }

    public TradeCargoTarget GetTarget(ItemData item)
    {
        if (item == null) return null;
        return cargoTargets.Find(t => t.item == item);
    }

    public void SetTarget(ItemData item, int desiredAmount)
    {
        if (item == null) return;
        var target = GetTarget(item);
        if (target != null)
        {
            target.desiredShipAmount = Mathf.Max(0, desiredAmount);
        }
        else
        {
            cargoTargets.Add(new TradeCargoTarget(item, desiredAmount));
        }
    }

    public void RemoveTarget(ItemData item)
    {
        if (item == null) return;
        cargoTargets.RemoveAll(t => t.item == item);
    }

    public TradeRouteStation Clone()
    {
        var copy = new TradeRouteStation
        {
            id = Guid.NewGuid().ToString(),
            islandId = islandId,
            islandIndex = islandIndex,
            stationName = stationName,
            cargoTargets = new List<TradeCargoTarget>()
        };

        foreach (var target in cargoTargets)
        {
            if (target != null) copy.cargoTargets.Add(target.Clone());
        }

        return copy;
    }
}

/// <summary>
/// Authoritative trade route definition owned by TradingRouteManager.
/// Multiple ships can be assigned to execute this one route configuration.
/// </summary>
[Serializable]
public class TradingRoute
{
    public string id = Guid.NewGuid().ToString();
    public string name = "Trading Route";
    public TradeRouteMode mode = TradeRouteMode.Continuous;
    public List<TradeRouteStation> stations = new List<TradeRouteStation>();
    public List<string> assignedShipIds = new List<string>();

    public TradingRoute()
    {
        id = Guid.NewGuid().ToString();
    }

    public TradingRoute(string routeName)
    {
        id = Guid.NewGuid().ToString();
        name = routeName;
    }

    public bool HasStation(string stationId)
    {
        return stations.Exists(s => s.id == stationId);
    }

    public void AddStation(TradeRouteStation station)
    {
        if (station == null) return;
        stations.Add(station);
    }

    public bool RemoveStation(string stationId)
    {
        int index = stations.FindIndex(s => s.id == stationId);
        if (index >= 0)
        {
            stations.RemoveAt(index);
            return true;
        }
        return false;
    }

    public void MoveStation(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= stations.Count) return;
        if (toIndex < 0 || toIndex >= stations.Count) return;
        if (fromIndex == toIndex) return;

        TradeRouteStation station = stations[fromIndex];
        stations.RemoveAt(fromIndex);
        stations.Insert(toIndex, station);
    }

    public bool AssignShip(string shipId)
    {
        if (string.IsNullOrEmpty(shipId)) return false;
        if (!assignedShipIds.Contains(shipId))
        {
            assignedShipIds.Add(shipId);
            return true;
        }
        return false;
    }

    public bool UnassignShip(string shipId)
    {
        return assignedShipIds.Remove(shipId);
    }
}

#region Serialization DTOs

[Serializable]
public class TradeCargoTargetDTO
{
    public string itemId;
    public int desiredShipAmount;
}

[Serializable]
public class TradeRouteStationDTO
{
    public string stationId;
    public string islandId;
    public int islandIndex;
    public string stationName;
    public List<TradeCargoTargetDTO> targets = new List<TradeCargoTargetDTO>();
}

[Serializable]
public class TradingRouteDTO
{
    public string id;
    public string name;
    public int mode;
    public List<TradeRouteStationDTO> stations = new List<TradeRouteStationDTO>();
    public List<string> assignedShipIds = new List<string>();
}

[Serializable]
public class TradingRouteCollectionDTO
{
    public List<TradingRouteDTO> routes = new List<TradingRouteDTO>();

    public static TradingRouteCollectionDTO FromRoutes(IEnumerable<TradingRoute> routes)
    {
        var collection = new TradingRouteCollectionDTO();
        if (routes == null) return collection;

        foreach (var route in routes)
        {
            if (route == null) continue;
            var rDto = new TradingRouteDTO
            {
                id = route.id,
                name = route.name,
                mode = (int)route.mode,
                assignedShipIds = new List<string>(route.assignedShipIds)
            };

            foreach (var station in route.stations)
            {
                if (station == null) continue;
                var sDto = new TradeRouteStationDTO
                {
                    stationId = station.id,
                    islandId = station.islandId,
                    islandIndex = station.islandIndex,
                    stationName = station.stationName
                };

                foreach (var target in station.cargoTargets)
                {
                    if (target == null || target.item == null) continue;
                    sDto.targets.Add(new TradeCargoTargetDTO
                    {
                        itemId = target.item.Id.FullId,
                        desiredShipAmount = target.desiredShipAmount
                    });
                }

                rDto.stations.Add(sDto);
            }

            collection.routes.Add(rDto);
        }

        return collection;
    }

    public List<TradingRoute> ToRoutes()
    {
        var result = new List<TradingRoute>();
        if (routes == null) return result;

        foreach (var rDto in routes)
        {
            if (rDto == null) continue;
            var route = new TradingRoute
            {
                id = string.IsNullOrEmpty(rDto.id) ? Guid.NewGuid().ToString() : rDto.id,
                name = string.IsNullOrEmpty(rDto.name) ? "Trading Route" : rDto.name,
                mode = (TradeRouteMode)rDto.mode,
                assignedShipIds = rDto.assignedShipIds != null ? new List<string>(rDto.assignedShipIds) : new List<string>()
            };

            if (rDto.stations != null)
            {
                foreach (var sDto in rDto.stations)
                {
                    if (sDto == null) continue;
                    var station = new TradeRouteStation
                    {
                        id = string.IsNullOrEmpty(sDto.stationId) ? Guid.NewGuid().ToString() : sDto.stationId,
                        islandId = sDto.islandId,
                        islandIndex = sDto.islandIndex,
                        stationName = sDto.stationName
                    };

                    if (sDto.targets != null)
                    {
                        foreach (var tDto in sDto.targets)
                        {
                            if (tDto == null || string.IsNullOrEmpty(tDto.itemId)) continue;
                            ItemData item = ItemCatalog.Resolve(tDto.itemId);
                            if (item != null)
                            {
                                station.cargoTargets.Add(new TradeCargoTarget(item, tDto.desiredShipAmount));
                            }
                        }
                    }

                    route.stations.Add(station);
                }
            }

            result.Add(route);
        }

        return result;
    }
}

#endregion
