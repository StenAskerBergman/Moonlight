using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The per-island quay authority. It shares GridSystem coordinates, batches every quay
/// top/wall into one structural mesh, and derives the optional authored detail layer
/// from the exact same perimeter topology.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GridSystem))]
public sealed class QuaySystem : MonoBehaviour
{
    public enum EdgeDirection { North, East, South, West }
    public enum PerimeterShape { Straight, OuterCorner, InnerCorner, End, LandTransition }

    public readonly struct PerimeterSegment
    {
        public PerimeterSegment(Vector2Int cell, EdgeDirection direction, PerimeterShape shape, float bottom)
        {
            Cell = cell;
            Direction = direction;
            Shape = shape;
            BottomElevation = bottom;
        }

        public Vector2Int Cell { get; }
        public EdgeDirection Direction { get; }
        public PerimeterShape Shape { get; }
        public float BottomElevation { get; }
    }

    private sealed class QuayCell
    {
        public bool Manual;
        public readonly HashSet<int> AutomaticOwners = new HashSet<int>();
        public bool IsOccupied => Manual || AutomaticOwners.Count > 0;
    }

    [Header("Structure")]
    [SerializeField, Min(0f)] private float deckClearanceAboveWater = 0.15f;
    [SerializeField, Min(0f)] private float wallGapAllowance = 0.35f;
    [SerializeField, Min(0f)] private float lowerTerrainThreshold = 0.2f;
    [SerializeField] private Material topMaterial;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private bool generateCollider = true;

    [Header("Perimeter detail prefabs")]
    [SerializeField] private GameObject straightDetailPrefab;
    [SerializeField] private GameObject outerCornerDetailPrefab;
    [SerializeField] private GameObject innerCornerDetailPrefab;
    [SerializeField] private GameObject endDetailPrefab;
    [SerializeField] private GameObject landTransitionDetailPrefab;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private readonly Dictionary<Vector2Int, QuayCell> cells = new Dictionary<Vector2Int, QuayCell>();
    private readonly List<PerimeterSegment> perimeter = new List<PerimeterSegment>();
    private GridSystem gridSystem;
    private MapGrid mapGrid;
    private Mesh structureMesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private Transform detailRoot;
    private bool dirty;

    public IReadOnlyList<PerimeterSegment> Perimeter => perimeter;

    public float TopElevationLocal
    {
        get
        {
            ResolveDependencies();
            TerrainGenerationSettings settings = mapGrid != null ? mapGrid.generationSettings : null;
            if (settings == null) return deckClearanceAboveWater;

            // Beach is the project's authored land/water transition. Matching it keeps
            // the deck cleanly joined to shore while remaining above the visible water.
            return Mathf.Max(settings.AuthoritativeWaterSurfaceHeight + deckClearanceAboveWater,
                             settings.beachHeight);
        }
    }

    public float TopElevationWorld => transform.TransformPoint(new Vector3(0f, TopElevationLocal, 0f)).y;

    public static QuaySystem GetOrCreate(GridSystem grid)
    {
        if (grid == null) return null;
        QuaySystem quay = grid.GetComponent<QuaySystem>();
        return quay != null ? quay : grid.gameObject.AddComponent<QuaySystem>();
    }

    public static bool IsLegalQuayTerrain(Cell.TerrainType terrain)
    {
        switch (terrain)
        {
            case Cell.TerrainType.Beach:
            case Cell.TerrainType.Coast:
            case Cell.TerrainType.Shore:
            case Cell.TerrainType.Shallow:
            case Cell.TerrainType.Water:
            case Cell.TerrainType.Sea:
            case Cell.TerrainType.Ocean:
                return true;
            default:
                return false;
        }
    }

    /// <summary>Manual ornaments and future quay tools use this same occupancy path.</summary>
    public bool PlaceQuay(Vector2Int coordinate)
    {
        if (!TryGetGridCell(coordinate, out Cell cell) || !IsLegalQuayTerrain(cell.currentTerrainType)) return false;
        QuayCell quayCell = GetOrAddCell(coordinate);
        if (quayCell.Manual) return true;
        quayCell.Manual = true;
        MarkDirty();
        return true;
    }

    public bool PlaceQuay(Cell cell)
    {
        return cell != null && PlaceQuay(new Vector2Int(cell.cellPosition.x, cell.cellPosition.z));
    }

