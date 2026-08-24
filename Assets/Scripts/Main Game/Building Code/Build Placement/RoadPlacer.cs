using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Places and removes road tiles on the grid and keeps RoadNetwork in sync.
public class RoadPlacer : MonoBehaviour
{
    [Tooltip("Optional. Left empty, the grid of the island the player is currently on is used.")]
    [SerializeField] private GridSystem gridSystem;
    [Tooltip("Optional. Left empty, the RoadNetwork singleton is used.")]
    [SerializeField] private RoadNetwork roadNetwork;
    [SerializeField] private GameObject roadTilePrefab;

    private Dictionary<Cell, GameObject> _placedRoadTiles = new Dictionary<Cell, GameObject>();

    public static event Action<Cell> OnRoadPlaced;
    public static event Action<Cell> OnRoadRemoved;

    // Islands and their grids are built at runtime, so the grid can't be a fixed
    // scene reference — it changes as the player moves between islands. The
    // serialized field stays supported as an explicit override for fixed test scenes.
    private GridSystem ActiveGridSystem
    {
        get
        {
            if (gridSystem != null) return gridSystem;
            return IslandManager.instance != null ? IslandManager.instance.GetCurrentGridSystem() : null;
        }
    }

    private RoadNetwork ActiveRoadNetwork => roadNetwork != null ? roadNetwork : RoadNetwork.Instance;

    // The cell under a world position on the island currently being played, or null
    // if that position is off-grid. Input handlers go through this so grid
    // resolution stays in one place.
    public Cell GetCellAtWorldPosition(Vector3 worldPosition)
    {
        GridSystem grid = ActiveGridSystem;
        return grid != null ? grid.GetCellAtWorldPosition(worldPosition) : null;
    }

    public bool PlaceRoad(Cell targetCell)
    {
        if (targetCell == null) return false;

        if (targetCell.isRoad || targetCell.isOccupied || targetCell.isBlocked)
        {
            return false;
        }

        if (targetCell.currentTerrainType != Cell.TerrainType.Land
            && targetCell.currentTerrainType != Cell.TerrainType.Beach)
        {
            return false;
        }

        targetCell.SetRoad(true);

        GridSystem grid = ActiveGridSystem;
        if (roadTilePrefab != null && grid != null)
        {
            // localCenter, not position: position carries the array index, the cell
            // physically occupies local [x, x+1) so the tile belongs at x+0.5.
            Vector3 worldPosition = grid.transform.TransformPoint(targetCell.localCenter);
            GameObject roadTileInstance = Instantiate(roadTilePrefab, worldPosition, Quaternion.identity, grid.transform);
            _placedRoadTiles[targetCell] = roadTileInstance;
        }

        RoadNetwork network = ActiveRoadNetwork;
        if (network != null)
        {
            network.RegisterRoadCell(targetCell);
        }

        OnRoadPlaced?.Invoke(targetCell);
        return true;
    }

    public bool RemoveRoad(Cell targetCell)
    {
        if (targetCell == null || !targetCell.isRoad) return false;

        targetCell.SetRoad(false);

        if (_placedRoadTiles.TryGetValue(targetCell, out GameObject roadTileInstance))
        {
            if (roadTileInstance != null)
            {
                Destroy(roadTileInstance);
            }
            _placedRoadTiles.Remove(targetCell);
        }

        RoadNetwork network = ActiveRoadNetwork;
        if (network != null)
        {
            network.UnregisterRoadCell(targetCell);
        }

        OnRoadRemoved?.Invoke(targetCell);
        return true;
    }
}
