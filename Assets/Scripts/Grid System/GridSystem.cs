using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GridSystem : MonoBehaviour
{   
    [SerializeField] private MapManager mapManager;
    [SerializeField] private LayerMask buildings; // = LayerMask.GetMask("Ground", "Building");

    #region section 1 

    private HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
    public int gridSize = 10;
    public float cellSize = 1f;
    public Vector3 gridPosition;
    public Vector3 offset = new Vector3(-5, 0, -5);
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
            Vector3Int cellPosition = WorldToCell(position);
            if (occupiedCells.Contains(cellPosition))
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
            Vector3Int cellPosition = WorldToCell(position);
            occupiedCells.Add(cellPosition);
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
            bank = FindObjectOfType<Bank>();                // Find the Bank, if for some reason I forgot
            grid = new Cell[gridSize, gridSize];            // determine the islands grid size
            mapManager = FindObjectOfType<MapManager>();    // locate the amount of islands to be generated
            gridCount = mapManager.numberOfIslands;         // count the amount of island grids that exist (protip: it starts at 0, so always add 1) 
            buildingChecker = FindObjectOfType<BuildingChecker>();

            // Generate Cell Grid
            GenerateGrid();
            
            // Log Bounds Island
            Island gridIsland = GetComponent<Island>();
            //gridIsland.LogBounds();
            
            // Log Bounds Render
            Renderer renderer = GetComponent<Renderer>();
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
                    cellPos = hit.point;
                    cellPos.x = Mathf.Round(cellPos.x / cellSize) * cellSize;
                    cellPos.z = Mathf.Round(cellPos.z / cellSize) * cellSize;
                    cellPos.y = 0f; // Set the Y value to 0
        
                    Cell cell = new Cell(cellPos, null, false);
                    grid[x, z] = cell;

                    Debug.DrawRay(cell.position, Vector3.up * 50f, Color.green * 2f, 10f);

                    
                    // Questions
                    // Does Grid Exist?
                    // Does Cell Exist?
                    // Does Bounds Exist?
                    

                    cellCount++; 
                    //Debug.Log("Cell created at position: " + cell.position);

                }
            }
        }

        // Total Number of Cells & Grids Generated - Console Debugging 
        Debug.Log("Cells: " + cellCount + " Grids: " + gridCount);
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
    // Within a island Bounds
    public Cell GetCellAtPosition(Vector3 position)
    {   

        // Outside island

        // Clamp Y value to 0
        position.y = 0f; 

        // If Out of Bounds - Check
        if (!GetComponent<Renderer>().bounds.Contains(position)) // Check if the position is within the island's bounds
        {
            // if Position is Out of Bounds then this

            //Debug.LogFormat("<color=red>Out of Bounds - {0}</color>", position); // Red
            buildingChecker.canPlace = false;

            return null;
        } 
        else 
        {
            // Inside Island

            // If Grid Position within Bounds - Check
            
                //Debug.LogFormat("<color=green>In Bounds - {0}</color>", position); // Green
            
            // Account for Global to Local Position Difference 
            // The difference in position is the real position
            // World Position -= grid position = local Real Position 

                position -= gridPosition;  
                
            // Reset the Y value for comparing it, since we adjusted for world difference
            // Clamp Y value to 0, Again bc of Potential World difference
                
                position.y = 0f; 

                //or                 

                // Clamp Y value to 0 or higher
                // position.y = Mathf.Clamp(position.y, 0f, Mathf.Infinity);

            // Assigns X & Z Cordinates based on position values and CellSize

                int x = Mathf.RoundToInt(position.x / cellSize);
                int z = Mathf.RoundToInt(position.z / cellSize);

                if (x < 0 || x >= gridSize || z < 0 || z >= gridSize)
                {

                    // Invalid Position

                    //Debug.LogFormat("<color=red>Invalid Position - {0}</color>", position); // Red
                    buildingChecker.canPlace = false;
                    //Debug.Log($"Invalid grid position: ({x}, {z})");
                    return null;
                }

            // If Cell Exists & Inbound (Cell Request Can be made)

            // Get Cell Position
            Cell cell = grid[x, z];
            
            //Debug.Log("<color=green>Cells grid position " + $"({x}, {z}), cell: {cell}</color>");  // Real & True
            buildingChecker.canPlace = true;
            // Cell Return
            return cell; 
        }
    }
    
    public bool CanPlaceAtPosition(Vector3 position, Vector3 size)
    {
        /*  CanPlaceAtPosition

            Requirements: Position - Cell && Position - Size Collider  

                1. The position argument must be within the bounds of the grid.
                2. The cell at the position must not already have a building placed on it.
                3. The size argument must not overlap with any other colliders in the scene. ´
            Or
                1. False == CanPlaceAtPosition
                2. Errors from CanPlaceAtPosition
            
        */

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
            Debug.Log("Cell is occupied!"+ cell.building);
            return false;
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

