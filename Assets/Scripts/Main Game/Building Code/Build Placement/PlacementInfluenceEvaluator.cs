using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Status of an individual grid cell evaluated inside a proposed building's influence area.
/// </summary>
public enum CellInfluenceStatus
{
    ValidBuildable,    // Green: flat, buildable land, inside island, acceptable slope, unoccupied
    InvalidBlocked,    // Red: mountain, cliff, excessive slope, deep water, blocked, occupied, or reserved
    Footprint          // Center: building's own footprint cells
}

/// <summary>
/// Result of evaluating all cells inside a candidate influence region.
/// </summary>
public struct InfluenceEvaluationResult
{
    public Vector3Int centerCell;
    public float influenceRadius;
    public Vector2Int footprintOrigin;
    public Vector2Int footprintSize;
    public List<Vector2Int> validCells;
    public List<Vector2Int> invalidCells;
    public List<Vector2Int> footprintCells;
    public int minX;
    public int maxX;
    public int minZ;
    public int maxZ;
}

/// <summary>
/// Evaluates candidate influence and footprint legality cell-by-cell in the style of Anno 2070.
///
/// Core principle:
/// Influence determines WHICH cells are in the proposed territory circle.
/// Terrain determines WHETHER those cells are buildable (green) or blocked (red).
/// A mountain or coastline does not warp the radial influence calculation;
/// instead, candidate cells intersecting mountains/cliffs/water/obstacles resolve as invalid (red).
/// </summary>
public static class PlacementInfluenceEvaluator
{
    public const float DefaultInfluenceRadius = 24f;

    /// <summary>
    /// Evaluates the discrete candidate influence circle around the candidate building position.
    /// </summary>
    public static InfluenceEvaluationResult EvaluateCandidateInfluence(
        GridSystem gridSystem,
        Vector3Int centerCellCoords,
        Vector2Int footprint,
        BuildingProperties properties)
    {
        BuildingData data = properties != null ? properties.buildingData : null;
        float radius = BuildingProperties.ResolveInfluenceRadius(properties, data, DefaultInfluenceRadius);

        // Fallback for buildings that might not have custom influence specified
        if (radius <= 0f)
        {
            radius = DefaultInfluenceRadius;
        }

        var result = new InfluenceEvaluationResult
        {
            centerCell = centerCellCoords,
            influenceRadius = radius,
            footprintSize = footprint,
            validCells = new List<Vector2Int>(),
            invalidCells = new List<Vector2Int>(),
            footprintCells = new List<Vector2Int>()
        };

        if (gridSystem == null) return result;

        int gridSize = gridSystem.gridSize;
        float cellSize = Mathf.Max(0.0001f, gridSystem.cellSize);
        int cellRadius = Mathf.CeilToInt(radius / cellSize);

        // Footprint origin in grid coords
        // In GridSystem convention, footprint is centered around centerCell or origin:
        int originX = centerCellCoords.x - (footprint.x / 2);
        int originZ = centerCellCoords.z - (footprint.y / 2);
        result.footprintOrigin = new Vector2Int(originX, originZ);

        // Populate footprint coordinate set
        var footprintSet = new HashSet<Vector2Int>();
        for (int fx = 0; fx < footprint.x; fx++)
        {
            for (int fz = 0; fz < footprint.y; fz++)
            {
                var fpCoord = new Vector2Int(originX + fx, originZ + fz);
                footprintSet.Add(fpCoord);
                result.footprintCells.Add(fpCoord);
            }
        }

        int minX = Mathf.Max(0, centerCellCoords.x - cellRadius);
        int maxX = Mathf.Min(gridSize - 1, centerCellCoords.x + cellRadius);
        int minZ = Mathf.Max(0, centerCellCoords.z - cellRadius);
        int maxZ = Mathf.Min(gridSize - 1, centerCellCoords.z + cellRadius);

        result.minX = minX;
        result.maxX = maxX;
        result.minZ = minZ;
        result.maxZ = maxZ;

        float radiusSq = radius * radius;
        // Evaluate every cell in the bounding box
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                // Radial cell distance from center cell in grid units
                float dx = (x - centerCellCoords.x) * cellSize;
                float dz = (z - centerCellCoords.z) * cellSize;
                float distSq = dx * dx + dz * dz;

                // Anno 2070 discrete stepped circle test
                if (distSq > radiusSq) continue;

                Vector2Int coord = new Vector2Int(x, z);

                // Skip footprint cells from influence shading (footprint has its own visual ghost/shading)
                if (footprintSet.Contains(coord))
                {
                    continue;
                }

                Cell cell = gridSystem.GetCell(x, z);
                if (cell == null)
                {
                    result.invalidCells.Add(coord);
                    continue;
                }

                if (IsCellUsableBuildable(cell, data))
                {
                    result.validCells.Add(coord);
                }
                else
                {
                    result.invalidCells.Add(coord);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Checks if a cell inside the influence area represents usable buildable land.
    /// Non-usable cells (mountains, cliffs, deep water, steep slopes, blocked/occupied) resolve to red.
    /// </summary>
    public static bool IsCellUsableBuildable(Cell cell, BuildingData data)
    {
        if (cell == null) return false;

        // Blocked cells (structures, props, forbidden areas)
        if (cell.isBlocked) return false;

        // Mountains, mountain summits, walls, cliffs, and rocky terrain are invalid for standard construction
        if (cell.IsBlockedByMountainOrCliff) return false;

        // Slope check
        if (!cell.IsSlopeSuitableForBuilding) return false;

        // Water check: In Anno 2070 land settlement buildings cannot build on ocean/water/river
        bool isWaterCell = PlacementRules.IsWater(cell.currentTerrainType);
        if (isWaterCell)
        {
            // If data explicitly allows water (e.g. offshore building), check that
            if (data != null && (data.buildingType == BuildingEnums.BuildingType.OffShore.ToString()
                              || data.buildingType == BuildingEnums.BuildingType.DeepSea.ToString()))
            {
                return true;
            }
            return false;
        }

        // Must be buildable flat surface
        if (!cell.IsBuildableFlatRegion && !cell.IsBuildableSurface)
        {
            return false;
        }

        // Occupied cells
        if (cell.isOccupied) return false;

        // Reserved deposit cells (unless matching extractor)
        if (cell.isDeposit)
        {
            if (data == null || data.requiredNodeType != cell.depositNodeType)
            {
                return false;
            }
        }

        return true;
    }
}
