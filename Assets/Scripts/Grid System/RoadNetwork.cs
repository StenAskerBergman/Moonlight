using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Tracks which cells are roads and how they connect to each other, so buildings
// can be queried for road access and roads can be pathed along.
public class RoadNetwork : MonoBehaviour
{
    public static RoadNetwork Instance { get; private set; }

    private HashSet<Cell> _roadCells = new HashSet<Cell>();
    private Dictionary<Cell, List<Cell>> _graph = new Dictionary<Cell, List<Cell>>();

    public IReadOnlyCollection<Cell> AllRoadCells => _roadCells;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RegisterRoadCell(Cell cell)
    {
        if (cell == null || _roadCells.Contains(cell)) return;

        _roadCells.Add(cell);

        if (!_graph.ContainsKey(cell))
        {
            _graph[cell] = new List<Cell>();
        }

        if (cell.neighbors == null) return;

        foreach (Cell neighbor in cell.neighbors)
        {
            if (neighbor == null || !_roadCells.Contains(neighbor) || !AreOrthogonal(cell, neighbor)) continue;

            AddEdge(cell, neighbor);
            AddEdge(neighbor, cell);
        }
    }

    private static bool AreOrthogonal(Cell first, Cell second)
    {
        Vector3Int delta = first.cellPosition - second.cellPosition;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.z) == 1;
    }

    public void UnregisterRoadCell(Cell cell)
    {
        if (cell == null || !_roadCells.Contains(cell)) return;

        _roadCells.Remove(cell);

        if (_graph.TryGetValue(cell, out List<Cell> connections))
        {
            foreach (Cell neighbor in connections)
            {
                if (_graph.TryGetValue(neighbor, out List<Cell> neighborConnections))
                {
                    neighborConnections.Remove(cell);
                }
            }
        }

        _graph.Remove(cell);
    }

    private void AddEdge(Cell from, Cell to)
    {
        if (!_graph.TryGetValue(from, out List<Cell> connections))
        {
            connections = new List<Cell>();
            _graph[from] = connections;
        }

        if (!connections.Contains(to))
        {
            connections.Add(to);
        }
    }

    public bool IsConnected(Cell from, Cell to)
    {
        return GetPath(from, to) != null;
    }

    // BFS shortest path along registered road cells only.
    public List<Cell> GetPath(Cell from, Cell to)
    {
        if (from == null || to == null) return null;
        if (!_roadCells.Contains(from) || !_roadCells.Contains(to)) return null;

        if (from == to) return new List<Cell> { from };

        Queue<Cell> frontier = new Queue<Cell>();
        Dictionary<Cell, Cell> cameFrom = new Dictionary<Cell, Cell>();

        frontier.Enqueue(from);
        cameFrom[from] = null;

        while (frontier.Count > 0)
        {
            Cell current = frontier.Dequeue();

            if (current == to)
            {
                return ReconstructPath(cameFrom, to);
            }

            if (!_graph.TryGetValue(current, out List<Cell> connections)) continue;

            foreach (Cell next in connections)
            {
                if (cameFrom.ContainsKey(next)) continue;

                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        return null;
    }

    private List<Cell> ReconstructPath(Dictionary<Cell, Cell> cameFrom, Cell end)
    {
        List<Cell> path = new List<Cell>();
        Cell current = end;

        while (current != null)
        {
            path.Add(current);
            cameFrom.TryGetValue(current, out current);
        }

        path.Reverse();
        return path;
    }

    // The first registered road cell adjacent to buildingCell, or null if nothing
    // next to it is a road. Buildings never stand on a road cell themselves, so this
    // is the cell a vehicle actually drives to when serving them.
    public Cell GetAdjacentRoadCell(Cell buildingCell)
    {
        if (buildingCell == null || buildingCell.neighbors == null) return null;

        foreach (Cell neighbor in buildingCell.neighbors)
        {
            if (neighbor != null && _roadCells.Contains(neighbor))
            {
                return neighbor;
            }
        }

        return null;
    }

    public bool HasRoadAccess(Cell buildingCell)
    {
        return GetAdjacentRoadCell(buildingCell) != null;
    }

    /// <summary>
    /// Collects all unique registered road cells orthogonally adjacent to any cell inside the
    /// building's rotated footprint. Used both for multi-cell road validation and for the
    /// preview road-connection highlight.
    /// </summary>
    public void GetAdjacentRoadCells(GridSystem grid, Vector3Int origin, Vector2Int footprint, List<Cell> results)
    {
        if (results == null) return;
        results.Clear();
        if (grid == null) return;

        int sizeX = Mathf.Max(1, footprint.x);
        int sizeZ = Mathf.Max(1, footprint.y);

        // North / South edges of the footprint
        for (int x = 0; x < sizeX; x++)
        {
            TryAddRoadCell(grid.GetCell(origin.x + x, origin.z - 1), results);
            TryAddRoadCell(grid.GetCell(origin.x + x, origin.z + sizeZ), results);
        }

        // West / East edges of the footprint
        for (int z = 0; z < sizeZ; z++)
        {
            TryAddRoadCell(grid.GetCell(origin.x - 1, origin.z + z), results);
            TryAddRoadCell(grid.GetCell(origin.x + sizeX, origin.z + z), results);
        }
    }

    /// <summary>
    /// Returns true when at least one orthogonal neighbor of the rotated footprint is a registered road cell.
    /// </summary>
    public bool HasRoadAccess(GridSystem grid, Vector3Int origin, Vector2Int footprint)
    {
        if (grid == null) return false;

        int sizeX = Mathf.Max(1, footprint.x);
        int sizeZ = Mathf.Max(1, footprint.y);

        for (int x = 0; x < sizeX; x++)
        {
            if (IsRegisteredRoadCell(grid.GetCell(origin.x + x, origin.z - 1)) ||
                IsRegisteredRoadCell(grid.GetCell(origin.x + x, origin.z + sizeZ)))
            {
                return true;
            }
        }

        for (int z = 0; z < sizeZ; z++)
        {
            if (IsRegisteredRoadCell(grid.GetCell(origin.x - 1, origin.z + z)) ||
                IsRegisteredRoadCell(grid.GetCell(origin.x + sizeX, origin.z + z)))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsRegisteredRoadCell(Cell cell)
    {
        return cell != null && _roadCells.Contains(cell);
    }

    private void TryAddRoadCell(Cell cell, List<Cell> results)
    {
        if (IsRegisteredRoadCell(cell) && !results.Contains(cell))
        {
            results.Add(cell);
        }
    }

    // Drivable route between the road cells serving two buildings. GetPath only
    // accepts road cells as endpoints, so each building's adjacent road cell is
    // resolved first. Null when either building lacks road access, or when the two
    // sit on disconnected stretches of road.
    public List<Cell> GetRouteBetween(Cell fromBuildingCell, Cell toBuildingCell)
    {
        Cell fromRoad = GetAdjacentRoadCell(fromBuildingCell);
        Cell toRoad = GetAdjacentRoadCell(toBuildingCell);

        if (fromRoad == null || toRoad == null) return null;

        return GetPath(fromRoad, toRoad);
    }
}
