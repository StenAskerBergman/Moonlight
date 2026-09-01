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
    public RoadDefinition roadDefinition { get; private set; }

    public CellStatus currentStatus { get; private set; }

    public TerrainType currentTerrainType { get; private set; }

    // Cell Neighbors
    public List<Cell> neighbors { get; private set; }

    // River Tuples
    Tuple<int, int> riverSource, riverMouth, riverEnd;

    // River Status
    public RiverStatus riverStatus { get; private set; }
    public RiverDirection riverDirection { get; private set; }

    /// <summary>
    /// The cell's centre in MapGrid-local space.
    ///
    /// position/cellPosition carry the cell's ARRAY INDEX - UpdateNeighbors and
    /// RiverArea index straight into Cell[,] with them, so they must stay integer.
    /// Spatially a cell occupies local [x, x+1) x [z, z+1), so anything placing an
    /// object on a cell or measuring distance to one wants this, not position.
    /// </summary>
    public Vector3 localCenter => new Vector3(cellPosition.x + 0.5f, height, cellPosition.z + 0.5f);

    // Height
    public float height { get; private set; }
    public float localHeightVariance { get; private set; }
    public float deliberatePlateauInfluence { get; private set; }
    public bool IsSlopeSuitableForBuilding { get; private set; }
    public bool IsDeliberateUnderwaterPlateau => deliberatePlateauInfluence >= 0.9999f;

    public bool IsUnderwater
    {
        get
        {
            switch (currentTerrainType)
            {
                case TerrainType.Abyssal:
                case TerrainType.River:
                case TerrainType.Water:
                case TerrainType.Stream:
                case TerrainType.Sea:
                case TerrainType.Ocean:
                case TerrainType.Shallow:
                case TerrainType.Deep:
                case TerrainType.Plateau:
                    return true;
                default:
                    return false;
            }
        }
    }

    public bool IsBuildableFlatRegion => currentTerrainType == TerrainType.Land
        || currentTerrainType == TerrainType.Beach
        || currentTerrainType == TerrainType.Plain
        || currentTerrainType == TerrainType.Plateau;

    public bool IsBuildableSurface => !IsUnderwater
        && IsBuildableFlatRegion
        && IsSlopeSuitableForBuilding;

    public bool IsBuildableUnderwaterPlateau => IsDeliberateUnderwaterPlateau
        && currentTerrainType == TerrainType.Plateau
        && IsSlopeSuitableForBuilding;

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

    public void ReleaseCell()
    {
        this.occupyingBuilding = null;
        this.currentStatus = CellStatus.Empty;
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

    public void SetRoad(bool value, RoadDefinition definition = null)
    {
        isRoad = value;
        roadDefinition = value ? definition : null;
    }

    public void SetRiverData(RiverStatus status, RiverDirection direction)
    {
        riverStatus = status;
        riverDirection = direction;
    }

    public void SetTerrainMetrics(float heightVariance, float maxBuildableHeightVariance)
    {
        localHeightVariance = Mathf.Max(0f, heightVariance);
        IsSlopeSuitableForBuilding = localHeightVariance <= Mathf.Max(0f, maxBuildableHeightVariance);
    }

    public void SetDeliberatePlateauInfluence(float influence)
    {
        deliberatePlateauInfluence = Mathf.Clamp01(influence);
    }

    public void SetDeliberatePlateauBuildability(float buildableWeight)
    {
        deliberatePlateauInfluence = Mathf.Clamp01(buildableWeight);
    }

    public Cell(Vector3 _position, Building building, TerrainType terrainType, bool isBlocked = false, bool isDeposit = false)
    {
        this.position = _position;
        this.occupyingBuilding = building;
        this.isBlocked = isBlocked;
        this.isDeposit = isDeposit;
        this.currentStatus = building == null ? CellStatus.Empty : CellStatus.Full;
        this.currentTerrainType = terrainType;
        this.height = _position.y;
        this.localHeightVariance = 0f;
        this.deliberatePlateauInfluence = 0f;
        this.IsSlopeSuitableForBuilding = true;

        // Set the cell position based on the position
        this.cellPosition = new Vector3Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y), Mathf.RoundToInt(position.z));


    }

    // Set the cell Neighbor - Cell.cs
    public void UpdateNeighbors(Cell[,] grid, int size)
    {
        // Initialize the neighbors list 
        neighbors = new List<Cell>();

        // Grid coordinates of the cell. The grid is laid out in world XZ, and cells
        // are built as position = (gridX, terrainHeight, gridZ) — so the second grid
        // index is cellPosition.z. Reading cellPosition.y here instead would use the
        // terrain height as a row index, which collapses every cell's neighbours onto
        // rows -1/0/1 and leaves cells further apart permanently unconnected.
        int x = cellPosition.x;
        int y = cellPosition.z;

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

