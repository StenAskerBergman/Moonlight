using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single unified failure reason for both live preview shaders/HUD and click-to-place transactions.
/// </summary>
public enum PlacementFailureReason
{
    None,
    NoGrid,
    InvalidTerrain,
    BlockedCell,
    OccupiedCell,
    ReservedDeposit,
    MissingRequiredDeposit,
    MissingRoadAccess,
    UnmetRequirement,
    OutOfTerritory,
    OutsideFoundingBoatRange,
    NoFoundingVessel,
    InsufficientBoatCargo,
    InsufficientGoods,
    InsufficientCredits
}

public struct AllocationRequest
{
    public GameObject Prefab;
    public BuildingProperties Properties;
    public BuildingData Data;
    public BuildingCost Cost;
    public GridSystem Grid;
    public Island Island;
    public Vector3 WorldPosition;
    public Vector3Int GridOrigin;
    public Vector2Int Footprint;
    public Inventory IslandGoods;
    public Bank Bank;
    public Unit PreferredBoat;
}

public sealed class PlacementValidationResult
{
    public bool IsPlacementValid;
    public bool IsAffordable;
    public bool CanCommit => IsPlacementValid && IsAffordable;
    public PlacementFailureReason FailureReason = PlacementFailureReason.None;
    public PlacementVerdict RuleVerdict = PlacementVerdict.Valid;
    public bool IsHarbor;
    public bool IsUnsettledHarbor;
    public bool UsesQuayFoundation;
    public Unit FoundingBoat;
    public readonly List<Cell> ConnectedRoadCells = new List<Cell>();

    public void Reset()
    {
        IsPlacementValid = false;
        IsAffordable = false;
        FailureReason = PlacementFailureReason.None;
        RuleVerdict = PlacementVerdict.Valid;
        IsHarbor = false;
        IsUnsettledHarbor = false;
        UsesQuayFoundation = false;
        FoundingBoat = null;
        ConnectedRoadCells.Clear();
    }

    public string FormatReasonLabel()
    {
        switch (FailureReason)
        {
            case PlacementFailureReason.None: return string.Empty;
            case PlacementFailureReason.InvalidTerrain: return "Unsuitable terrain";
            case PlacementFailureReason.BlockedCell: return "Blocked by slope or cliff";
            case PlacementFailureReason.OccupiedCell: return "Space already occupied";
            case PlacementFailureReason.ReservedDeposit: return "Reserved resource site";
            case PlacementFailureReason.MissingRequiredDeposit: return "Requires resource deposit";
            case PlacementFailureReason.MissingRoadAccess: return "Requires road connection";
            case PlacementFailureReason.UnmetRequirement: return "Placement requirement unmet";
            case PlacementFailureReason.OutOfTerritory: return "Outside harbor territory";
            case PlacementFailureReason.OutsideFoundingBoatRange: return "Outside vessel founding range";
            case PlacementFailureReason.NoFoundingVessel: return "Requires ship in coastal range";
            case PlacementFailureReason.InsufficientBoatCargo: return "Vessel lacks building cargo";
            case PlacementFailureReason.InsufficientGoods: return "Insufficient construction materials";
            case PlacementFailureReason.InsufficientCredits: return "Insufficient credits";
            default: return "Cannot build here";
        }
    }
}

/// <summary>
/// Single authoritative evaluator shared by preview overlays, cursor HUD, normal building
/// placement, and stamp placement so preview validity and click commit never diverge.
/// </summary>
public static class PlacementEvaluator
{
    public static PlacementValidationResult Evaluate(in AllocationRequest request, PlacementValidationResult reuse = null)
    {
        PlacementValidationResult result = reuse ?? new PlacementValidationResult();
        result.Reset();

        if (request.Grid == null)
        {
            result.FailureReason = PlacementFailureReason.NoGrid;
            result.RuleVerdict = PlacementVerdict.NoCell;
            return result;
        }

        Vector2Int footprint = new Vector2Int(Mathf.Max(1, request.Footprint.x), Mathf.Max(1, request.Footprint.y));
        BuildingData data = request.Data;
        BuildingProperties properties = request.Properties;
        if (properties == null && request.Prefab != null)
        {
            properties = request.Prefab.GetComponent<BuildingProperties>();
        }
        if (data == null && properties != null)
        {
            data = properties.buildingData;
        }

        BuildingCost cost = request.Cost;
        if (cost == null && request.Prefab != null)
        {
            cost = request.Prefab.GetComponent<BuildingCost>();
        }

        result.UsesQuayFoundation = data != null && data.requiresQuayFoundation;
        result.IsHarbor = InfluenceManager.IsHarborBuilding(properties) || InfluenceManager.IsHarborBuilding(request.Prefab);

        if (RoadNetwork.Instance != null)
        {
            RoadNetwork.Instance.GetAdjacentRoadCells(request.Grid, request.GridOrigin, footprint, result.ConnectedRoadCells);
        }

        // 1. Footprint terrain, occupancy, deposits, and perimeter road rules
        bool footprintOk = PlacementRules.EvaluateFootprint(
            request.Grid,
            request.GridOrigin,
            footprint,
            data,
            out PlacementVerdict ruleVerdict);

        if (!footprintOk)
        {
            result.RuleVerdict = ruleVerdict;
            result.FailureReason = MapVerdict(ruleVerdict, data, request.Grid, request.GridOrigin, footprint);
            EvaluateAffordabilityOnly(request, cost, result);
            return result;
        }

        // 2. Influence / territory / founding boat reach
        InfluenceManager influence = PlacementRules.GetInfluenceManager(request.Island, createIfMissing: false);
        result.IsUnsettledHarbor = result.IsHarbor && (influence == null || !influence.HasWarehouse);

        Unit foundingBoat = request.PreferredBoat;
        bool influenceOk = true;
        if (influence != null)
        {
            influenceOk = PlacementRules.EvaluateInfluence(
                influence,
                result.IsHarbor,
                request.WorldPosition,
                request.Grid,
                out Unit resolvedBoat,
                out PlacementVerdict influenceVerdict);

            if (foundingBoat == null) foundingBoat = resolvedBoat;
            if (!influenceOk)
            {
                result.RuleVerdict = influenceVerdict;
                result.FailureReason = MapVerdict(influenceVerdict, data, request.Grid, request.GridOrigin, footprint);
                result.FoundingBoat = foundingBoat;
                EvaluateAffordabilityOnly(request, cost, result);
                return result;
            }
        }
        else if (!result.IsHarbor && request.Island != null)
        {
            // No InfluenceManager on an island means it has no Warehouse/territory yet.
            result.RuleVerdict = PlacementVerdict.OutOfInfluence;
            result.FailureReason = PlacementFailureReason.OutOfTerritory;
            EvaluateAffordabilityOnly(request, cost, result);
            return result;
        }
        else if (result.IsUnsettledHarbor)
        {
            if (foundingBoat == null)
            {
                foundingBoat = InfluenceManager.GetNearestPlayerBoat(request.WorldPosition);
            }
            if (foundingBoat == null)
            {
                result.RuleVerdict = PlacementVerdict.OutOfBoatRange;
                result.FailureReason = PlacementFailureReason.NoFoundingVessel;
                EvaluateAffordabilityOnly(request, cost, result);
                return result;
            }
        }

        // 3. Non-grid BuildingRequirements (Resource, Zone, Seed, View)
        if (data != null && data.BuildingRequirements != null)
        {
            foreach (BuildingRequirement req in data.BuildingRequirements)
            {
                if (req == null || req is GridRequirement) continue;
                if (!req.IsSatisfied())
                {
                    result.RuleVerdict = PlacementVerdict.UnmetRequirement;
                    result.FailureReason = PlacementFailureReason.UnmetRequirement;
                    result.FoundingBoat = foundingBoat;
                    EvaluateAffordabilityOnly(request, cost, result);
                    return result;
                }
            }
        }

        result.FoundingBoat = foundingBoat;
        result.IsPlacementValid = true;
        EvaluateAffordabilityOnly(request, cost, result);
        return result;
    }

