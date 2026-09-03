using System.Collections.Generic;
using UnityEngine;

public static class RoadTopologyResolver
{
    public const int North = 1;
    public const int East = 2;
    public const int South = 4;
    public const int West = 8;

    public readonly struct Result
    {
        public readonly int ConnectionMask;
        public readonly int ParallelMask;
        public readonly GameObject Prefab;
        public readonly float Rotation;
        public readonly float VerticalOffset;
        public readonly bool IsBridge;
        public readonly int BridgeAxisMask;
        public readonly int BridgeApproachMask;
        public readonly RoadVisualStyle VisualStyle;
        public readonly float Wear;

        public Result(
            int connectionMask,
            int parallelMask,
            GameObject prefab,
            float rotation,
            float verticalOffset = 0f,
            bool isBridge = false,
            int bridgeAxisMask = 0,
            int bridgeApproachMask = 0,
            RoadVisualStyle visualStyle = RoadVisualStyle.CityRoad,
            float wear = 0.45f)
        {
            ConnectionMask = connectionMask;
            ParallelMask = parallelMask;
            Prefab = prefab;
            Rotation = rotation;
            VerticalOffset = verticalOffset;
            IsBridge = isBridge;
            BridgeAxisMask = bridgeAxisMask;
            BridgeApproachMask = bridgeApproachMask;
            VisualStyle = visualStyle;
            Wear = Mathf.Clamp01(wear);
        }
    }

    public static Result Resolve(GridSystem grid, Cell cell, GameObject fallbackPrefab, GameObject fallbackBridgePrefab = null)
    {
        int connections = GetConnectionMask(grid, cell);
        int parallel = GetParallelMask(grid, cell, connections);
        RoadDefinition definition = cell != null ? cell.roadDefinition : null;
        RoadVisualStyle visualStyle = definition != null ? definition.VisualStyle : RoadVisualStyle.CityRoad;
        float wear = definition != null ? definition.Wear : 0.45f;

        if (BridgePlacementRules.IsBridgeTerrain(cell))
        {
            int maxSpan = definition != null ? definition.MaxBridgeSpan : RoadPlacer.DefaultMaxBridgeSpan;
            int axis = 0;
            BridgePlacementRules.TryGetBridgeAxis(grid, cell, maxSpan, out axis);
            IReadOnlyList<RoadVisualRule> bridgeRules = definition != null ? definition.BridgeVisualRules : null;
            if (TryFindRule(bridgeRules, connections, 0, out RoadVisualRule bridgeRule, out int bridgeTurns))
            {
                float deckHeight = definition != null ? definition.BridgeDeckHeight : RoadPlacer.DefaultBridgeDeckHeight;
                return new Result(
                    connections, 0, bridgeRule.prefab, bridgeRule.yRotation + bridgeTurns * 90f,
                    deckHeight, true, axis, 0, visualStyle, wear);
            }

            float rotation = axis == (East | West) ? 90f : 0f;
            float fallbackDeckHeight = definition != null ? definition.BridgeDeckHeight : RoadPlacer.DefaultBridgeDeckHeight;
            return new Result(
                connections, 0, fallbackBridgePrefab != null ? fallbackBridgePrefab : fallbackPrefab, rotation,
                fallbackDeckHeight, true, axis, 0, visualStyle, wear);
        }

        int bridgeApproachMask = GetBridgeNeighborMask(grid, cell);

        if (definition != null)
        {
            if (TryFindRule(definition, connections, parallel, out RoadVisualRule rule, out int turns))
            {
                return new Result(
                    connections, parallel, rule.prefab, rule.yRotation + turns * 90f,
                    0f, false, 0, bridgeApproachMask, visualStyle, wear);
            }

            if (parallel != 0 && TryFindRule(definition, connections, 0, out rule, out turns))
            {
                return new Result(
                    connections, parallel, rule.prefab, rule.yRotation + turns * 90f,
                    0f, false, 0, bridgeApproachMask, visualStyle, wear);
            }
        }

        return new Result(
            connections, parallel, fallbackPrefab, 0f,
            0f, false, 0, bridgeApproachMask, visualStyle, wear);
    }

