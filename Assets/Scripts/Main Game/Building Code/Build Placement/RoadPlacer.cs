using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Places and removes road tiles on the grid and keeps RoadNetwork in sync.
public class RoadPlacer : MonoBehaviour
{
    public const int DefaultMaxBridgeSpan = 6;
    public const float DefaultBridgeDeckHeight = 0.35f;

    [Tooltip("Optional. Left empty, the grid of the island the player is currently on is used.")]
    [SerializeField] private GridSystem gridSystem;
    [Tooltip("Optional. Left empty, the RoadNetwork singleton is used.")]
    [SerializeField] private RoadNetwork roadNetwork;
    [SerializeField] private GameObject roadTilePrefab;
    [Tooltip("Optional visual used when a bridge-capable road crosses water. Falls back to the normal road prefab.")]
    [SerializeField] private GameObject bridgeTilePrefab;
    [Tooltip("Definition used by the existing PlaceRoad(Cell) interface. Leave empty to retain legacy untyped roads and the fallback prefab.")]
    [SerializeField] private RoadDefinition defaultRoadDefinition;

    private Dictionary<Cell, RoadTileVisual> _placedRoadTiles = new Dictionary<Cell, RoadTileVisual>();

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
        return PlaceRoad(targetCell, defaultRoadDefinition);
    }

    public bool PlaceRoad(Cell targetCell, RoadDefinition definition)
    {
        if (targetCell == null) return false;

        if (targetCell.isRoad || targetCell.isOccupied || targetCell.isBlocked)
        {
            return false;
        }

        bool isBridge = BridgePlacementRules.IsBridgeTerrain(targetCell);
        if (isBridge)
        {
            bool supportsBridges = definition == null || definition.SupportsBridges;
            int maxSpan = definition != null ? definition.MaxBridgeSpan : DefaultMaxBridgeSpan;
            if (!supportsBridges || !BridgePlacementRules.TryGetBridgeAxis(ActiveGridSystem, targetCell, maxSpan, out _))
            {
                return false;
            }
        }
        else if (targetCell.currentTerrainType != Cell.TerrainType.Land
            && targetCell.currentTerrainType != Cell.TerrainType.Beach)
        {
            return false;
        }

        targetCell.SetRoad(true, definition);

        GridSystem grid = ActiveGridSystem;
        if (grid != null)
        {
            // localCenter, not position: position carries the array index, the cell
            // physically occupies local [x, x+1) so the tile belongs at x+0.5.
            Vector3 worldPosition = grid.transform.TransformPoint(targetCell.localCenter);
            GameObject roadTileInstance = new GameObject($"Road {targetCell.cellPosition.x},{targetCell.cellPosition.z}");
            roadTileInstance.transform.SetParent(grid.transform, false);
            roadTileInstance.transform.position = worldPosition;
            _placedRoadTiles[targetCell] = roadTileInstance.AddComponent<RoadTileVisual>();
        }

        RoadNetwork network = ActiveRoadNetwork;
        if (network != null)
        {
            network.RegisterRoadCell(targetCell);
        }

        RefreshLocalArea(targetCell);

        OnRoadPlaced?.Invoke(targetCell);
        return true;
    }

    public bool RemoveRoad(Cell targetCell)
    {
        if (targetCell == null || !targetCell.isRoad) return false;

        targetCell.SetRoad(false);

        if (_placedRoadTiles.TryGetValue(targetCell, out RoadTileVisual roadTileInstance))
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

        RefreshLocalArea(targetCell);

        OnRoadRemoved?.Invoke(targetCell);
        return true;
    }

    public bool ReplaceRoad(Cell targetCell, RoadDefinition definition)
    {
        if (targetCell == null || !targetCell.isRoad) return false;
        if (targetCell.roadDefinition == definition) return true;

        if (BridgePlacementRules.IsBridgeTerrain(targetCell))
        {
            int maxSpan = definition != null ? definition.MaxBridgeSpan : DefaultMaxBridgeSpan;
            if ((definition != null && !definition.SupportsBridges)
                || !BridgePlacementRules.TryGetBridgeAxis(ActiveGridSystem, targetCell, maxSpan, out _))
            {
                return false;
            }
        }

        targetCell.SetRoad(true, definition);
        RefreshLocalArea(targetCell);
        OnRoadPlaced?.Invoke(targetCell);
        return true;
    }

    public bool UpgradeRoad(Cell targetCell, RoadDefinition definition)
    {
        return ReplaceRoad(targetCell, definition);
    }

    private void RefreshLocalArea(Cell changedCell)
    {
        GridSystem grid = ActiveGridSystem;
        if (grid == null || changedCell == null) return;

        int centerX = changedCell.cellPosition.x;
        int centerZ = changedCell.cellPosition.z;
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dz = -2; dz <= 2; dz++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dz) > 2) continue;
                Cell cell = grid.GetCell(centerX + dx, centerZ + dz);
                if (cell == null || !cell.isRoad) continue;
                if (_placedRoadTiles.TryGetValue(cell, out RoadTileVisual visual) && visual != null)
                {
                    visual.Apply(RoadTopologyResolver.Resolve(grid, cell, roadTilePrefab, bridgeTilePrefab));
                }
            }
        }
    }
}
