using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Grid Type Requirement", menuName = "Building/Requirements/Grid")]
public class GridRequirement : BuildingRequirement
{
    public GridType gridType;
    public enum GridType { island, plataeu, coastal, shore, other }; // Add more when ya need

    // When true, the target cell must also have a road cell in its neighbors
    // (checked via RoadNetwork) on top of the gridType terrain check below.
    public bool requiresRoadAccess;

    // Set by BuildingChecker.UpdateBuildsite() before this requirement is evaluated.
    // BuildingRequirement.IsSatisfied() takes no arguments and this requirement is a
    // shared ScriptableObject asset, so the cell being checked has to be handed in
    // from outside rather than looked up here.
    private Cell targetCell;
    public void SetTargetCell(Cell cell)
    {
        targetCell = cell;
    }

    public override bool IsSatisfied()
    {
        if (targetCell == null) return false;

        bool terrainSatisfied;
        switch (gridType)
        {
            case GridType.island:
                terrainSatisfied = targetCell.currentTerrainType == Cell.TerrainType.Land
                    || targetCell.currentTerrainType == Cell.TerrainType.Beach;
                break;

            case GridType.coastal:
                terrainSatisfied = targetCell.currentTerrainType == Cell.TerrainType.Coast
                    || targetCell.currentTerrainType == Cell.TerrainType.Beach;
                break;

            case GridType.shore:
                terrainSatisfied = targetCell.currentTerrainType == Cell.TerrainType.Shallow;
                break;

            case GridType.plataeu:
                terrainSatisfied = targetCell.currentTerrainType == Cell.TerrainType.Plateau;
                break;

            case GridType.other:
            default:
                // TODO: define what "other" should require once a concrete use case exists
                terrainSatisfied = true;
                break;
        }

        if (!terrainSatisfied) return false;

        if (requiresRoadAccess)
        {
            return RoadNetwork.Instance != null && RoadNetwork.Instance.HasRoadAccess(targetCell);
        }

        return true;
    }
}