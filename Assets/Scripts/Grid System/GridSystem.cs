using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static BuildingProperties;

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
        int x = Mathf.FloorToInt((position.x - gridPosition.x) / cellSize);
        int y = Mathf.FloorToInt((position.y - gridPosition.y) / cellSize);
        int z = Mathf.FloorToInt((position.z - gridPosition.z) / cellSize);

        return new Vector3Int(x, y, z);
    }

    //  Empty Cell Check 
    public bool IsCellEmpty(Vector3 position)
    {
        Cell cell = GetCellAtWorldPosition(position);
        if (cell == null || cell.isOccupied)
        {
            Debug.Log("Cell Position Occupied");
            return false;
        }

        Debug.Log("Cell Position Not Occupied");
        return true;
    }

    // Fill Cell Post
    public void MarkCellAsOccupied(Vector3 position)
    {
        Cell cell = GetCellAtWorldPosition(position);
        if (cell != null)
        {
            cell.isOccupied = true;
        }
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
        grid = new Cell[gridSize, gridSize];                    // determine the islands grid size
        mapManager = FindObjectOfType<MapManager>();            // locate the amount of islands to be generated
        gridCount = mapManager.numberOfIslands;                 // count the amount of island grids that exist (protip: it starts at 0, so always add 1) 
        buildingChecker = FindObjectOfType<BuildingChecker>();
        
        // Set Bounds Related Information
        SetIslandBounds();
        //gridPosition = transform.position;
        //SetCurrentIsland(GetComponent<Island>());               // Set Local Island Bounds

        // Generate Cell Grid
        GenerateGrid();

        // Log Bounds Island
        Island gridIsland = GetComponent<Island>();

        // Log Bounds Render
        Renderer renderer = GetComponent<Renderer>();
        
        gridIsland.LogBounds();
        //Debug.Log("Bounds: " + renderer.bounds);
    }

    // Grid Generation Method
    private void GenerateGrid()
    {
        gridCount++;
        Vector3 cellSizeVec = new Vector3(cellSize, 0f, cellSize);
        int cellCount = 0; // How Many Cells was Generated per Island

        // Square Island Generation
        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                Vector3 cellPos = transform.position + new Vector3(x * cellSize, 0f, z * cellSize);


                RaycastHit hit;
                if (Physics.Raycast(cellPos + Vector3.up * 100f, Vector3.down, out hit, Mathf.Infinity))
                {
                    cellPos = hit.point; // - offset;
                    cellPos.x = Mathf.Round(cellPos.x / cellSize) * cellSize;
                    cellPos.z = Mathf.Round(cellPos.z / cellSize) * cellSize;
                    cellPos.y = 0f; // Set the Y value to 0

                    Cell cell = new Cell(cellPos, null, false);
                    grid[x, z] = cell;

                    cellCount++;
                }
            }
        }

        // Total Number of Cells & Grids Generated - Console Debugging 
        Debug.Log("Cells: " + cellCount + " Grids: " + gridCount);
    }

    void OnDrawGizmos()
    {
        Vector3 gridMinPosition = gridPosition - new Vector3(gridSize * cellSize * 0.5f, 0f, gridSize * cellSize * 0.5f);
        Vector3 gridMaxPosition = gridPosition + new Vector3(gridSize * cellSize * 0.5f, 0f, gridSize * cellSize * 0.5f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(gridMinPosition, new Vector3(gridMaxPosition.x, gridMinPosition.y, gridMinPosition.z));
        Gizmos.DrawLine(gridMinPosition, new Vector3(gridMinPosition.x, gridMinPosition.y, gridMaxPosition.z));
        Gizmos.DrawLine(new Vector3(gridMaxPosition.x, gridMinPosition.y, gridMaxPosition.z), new Vector3(gridMaxPosition.x, gridMinPosition.y, gridMinPosition.z));
        Gizmos.DrawLine(new Vector3(gridMaxPosition.x, gridMinPosition.y, gridMaxPosition.z), new Vector3(gridMinPosition.x, gridMinPosition.y, gridMaxPosition.z));
    }

    public void SetIslandBounds()
    {
        Island island = GetComponent<Island>();

        Debug.Log($"Island: {island.islandName + "id: " + island.id} Bounds: {island.bounds}");


        Vector3 gridMinPosition = gridPosition - new Vector3(gridSize * cellSize * 0.5f, 0f, gridSize * cellSize * 0.5f);
        Vector3 gridMaxPosition = gridPosition + new Vector3(gridSize * cellSize * 0.5f, 0f, gridSize * cellSize * 0.5f);

        Vector3 center = (gridMinPosition + gridMaxPosition) / 2;
        Vector3 size = gridMaxPosition - gridMinPosition;

        Bounds bounds = new Bounds(center, size);
        island.bounds = bounds;
    }

    public Cell GetCellAtWorldPosition(Vector3 worldPosition)
    {

        Vector3Int gridPosition = WorldToCell(worldPosition);

        return GetCellAtPosition(gridPosition);
    }

    public Vector3 SnapToGrid(Vector3 pos)
    {
        Vector3 snappedPos = new Vector3(Mathf.Round(pos.x / cellSize) * cellSize, pos.y, Mathf.Round(pos.z / cellSize) * cellSize);
        return snappedPos;
    }
    public Vector3 GetNearestDepositPosition(Vector3 position)
    {
        Vector3 nearestDepositPos = Vector3.zero;
        float minDistance = float.MaxValue;

        foreach (Cell cell in grid)
        {
            if (cell.isDeposit)
            {
                float distance = Vector3.Distance(position, cell.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestDepositPos = cell.position;
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
            //Debug.Log("result: " + snappedPos);
            return snappedPos;
        }
        else
        {

            position -= offset; // Subtract the offset before calculating the grid position

            int xCount = Mathf.RoundToInt(position.x / cellSize);
            int yCount = Mathf.RoundToInt(position.y / cellSize);
            int zCount = Mathf.RoundToInt(position.z / cellSize);

            Vector3 result = new Vector3(
                (float)xCount * cellSize,
                (float)yCount * cellSize,
                (float)zCount * cellSize);

            result += offset; // Add the offset back to the result

            cell = GetCellAtPosition(result);

            if (cell != null)
            {
                //Debug.Log("result 1: " + result);
                return result;
            }

            //Debug.Log("result 2: " + result);
            return result;
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

        // Debug.Log(position);
        position.y = 0f;
        // Get Local Island - GridSystem Bounds
        Island gridIsland = GetComponent<Island>();
        Bounds islandBounds = gridIsland.bounds;

        // Null Check
        if (islandBounds == null)
        {
            Debug.Log($"Island{gridIsland.islandName} has No Bounds.");
            return null;
        }
        position.y = 0f;

        // Calculate the local position within the island's grid
        Vector3 localPosition = gridIsland.transform.InverseTransformPoint(position);

        // Calculate the grid's local minimum and maximum positions
        Vector3 gridMinPosition = -new Vector3(gridSize * cellSize * 0.5f, 0f, gridSize * cellSize * 0.5f);
        Vector3 gridMaxPosition = new Vector3(gridSize * cellSize * 0.5f, 0f, gridSize * cellSize * 0.5f);

        // Future Note:
        // Need a Shoreline Case for
        // Fisheries and vice versa.
        // Check if Shoreline then Nope
        
        if (!_ReqShore || _ReqSea || _ReqSub || _ReqLand || _ReqOther) // None Shoreline Buildings
        {
            // Bounds Check
            if (!islandBounds.Contains(localPosition) ||
            localPosition.x < gridMinPosition.x ||
            localPosition.x > gridMaxPosition.x ||
            localPosition.z < gridMinPosition.z ||
            localPosition.z > gridMaxPosition.z) 
            {
                    buildingChecker.canPlace = false;
                    return null;
            }
            
        }

        // Convert the local position to grid indices
        localPosition -= gridMinPosition;
        localPosition.y = 0f;

        int x = Mathf.FloorToInt(localPosition.x / cellSize);
        int z = Mathf.FloorToInt(localPosition.z / cellSize);

        x = Mathf.Clamp(x, 0, gridSize - 1);
        z = Mathf.Clamp(z, 0, gridSize - 1);

        //Debug.LogFormat("GridSize: {0}, CellSize: {1}, LocalPosition: {2} Position:{3} x:{4} z:{5}", gridSize, cellSize, localPosition, position, x, z);

        // Indices are within bounds, so it's safe to access the array
        Cell cell = grid[x, z];
        buildingChecker.canPlace = true;
        return cell;
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
        if (cell.building != null)
        {
            Debug.Log("Cell is occupied!" + cell.building);
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

                if (targetCell == null || targetCell.building != null)
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
