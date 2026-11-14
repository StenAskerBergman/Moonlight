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

    /// <summary>
    /// Where each piece of the quay lives in the shared atlas, in UV space (0-1, origin
    /// bottom-left). Defaults are read off the Anno reference sheet and are meant to be
    /// nudged in the Inspector against the real texture - the geometry does not care what
    /// the numbers are, it just maps each surface to the rect it is given.
    /// </summary>
    [System.Serializable]
    public sealed class AtlasLayout
    {
        [Tooltip("Deck plating - the grey slab with the worn gold border.")]
        public Rect deck = new Rect(0.792f, 0.731f, 0.201f, 0.116f);

        [Tooltip("Capping course along the very top of the wall.")]
        public Rect coping = new Rect(0.006f, 0.936f, 0.746f, 0.058f);

        [Tooltip("Banded block course, used under the coping.")]
        public Rect band = new Rect(0.006f, 0.865f, 0.746f, 0.064f);

        [Tooltip("One bay of the main wall - the panel between two buttresses.")]
        public Rect wallBay = new Rect(0.150f, 0.285f, 0.145f, 0.430f);

        [Tooltip("A single free-standing buttress column, flared at the foot.")]
        public Rect buttress = new Rect(0.744f, 0.285f, 0.124f, 0.430f);

        [Tooltip("Rock face the structure is founded on.")]
        public Rect rock = new Rect(0.006f, 0.000f, 0.746f, 0.277f);
    }

    [Header("Structure")]
    [Tooltip("Freeboard: how far the deck stands above the visible waterline. The " +
             "reference structure is a platform you look UP at from a boat, not a raft.")]
    [SerializeField, Min(0f)] private float deckClearanceAboveWater = 1.9f;
    [SerializeField, Min(0f)] private float wallGapAllowance = 0.35f;
    [SerializeField, Min(0f)] private float lowerTerrainThreshold = 0.2f;
    [SerializeField] private Material topMaterial;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private bool generateCollider = true;

    [Header("Wall profile")]
    [Tooltip("Thickness of the capping course that finishes the top of the wall.")]
    [SerializeField, Min(0f)] private float copingHeight = 0.12f;
    [Tooltip("How far the capping course oversails the wall face beneath it.")]
    [SerializeField, Min(0f)] private float copingOverhang = 0.05f;
    [Tooltip("How far the wall leans out per unit of depth. 0 is a vertical slab; the " +
             "reference structure widens noticeably towards the seabed.")]
    [SerializeField, Range(0f, 0.5f)] private float wallBatter = 0.08f;
    [Tooltip("How far the wall bays sit BEHIND the rib line. This is what makes the ribs " +
             "read as structure and the bays as recessed panels, rather than a flat face " +
             "with strips glued on. It also keeps the quay inside its own grid cells.")]
    [SerializeField, Min(0f)] private float wallRecess = 0.22f;

    [Tooltip("Depth of dressed wall below the coping before the rock footing takes over.")]
    [SerializeField, Min(0.1f)] private float dressedWallDepth = 2.5f;
    [Tooltip("Height of one rock course. The footing repeats it down to the seabed rather " +
             "than stretching one copy over the whole drop.")]
    [SerializeField, Min(0.25f)] private float rockCourseHeight = 2f;

    [Header("Buttresses")]
    [SerializeField] private bool generateButtresses = true;
    [SerializeField, Min(0.05f)] private float buttressWidth = 0.30f;
    [SerializeField, Min(0.02f)] private float buttressProjection = 0.14f;
    [Tooltip("Extra projection at the foot, which is what gives the splayed silhouette.")]
    [SerializeField, Min(0f)] private float buttressFlare = 0.16f;
    [Tooltip("Cells between buttresses along a straight run. Corners always get one.")]
    [SerializeField, Min(1)] private int buttressSpacing = 2;

    [SerializeField] private AtlasLayout atlas = new AtlasLayout();

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

            // Freeboard is measured from the water the player can actually see. The
            // generation setting and the rendered ocean disagree in this project - the
            // setting says -3 while Ocean Cube sits at -0.5 - and building on the setting
            // put the deck barely a finger above the waterline with the whole structure
            // submerged.
            float waterSurface = ResolveVisibleWaterSurface(settings.AuthoritativeWaterSurfaceHeight);

            // Never below the shoreline, so a quay meeting the beach still meets it cleanly.
            return Mathf.Max(waterSurface + deckClearanceAboveWater, settings.beachHeight);
        }
    }

    public float TopElevationWorld => transform.TransformPoint(new Vector3(0f, TopElevationLocal, 0f)).y;

    private bool waterSurfaceResolved;
    private float cachedWaterSurfaceLocal;

    /// <summary>
    /// The top of the ocean the player is looking at, in this island's local space.
    ///
    /// Resolved from the scene's Water-layer geometry rather than from generation
    /// settings, because the two do not agree here. Falls back to the supplied setting
    /// when there is no ocean in the scene - an editor-only island, say.
    /// </summary>
    private float ResolveVisibleWaterSurface(float fallback)
    {
        if (waterSurfaceResolved) return cachedWaterSurfaceLocal;

        int waterLayer = LayerMask.NameToLayer("Water");
        float highest = float.NegativeInfinity;

        if (waterLayer >= 0)
        {
            foreach (Renderer renderer in FindObjectsOfType<Renderer>())
            {
                if (renderer.gameObject.layer != waterLayer) continue;
                highest = Mathf.Max(highest, renderer.bounds.max.y);
            }
        }

        cachedWaterSurfaceLocal = float.IsNegativeInfinity(highest)
            ? fallback
            : transform.InverseTransformPoint(new Vector3(0f, highest, 0f)).y;

        waterSurfaceResolved = true;
        return cachedWaterSurfaceLocal;
    }

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
        // Deliberately does NOT build the render objects. Every island carries a
        // QuaySystem so its atlas and wall profile can be authored on the prefab, and most
        // of them never grow a quay - Rebuild creates the objects when there is finally
        // something to draw.
        ResolveDependencies();
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

        // Buttresses come after the walls so they can be decided from the finished
        // perimeter - a corner is only known once every edge has been classified.
        if (generateButtresses)
        {
            for (int i = 0; i < perimeter.Count; i++)
            {
                PerimeterSegment segment = perimeter[i];
                if (segment.Shape == PerimeterShape.LandTransition) continue;
                if (!WantsButtress(segment)) continue;

                AddButtress(segment, vertices, uvs, wallTriangles);
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

        // One plate per cell, so the deck reads as laid slabs rather than one stretched image.
        AddQuad(vertices, uvs, triangles,
            new Vector3(x0, y, z0), new Vector3(x0, y, z1),
            new Vector3(x1, y, z1), new Vector3(x1, y, z0),
            atlas.deck);
    }

    /// <summary>
    /// One bay of retaining wall, built as the reference structure is: a capping course
    /// that oversails the face, a battered run of dressed wall below it, then a rock
    /// footing carried down to the seabed. A single flat quad read as a plain slab.
    /// </summary>
    private void AddWall(Vector2Int coordinate, EdgeDirection direction, float bottom,
                         List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
    {
        float top = TopElevationLocal;
        Vector3 edgeA, edgeB;
        GetEdge(coordinate, direction, top, out edgeA, out edgeB);
        Vector3 outward = OutwardVector(direction);

        // The ribs stand on the cell boundaries at either end of this edge, so the bay is
        // only the span between them. Drawing it full width put a full-width panel behind
        // every rib, and the eye read one continuous wall with strips laid on it instead
        // of piers with panels recessed between them.
        Vector3 alongEdge = (edgeB - edgeA).normalized;
        float inset = generateButtresses ? buttressWidth * 0.5f : 0f;
        Vector3 a = edgeA + alongEdge * inset;
        Vector3 b = edgeB - alongEdge * inset;

        // --- capping course -------------------------------------------------------
        float copingBase = top - copingHeight;

        // The cap runs the full edge - it is a continuous course over ribs and bays alike.
        Vector3 capLipA = edgeA + outward * copingOverhang;
        Vector3 capLipB = edgeB + outward * copingOverhang;

        // Its top surface, filling the sliver between the deck edge and the oversail.
        AddQuad(vertices, uvs, triangles,
            new Vector3(edgeA.x, top, edgeA.z), new Vector3(capLipA.x, top, capLipA.z),
            new Vector3(capLipB.x, top, capLipB.z), new Vector3(edgeB.x, top, edgeB.z),
            atlas.coping);

        // Its outward face.
        AddQuad(vertices, uvs, triangles,
            new Vector3(capLipA.x, top, capLipA.z), new Vector3(capLipB.x, top, capLipB.z),
            new Vector3(capLipB.x, copingBase, capLipB.z), new Vector3(capLipA.x, copingBase, capLipA.z),
            atlas.coping);

        // --- dressed wall, leaning outward as it descends -------------------------
        float dressedBase = Mathf.Max(bottom, copingBase - dressedWallDepth);
        float dressedDrop = copingBase - dressedBase;
        Vector3 dressedOut = outward * (dressedDrop * wallBatter);

        Vector3 wallBottomA = new Vector3(a.x + dressedOut.x, dressedBase, a.z + dressedOut.z);
        Vector3 wallBottomB = new Vector3(b.x + dressedOut.x, dressedBase, b.z + dressedOut.z);

        AddQuad(vertices, uvs, triangles,
            new Vector3(a.x, copingBase, a.z), new Vector3(b.x, copingBase, b.z),
            wallBottomB, wallBottomA,
            atlas.wallBay);

        // --- rock footing, repeated by course rather than stretched ---------------
        if (dressedBase - bottom > 0.01f)
        {
            int courses = Mathf.Max(1, Mathf.CeilToInt((dressedBase - bottom) / rockCourseHeight));
            float step = (dressedBase - bottom) / courses;

            Vector3 courseTopA = wallBottomA;
            Vector3 courseTopB = wallBottomB;

            for (int i = 0; i < courses; i++)
            {
                float courseBottomY = dressedBase - step * (i + 1);
                Vector3 courseOut = outward * (step * wallBatter);

                Vector3 courseBottomA = new Vector3(courseTopA.x + courseOut.x, courseBottomY, courseTopA.z + courseOut.z);
                Vector3 courseBottomB = new Vector3(courseTopB.x + courseOut.x, courseBottomY, courseTopB.z + courseOut.z);

                AddQuad(vertices, uvs, triangles,
                    courseTopA, courseTopB, courseBottomB, courseBottomA,
                    atlas.rock);

                courseTopA = courseBottomA;
                courseTopB = courseBottomB;
            }
        }
    }

    /// <summary>
    /// Whether this stretch of perimeter carries a buttress. Corners and run ends always
    /// do - they are what reads as structure from a distance - and straight runs take one
    /// every <see cref="buttressSpacing"/> cells so the spacing stays even along a wall.
    /// </summary>
    private bool WantsButtress(PerimeterSegment segment)
    {
        if (segment.Shape == PerimeterShape.OuterCorner || segment.Shape == PerimeterShape.End) return true;
        if (segment.Shape != PerimeterShape.Straight) return false;

        // Stepping along the run's own axis keeps the rhythm continuous around the
        // structure instead of restarting on every side.
        int along = (segment.Direction == EdgeDirection.North || segment.Direction == EdgeDirection.South)
            ? segment.Cell.x
            : segment.Cell.y;

        return Mathf.Abs(along) % Mathf.Max(1, buttressSpacing) == 0;
    }

    /// <summary>
    /// A pier standing proud of the wall, carried the whole way from the capping course
    /// down to the seabed - dressed stone across the structure's face, then continuing as
    /// a rock pier through the footing, which is how the reference reads.
    ///
    /// It follows the wall's own batter, so the column stays welded to the face instead of
    /// drifting away from it as both lean out. Three faces per section; the inward one is
    /// omitted because the wall is always behind it.
    /// </summary>
    private void AddButtress(PerimeterSegment segment, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
    {
        float top = TopElevationLocal;
        Vector3 a, b;
        GetEdge(segment.Cell, segment.Direction, top, out a, out b);

        Vector3 outward = OutwardVector(segment.Direction);
        Vector3 along = (b - a).normalized;

        // On the boundary at the start of this edge rather than mid-bay. Neighbouring
        // segments share that boundary and each only claims its own start, so a run gets
        // one rib per cell line with a recessed panel between each pair.
        Vector3 centre = a;
        float half = buttressWidth * 0.5f;

        float copingBase = top - copingHeight;
        float dressedBase = Mathf.Max(segment.BottomElevation, copingBase - dressedWallDepth);
        if (top - dressedBase <= 0.01f) return;

        // Dressed head: the part above the footing, and the only part usually in view.
        AddColumnSection(vertices, uvs, triangles, centre, along, outward, half,
                         top, dressedBase, atlas.buttress);

        // Cap, sitting on the head.
        Vector3 capBack = outward * WallFaceOffset(top);
        Vector3 capFront = outward * ButtressFaceOffset(top);
        AddQuad(vertices, uvs, triangles,
            centre + along * half + capBack, centre - along * half + capBack,
            centre - along * half + capFront, centre + along * half + capFront,
            atlas.coping);

        // Rock pier, repeated by course so a deep drop is not one smeared copy.
        float footing = dressedBase - segment.BottomElevation;
        if (footing <= 0.01f) return;

        int courses = Mathf.Max(1, Mathf.CeilToInt(footing / rockCourseHeight));
        float step = footing / courses;
        for (int i = 0; i < courses; i++)
        {
            AddColumnSection(vertices, uvs, triangles, centre, along, outward, half,
                             dressedBase - step * i, dressedBase - step * (i + 1), atlas.rock);
        }
    }

    /// <summary>
    /// One vertical slice of a buttress between two heights: outward face plus both flanks.
    /// </summary>
    private void AddColumnSection(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles,
                                  Vector3 centre, Vector3 along, Vector3 outward, float half,
                                  float topY, float bottomY, Rect uvRect)
    {
        Vector3 sideL = along * -half;
        Vector3 sideR = along * half;

        Vector3 topBack = outward * WallFaceOffset(topY);
        Vector3 topFront = outward * ButtressFaceOffset(topY);
        Vector3 bottomBack = outward * WallFaceOffset(bottomY);
        Vector3 bottomFront = outward * ButtressFaceOffset(bottomY);

        Vector3 headL = new Vector3(centre.x, topY, centre.z) + sideL + topFront;
        Vector3 headR = new Vector3(centre.x, topY, centre.z) + sideR + topFront;
        Vector3 footL = new Vector3(centre.x, bottomY, centre.z) + sideL + bottomFront;
        Vector3 footR = new Vector3(centre.x, bottomY, centre.z) + sideR + bottomFront;

        Vector3 backL = new Vector3(centre.x, topY, centre.z) + sideL + topBack;
        Vector3 backR = new Vector3(centre.x, topY, centre.z) + sideR + topBack;
        Vector3 backFootL = new Vector3(centre.x, bottomY, centre.z) + sideL + bottomBack;
        Vector3 backFootR = new Vector3(centre.x, bottomY, centre.z) + sideR + bottomBack;

        AddQuad(vertices, uvs, triangles, headR, headL, footL, footR, uvRect);          // outward
        AddQuad(vertices, uvs, triangles, headL, backL, backFootL, footL, uvRect);      // left flank
        AddQuad(vertices, uvs, triangles, backR, headR, footR, backFootR, uvRect);      // right flank
    }

    /// <summary>
    /// How far the wall face has leaned out by this height. The buttress rides on this so
    /// the two lean together.
    /// </summary>
    private float WallFaceOffset(float y)
    {
        float below = Mathf.Max(0f, (TopElevationLocal - copingHeight) - y);
        return below * wallBatter - wallRecess;
    }

    /// <summary>
    /// Where the front of a buttress sits at this height: clear of the wall by its
    /// projection, plus a flare that reaches full width at the base of the dressed wall
    /// and holds there. Tying the flare to the dressed depth rather than to total drop
    /// keeps a column in deep water from splaying into a spike.
    /// </summary>
    private float ButtressFaceOffset(float y)
    {
        float below = Mathf.Max(0f, (TopElevationLocal - copingHeight) - y);
        float flare = dressedWallDepth > 0.01f
            ? buttressFlare * Mathf.Clamp01(below / dressedWallDepth)
            : buttressFlare;

        // Measured from the cell boundary, not from the recessed wall - a rib at zero
        // projection sits flush with its cell, so the structure never oversails the grid.
        return below * wallBatter + buttressProjection + flare;
    }

    /// <summary>The top edge of one cell side, wound so its outward face is front-facing.</summary>
    private void GetEdge(Vector2Int coordinate, EdgeDirection direction, float y, out Vector3 a, out Vector3 b)
    {
        float cellSize = gridSystem != null ? gridSystem.cellSize : 1f;
        float x0 = coordinate.x * cellSize;
        float z0 = coordinate.y * cellSize;
        float x1 = x0 + cellSize;
        float z1 = z0 + cellSize;

        switch (direction)
        {
            case EdgeDirection.North: a = new Vector3(x1, y, z1); b = new Vector3(x0, y, z1); break;
            case EdgeDirection.East:  a = new Vector3(x1, y, z0); b = new Vector3(x1, y, z1); break;
            case EdgeDirection.South: a = new Vector3(x0, y, z0); b = new Vector3(x1, y, z0); break;
            default:                  a = new Vector3(x0, y, z1); b = new Vector3(x0, y, z0); break;
        }
    }

    private static Vector3 OutwardVector(EdgeDirection direction)
    {
        Vector2Int outward = DirectionVector(direction);
        return new Vector3(outward.x, 0f, outward.y);
    }

    /// <summary>
    /// Adds one quad wound v0-v1-v2-v3, with the given atlas rect stretched across it.
    /// Every surface in the structure goes through here, so the atlas is the only thing
    /// that decides what a face looks like.
    /// </summary>
    private static void AddQuad(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles,
                                Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Rect uvRect)
    {
        int start = vertices.Count;
        vertices.Add(v0); vertices.Add(v1); vertices.Add(v2); vertices.Add(v3);

        uvs.Add(new Vector2(uvRect.xMin, uvRect.yMax));
        uvs.Add(new Vector2(uvRect.xMax, uvRect.yMax));
        uvs.Add(new Vector2(uvRect.xMax, uvRect.yMin));
        uvs.Add(new Vector2(uvRect.xMin, uvRect.yMin));

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
