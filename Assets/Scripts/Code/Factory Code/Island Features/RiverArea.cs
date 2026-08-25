using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RiverArea : IFeature
{
    private bool isBuildable = false;
    private List<Vector3> riverPoints;

    // Grid context needed to actually carve a river. IFeature.GenerateFeature() takes
    // no arguments, so callers that have a Cell[,] grid (MapGrid's post-process pass)
    // should call GenerateRiver(grid, rng) directly instead of going through the
    // parameterless IFeature path.
    private Cell[,] grid;
    private System.Random rng;

    private static readonly (int dx, int dz, Cell.RiverDirection direction)[] NeighborOffsets = new (int, int, Cell.RiverDirection)[]
    {
        (0, 1, Cell.RiverDirection.North),
        (0, -1, Cell.RiverDirection.South),
        (1, 0, Cell.RiverDirection.East),
        (-1, 0, Cell.RiverDirection.West),
        (1, 1, Cell.RiverDirection.NorthEast),
        (-1, 1, Cell.RiverDirection.NorthWest),
        (1, -1, Cell.RiverDirection.SouthEast),
        (-1, -1, Cell.RiverDirection.SouthWest),
    };

    public RiverArea()
    {
        this.isBuildable = false;
        this.riverPoints = new List<Vector3>();
    }

    public void buildingRoad()
    {
        isBuildable = true;
    }

    public void GenerateFeature()
    {
        // Implementation for generating a river area
        GenerateRiver();
    }

    // Parameterless IFeature entry point. Only does something if a grid was supplied
    // via GenerateRiver(grid, rng) beforehand - otherwise there is nothing to carve into.
    private void GenerateRiver()
    {
        if (grid == null) return; // TODO: wire this feature up with a real grid before relying on GenerateFeature()
        GenerateRiver(grid, rng ?? new System.Random());
    }

    /// <summary>
    /// Carves 1-3 rivers from the highest mountain cells down to the sea via steepest
    /// descent, stamping River terrain/status/direction along the way and marking
    /// RiverBank/LakeMouth deposits.
    /// </summary>
    public void GenerateRiver(Cell[,] grid, System.Random rng)
    {
        GenerateRiver(grid, null, rng);
    }

    public void GenerateRiver(Cell[,] grid, FeatureReservationMap reservations, System.Random rng)
    {
        this.grid = grid;
        this.rng = rng ?? new System.Random();

        int size = grid.GetLength(0);

        if (reservations != null && reservations.Rivers.Count > 0)
        {
            foreach (var river in reservations.Rivers)
            {
                ApplyReservedRiver(grid, size, river);
            }
            return;
        }

        // Fallback: Prefer the tallest available terrain (peaks) as river sources
        List<Cell> peakCandidates = new List<Cell>();
        List<Cell> mountainCandidates = new List<Cell>();
        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                Cell cell = grid[x, z];
                if (cell.currentTerrainType == Cell.TerrainType.MountainPeak) peakCandidates.Add(cell);
                else if (cell.currentTerrainType == Cell.TerrainType.Mountain) mountainCandidates.Add(cell);
            }
        }

        List<Cell> sourcePool = peakCandidates.Count > 0 ? peakCandidates : mountainCandidates;
        if (sourcePool.Count == 0) return; // No mountains generated - nothing to carve a river from

        int riverCount = Mathf.Min(sourcePool.Count, 1 + this.rng.Next(3)); // 1-3 rivers
        List<Cell> sources = PickRandomDistinct(sourcePool, riverCount, this.rng);

        foreach (Cell source in sources)
        {
            CarveRiver(grid, size, source);
        }
    }

    private void ApplyReservedRiver(Cell[,] grid, int size, FeatureReservationMap.RiverCorridor river)
    {
        if (river.Waypoints.Count < 2) return;

        List<Vector2Int> pathCells = new List<Vector2Int>();
        for (int i = 0; i < river.Waypoints.Count - 1; i++)
        {
            Vector2 start = river.Waypoints[i].Position;
            Vector2 end = river.Waypoints[i + 1].Position;
            float dist = Vector2.Distance(start, end);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist * 2f));

            for (int s = 0; s <= steps; s++)
            {
                Vector2 pt = Vector2.Lerp(start, end, s / (float)steps);
                int gx = Mathf.Clamp(Mathf.RoundToInt(pt.x), 0, size - 1);
                int gz = Mathf.Clamp(Mathf.RoundToInt(pt.y), 0, size - 1);
                Vector2Int cellCoord = new Vector2Int(gx, gz);
                if (pathCells.Count == 0 || pathCells[pathCells.Count - 1] != cellCoord)
                {
                    pathCells.Add(cellCoord);
                }
            }
        }

        for (int i = 0; i < pathCells.Count; i++)
        {
            Vector2Int coord = pathCells[i];
            Cell cell = grid[coord.x, coord.y];

            Cell.RiverDirection dir = Cell.RiverDirection.None;
            if (i < pathCells.Count - 1)
            {
                Vector2Int nextCoord = pathCells[i + 1];
                int dx = nextCoord.x - coord.x;
                int dz = nextCoord.y - coord.y;
                foreach (var offset in NeighborOffsets)
                {
                    if (offset.dx == dx && offset.dz == dz)
                    {
                        dir = offset.direction;
                        break;
                    }
                }
            }

            bool isLast = (i == pathCells.Count - 1);
            bool isFirst = (i == 0);

            if (isFirst)
            {
                cell.SetRiverData(Cell.RiverStatus.RiverSource, dir);
                cell.ChangeTerrainType(Cell.TerrainType.River);
                MarkRiverBank(cell, grid, size);
            }
            else if (isLast || cell.IsUnderwater)
            {
                cell.SetRiverData(Cell.RiverStatus.RiverMouth, Cell.RiverDirection.None);
                cell.SetDeposit(ResourceNodeType.LakeMouth);
            }
            else
            {
                cell.SetRiverData(Cell.RiverStatus.River, dir);
                cell.ChangeTerrainType(Cell.TerrainType.River);
                MarkRiverBank(cell, grid, size);
            }
        }
    }

    private List<Cell> PickRandomDistinct(List<Cell> pool, int count, System.Random rng)
    {
        List<Cell> remaining = new List<Cell>(pool);
        List<Cell> picked = new List<Cell>();
        for (int i = 0; i < count && remaining.Count > 0; i++)
        {
            int index = rng.Next(remaining.Count);
            picked.Add(remaining[index]);
            remaining.RemoveAt(index);
        }
        return picked;
    }

    private void CarveRiver(Cell[,] grid, int size, Cell source)
    {
        // Already claimed by another river this pass - skip rather than crossing it.
        if (source.riverStatus != Cell.RiverStatus.None) return;

        source.SetRiverData(Cell.RiverStatus.RiverSource, Cell.RiverDirection.None);
        MarkRiverBank(source, grid, size);

        Cell current = source;
        int maxSteps = size * size;

        for (int step = 0; step < maxSteps; step++)
        {
            int cx = current.cellPosition.x;
            int cz = current.cellPosition.z;

            Cell next = null;
            Cell.RiverDirection direction = Cell.RiverDirection.None;
            float lowestHeight = ApproximateHeight(current);

            foreach (var offset in NeighborOffsets)
            {
                int nx = cx + offset.dx;
                int nz = cz + offset.dz;
                if (nx < 0 || nx >= size || nz < 0 || nz >= size) continue;

                Cell candidate = grid[nx, nz];
                if (candidate.riverStatus != Cell.RiverStatus.None) continue; // don't cross an existing river

                float candidateHeight = ApproximateHeight(candidate);
                if (candidateHeight < lowestHeight)
                {
                    lowestHeight = candidateHeight;
                    next = candidate;
                    direction = offset.direction;
                }
            }

            if (next == null) break; // Local minimum - no lower unclaimed neighbor to flow into

            // Record which way the current cell flows before advancing.
            current.SetRiverData(current.riverStatus, direction);

            bool reachedSea = next.currentTerrainType == Cell.TerrainType.Water || next.currentTerrainType == Cell.TerrainType.Shallow;

            if (reachedSea)
            {
                next.SetRiverData(Cell.RiverStatus.RiverMouth, Cell.RiverDirection.None);
                next.SetDeposit(ResourceNodeType.LakeMouth);
                break;
            }

            next.SetRiverData(Cell.RiverStatus.River, Cell.RiverDirection.None);
            next.ChangeTerrainType(Cell.TerrainType.River);
            MarkRiverBank(next, grid, size);

            current = next;
        }
    }

    private void MarkRiverBank(Cell riverCell, Cell[,] grid, int size)
    {
        int cx = riverCell.cellPosition.x;
        int cz = riverCell.cellPosition.z;

        foreach (var offset in NeighborOffsets)
        {
            int nx = cx + offset.dx;
            int nz = cz + offset.dz;
            if (nx < 0 || nx >= size || nz < 0 || nz >= size) continue;

            Cell neighbor = grid[nx, nz];
            if (neighbor.currentTerrainType == Cell.TerrainType.Land)
            {
                neighbor.SetDeposit(ResourceNodeType.RiverBank);
            }
        }
    }

    // Terrain generation persists its semantic height on Cell, so river routing and
    // rendering now consume the same authoritative elevation data.
    private static float ApproximateHeight(Cell cell)
    {
        return cell.height;
    }
}
