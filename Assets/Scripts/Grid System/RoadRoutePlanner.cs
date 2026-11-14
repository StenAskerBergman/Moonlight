using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Works out which run of cells a dragged road should occupy.
///
/// Dragging from A to B should spend as few new road tiles as possible, so this is
/// a Dijkstra search where travelling through an existing road cell is free and a
/// cell that still has to be built costs one. Among equally cheap runs the one with
/// the fewest corners wins, which is what turns a straight drag into a straight
/// road rather than a staircase, and makes a detour around a building read as a
/// single clean dog-leg.
///
/// Whether a cell may become a road at all stays RoadPlacer's decision - this only
/// decides which of the legal cells to string together.
/// </summary>
public static class RoadRoutePlanner
{
    // N, E, S, W - same order as RoadTopologyResolver's mask bits.
    private static readonly int[] StepX = { 0, 1, 0, -1 };
    private static readonly int[] StepZ = { 1, 0, -1, 0 };

    // Direction index used by the start cell, which was not entered from anywhere.
    private const int NoDirection = 4;

    // Corners are packed into the low bits of the priority key so tile count always
    // dominates and straightness only ever breaks a tie between equal-cost routes.
    private const int TurnBits = 20;
    private const long MaxTurns = (1L << TurnBits) - 1L;

    public const int DefaultMaxNewTiles = 256;

    // How far past the straight-line distance a route may wander to get around
    // water, cliffs or buildings. This is what keeps the search area proportional
    // to the drag instead of creeping over the whole island on every replan.
    private const int DetourAllowance = 16;

    /// <summary>
    /// The cells to lay road on to get from <paramref name="start"/> to
    /// <paramref name="goal"/>, both included, or null when no legal run exists
    /// within <paramref name="maxNewTiles"/>. Cells that are already road are part
    /// of the returned route - placing them again is a no-op, and they are what
    /// makes reusing an existing stretch cheaper than building beside it.
    /// </summary>
    public static List<Cell> Plan(
        GridSystem grid,
        RoadPlacer placer,
        RoadDefinition definition,
        Cell start,
        Cell goal,
        int maxNewTiles = DefaultMaxNewTiles)
    {
        if (grid == null || placer == null || start == null || goal == null) return null;

        if (!TryGetStepCost(placer, definition, start, NoDirection, out int startCost)) return null;
        if (!TryGetStepCost(placer, definition, goal, NoDirection, out _)) return null;

        int straightLine = Mathf.Abs(start.cellPosition.x - goal.cellPosition.x)
                           + Mathf.Abs(start.cellPosition.z - goal.cellPosition.z);
        int tileBudget = Mathf.Min(maxNewTiles, straightLine * 2 + DetourAllowance);

        List<Node> nodes = new List<Node>();
        Dictionary<StateKey, long> best = new Dictionary<StateKey, long>();
        MinHeap open = new MinHeap();

        long startKey = Key(startCost, 0);
        nodes.Add(new Node(start, NoDirection, -1, startCost, 0));
        best[new StateKey(start, NoDirection)] = startKey;
        open.Push(startKey, 0);

        while (open.Count > 0)
        {
            int index = open.Pop(out long key);
            Node node = nodes[index];

            // A cheaper way into this same cell/direction was queued later.
            if (best.TryGetValue(new StateKey(node.Cell, node.Direction), out long recorded) && recorded < key)
            {
                continue;
            }

            if (node.Cell == goal) return BuildRoute(nodes, index);

            int bridgeAxis = GetBridgeAxis(placer, definition, node.Cell);

            for (int direction = 0; direction < 4; direction++)
            {
                // A bridge is a straight span: it may only be crossed along its own
                // axis, and a route may not turn a corner while standing on one.
                if (bridgeAxis != 0)
                {
                    if (AxisOf(direction) != bridgeAxis) continue;
                    if (node.Direction != NoDirection && direction != node.Direction) continue;
                }

                Cell next = grid.GetCell(
                    node.Cell.cellPosition.x + StepX[direction],
                    node.Cell.cellPosition.z + StepZ[direction]);

                if (next == null) continue;
                if (!TryGetStepCost(placer, definition, next, direction, out int stepCost)) continue;

                int newTiles = node.NewTiles + stepCost;
                if (newTiles > tileBudget) continue;

                int turns = node.Turns;
                if (node.Direction != NoDirection && direction != node.Direction) turns++;

                long nextKey = Key(newTiles, turns);
                StateKey stateKey = new StateKey(next, direction);
                if (best.TryGetValue(stateKey, out long known) && known <= nextKey) continue;

                best[stateKey] = nextKey;
                nodes.Add(new Node(next, direction, index, newTiles, turns));
                open.Push(nextKey, nodes.Count - 1);
            }
        }

        return null;
    }

