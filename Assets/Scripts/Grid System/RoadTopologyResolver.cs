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

        public Result(int connectionMask, int parallelMask, GameObject prefab, float rotation)
        {
            ConnectionMask = connectionMask;
            ParallelMask = parallelMask;
            Prefab = prefab;
            Rotation = rotation;
        }
    }

    public static Result Resolve(GridSystem grid, Cell cell, GameObject fallbackPrefab)
    {
        int connections = GetConnectionMask(grid, cell);
        int parallel = GetParallelMask(grid, cell, connections);
        RoadDefinition definition = cell != null ? cell.roadDefinition : null;

        if (definition != null)
        {
            if (TryFindRule(definition, connections, parallel, out RoadVisualRule rule, out int turns))
            {
                return new Result(connections, parallel, rule.prefab, rule.yRotation + turns * 90f);
            }

            if (parallel != 0 && TryFindRule(definition, connections, 0, out rule, out turns))
            {
                return new Result(connections, parallel, rule.prefab, rule.yRotation + turns * 90f);
            }
        }

        return new Result(connections, parallel, fallbackPrefab, 0f);
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

    private static bool TryFindRule(RoadDefinition definition, int connections, int parallel, out RoadVisualRule match, out int turns)
    {
        foreach (RoadVisualRule rule in definition.VisualRules)
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