    public static int GetConnectionMask(GridSystem grid, Cell cell)
    {
        if (grid == null || cell == null || !cell.isRoad) return 0;
        int x = cell.cellPosition.x;
        int z = cell.cellPosition.z;
        int mask = 0;
        if (IsCompatible(cell, grid.GetCell(x, z + 1))) mask |= North;
        if (IsCompatible(cell, grid.GetCell(x + 1, z))) mask |= East;
        if (IsCompatible(cell, grid.GetCell(x, z - 1))) mask |= South;
        if (IsCompatible(cell, grid.GetCell(x - 1, z))) mask |= West;
        return mask;
    }

    private static int GetParallelMask(GridSystem grid, Cell cell, int connections)
    {
        RoadDefinition definition = cell != null ? cell.roadDefinition : null;
        if (grid == null || definition == null || !definition.SupportsParallelDoubleRoad) return 0;

        int axis = GetSingleAxis(connections);
        if (axis == 0) return 0;

        int x = cell.cellPosition.x;
        int z = cell.cellPosition.z;
        int candidates = axis == (North | South) ? East | West : North | South;
        int mask = 0;
        TryAddParallel(grid, cell, grid.GetCell(x, z + 1), North, candidates, axis, ref mask);
        TryAddParallel(grid, cell, grid.GetCell(x + 1, z), East, candidates, axis, ref mask);
        TryAddParallel(grid, cell, grid.GetCell(x, z - 1), South, candidates, axis, ref mask);
        TryAddParallel(grid, cell, grid.GetCell(x - 1, z), West, candidates, axis, ref mask);
        return mask;
    }

    private static void TryAddParallel(GridSystem grid, Cell cell, Cell neighbor, int direction, int candidates, int axis, ref int mask)
    {
        if ((candidates & direction) == 0 || neighbor == null || !neighbor.isRoad) return;
        if (!cell.roadDefinition.FormsDoubleRoadWith(neighbor.roadDefinition)) return;
        if (GetSingleAxis(GetConnectionMask(grid, neighbor)) == axis) mask |= direction;
    }

    private static int GetSingleAxis(int mask)
    {
        bool northSouth = (mask & (North | South)) != 0;
        bool eastWest = (mask & (East | West)) != 0;
        if (northSouth == eastWest) return 0;
        return northSouth ? North | South : East | West;
    }

    private static bool IsCompatible(Cell cell, Cell neighbor)
    {
        if (neighbor == null || !neighbor.isRoad) return false;
        if (cell.roadDefinition == null || neighbor.roadDefinition == null)
            return cell.roadDefinition == null && neighbor.roadDefinition == null;
        return cell.roadDefinition.ConnectsTo(neighbor.roadDefinition);
    }

    private static int GetBridgeNeighborMask(GridSystem grid, Cell cell)
    {
        if (grid == null || cell == null) return 0;

        int x = cell.cellPosition.x;
        int z = cell.cellPosition.z;
        int mask = 0;
        if (IsBridgeRoad(grid.GetCell(x, z + 1))) mask |= North;
        if (IsBridgeRoad(grid.GetCell(x + 1, z))) mask |= East;
        if (IsBridgeRoad(grid.GetCell(x, z - 1))) mask |= South;
        if (IsBridgeRoad(grid.GetCell(x - 1, z))) mask |= West;
        return mask;
    }

    private static bool IsBridgeRoad(Cell cell)
    {
        return cell != null && cell.isRoad && BridgePlacementRules.IsBridgeTerrain(cell);
    }

    private static bool TryFindRule(RoadDefinition definition, int connections, int parallel, out RoadVisualRule match, out int turns)
    {
        return TryFindRule(definition.VisualRules, connections, parallel, out match, out turns);
    }

