using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cell
{
    public Vector3Int cellPosition { get; private set; }
    public Vector3 position { get; private set; }
    public Building occupyingBuilding { get; private set; }
    public bool isBlocked { get; private set; }
    public bool isDeposit { get; private set; }
    public ResourceNodeType depositNodeType { get; private set; }
    public bool isOccupied => occupyingBuilding != null;
    public bool isRoad { get; private set; }

    public CellStatus currentStatus { get; private set; }

    public TerrainType currentTerrainType { get; private set; }

    // Cell Neighbors
    public List<Cell> neighbors { get; private set; }

    // River Tuples
    Tuple<int, int> riverSource, riverMouth, riverEnd;

    // River Status
    public RiverStatus riverStatus { get; private set; }
    public RiverDirection riverDirection { get; private set; }

    // Height
    public float height { get; private set; }

    public (EdgeTypes, EdgeType) Edges { get; set; }

    public Dictionary<EdgeTypes, EdgeType> CellEdgeType;

    public enum EdgeTypes
    {
        // No Edge - Default
        None,

        // has Edge - (Max 4, Min 1)
        North,
        East,
        South,
        West
    }

    public enum EdgeType
    {
        // No Edge - Default
        None,

        // One Block Type
        beach,
        river,
        deep,

        // Two Block Type
        coast,
        ocean,
        abyssal,
        
        // Block Type
        plateau
    }

    public enum CellStatus
    {
        Full,
        Empty,
        River,
        Water,
        Ocean,
        Road,
    }

    #region River Section
    public enum RiverStatus
    {
        None,
        River,
        RiverSource,
        RiverMouth
    }
    public enum RiverDirection
    {
        // Default
        None,

        // Horizontal
        North,
        East,
        South,
        West,
        
        // Diagonal
        NorthEast,
        NorthWest,
        SouthEast,
        SouthWest,

    }
    #endregion

    public enum TerrainType
    {
        // Special Types
        Unknown,
        None,

        // Water Types
        Abyssal,
        River,
        Water,
        Stream, 
        Sea,
        Ocean,
        Shallow,
        Deep,
        Plateau,

        // Terrain Types
        Land,
        Shore,
        Coast,
        Desert,
        Forest,
        Beach,
        Plain,
        Rocky,
        
        //TerrainHeight
        Ground,

        // Above
        HillSide,
        Hill,

        Cliff,
        CliffWall,
        CliffPeak,
        
        Mountain,
        MountainWall,
        MountainPeak,
        MountainSummit,

        // Special
        WaterFall,
        Other,
    }

    public void OccupyWaterCellWithBuilding(Building building)
    {
        if (this.occupyingBuilding == null)
        {
            this.occupyingBuilding = building;
            this.currentStatus = CellStatus.Full;

        }
        else
        {
            Debug.LogWarning("Water Cell is already occupied.");
        }
    }

    public void OccupyCellWithBuilding(Building building)
    {
        if (this.occupyingBuilding == null)
        {
            this.occupyingBuilding = building;
            this.currentStatus = CellStatus.Full;
            
        }
        else
        {
            Debug.LogWarning("Cell is already occupied.");
        }
    }

    public void ChangeTerrainType(TerrainType newTerrainType)
    {
        currentTerrainType = newTerrainType;

        // Any other logic that needs to happen when the terrain type changes
        // For example, update the visuals or notify other components
    }

    public void SetDeposit(ResourceNodeType nodeType)
    {
        isDeposit = true;
        depositNodeType = nodeType;
    }

    public void SetRoad(bool value)
    {
        isRoad = value;
    }

    public void SetRiverData(RiverStatus status, RiverDirection direction)
    {
        riverStatus = status;
        riverDirection = direction;
    }

    public Cell(Vector3 _position, Building building, TerrainType terrainType, bool isBlocked = false, bool isDeposit = false)
    {
        this.position = _position;
        this.occupyingBuilding = building;
        this.isBlocked = isBlocked;
        this.isDeposit = isDeposit;
        this.currentStatus = building == null ? CellStatus.Empty : CellStatus.Full;
        this.currentTerrainType = terrainType;

        // Set the cell position based on the position
        this.cellPosition = new Vector3Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y), Mathf.RoundToInt(position.z));


    }

    // Set the cell Neighbor - Cell.cs
    public void UpdateNeighbors(Cell[,] grid, int size)
    {
        // Initialize the neighbors list 
        neighbors = new List<Cell>();

        // Coordinates of the cell
        int x = cellPosition.x;
        int y = cellPosition.y;

        // Add the neighbors
        // Debug.Log("x: " + x + " y: " + y);

        // orthogonal neighbors
        if (x > 0 && y >= 0 && y < size) neighbors.Add(grid[x - 1, y]);
        if (x < size - 1 && y >= 0 && y < size) neighbors.Add(grid[x + 1, y]);
        if (y > 0 && x >= 0 && x < size) neighbors.Add(grid[x, y - 1]);
        if (y < size - 1 && x >= 0 && x < size) neighbors.Add(grid[x, y + 1]);

        // Diagonal neighbors
        if (x > 0 && y > 0) neighbors.Add(grid[x - 1, y - 1]);
        if (x < size - 1 && y > 0) neighbors.Add(grid[x + 1, y - 1]);
        if (x > 0 && y < size - 1) neighbors.Add(grid[x - 1, y + 1]);
        if (x < size - 1 && y < size - 1) neighbors.Add(grid[x + 1, y + 1]);
    }
}

