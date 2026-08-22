using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Places and removes road tiles on the grid and keeps RoadNetwork in sync.
public class RoadPlacer : MonoBehaviour
{
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private RoadNetwork roadNetwork;
    [SerializeField] private GameObject roadTilePrefab; // TODO: assign road tile prefab in Inspector

    private Dictionary<Cell, GameObject> _placedRoadTiles = new Dictionary<Cell, GameObject>();

    public static event Action<Cell> OnRoadPlaced;
    public static event Action<Cell> OnRoadRemoved;

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

        if (roadTilePrefab != null && gridSystem != null)
        {
            Vector3 worldPosition = gridSystem.transform.TransformPoint(targetCell.position);
            GameObject roadTileInstance = Instantiate(roadTilePrefab, worldPosition, Quaternion.identity, gridSystem.transform);
            _placedRoadTiles[targetCell] = roadTileInstance;
        }

        if (roadNetwork != null)
        {
            roadNetwork.RegisterRoadCell(targetCell);
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

        if (roadNetwork != null)
        {
            roadNetwork.UnregisterRoadCell(targetCell);
        }

        OnRoadRemoved?.Invoke(targetCell);
        return true;
    }
}