    /// <summary>
    /// How many cells of a planned route still have to be built - what the run
    /// actually costs the player, as opposed to how long it looks.
    /// </summary>
    public static int CountNewTiles(IReadOnlyList<Cell> route)
    {
        if (route == null) return 0;

        int count = 0;
        foreach (Cell cell in route)
        {
            if (cell != null && !cell.isRoad) count++;
        }

        return count;
    }

    // Cost of standing on a cell: free if it is already road, one if it has to be
    // built, impossible if RoadPlacer would refuse it. A water cell may only be
    // entered along the axis its bridge span runs on.
    private static bool TryGetStepCost(
        RoadPlacer placer,
        RoadDefinition definition,
        Cell cell,
        int enterDirection,
        out int cost)
    {
        cost = 0;
        if (cell == null) return false;
        if (cell.isRoad) return true;

        if (!placer.CanPlaceRoad(cell, definition, out int bridgeAxis)) return false;
        if (bridgeAxis != 0 && enterDirection != NoDirection && AxisOf(enterDirection) != bridgeAxis) return false;

        cost = 1;
        return true;
    }

    private static int GetBridgeAxis(RoadPlacer placer, RoadDefinition definition, Cell cell)
    {
        if (!BridgePlacementRules.IsBridgeTerrain(cell)) return 0;

        // An already-built bridge tile no longer passes CanPlaceRoad, so ask the
        // bridge rules directly for its axis.
        int maxSpan = definition != null ? definition.MaxBridgeSpan : RoadPlacer.DefaultMaxBridgeSpan;
        BridgePlacementRules.TryGetBridgeAxis(placer.Grid, cell, maxSpan, out int axis);
        return axis;
    }

    private static int AxisOf(int direction)
    {
        return direction == 0 || direction == 2
            ? RoadTopologyResolver.North | RoadTopologyResolver.South
            : RoadTopologyResolver.East | RoadTopologyResolver.West;
    }

    private static long Key(int newTiles, int turns)
    {
        return ((long)newTiles << TurnBits) | (uint)Mathf.Clamp(turns, 0, (int)MaxTurns);
    }

    private static List<Cell> BuildRoute(List<Node> nodes, int endIndex)
    {
        List<Cell> route = new List<Cell>();
        for (int index = endIndex; index >= 0; index = nodes[index].Parent)
        {
            route.Add(nodes[index].Cell);
        }

        route.Reverse();
        return route;
    }

    private readonly struct Node
    {
        public readonly Cell Cell;
        public readonly int Direction;
        public readonly int Parent;
        public readonly int NewTiles;
        public readonly int Turns;

        public Node(Cell cell, int direction, int parent, int newTiles, int turns)
        {
            Cell = cell;
            Direction = direction;
            Parent = parent;
            NewTiles = newTiles;
            Turns = turns;
        }
    }

    // A cell reached from a given direction. The direction is part of the identity
    // because arriving from the side that avoids a corner can be worth more than
    // arriving first.
    private readonly struct StateKey : System.IEquatable<StateKey>
    {
        private readonly Cell _cell;
        private readonly int _direction;

        public StateKey(Cell cell, int direction)
        {
            _cell = cell;
            _direction = direction;
        }

        public bool Equals(StateKey other)
        {
            return ReferenceEquals(_cell, other._cell) && _direction == other._direction;
        }

        public override bool Equals(object obj)
        {
            return obj is StateKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (_cell != null ? _cell.GetHashCode() : 0) * 5 + _direction;
        }
    }

    private sealed class MinHeap
    {
        private long[] _keys = new long[64];
        private int[] _values = new int[64];
        private int _count;

        public int Count => _count;

        public void Push(long key, int value)
        {
            if (_count == _keys.Length)
            {
                System.Array.Resize(ref _keys, _count * 2);
                System.Array.Resize(ref _values, _count * 2);
            }

            int child = _count++;
            _keys[child] = key;
            _values[child] = value;

            while (child > 0)
            {
                int parent = (child - 1) / 2;
                if (_keys[parent] <= _keys[child]) break;
                Swap(parent, child);
                child = parent;
            }
        }

        public int Pop(out long key)
        {
            key = _keys[0];
            int value = _values[0];

            _count--;
            _keys[0] = _keys[_count];
            _values[0] = _values[_count];

            int parent = 0;
            while (true)
            {
                int left = parent * 2 + 1;
                if (left >= _count) break;

                int smallest = left;
                int right = left + 1;
                if (right < _count && _keys[right] < _keys[left]) smallest = right;
                if (_keys[parent] <= _keys[smallest]) break;

                Swap(parent, smallest);
                parent = smallest;
            }

            return value;
        }

        private void Swap(int first, int second)
        {
            (_keys[first], _keys[second]) = (_keys[second], _keys[first]);
            (_values[first], _values[second]) = (_values[second], _values[first]);
        }
    }
}
