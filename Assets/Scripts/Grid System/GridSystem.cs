using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Cell;

public class GridSystem : MonoBehaviour
{
    [SerializeField] private MapManager mapManager;
    [SerializeField] private LayerMask buildings; // = LayerMask.GetMask("Ground", "Building");

    #region section 1 

    private HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();

    public int gridSize = 10;
    public float cellSize = 1f;
    public Vector3 gridPosition;
    public Vector3 offset = new Vector3(0, 0, 0);
    private Cell[,] grid;
    public Bank bank;
    public BuildingChecker buildingChecker;
    private int gridCount;

    // Local List Method
    public List<Bank.Building> localBuildings; // NEW: A list of Building objects.
    public void AddLocalBuilding(Bank.Building building)
    {
        localBuildings.Add(building);
    }

    // Global List Methods
    [HideInInspector] public List<Building> globalBuildings; // NEW: A list of Building objects.
    private void AddBuilding(BuildingCost buildingCost)
    {
        Building building = buildingCost.GetComponent<Building>();
        if (building != null && !globalBuildings.Contains(building))
        {
            globalBuildings.Add(building);
            //Debug.Log("Global Building List added: " + building.name); // Success
        }
        else
        {
            Debug.LogWarning("Failed to add building: " + buildingCost.name); // Fail
        }
    }

    #endregion

    public Vector3Int WorldToCell(Vector3 position)
    {
        Vector3 localPos = transform.InverseTransformPoint(position);
        int x = Mathf.FloorToInt(localPos.x / cellSize);
        int y = Mathf.FloorToInt(localPos.y / cellSize);
        int z = Mathf.FloorToInt(localPos.z / cellSize);

        return new Vector3Int(x, y, z);
    }

    // Empty Cell Check 
    public bool IsCellEmpty(Vector3 position)
    {
        Cell cell = GetCellAtWorldPosition(position);
        if (cell == null || cell.currentStatus != Cell.CellStatus.Empty)
        {
            Debug.Log("Cell Position Occupied");
            return false;
        }

        Debug.Log("Cell Position Not Occupied");
        return true;
    }

    // Fill Cell Post
    public void MarkCellAsOccupied(Vector3 position, Building building)
    {
        Cell cell = GetCellAtWorldPosition(position);
        if (cell != null)
        {
            cell.OccupyCellWithBuilding(building);
        }
    }


    // Get Cell Type
    public Cell.TerrainType GetCellType(Vector3 position)
    {
        Cell cell = GetCellAtWorldPosition(position);

        if (cell != null)
        {
            return cell.currentTerrainType;
        }

        return Cell.TerrainType.None;
    }


    public bool IsCellWater(Vector3 position)
    {
        Cell cell = GetCellAtWorldPosition(position);
        if (cell != null && cell.currentTerrainType == Cell.TerrainType.Water)
        {
            return true;
        }
        return false;
    }


    #region Section 2 
    public List<Building> GetAllBuildings()
    {
        return globalBuildings;
    }

    private void OnEnable()
    {
        BuildingCost.OnBuildingPlaced += AddBuilding;
    }

    private void OnDisable()
    {
        BuildingCost.OnBuildingPlaced -= AddBuilding;
    }

    // Start Method
    private void Start()
    {
        // Basic Setup
        bank = FindObjectOfType<Bank>();                        // Find the Bank, if for some reason I forgot
        mapManager = FindObjectOfType<MapManager>();            // locate the amount of islands to be generated
        gridCount = mapManager.numberOfIslands;                 // count the amount of island grids that exist (protip: it starts at 0, so always add 1) 
        buildingChecker = FindObjectOfType<BuildingChecker>();
        
        // Generate Cell Grid
        GenerateGrid();
        
        // Set Bounds Related Information
        SetIslandBounds();
    }

    // Grid Generation Method - from GridSystem.cs / Called from Start Method
    public void GenerateGrid()
    {
        MapGrid mapGrid = GetComponent<MapGrid>();
        if (mapGrid != null && mapGrid.Grid != null)
        {
            grid = mapGrid.Grid;
            gridSize = mapGrid.Size;
            cellSize = 1f; // MapGrid generates cells at 1-unit intervals
        }
        else
        {
            grid = new Cell[gridSize, gridSize];
        }
    }

    /*
    public void SetupGrid(IslandData data)
    {
        this.gridPosition = data.gridData.gridPosition;
        // Use GridData to set up the grid system's logic and initial state.
    }
    */

    void OnDrawGizmos()
    {
        Vector3 gridMinPosition = transform.position;
        Vector3 gridMaxPosition = transform.position + new Vector3(gridSize * cellSize, 0f, gridSize * cellSize);

        Gizmos.color = Color.red;

        Gizmos.DrawLine(gridMinPosition, new Vector3(gridMaxPosition.x, gridMinPosition.y, gridMinPosition.z));
        Gizmos.DrawLine(gridMinPosition, new Vector3(gridMinPosition.x, gridMinPosition.y, gridMaxPosition.z));

        Gizmos.DrawLine(new Vector3(gridMaxPosition.x, gridMinPosition.y, gridMaxPosition.z), new Vector3(gridMaxPosition.x, gridMinPosition.y, gridMinPosition.z));
        Gizmos.DrawLine(new Vector3(gridMaxPosition.x, gridMinPosition.y, gridMaxPosition.z), new Vector3(gridMinPosition.x, gridMinPosition.y, gridMaxPosition.z));
    }