    private static bool TryFindRule(IReadOnlyList<RoadVisualRule> rules, int connections, int parallel, out RoadVisualRule match, out int turns)
    {
        if (rules != null)
        {
            foreach (RoadVisualRule rule in rules)
            {
                if (rule == null || rule.prefab == null) continue;
                for (int turn = 0; turn < 4; turn++)
                {
                    if (RotateMask(rule.connectionMask, turn) == connections
                        && RotateMask(rule.parallelMask, turn) == parallel)
                    {
                        match = rule;
                        turns = turn;
                        return true;
                    }
                }
            }
        }

        match = null;
        turns = 0;
        return false;
    }

    private static int RotateMask(int mask, int quarterTurns)
    {
        for (int i = 0; i < quarterTurns; i++)
        {
            mask = ((mask & North) != 0 ? East : 0)
                | ((mask & East) != 0 ? South : 0)
                | ((mask & South) != 0 ? West : 0)
                | ((mask & West) != 0 ? North : 0);
        }
        return mask;
    }
}

/// <summary>
/// Owns the terrain rules for a road bridge. A valid bridge is a short, straight
/// run of supported water cells with ordinary road terrain at both ends.
/// </summary>
public static class BridgePlacementRules
{
    public static bool IsBridgeTerrain(Cell cell)
    {
        if (cell == null) return false;

        switch (cell.currentTerrainType)
        {
            case Cell.TerrainType.River:
            case Cell.TerrainType.Lake:
            case Cell.TerrainType.Stream:
            case Cell.TerrainType.Shallow:
            case Cell.TerrainType.Water:
                return true;
            default:
                return false;
        }
    }

    public static bool TryGetBridgeAxis(GridSystem grid, Cell cell, int maxSpan, out int axisMask)
    {
        axisMask = 0;
        if (grid == null || !IsBridgeTerrain(cell)) return false;

        bool northSouth = IsBoundedSpan(grid, cell, 0, 1, maxSpan);
        bool eastWest = IsBoundedSpan(grid, cell, 1, 0, maxSpan);
        if (!northSouth && !eastWest) return false;

        if (northSouth && eastWest)
        {
            int connections = RoadTopologyResolver.GetConnectionMask(grid, cell);
            bool hasNorthSouthRoad = (connections & (RoadTopologyResolver.North | RoadTopologyResolver.South)) != 0;
            bool hasEastWestRoad = (connections & (RoadTopologyResolver.East | RoadTopologyResolver.West)) != 0;
            if (hasEastWestRoad && !hasNorthSouthRoad)
            {
                axisMask = RoadTopologyResolver.East | RoadTopologyResolver.West;
                return true;
            }
        }

        axisMask = northSouth
            ? RoadTopologyResolver.North | RoadTopologyResolver.South
            : RoadTopologyResolver.East | RoadTopologyResolver.West;
        return true;
    }

    private static bool IsBoundedSpan(GridSystem grid, Cell origin, int dx, int dz, int maxSpan)
    {
        int negativeWaterCells;
        int positiveWaterCells;
        bool negativeShore = FindShore(grid, origin, -dx, -dz, maxSpan, out negativeWaterCells);
        bool positiveShore = FindShore(grid, origin, dx, dz, maxSpan, out positiveWaterCells);
        return negativeShore
            && positiveShore
            && negativeWaterCells + 1 + positiveWaterCells <= Mathf.Max(1, maxSpan);
    }

    private static bool FindShore(GridSystem grid, Cell origin, int dx, int dz, int maxSpan, out int waterCells)
    {
        waterCells = 0;
        int x = origin.cellPosition.x;
        int z = origin.cellPosition.z;

        for (int distance = 1; distance <= Mathf.Max(1, maxSpan); distance++)
        {
            Cell candidate = grid.GetCell(x + dx * distance, z + dz * distance);
            if (candidate == null) return false;
            if (IsBridgeTerrain(candidate))
            {
                waterCells++;
                continue;
            }

            return IsRoadTerrain(candidate);
        }

        return false;
    }

    private static bool IsRoadTerrain(Cell cell)
    {
        return cell != null
            && (cell.currentTerrainType == Cell.TerrainType.Land
                || cell.currentTerrainType == Cell.TerrainType.Beach);
    }
}