    public bool RemoveQuay(Vector2Int coordinate)
    {
        if (!cells.TryGetValue(coordinate, out QuayCell quayCell) || !quayCell.Manual) return false;
        quayCell.Manual = false;
        RemoveIfUnowned(coordinate, quayCell);
        MarkDirty();
        return true;
    }

    public bool RemoveQuay(Cell cell)
    {
        return cell != null && RemoveQuay(new Vector2Int(cell.cellPosition.x, cell.cellPosition.z));
    }

    /// <summary>
    /// Reserves the dock platform a harbor building stands on: its own footprint plus
    /// <paramref name="padding"/> cells of open deck on every side.
    ///
    /// The platform is deliberately larger than the building. A quay whose outline is the
    /// building's outline reads as a slab underneath the model - the retaining wall lands
    /// straight against the building's own wall, and there is nowhere to walk, moor, or
    /// put harbor ornaments. The margin is what makes it read as a dock.
    ///
    /// Cells are owned rather than merely flagged, so two harbors sharing deck keep the
    /// shared cells until both are gone, and the perimeter pass in Rebuild sees one merged
    /// region - no retaining wall is generated between connected cells.
    /// </summary>
    public void RegisterAutomaticFoundation(Building owner, Vector3Int origin, Vector2Int footprint, int padding)
    {
        if (owner == null) return;

        var ownedCoordinates = new List<Vector2Int>();
        CollectFoundationCells(origin, footprint, padding, ownedCoordinates);

        int ownerId = owner.GetInstanceID();
        for (int i = 0; i < ownedCoordinates.Count; i++)
        {
            GetOrAddCell(ownedCoordinates[i]).AutomaticOwners.Add(ownerId);
        }

        QuayFoundationOwner lifetime = owner.GetComponent<QuayFoundationOwner>();
        if (lifetime == null)
        {
            lifetime = owner.gameObject.AddComponent<QuayFoundationOwner>();
        }
        lifetime.Configure(this, ownerId, ownedCoordinates);
        MarkDirty();
    }

