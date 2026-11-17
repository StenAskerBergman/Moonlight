using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Places and removes road tiles on the grid and keeps RoadNetwork in sync.
public class RoadPlacer : MonoBehaviour
{
    public const int DefaultMaxBridgeSpan = 12;
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

    // While a whole route is being laid, visual refreshes are collected and run once
    // at the end - see PlaceRoute.
    private bool _deferVisualRefresh;
    private readonly HashSet<Cell> _deferredRefreshCells = new HashSet<Cell>();

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

    // The grid roads are currently being laid on. Route planning and previews need
    // it to walk neighbours, and it is the same grid PlaceRoad commits against.
    public GridSystem Grid => ActiveGridSystem;

    public RoadDefinition DefaultRoadDefinition => defaultRoadDefinition;

    // The cell under a world position on the island currently being played, or null
    // if that position is off-grid. Input handlers go through this so grid
    // resolution stays in one place.
    public Cell GetCellAtWorldPosition(Vector3 worldPosition)
    {
        GridSystem grid = ActiveGridSystem;
        return grid != null ? grid.GetCellAtWorldPosition(worldPosition) : null;
    }

    // Where a road tile on this cell physically sits. localCenter, not position:
    // position carries the array index, the cell occupies local [x, x+1).
    public Vector3 GetCellWorldCenter(Cell cell)
    {
        if (cell == null) return Vector3.zero;

        GridSystem grid = ActiveGridSystem;
        return grid != null ? grid.transform.TransformPoint(cell.localCenter) : cell.localCenter;
    }

    public bool CanPlaceRoad(Cell targetCell)
    {
        return CanPlaceRoad(targetCell, defaultRoadDefinition, out _);
    }

    public bool CanPlaceRoad(Cell targetCell, RoadDefinition definition)
    {
        return CanPlaceRoad(targetCell, definition, out _);
    }

    /// <summary>
    /// Whether this cell could become a road right now. <paramref name="bridgeAxisMask"/>
    /// comes back as the N|S or E|W axis a water cell's bridge span has to run along,
    /// and 0 for ordinary ground - route planning uses it to avoid proposing a
    /// bridge that crosses its own span sideways.
    /// </summary>
    public bool CanPlaceRoad(Cell targetCell, RoadDefinition definition, out int bridgeAxisMask)
    {
        bridgeAxisMask = 0;
        if (targetCell == null) return false;

        if (targetCell.isRoad || targetCell.isOccupied || targetCell.isBlocked)
        {
            return false;
        }

        if (BridgePlacementRules.IsBridgeTerrain(targetCell))
        {
            bool supportsBridges = definition == null || definition.SupportsBridges;
            int maxSpan = definition != null ? definition.MaxBridgeSpan : DefaultMaxBridgeSpan;
            return supportsBridges
                && BridgePlacementRules.TryGetBridgeAxis(ActiveGridSystem, targetCell, maxSpan, out bridgeAxisMask);
        }

        return BridgePlacementRules.IsRoadTerrain(targetCell);
    }

    public bool PlaceRoad(Cell targetCell)
    {
        return PlaceRoad(targetCell, defaultRoadDefinition);
    }

    public bool PlaceRoad(Cell targetCell, RoadDefinition definition)
    {
        if (!CanPlaceRoad(targetCell, definition, out _)) return false;

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
                // The whole tile object, not just the component - the art lives on a
                // child of it, so destroying the component alone leaves the road visible.
                Destroy(roadTileInstance.gameObject);
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

    public int PlaceRoute(IReadOnlyList<Cell> route)
    {
        return PlaceRoute(route, defaultRoadDefinition);
    }

    /// <summary>
    /// Lays a whole planned run in one go and returns how many tiles were actually
    /// built. Cells already carrying road are skipped, so a route that reuses an
    /// existing stretch only pays for the gaps.
    /// </summary>
    public int PlaceRoute(IReadOnlyList<Cell> route, RoadDefinition definition)
    {
        if (route == null || route.Count == 0) return 0;

        // Laying a tile changes how its neighbours join up, so refreshing per tile
        // would rebuild the same visuals several times as the run grows. Collect the
        // refreshes and run them once, against the finished route.
        _deferVisualRefresh = true;
        int placed = 0;
        try
        {
            foreach (Cell cell in route)
            {
                if (PlaceRoad(cell, definition)) placed++;
            }
        }
        finally
        {
            _deferVisualRefresh = false;
            FlushDeferredRefreshes();
        }

        return placed;
    }

    public int RemoveRoute(IReadOnlyList<Cell> route)
    {
        if (route == null || route.Count == 0) return 0;

        _deferVisualRefresh = true;
        int removed = 0;
        try
        {
            foreach (Cell cell in route)
            {
                if (RemoveRoad(cell)) removed++;
            }
        }
        finally
        {
            _deferVisualRefresh = false;
            FlushDeferredRefreshes();
        }

        return removed;
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

    private void FlushDeferredRefreshes()
    {
        if (_deferredRefreshCells.Count == 0) return;

        List<Cell> pending = new List<Cell>(_deferredRefreshCells);
        _deferredRefreshCells.Clear();

        foreach (Cell cell in pending)
        {
            RefreshLocalArea(cell);
        }
    }

    private void RefreshLocalArea(Cell changedCell)
    {
        if (changedCell == null) return;

        if (_deferVisualRefresh)
        {
            _deferredRefreshCells.Add(changedCell);
            return;
        }

        GridSystem grid = ActiveGridSystem;
        if (grid == null) return;

        // Resolved once for the whole refresh - creating a sampler costs a heightfield
        // lookup, and every tile in this area reads the same field.
        TerrainHeightSampler heightSampler = grid.CreateHeightSampler(changedCell.height);

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
                    RoadTopologyResolver.Result topology = RoadTopologyResolver.Resolve(grid, cell, roadTilePrefab, bridgeTilePrefab);
                    BridgeAppearance bridge = BridgeSpanResolver.Resolve(grid, cell, cell.roadDefinition);
                    visual.Apply(topology, bridge, new RoadSurfaceDrape(heightSampler, cell.localCenter));
                }
            }
        }
    }
}
