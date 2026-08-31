using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles placing and removing individual 1x1 CropFieldModule tiles on the grid for a target CropFarmCore.
/// Validates terrain, fertility, tile availability, and connectivity back to the Farm Core.
/// </summary>
public class CropFieldPlacer : MonoBehaviour
{
    [Header("Default Visuals / Prefabs")]
    [SerializeField] private GameObject defaultFieldPrefab;

    [Header("Grid Reference")]
    [SerializeField] private GridSystem gridSystem;

    public static event Action<CropFarmCore, CropFieldModule> OnFieldPlaced;
    public static event Action<CropFarmCore, Vector2Int> OnFieldRemoved;

    private GridSystem ActiveGridSystem
    {
        get
        {
            if (gridSystem != null) return gridSystem;
            return IslandManager.instance != null ? IslandManager.instance.GetCurrentGridSystem() : null;
        }
    }

    /// <summary>
    /// Gets the cell at world position from the active island grid.
    /// </summary>
    public Cell GetCellAtWorldPosition(Vector3 worldPosition)
    {
        GridSystem grid = ActiveGridSystem;
        return grid != null ? grid.GetCellAtWorldPosition(worldPosition) : null;
    }

    /// <summary>
    /// Checks if a field tile can be placed at the target cell for the given Farm Core.
    /// Requirements:
    /// 1. Cell and Farm Core must exist
    /// 2. Tile must be available (not occupied, not blocked, not road)
    /// 3. Terrain must be valid buildable land (not underwater, not cliff/mountain)
    /// 4. Required fertility exists on the island
    /// 5. Must connect back to the Farm Core or an existing connected field belonging to this farm
    /// </summary>
    public bool CanPlaceField(CropFarmCore farmCore, Cell targetCell, out string failureReason)
    {
        failureReason = string.Empty;

        if (farmCore == null)
        {
            failureReason = "No Farm Core selected.";
            return false;
        }

        if (targetCell == null)
        {
            failureReason = "Invalid grid position.";
            return false;
        }

        // 1. Tile availability
        if (targetCell.isOccupied || targetCell.isBlocked || targetCell.isRoad)
        {
            failureReason = "Tile is occupied or blocked.";
            return false;
        }

        // 2. Terrain validity
        if (!targetCell.IsBuildableSurface)
        {
            failureReason = "Terrain is invalid (water, steep slope, or mountain).";
            return false;
        }

        // 3. Fertility requirement
        if (!farmCore.HasRequiredFertility)
        {
            failureReason = $"Missing required fertility ({farmCore.RequiredFertility}) on this island.";
            return false;
        }

        // 4. Connectivity requirement: must connect to core footprint or an existing connected field
        Vector2Int coords = new Vector2Int(targetCell.cellPosition.x, targetCell.cellPosition.z);
        if (!farmCore.IsCoordinateAdjacentToConnectedFarm(coords))
        {
            failureReason = "Field must connect to the Farm Core or an existing connected field.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Places a 1x1 field module at the given cell for the target Farm Core.
    /// </summary>
    public CropFieldModule PlaceField(CropFarmCore farmCore, Cell targetCell)
    {
        if (!CanPlaceField(farmCore, targetCell, out string reason))
        {
            return null;
        }

        GridSystem grid = ActiveGridSystem;
        Vector2Int coords = new Vector2Int(targetCell.cellPosition.x, targetCell.cellPosition.z);

        // Instantiate field prefab
        GameObject prefabToUse = farmCore.FarmData != null && farmCore.FarmData.fieldPrefab != null
            ? farmCore.FarmData.fieldPrefab
            : defaultFieldPrefab;

        GameObject fieldObj;
        Vector3 worldPos = grid != null 
            ? grid.transform.TransformPoint(targetCell.localCenter) 
            : new Vector3(targetCell.cellPosition.x + 0.5f, targetCell.height, targetCell.cellPosition.z + 0.5f);

        Transform parentTransform = farmCore.transform;

        if (prefabToUse != null)
        {
            fieldObj = Instantiate(prefabToUse, worldPos, Quaternion.identity, parentTransform);
        }
        else
        {
            fieldObj = CreateDefaultFieldVisual(worldPos, parentTransform, farmCore.FarmData);
        }

        fieldObj.name = $"Field_{coords.x}_{coords.y}";

        CropFieldModule fieldModule = fieldObj.GetComponent<CropFieldModule>();
        if (fieldModule == null)
        {
            fieldModule = fieldObj.AddComponent<CropFieldModule>();
        }

        Vector3Int coords3D = new Vector3Int(coords.x, Mathf.RoundToInt(targetCell.height), coords.y);
        fieldModule.Initialize(farmCore, coords3D, targetCell);

        // Occupy cell
        Building farmBuilding = farmCore.GetComponent<Building>();
        targetCell.OccupyCellWithBuilding(farmBuilding);

        // Register with Farm Core
        farmCore.RegisterField(fieldModule, coords);

        OnFieldPlaced?.Invoke(farmCore, fieldModule);
        return fieldModule;
    }

    /// <summary>
    /// Removes a field module at the given cell.
    /// </summary>
    public bool RemoveField(CropFarmCore farmCore, Cell targetCell)
    {
        if (farmCore == null || targetCell == null) return false;

        Vector2Int coords = new Vector2Int(targetCell.cellPosition.x, targetCell.cellPosition.z);

        // Find field on this cell owned by this farm
        CropFieldModule targetField = null;
        foreach (var field in farmCore.AllOwnedFields)
        {
            if (field != null && field.GridCoordinates.x == coords.x && field.GridCoordinates.z == coords.y)
            {
                targetField = field;
                break;
            }
        }

        if (targetField == null) return false;

        farmCore.UnregisterField(targetField, coords);
        targetCell.ReleaseCell();

        Destroy(targetField.gameObject);

        OnFieldRemoved?.Invoke(farmCore, coords);
        return true;
    }

    private GameObject CreateDefaultFieldVisual(Vector3 worldPos, Transform parent, CropFarmData data)
    {
        GameObject fieldObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fieldObj.transform.position = worldPos;
        fieldObj.transform.localScale = new Vector3(0.95f, 0.08f, 0.95f);
        fieldObj.transform.SetParent(parent);

        // Remove default collider so it doesn't block terrain raycasts
        Collider col = fieldObj.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = fieldObj.GetComponent<Renderer>();
        if (rend != null)
        {
            Color cropColor = data != null ? data.fieldColor : new Color(0.85f, 0.75f, 0.2f);
            rend.material.color = cropColor;
        }

        return fieldObj;
    }
}
