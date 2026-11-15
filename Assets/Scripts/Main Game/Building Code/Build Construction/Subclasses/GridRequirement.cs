using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Grid Type Requirement", menuName = "Building/Requirements/Grid")]
public class GridRequirement : BuildingRequirement
{
    public GridType gridType;
    // Existing values 0-4 keep their serialized ordinals. Renaming plataeu/shore is
    // therefore safe for current ScriptableObject assets while making the intent clear.
    public enum GridType { island, underwaterPlateau, coastal, shallow, other, deep, abyssal };

    // When true, the target footprint must also touch a registered road cell along its perimeter.
    public bool requiresRoadAccess;

    private Cell targetCell;
    public void SetTargetCell(Cell cell)
    {
        targetCell = cell;
    }

    /// <summary>
    /// Evaluates only terrain compatibility for a single cell so multi-cell footprints
    /// can validate road connectivity across the full perimeter instead of interior cells.
    /// </summary>
    public bool IsTerrainSatisfied(Cell cell)
    {
        if (cell == null) return false;

        switch (gridType)
        {
            case GridType.island:
                return cell.currentTerrainType == Cell.TerrainType.Land
                    || cell.currentTerrainType == Cell.TerrainType.Beach;

            case GridType.coastal:
                return cell.currentTerrainType == Cell.TerrainType.Coast
                    || cell.currentTerrainType == Cell.TerrainType.Beach;

            case GridType.shallow:
                return cell.currentTerrainType == Cell.TerrainType.Shallow;

            case GridType.underwaterPlateau:
                return cell.IsBuildableUnderwaterPlateau;

            case GridType.deep:
                return cell.currentTerrainType == Cell.TerrainType.Deep;

            case GridType.abyssal:
                return cell.currentTerrainType == Cell.TerrainType.Abyssal;

            case GridType.other:
            default:
                // TODO: define what "other" should require once a concrete use case exists
                return true;
        }
    }

    public override bool IsSatisfied()
    {
        if (!IsTerrainSatisfied(targetCell)) return false;

        if (requiresRoadAccess)
        {
            return RoadNetwork.Instance != null && RoadNetwork.Instance.HasRoadAccess(targetCell);
        }

        return true;
    }
}
