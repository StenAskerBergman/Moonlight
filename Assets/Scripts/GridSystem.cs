using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridSystem : MonoBehaviour
{
    public int gridSize = 10;
    public float cellSize = 1f;
    public GameObject islandGameObject;
    public Transform islandPlane;

    private Cell[,] grid;

    private void Start()
    {
        islandGameObject = gameObject;
        islandPlane = islandGameObject.transform;
        grid = new Cell[gridSize, gridSize];
        GenerateGrid();
    }

    private void GenerateGrid()
    {
        // Determine the size of each grid cell based on the desired cell size
        Vector3 cellSizeVec = new Vector3(cellSize, 0f, cellSize);

        // Loop over each cell in the grid and determine its position in the game world
        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                Vector3 cellPos = islandPlane.position + new Vector3(x * cellSize, 0f, z * cellSize);

                // Use raycasting to determine the height of the terrain at the cell position
                RaycastHit hit;
                if (Physics.Raycast(cellPos + Vector3.up * 100f, Vector3.down, out hit, Mathf.Infinity))
                {
                    // Align the cell position with the terrain and snap it to the nearest grid point
                    cellPos = hit.point;
                    cellPos.x = Mathf.Round(cellPos.x / cellSize) * cellSize;
                    cellPos.z = Mathf.Round(cellPos.z / cellSize) * cellSize;

                    // Create a new cell at the position and add it to the grid
                    Cell cell = new Cell(cellPos, null, false);
                    grid[x, z] = cell;
                    
                    // Draw the grid cell
                    //GameObject cellObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    //cellObject.transform.position = cellPos;
                    //cellObject.transform.localScale = cellSizeVec;
                }
            }
        }
    }
    private void OnDrawGizmos()
    {
        // Determine the size of each grid cell based on the desired cell size
        Vector3 cellSizeVec = new Vector3(cellSize, 0f, cellSize);

        // Loop over each cell in the grid and determine its position in the game world
        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                Vector3 cellPos = islandPlane.position + new Vector3(x * cellSize, 0f, z * cellSize);

                // Draw a wire cube at the cell position
                Gizmos.DrawWireCube(cellPos + cellSizeVec * 0.5f, cellSizeVec);
            }
        }
    }


    public Vector3 SnapToGrid(Vector3 pos)
    {
        Vector3 snappedPos = new Vector3(Mathf.Round(pos.x / cellSize) * cellSize, pos.y, Mathf.Round(pos.z / cellSize) * cellSize);
        return snappedPos;
    }

    public Cell GetCellAtPosition(Vector3 position)
    {
        // Convert the world position to a grid cell index
        int x = Mathf.RoundToInt(position.x / cellSize);
        int z = Mathf.RoundToInt(position.z / cellSize);

        // Check if the cell is out of bounds
        if (x < 0 || x >= gridSize || z < 0 || z >= gridSize)
        {
            return null;
        }

        // Return the cell at the specified grid position
        return grid[x, z];
    }

    public bool IsEmptyAtPosition(Vector3 position)
    {
        Cell cell = GetCellAtPosition(position);

        // Check if the cell is out of bounds
        if (cell == null)
        {
            return false;
        }

        // Check if the cell is occupied by a building
        if (cell.building != null)
        {
            return false;
        }

        // // Check if the cell is blocked by a game object
        // if (cell.gameObject != null)
        // {
        //     return false;
        // }

        // The cell is empty
        return true;
    }

    private void Update()
    {
        // Check for building placement and update the grid accordingly
    }
}