    private static void EvaluateAffordabilityOnly(
        in AllocationRequest request,
        BuildingCost cost,
        PlacementValidationResult result)
    {
        if (cost == null)
        {
            result.IsAffordable = true;
            return;
        }

        int creditPrice = Mathf.Max(0, cost.GetPrice());
        if (creditPrice > 0 && request.Bank != null && !request.Bank.CanAfford(creditPrice))
        {
            result.IsAffordable = false;
            if (result.FailureReason == PlacementFailureReason.None)
            {
                result.FailureReason = PlacementFailureReason.InsufficientCredits;
            }
            return;
        }

        if (result.IsUnsettledHarbor)
        {
            if (result.FoundingBoat == null)
            {
                result.IsAffordable = false;
                if (result.FailureReason == PlacementFailureReason.None)
                {
                    result.FailureReason = PlacementFailureReason.NoFoundingVessel;
                }
                return;
            }
            if (!cost.CanBePaidFromBoat(result.FoundingBoat))
            {
                result.IsAffordable = false;
                if (result.FailureReason == PlacementFailureReason.None)
                {
                    result.FailureReason = PlacementFailureReason.InsufficientBoatCargo;
                }
                return;
            }
        }
        else
        {
            Inventory goods = request.IslandGoods != null
                ? request.IslandGoods
                : (request.Island != null ? request.Island.GetComponent<Inventory>() : null);

            if (!cost.CanBePaidFrom(goods))
            {
                result.IsAffordable = false;
                if (result.FailureReason == PlacementFailureReason.None)
                {
                    result.FailureReason = PlacementFailureReason.InsufficientGoods;
                }
                return;
            }
        }

        result.IsAffordable = true;
    }

    private static PlacementFailureReason MapVerdict(
        PlacementVerdict verdict,
        BuildingData data,
        GridSystem grid,
        Vector3Int origin,
        Vector2Int footprint)
    {
        switch (verdict)
        {
            case PlacementVerdict.Blocked: return PlacementFailureReason.BlockedCell;
            case PlacementVerdict.Occupied: return PlacementFailureReason.OccupiedCell;
            case PlacementVerdict.ReservedDeposit: return PlacementFailureReason.ReservedDeposit;
            case PlacementVerdict.WrongTerrain: return PlacementFailureReason.InvalidTerrain;
            case PlacementVerdict.MissingDeposit: return PlacementFailureReason.MissingRequiredDeposit;
            case PlacementVerdict.OutOfInfluence: return PlacementFailureReason.OutOfTerritory;
            case PlacementVerdict.OutOfBoatRange: return PlacementFailureReason.OutsideFoundingBoatRange;
            case PlacementVerdict.NoCargo: return PlacementFailureReason.InsufficientBoatCargo;
            case PlacementVerdict.UnmetRequirement:
                if (data != null && data.BuildingRequirements != null)
                {
                    foreach (BuildingRequirement req in data.BuildingRequirements)
                    {
                        if (req is GridRequirement gridReq && gridReq.requiresRoadAccess &&
                            (RoadNetwork.Instance == null || !RoadNetwork.Instance.HasRoadAccess(grid, origin, footprint)))
                        {
                            return PlacementFailureReason.MissingRoadAccess;
                        }
                    }
                }
                return PlacementFailureReason.UnmetRequirement;
            default:
                return PlacementFailureReason.InvalidTerrain;
        }
    }
}
