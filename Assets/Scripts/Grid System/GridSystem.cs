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
    private GameObject buildGridOverlay;
    private Mesh buildGridMesh;
    private Material buildGridMaterial;
    private bool buildGridVisible;

    [Header("Build Grid Visualization")]
    [SerializeField] private Color buildGridColor = new Color(0.2f, 0.85f, 1f, 0.65f);
    [Tooltip("Clears z-fighting against the terrain and nothing more. The grid follows the " +
             "terrain surface itself, so this stays small enough to read as flush.")]
    [SerializeField, Min(0f)] private float buildGridHeightOffset = 0.02f;
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

    #region Footprint coordinate convention

    // ------------------------------------------------------------------------------
    // THE convention. Cell (x,z) physically occupies grid-local [x, x+1) on both axes
    // and its centre is (x+0.5, z+0.5) - see Cell.localCenter. A building's footprint is
    // CENTRED on its transform, which falls out of that as:
    //
    //     odd footprint  -> pivot sits on a cell centre   (identical to what
    //                       GetNearestPointOnGrid has always returned)
    //     even footprint -> pivot sits on a cell boundary
    //
    // A 1x1 building therefore snaps exactly where it always did; only multi-cell
    // footprints move, and they move from "model half a cell off its reserved cells" to
    // "model centred on them".
    //
    // Every consumer that turns a world position into cells - the blueprint's snap, the
    // placement check, cell reservation, the quay foundation - goes through these
    // methods, so none of them can drift apart again.
    // ------------------------------------------------------------------------------

    /// <summary>
    /// The footprint in cells for a building of this size at this rotation. A quarter
    /// turn about Y exchanges the axes, so a 3x2 building becomes 2x3.
    /// </summary>
    public static Vector2Int GetFootprint(Vector3 buildingSize, Quaternion rotation)
    {
        Vector2Int footprint = GetFootprint(buildingSize);
        int quarterTurns = Mathf.RoundToInt(rotation.eulerAngles.y / 90f) & 3;
        return (quarterTurns & 1) == 1 ? new Vector2Int(footprint.y, footprint.x) : footprint;
    }

    /// <summary>The unrotated footprint in cells, never smaller than one cell.</summary>
    public static Vector2Int GetFootprint(Vector3 buildingSize)
    {
        return new Vector2Int(
            Mathf.Max(1, Mathf.RoundToInt(buildingSize.x)),
            Mathf.Max(1, Mathf.RoundToInt(buildingSize.z)));
    }

    /// <summary>The -X/-Z cell of the footprint a building centred here would cover.</summary>
    public Vector3Int GetFootprintOrigin(Vector3 worldPosition, Vector2Int footprint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        return new Vector3Int(
            RoundHalfUp(local.x / cellSize - footprint.x * 0.5f),
            0,
            RoundHalfUp(local.z / cellSize - footprint.y * 0.5f));
    }

    /// <summary>Keeps a whole footprint on the island rather than only its origin cell.</summary>
    public Vector3Int ClampFootprintOrigin(Vector3Int origin, Vector2Int footprint)
    {
        return new Vector3Int(
            Mathf.Clamp(origin.x, 0, Mathf.Max(0, gridSize - footprint.x)),
            origin.y,
            Mathf.Clamp(origin.z, 0, Mathf.Max(0, gridSize - footprint.y)));
    }

    /// <summary>
    /// Where a footprint of this size comes to rest over a point. The multi-cell
    /// generalisation of GetNearestPointOnGrid, which it reduces to exactly at 1x1.
    /// </summary>
    public Vector3 SnapFootprintToGrid(Vector3 worldPosition, Vector2Int footprint)
    {
        Vector3Int origin = ClampFootprintOrigin(GetFootprintOrigin(worldPosition, footprint), footprint);
        return GetFootprintCenterWorld(origin, footprint);
    }

    /// <summary>World centre of the footprint anchored at this origin cell.</summary>
    public Vector3 GetFootprintCenterWorld(Vector3Int origin, Vector2Int footprint)
    {
        // Elevation comes from the cell the centre falls in, which is what
        // GetNearestPointOnGrid returned through Cell.localCenter.
        Cell centerCell = GetCell(origin.x + (footprint.x - 1) / 2, origin.z + (footprint.y - 1) / 2);
        float y = centerCell != null ? centerCell.height : 0f;

        return transform.TransformPoint(new Vector3(
            (origin.x + footprint.x * 0.5f) * cellSize,
            y,
            (origin.z + footprint.y * 0.5f) * cellSize));
    }

    /// <summary>World centre of one cell - the position MarkCellAsOccupied expects.</summary>
    public Vector3 GetCellCenterWorld(int x, int z)
    {
        return transform.TransformPoint(new Vector3((x + 0.5f) * cellSize, 0f, (z + 0.5f) * cellSize));
    }

    // Mathf.RoundToInt sends .5 to the nearest EVEN integer, so an even footprint resting
    // exactly over a cell centre would snap left or right depending on where on the island
    // it happened to be. Rounding .5 consistently upwards keeps the snap uniform.
    private static int RoundHalfUp(float value)
    {
        return Mathf.FloorToInt(value + 0.5f);
    }

    #endregion

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
        gridCount = mapManager != null ? mapManager.RunGridSize : 0;
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

        RebuildBuildGridOverlay();
    }

    /// <summary>
    /// Shows the exact cells used by snapping and placement validation. The overlay is
    /// generated from this GridSystem's adopted MapGrid.Cell array, so it cannot drift
    /// from the per-island gameplay grid.
    /// </summary>
    public void SetBuildGridVisible(bool visible)
    {
        buildGridVisible = visible;

        if (visible && buildGridOverlay == null)
        {
            RebuildBuildGridOverlay();
        }

        if (buildGridOverlay != null)
        {
            buildGridOverlay.SetActive(visible);
        }
    }

    /// <summary>
    /// Regenerates the build grid after something changed which cells are buildable or
    /// how high they sit - a quay deck being laid or removed, in practice. Cheap enough
    /// to call on those events and never called per frame.
    /// </summary>
    public void RefreshBuildGridOverlay()
    {
        if (grid == null) return;
        RebuildBuildGridOverlay();
    }

    private void RebuildBuildGridOverlay()
    {
        if (buildGridOverlay != null)
        {
            Destroy(buildGridOverlay);
        }
        if (buildGridMesh != null)
        {
            Destroy(buildGridMesh);
        }
        if (buildGridMaterial != null)
        {
            Destroy(buildGridMaterial);
        }

        buildGridOverlay = null;
        buildGridMesh = null;
        buildGridMaterial = null;

        if (grid == null || gridSize <= 0) return;

        // Four independent edges per cell deliberately preserve height changes between
        // neighbouring cells instead of flattening the display to the island origin.
        var vertices = new List<Vector3>(gridSize * gridSize * 8);
        var indices = new List<int>(gridSize * gridSize * 8);

        QuaySystem quaySystem = GetComponent<QuaySystem>();

        // The same sampled heightfield the terrain mesh is built from, so a grid line and
        // the ground under it agree at every cell corner. Cell.height is one quantised
        // value for the whole cell and cannot follow a continuous surface - reading it
        // here is what made the overlay step and float over beaches and slopes.
        TerrainSampleCache heightField = ResolveTerrainHeightField();
        float quayTop = quaySystem != null ? quaySystem.TopElevationLocal : 0f;

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                Cell cell = grid[x, z];
                if (cell == null) continue;

                bool hasQuay = quaySystem != null && quaySystem.HasQuay(new Vector2Int(x, z));

                // Construction grid on the open seabed means nothing - nothing can be
                // built there until a quay deck exists to build on.
                if (cell.IsUnderwater && !hasQuay) continue;

                // A quay deck is flat by construction, so its cell reads one elevation.
                // Land and beach follow the terrain corner by corner instead.
                float c00 = hasQuay ? quayTop : SampleCornerHeight(heightField, cell, x, z);
                float c10 = hasQuay ? quayTop : SampleCornerHeight(heightField, cell, x + 1, z);
                float c11 = hasQuay ? quayTop : SampleCornerHeight(heightField, cell, x + 1, z + 1);
                float c01 = hasQuay ? quayTop : SampleCornerHeight(heightField, cell, x, z + 1);

                float o = buildGridHeightOffset;
                Vector3 p00 = new Vector3(x, c00 + o, z);
                Vector3 p10 = new Vector3(x + 1f, c10 + o, z);
                Vector3 p11 = new Vector3(x + 1f, c11 + o, z + 1f);
                Vector3 p01 = new Vector3(x, c01 + o, z + 1f);

                AddGridLine(vertices, indices, p00, p10);
                AddGridLine(vertices, indices, p10, p11);
                AddGridLine(vertices, indices, p11, p01);
                AddGridLine(vertices, indices, p01, p00);
            }
        }

        buildGridMesh = new Mesh { name = $"{name} Build Grid" };
        if (vertices.Count > 65535)
        {
            buildGridMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }
        buildGridMesh.SetVertices(vertices);
        buildGridMesh.SetIndices(indices, MeshTopology.Lines, 0);
        buildGridMesh.RecalculateBounds();

        buildGridOverlay = new GameObject("Build Grid Overlay");
        buildGridOverlay.transform.SetParent(transform, false);
        buildGridOverlay.AddComponent<MeshFilter>().sharedMesh = buildGridMesh;

        MeshRenderer renderer = buildGridOverlay.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        buildGridMaterial = OverlayMaterial.Create(buildGridColor);
        renderer.sharedMaterial = buildGridMaterial;
        buildGridOverlay.SetActive(buildGridVisible);
    }

    /// <summary>
    /// The terrain heightfield this island's mesh was generated from, or null when the
    /// island was not built through IslandTerrainProvider. Already cached by the provider,
    /// so this is a lookup rather than a regeneration.
    /// </summary>
    private TerrainSampleCache ResolveTerrainHeightField()
    {
        MapGrid grid = GetComponent<MapGrid>();
        if (grid == null || grid.TerrainSource == null || grid.generationSettings == null) return null;

        return grid.TerrainSource.GetOrCreateSampleCache(grid.generationSettings.visualSamplesPerCell);
    }

    /// <summary>
    /// Terrain height at grid corner (x, z), falling back to the cell's own quantised
    /// height when there is no sampled field to read.
    /// </summary>
    private static float SampleCornerHeight(TerrainSampleCache heightField, Cell cell, int x, int z)
    {
        if (heightField == null) return cell.height;

        int samples = heightField.VisualSamplesPerCell;
        return heightField.GetHeight(x * samples, z * samples);
    }

    private static void AddGridLine(List<Vector3> vertices, List<int> indices, Vector3 start, Vector3 end)
    {
        int first = vertices.Count;
        vertices.Add(start);
        vertices.Add(end);
        indices.Add(first);
        indices.Add(first + 1);
    }

    private void OnDestroy()
    {
        if (buildGridMesh != null) Destroy(buildGridMesh);
        if (buildGridMaterial != null) Destroy(buildGridMaterial);
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
            (Mathf.Floor(localPos.x / cellSize) + 0.5f) * cellSize, 
            localPos.y, 
            (Mathf.Floor(localPos.z / cellSize) + 0.5f) * cellSize
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
                // localCenter, not position: position carries the array index, the
                // cell physically occupies local [x, x+1) and its centre is x+0.5.
                Vector3 worldCellPos = transform.TransformPoint(cell.localCenter);
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
        Cell cell = GetCellAtPosition(snappedPos);

        if (cell != null)
        {
            return transform.TransformPoint(cell.localCenter);
        }

        return snappedPos;
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

    public bool IsValidSurfaceConstructionCell(Cell cell)
    {
        return cell != null
            && !cell.isBlocked
            && !cell.isOccupied
            && cell.IsBuildableSurface;
    }

    public bool IsValidUnderwaterPlateauCell(Cell cell)
    {
        return cell != null
            && !cell.isBlocked
            && !cell.isOccupied
            && cell.IsBuildableUnderwaterPlateau;
    }

    public bool IsValidUnderwaterPlateauPosition(Vector3 worldPosition)
    {
        return IsValidUnderwaterPlateauCell(GetCellAtWorldPosition(worldPosition));
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