    public void SetIslandBounds()
    {
        Island island = GetComponent<Island>();

        Vector3 gridMinPosition = transform.position;
        Vector3 gridMaxPosition = transform.position + new Vector3(gridSize * cellSize, 0f, gridSize * cellSize);

        Vector3 center = (gridMinPosition + gridMaxPosition) / 2;
        Vector3 size = gridMaxPosition - gridMinPosition;

        Bounds bounds = new Bounds(center, size);
        island.bounds = bounds;
    }

    public Cell GetCellAtWorldPosition(Vector3 worldPosition)
    {
        return GetCellAtPosition(worldPosition);
    }

    public Vector3 SnapToGrid(Vector3 pos)
    {
        Vector3 localPos = transform.InverseTransformPoint(pos);
        Vector3 snappedLocalPos = new Vector3(
            Mathf.Round(localPos.x / cellSize) * cellSize, 
            localPos.y, 
            Mathf.Round(localPos.z / cellSize) * cellSize
        );
        return transform.TransformPoint(snappedLocalPos);
    }
    public Vector3 GetNearestDepositPosition(Vector3 position)
    {
        Vector3 nearestDepositPos = Vector3.zero;
        float minDistance = float.MaxValue;

        foreach (Cell cell in grid)
        {
            if (cell.isDeposit)
            {
                // cell.position is local to MapGrid
                Vector3 worldCellPos = transform.TransformPoint(cell.position);
                float distance = Vector3.Distance(position, worldCellPos);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestDepositPos = worldCellPos;
                }
            }
        }

        return nearestDepositPos;
    }

    // GetNearestPointOnGrid Method
    public Vector3 GetNearestPointOnGrid(Vector3 position)
    {
        Vector3 snappedPos = SnapToGrid(position);
        snappedPos.y = 1f;
        Cell cell = GetCellAtPosition(snappedPos);

        if (cell != null)
        {
            return snappedPos;
        }
        else
        {
            return snappedPos; // simplified fallback
        }
    }
    // Add the bounds to the GridSystem class
    public Bounds gridBounds;

    // Update the bounds when the current island changes
    public void SetCurrentIsland(Island island)
    {
        // Update the bounds based on the current island
        gridBounds = island.bounds;
    }

    // Sets Req Bools
    private bool _ReqShore, _ReqSea, _ReqSub, _ReqLand, _ReqOther;

    public Cell GetCellAtPosition(Vector3 position)
    {
        // Calculate the local position within the island's grid
        Vector3 localPosition = transform.InverseTransformPoint(position);

        int x = Mathf.FloorToInt(localPosition.x / cellSize);
        int z = Mathf.FloorToInt(localPosition.z / cellSize);

        if (x < 0 || x >= gridSize || z < 0 || z >= gridSize)
        {
            return null;
        }

        // Indices are within bounds, so it's safe to access the array
        if (grid != null) {
            return grid[x, z];
        }
        
        return null;
    }

    public Cell GetCell(int x, int z)
    {
        if (x < 0 || x >= gridSize || z < 0 || z >= gridSize)
            return null;
        if (grid != null)
            return grid[x, z];
        return null;
    }


    public bool CanPlaceAtPosition(Vector3 position, Vector3 size)
    {
        Cell cell = GetCellAtPosition(position);

        // Check for building
        if (cell == null)
        {
            Debug.Log("Position is out of bounds!");
            return false;
        }

        // Check for building
        if (cell.occupyingBuilding != null)
        {
            Debug.Log("Cell is occupied!" + cell.occupyingBuilding);
            return false;
        }


        int startX = Mathf.FloorToInt(position.x - size.x / 2);
        int startZ = Mathf.FloorToInt(position.z - size.z / 2);

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.z; z++)
            {
                int targetX = startX + x;
                int targetZ = startZ + z;

                Cell targetCell = GetCellAtPosition(new Vector3(targetX, 0, targetZ));

                if (targetCell == null || targetCell.occupyingBuilding != null)
                {
                    return false;
                }
            }
        }

        // Perform an overlap test using a box collider
        Collider[] overlap = Physics.OverlapBox(position, size / 2, Quaternion.identity, buildings);
        if (overlap.Length > 0)
        {
            foreach (Collider collider in overlap)
            {
                Debug.Log("Buildings Overlap detected with " + collider.name);
            }

            // Debug.Log("Object overlaps with another object!");
            return false;
        }

        Debug.LogFormat("<color=orange>Position is valid!</color>");

        return true;
    }

    #endregion

}
