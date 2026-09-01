using UnityEngine;

/// <summary>
/// Why a candidate site was accepted or rejected. The overlay colours cells by this, so
/// the reason the player sees and the reason placement enforces are the same value.
/// </summary>
public enum PlacementVerdict
{
    Valid,

    // Cell level
    NoCell,
    Blocked,
    Occupied,
    ReservedDeposit,
    WrongTerrain,
    MissingDeposit,
    UnmetRequirement,

    // Site level
    OutOfBoatRange,
    OutOfInfluence,
    NoCargo
}

/// <summary>
/// The single definition of "can this building stand on this cell".
///
/// BuildingChecker used to carry this logic inline in UpdateBuildsite, which meant any
/// second reader of the rules (the placement overlay, the boat's build interaction) had
/// to re-implement them and could silently drift. Both now call in here instead.
/// </summary>
public static class PlacementRules
{
    /// <summary>
    /// Tests every cell the building's footprint would cover, starting at the given grid
    /// origin. Purely cell/terrain level - influence and boat reach are separate, because
    /// they answer a different question and are evaluated per site rather than per cell.
    /// </summary>
    public static bool EvaluateFootprint(GridSystem gridSystem, Vector3Int gridOrigin, Vector3 buildingSize,
                                         BuildingData data, out PlacementVerdict verdict)
    {
        verdict = PlacementVerdict.Valid;
        if (gridSystem == null)
        {
            verdict = PlacementVerdict.NoCell;
            return false;
        }

        int sizeX = Mathf.Max(1, Mathf.RoundToInt(buildingSize.x));
        int sizeZ = Mathf.Max(1, Mathf.RoundToInt(buildingSize.z));

        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                Cell targetCell = gridSystem.GetCell(gridOrigin.x + x, gridOrigin.z + z);
                if (!EvaluateCell(targetCell, data, out verdict))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// One cell of a footprint. Split out so the overlay can shade a single tile without
    /// pretending a building sits on it.
    /// </summary>
    public static bool EvaluateCell(Cell cell, BuildingData data, out PlacementVerdict verdict)
    {
        if (cell == null)
        {
            verdict = PlacementVerdict.NoCell;
            return false;
        }

        if (cell.isBlocked)
        {
            verdict = PlacementVerdict.Blocked;
            return false;
        }

        if (cell.isOccupied)
        {
            verdict = PlacementVerdict.Occupied;
            return false;
        }

        // Resource deposits reserve their cells from unrelated construction. A matching
        // extractor is allowed through.
        if (cell.isDeposit && (data == null || data.requiredNodeType != cell.depositNodeType))
        {
            verdict = PlacementVerdict.ReservedDeposit;
            return false;
        }

        // Fail closed on water. Terrain legality used to be checked only inside the
        // "data != null" block below, so a building whose BuildingData was never assigned
        // had no terrain rule at all and could be dropped straight into the sea. A
        // building that wants to sit on water has to say so via its buildingType.
        if (IsWater(cell.currentTerrainType) && !AllowsWater(data))
        {
            verdict = PlacementVerdict.WrongTerrain;
            return false;
        }

        if (data != null)
        {
            if (data.buildingType == BuildingEnums.BuildingType.OnShore.ToString())
            {
                if (cell.currentTerrainType != Cell.TerrainType.Beach)
                {
                    verdict = PlacementVerdict.WrongTerrain;
                    return false;
                }
            }
            else if (data.buildingType == BuildingEnums.BuildingType.OffShore.ToString())
            {
                if (cell.currentTerrainType != Cell.TerrainType.Shallow)
                {
                    verdict = PlacementVerdict.WrongTerrain;
                    return false;
                }
            }

            if (data.requiredNodeType != ResourceNodeType.None)
            {
                if (!cell.isDeposit || cell.depositNodeType != data.requiredNodeType)
                {
                    verdict = PlacementVerdict.MissingDeposit;
                    return false;
                }
            }

            if (data.BuildingRequirements != null)
            {
                foreach (BuildingRequirement req in data.BuildingRequirements)
                {
                    if (req is GridRequirement gridReq)
                    {
                        gridReq.SetTargetCell(cell);
                        if (!gridReq.IsSatisfied())
                        {
                            verdict = PlacementVerdict.UnmetRequirement;
                            return false;
                        }
                    }
                }
            }
        }

        verdict = PlacementVerdict.Valid;
        return true;
    }

    /// <summary>
    /// Terrain the player cannot build dry land on. Shallow is included: an offshore
    /// building has to opt in through its buildingType rather than getting water for
    /// free just because the water happens to be wadeable.
    /// </summary>
    public static bool IsWater(Cell.TerrainType terrain)
    {
        switch (terrain)
        {
            case Cell.TerrainType.Abyssal:
            case Cell.TerrainType.River:
            case Cell.TerrainType.Water:
            case Cell.TerrainType.Stream:
            case Cell.TerrainType.Sea:
            case Cell.TerrainType.Ocean:
            case Cell.TerrainType.Shallow:
            case Cell.TerrainType.Deep:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Only the building types that are meant to stand in water may sit on it. A null
    /// BuildingData deliberately returns false - unconfigured means land-only, not
    /// anything-goes.
    /// </summary>
    private static bool AllowsWater(BuildingData data)
    {
        if (data == null) return false;

        return data.buildingType == BuildingEnums.BuildingType.OffShore.ToString()
            || data.buildingType == BuildingEnums.BuildingType.DeepSea.ToString();
    }

    /// <summary>
    /// The site-level half of the rules: a harbor must be inside a vessel's founding
    /// reach on an unsettled island, anything else must stand in existing influence.
    /// </summary>
    public static bool EvaluateInfluence(InfluenceManager influenceManager, bool isHarbor,
                                         Vector3 worldPosition, GridSystem gridSystem,
                                         out Unit foundingBoat, out PlacementVerdict verdict)
    {
        foundingBoat = null;
        verdict = PlacementVerdict.Valid;

        if (influenceManager == null) return true;

        if (!isHarbor)
        {
            if (influenceManager.IsWithinBuildableArea(worldPosition)) return true;

            verdict = PlacementVerdict.OutOfInfluence;
            return false;
        }

        if (influenceManager.CanPlaceWarehouse(worldPosition, gridSystem, out foundingBoat)) return true;

        // On an unsettled island the only thing a valid beach cell can still be missing
        // is a vessel in reach; afterwards the failure is island influence.
        verdict = influenceManager.HasWarehouse ? PlacementVerdict.OutOfInfluence : PlacementVerdict.OutOfBoatRange;
        return false;
    }

    /// <summary>
    /// Resolves the influence manager for an island, creating one if the island has never
    /// had a building. Mirrors what BuildingChecker did inline.
    /// </summary>
    public static InfluenceManager GetInfluenceManager(Island island, bool createIfMissing = false)
    {
        if (island == null) return null;

        InfluenceManager influenceManager = island.GetComponent<InfluenceManager>();
        if (influenceManager == null && island.islandObject != null)
        {
            influenceManager = island.islandObject.GetComponent<InfluenceManager>();
        }
        if (influenceManager == null && createIfMissing)
        {
            influenceManager = island.gameObject.AddComponent<InfluenceManager>();
        }

        return influenceManager;
    }
}