    /// <summary>
    /// The cells a foundation of this shape would claim, without claiming them.
    ///
    /// The blueprint preview draws exactly this, so what the player is shown before the
    /// click and what the quay builds after it come from one rule rather than two.
    /// </summary>
    public void CollectFoundationCells(Vector3Int origin, Vector2Int footprint, int padding, List<Vector2Int> results)
    {
        if (results == null) return;
        results.Clear();

        padding = Mathf.Max(0, padding);
        int sizeX = Mathf.Max(1, footprint.x) + padding * 2;
        int sizeZ = Mathf.Max(1, footprint.y) + padding * 2;
        int originX = origin.x - padding;
        int originZ = origin.z - padding;

        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                Vector2Int coordinate = new Vector2Int(originX + x, originZ + z);
                Cell cell;
                if (!TryGetGridCell(coordinate, out cell)) continue;

                bool isBuildingCell = x >= padding && x < sizeX - padding
                                   && z >= padding && z < sizeZ - padding;

                // The building's own cells always become deck - it has to stand on
                // something. The margin only forms where a quay may legally stand, so the
                // dock reaches out to sea and along the shore rather than paving the hill
                // behind it.
                if (!isBuildingCell && !IsLegalQuayTerrain(cell.currentTerrainType)) continue;

                results.Add(coordinate);
            }
        }
    }

    /// <summary>The deck elevation a wall under this cell has to reach down to.</summary>
    public float GetWallBottom(Vector2Int cell, Vector2Int neighbor)
    {
        return CalculateWallBottom(cell, neighbor);
    }

    /// <summary>Whether this cell's neighbour is shore the deck should blend into rather than wall off.</summary>
    public bool IsLandConnection(Vector2Int coordinate)
    {
        return IsSuitableLandConnection(coordinate);
    }

    internal void ReleaseAutomaticFoundation(int ownerId, IReadOnlyList<Vector2Int> ownedCoordinates)
    {
        if (ownedCoordinates == null) return;
        for (int i = 0; i < ownedCoordinates.Count; i++)
        {
            Vector2Int coordinate = ownedCoordinates[i];
            if (!cells.TryGetValue(coordinate, out QuayCell quayCell)) continue;
            quayCell.AutomaticOwners.Remove(ownerId);
            RemoveIfUnowned(coordinate, quayCell);
        }
        MarkDirty();
    }

    private void Awake()
    {
        ResolveDependencies();
        EnsureRenderObjects();
    }

    private void LateUpdate()
    {
        if (!dirty) return;
        Rebuild();
    }

    private void ResolveDependencies()
    {
        if (gridSystem == null) gridSystem = GetComponent<GridSystem>();
        if (mapGrid == null) mapGrid = GetComponent<MapGrid>();
    }

    private QuayCell GetOrAddCell(Vector2Int coordinate)
    {
        if (!cells.TryGetValue(coordinate, out QuayCell cell))
        {
            cell = new QuayCell();
            cells.Add(coordinate, cell);
        }
        return cell;
    }

    private void RemoveIfUnowned(Vector2Int coordinate, QuayCell cell)
    {
        if (!cell.IsOccupied) cells.Remove(coordinate);
    }

    private bool TryGetGridCell(Vector2Int coordinate, out Cell cell)
    {
        ResolveDependencies();
        cell = gridSystem != null ? gridSystem.GetCell(coordinate.x, coordinate.y) : null;
        return cell != null;
    }

    public bool HasQuay(Vector2Int coordinate)
    {
        return cells.TryGetValue(coordinate, out QuayCell cell) && cell.IsOccupied;
    }

    private void MarkDirty()
    {
        dirty = true;
        if (!Application.isPlaying) Rebuild();
    }

    private void EnsureRenderObjects()
    {
        Transform structureTransform = transform.Find("Generated Quay Structure");
        GameObject structure = structureTransform != null ? structureTransform.gameObject : null;
        if (structure == null)
        {
            structure = new GameObject("Generated Quay Structure");
            structure.transform.SetParent(transform, false);
        }

        meshFilter = structure.GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = structure.AddComponent<MeshFilter>();

        meshRenderer = structure.GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = structure.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.On;
        meshRenderer.receiveShadows = true;

        if (generateCollider)
        {
            meshCollider = structure.GetComponent<MeshCollider>();
            if (meshCollider == null) meshCollider = structure.AddComponent<MeshCollider>();
        }

        Transform existingDetails = transform.Find("Generated Quay Details");
        if (existingDetails == null)
        {
            GameObject details = new GameObject("Generated Quay Details");
            details.transform.SetParent(transform, false);
            detailRoot = details.transform;
        }
        else detailRoot = existingDetails;
    }

    private void Rebuild()
    {
        dirty = false;
        ResolveDependencies();
        EnsureRenderObjects();

        var vertices = new List<Vector3>(cells.Count * 20);
        var uvs = new List<Vector2>(cells.Count * 20);
        var topTriangles = new List<int>(cells.Count * 6);
        var wallTriangles = new List<int>(cells.Count * 24);
        perimeter.Clear();

        foreach (KeyValuePair<Vector2Int, QuayCell> entry in cells)
        {
            if (!entry.Value.IsOccupied) continue;
            AddTop(entry.Key, vertices, uvs, topTriangles);

            for (int directionIndex = 0; directionIndex < 4; directionIndex++)
            {
                EdgeDirection direction = (EdgeDirection)directionIndex;
                Vector2Int outward = DirectionVector(direction);
                Vector2Int neighborCoordinate = entry.Key + outward;
                if (HasQuay(neighborCoordinate)) continue;

                bool landTransition = IsSuitableLandConnection(neighborCoordinate);
                float bottom = CalculateWallBottom(entry.Key, neighborCoordinate);
                PerimeterShape shape = landTransition
                    ? PerimeterShape.LandTransition
                    : ClassifyPerimeter(entry.Key, direction);
                perimeter.Add(new PerimeterSegment(entry.Key, direction, shape, bottom));

                if (!landTransition)
                {
                    AddWall(entry.Key, direction, bottom, vertices, uvs, wallTriangles);
                }
            }
        }

        if (structureMesh != null) DestroyGeneratedObject(structureMesh);
        structureMesh = new Mesh { name = $"{name} Quay Structure" };
        if (vertices.Count > 65535) structureMesh.indexFormat = IndexFormat.UInt32;
        structureMesh.SetVertices(vertices);
        structureMesh.SetUVs(0, uvs);
        structureMesh.subMeshCount = 2;
        structureMesh.SetTriangles(topTriangles, 0, false);
        structureMesh.SetTriangles(wallTriangles, 1, false);
        structureMesh.RecalculateNormals();
        structureMesh.RecalculateBounds();
        meshFilter.sharedMesh = structureMesh;

        Material resolvedTop = topMaterial != null ? topMaterial : (mapGrid != null ? mapGrid.terrainMaterial : null);
        Material resolvedWall = wallMaterial != null ? wallMaterial : (mapGrid != null ? mapGrid.edgeMaterial : resolvedTop);
        meshRenderer.sharedMaterials = new[] { resolvedTop, resolvedWall };

        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = structureMesh;
        }

        RebuildDetails();

        // The deck is buildable ground at an elevation the terrain does not have, so the
        // construction grid has to be told about it.
        if (gridSystem != null) gridSystem.RefreshBuildGridOverlay();
    }

    private void AddTop(Vector2Int coordinate, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
    {
        float cellSize = gridSystem != null ? gridSystem.cellSize : 1f;
        float x0 = coordinate.x * cellSize;
        float z0 = coordinate.y * cellSize;
        float x1 = x0 + cellSize;
        float z1 = z0 + cellSize;
        float y = TopElevationLocal;
        int start = vertices.Count;
        vertices.Add(new Vector3(x0, y, z0));
        vertices.Add(new Vector3(x0, y, z1));
        vertices.Add(new Vector3(x1, y, z1));
        vertices.Add(new Vector3(x1, y, z0));
        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(0f, 1f));
        uvs.Add(new Vector2(1f, 1f));
        uvs.Add(new Vector2(1f, 0f));
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }

    private void AddWall(Vector2Int coordinate, EdgeDirection direction, float bottom,
                         List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
    {
        float cellSize = gridSystem != null ? gridSystem.cellSize : 1f;
        float x0 = coordinate.x * cellSize;
        float z0 = coordinate.y * cellSize;
        float x1 = x0 + cellSize;
        float z1 = z0 + cellSize;
        float top = TopElevationLocal;
        Vector3 a;
        Vector3 b;
        switch (direction)
        {
            case EdgeDirection.North: a = new Vector3(x1, top, z1); b = new Vector3(x0, top, z1); break;
            case EdgeDirection.East:  a = new Vector3(x1, top, z0); b = new Vector3(x1, top, z1); break;
            case EdgeDirection.South: a = new Vector3(x0, top, z0); b = new Vector3(x1, top, z0); break;
            default:                  a = new Vector3(x0, top, z1); b = new Vector3(x0, top, z0); break;
        }

        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(new Vector3(b.x, bottom, b.z));
        vertices.Add(new Vector3(a.x, bottom, a.z));
        float depth = Mathf.Max(0.01f, top - bottom);
        uvs.Add(new Vector2(0f, depth));
        uvs.Add(new Vector2(1f, depth));
        uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(0f, 0f));
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }

    private bool IsSuitableLandConnection(Vector2Int coordinate)
    {
        if (!TryGetGridCell(coordinate, out Cell cell)) return false;
        return !cell.IsUnderwater && cell.height >= TopElevationLocal - lowerTerrainThreshold;
    }

    private float CalculateWallBottom(Vector2Int cellCoordinate, Vector2Int neighborCoordinate)
    {
        float bottom = TopElevationLocal - 1f;
        if (TryGetGridCell(cellCoordinate, out Cell current)) bottom = Mathf.Min(bottom, current.height);
        if (TryGetGridCell(neighborCoordinate, out Cell neighbor)) bottom = Mathf.Min(bottom, neighbor.height);
        return bottom - wallGapAllowance;
    }

    private PerimeterShape ClassifyPerimeter(Vector2Int coordinate, EdgeDirection direction)
    {
        Vector2Int outward = DirectionVector(direction);
        Vector2Int left = new Vector2Int(-outward.y, outward.x);
        Vector2Int right = -left;
        bool leftQuay = HasQuay(coordinate + left);
        bool rightQuay = HasQuay(coordinate + right);
        bool leftOutsideQuay = HasQuay(coordinate + outward + left);
        bool rightOutsideQuay = HasQuay(coordinate + outward + right);

        // A diagonal outside cell closes a concave notch at this endpoint. Otherwise
        // an adjacent quay continues the same perimeter run only while its outside is free.
        if ((leftQuay && leftOutsideQuay) || (rightQuay && rightOutsideQuay))
            return PerimeterShape.InnerCorner;

        bool continuesLeft = leftQuay && !leftOutsideQuay;
        bool continuesRight = rightQuay && !rightOutsideQuay;
        if (continuesLeft && continuesRight) return PerimeterShape.Straight;
        if (!continuesLeft && !continuesRight) return PerimeterShape.End;
        return PerimeterShape.OuterCorner;
    }

    private void RebuildDetails()
    {
        if (detailRoot == null) return;
        for (int i = detailRoot.childCount - 1; i >= 0; i--) DestroyGeneratedObject(detailRoot.GetChild(i).gameObject);

        float cellSize = gridSystem != null ? gridSystem.cellSize : 1f;
        for (int i = 0; i < perimeter.Count; i++)
        {
            PerimeterSegment segment = perimeter[i];
            GameObject prefab = GetDetailPrefab(segment.Shape);
            if (prefab == null) continue;

            Vector2Int outward = DirectionVector(segment.Direction);
            Vector3 localPosition = new Vector3(
                (segment.Cell.x + 0.5f + outward.x * 0.5f) * cellSize,
                TopElevationLocal,
                (segment.Cell.y + 0.5f + outward.y * 0.5f) * cellSize);
            Quaternion localRotation = Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.y), Vector3.up);
            GameObject detail = Instantiate(prefab, detailRoot);
            detail.name = $"{segment.Shape} {segment.Cell.x},{segment.Cell.y} {segment.Direction}";
            detail.transform.localPosition = localPosition;
            detail.transform.localRotation = localRotation;
        }
    }

    private GameObject GetDetailPrefab(PerimeterShape shape)
    {
        switch (shape)
        {
            case PerimeterShape.OuterCorner: return outerCornerDetailPrefab;
            case PerimeterShape.InnerCorner: return innerCornerDetailPrefab;
            case PerimeterShape.End: return endDetailPrefab;
            case PerimeterShape.LandTransition: return landTransitionDetailPrefab;
            default: return straightDetailPrefab;
        }
    }

    private static Vector2Int DirectionVector(EdgeDirection direction)
    {
        switch (direction)
        {
            case EdgeDirection.North: return Vector2Int.up;
            case EdgeDirection.East: return Vector2Int.right;
            case EdgeDirection.South: return Vector2Int.down;
            default: return Vector2Int.left;
        }
    }

    private static void DestroyGeneratedObject(UnityEngine.Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }

    private void OnDestroy()
    {
        if (structureMesh != null) DestroyGeneratedObject(structureMesh);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        ResolveDependencies();
        float cellSize = gridSystem != null ? gridSystem.cellSize : 1f;
        foreach (KeyValuePair<Vector2Int, QuayCell> entry in cells)
        {
            Gizmos.color = entry.Value.Manual
                ? new Color(0.2f, 0.9f, 1f, 0.6f)
                : new Color(1f, 0.65f, 0.1f, 0.6f);
            Vector3 center = transform.TransformPoint(new Vector3(
                (entry.Key.x + 0.5f) * cellSize, TopElevationLocal + 0.05f,
                (entry.Key.y + 0.5f) * cellSize));
            Gizmos.DrawWireCube(center, new Vector3(cellSize * 0.9f, 0.05f, cellSize * 0.9f));
        }

        Gizmos.color = Color.magenta;
        for (int i = 0; i < perimeter.Count; i++)
        {
            PerimeterSegment segment = perimeter[i];
            Vector2Int outward = DirectionVector(segment.Direction);
            Vector3 center = transform.TransformPoint(new Vector3(
                (segment.Cell.x + 0.5f + outward.x * 0.5f) * cellSize,
                TopElevationLocal + 0.12f,
                (segment.Cell.y + 0.5f + outward.y * 0.5f) * cellSize));
            Gizmos.DrawRay(center, transform.TransformDirection(new Vector3(outward.x, 0f, outward.y)) * 0.35f);
        }
    }
}
