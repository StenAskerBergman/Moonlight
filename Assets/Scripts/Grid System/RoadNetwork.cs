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
            if (neighbor == null || !_roadCells.Contains(neighbor)) continue;

            AddEdge(cell, neighbor);
            AddEdge(neighbor, cell);
        }
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
