using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Shades the island grid while a blueprint is out: green on every cell the building
/// could legally stand on, red on the cells inside reach that it cannot.
///
/// This exists because the first harbor is the one placement the player has no way to
/// reason about. Nothing is built yet, so there is no influence circle to read, and the
/// rule - a beach cell inside the vessel's founding range - is invisible until a click
/// is refused. The overlay answers it up front.
///
/// It deliberately shades only the cells actually under consideration (the founding
/// vessel's reach, or existing island influence) rather than the whole island. A red
/// wash over ten thousand cells communicates nothing; red only means "this one is in
/// range and still will not work".
/// </summary>
[DisallowMultipleComponent]
public class PlacementValidityOverlay : MonoBehaviour
{
    [Header("Colours")]
    [SerializeField] private Color validColor = new Color(0.2f, 0.95f, 0.35f, 0.38f);
    [SerializeField] private Color invalidColor = new Color(0.95f, 0.15f, 0.15f, 0.42f);
    [SerializeField] private Color boundaryLineColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Color gridLineColor = new Color(0.3f, 1f, 0.4f, 0.45f);

    [Tooltip("Lifts the shading clear of the terrain so it does not z-fight the ground.")]
    [SerializeField, Min(0f)] private float heightOffset = 0.03f;

    [Tooltip("Shrinks each shaded quad so individual cells stay readable as tiles.")]
    [SerializeField, Range(0f, 0.4f)] private float cellInset = 0.05f;

    [Tooltip("Hard ceiling on shaded cells, so an enormous reach cannot stall a frame.")]
    [SerializeField, Min(64)] private int maxShadedCells = 20000;

    private GridSystem gridSystem;

    private GameObject validObject;
    private GameObject invalidObject;
    private GameObject boundaryLineObject;
    private GameObject gridLineObject;

    private Mesh validMesh;
    private Mesh invalidMesh;
    private Mesh boundaryLineMesh;
    private Mesh gridLineMesh;

    private Material validMaterial;
    private Material invalidMaterial;
    private Material boundaryLineMaterial;
    private Material gridLineMaterial;

    // Cache keys to avoid unnecessary mesh re-evaluations
    private BuildingProperties builtForProperties;
    private Vector3Int builtForCenterCell = new Vector3Int(int.MinValue, 0, int.MinValue);
    private Vector3Int builtForBoatCell = new Vector3Int(int.MinValue, 0, int.MinValue);
    private int builtForZoneCount = -1;
    private bool builtForHasWarehouse;
    private bool isShown;

    private static readonly List<PlacementValidityOverlay> ActiveOverlays = new List<PlacementValidityOverlay>();

    #region Static entry points

    /// <summary>
    /// Draws the overlay for one island's grid and hides it everywhere else, so dragging
    /// a blueprint between islands cannot leave stale shading behind.
    /// Overload with previewWorldPos tracks the building ghost as it moves over the island.
    /// </summary>
    public static void Show(GridSystem gridSystem, Island island, BuildingProperties properties, Unit foundingBoat, Vector3? previewWorldPos = null)
    {
        for (int i = ActiveOverlays.Count - 1; i >= 0; i--)
        {
            if (ActiveOverlays[i] == null)
            {
                ActiveOverlays.RemoveAt(i);
                continue;
            }
            if (ActiveOverlays[i].gridSystem != gridSystem) ActiveOverlays[i].Hide();
        }

        if (gridSystem == null || properties == null) return;

        PlacementValidityOverlay overlay = GetOrCreate(gridSystem);
        overlay.Display(island, properties, foundingBoat, previewWorldPos);
    }

    public static void HideAll()
    {
        for (int i = ActiveOverlays.Count - 1; i >= 0; i--)
        {
            if (ActiveOverlays[i] == null)
            {
                ActiveOverlays.RemoveAt(i);
                continue;
            }
            ActiveOverlays[i].Hide();
        }
    }

    private static PlacementValidityOverlay GetOrCreate(GridSystem gridSystem)
    {
        PlacementValidityOverlay overlay = gridSystem.GetComponent<PlacementValidityOverlay>();
        if (overlay == null)
        {
            overlay = gridSystem.gameObject.AddComponent<PlacementValidityOverlay>();
        }
        overlay.gridSystem = gridSystem;
        return overlay;
    }

    #endregion

    private void OnEnable()
    {
        if (gridSystem == null) gridSystem = GetComponent<GridSystem>();
        if (!ActiveOverlays.Contains(this)) ActiveOverlays.Add(this);
    }

    private void OnDisable()
    {
        ActiveOverlays.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveOverlays.Remove(this);
        if (validMesh != null) Destroy(validMesh);
        if (invalidMesh != null) Destroy(invalidMesh);
        if (boundaryLineMesh != null) Destroy(boundaryLineMesh);
        if (gridLineMesh != null) Destroy(gridLineMesh);

        if (validMaterial != null) Destroy(validMaterial);
        if (invalidMaterial != null) Destroy(invalidMaterial);
        if (boundaryLineMaterial != null) Destroy(boundaryLineMaterial);
        if (gridLineMaterial != null) Destroy(gridLineMaterial);
    }

    #region Display

    private void Display(Island island, BuildingProperties properties, Unit foundingBoat, Vector3? previewWorldPos)
    {
        InfluenceManager influenceManager = PlacementRules.GetInfluenceManager(island);
        bool hasWarehouse = influenceManager != null && influenceManager.HasWarehouse;
        int zoneCount = influenceManager != null ? influenceManager.ActiveZoneCount : 0;

        Vector3Int boatCell = foundingBoat != null
            ? gridSystem.WorldToCell(foundingBoat.transform.position)
            : new Vector3Int(int.MinValue, 0, int.MinValue);

        Vector3Int centerCell = previewWorldPos.HasValue
            ? gridSystem.WorldToCell(previewWorldPos.Value)
            : new Vector3Int(int.MinValue, 0, int.MinValue);

        bool dirty = !isShown
                     || builtForProperties != properties
                     || builtForCenterCell != centerCell
                     || builtForBoatCell != boatCell
                     || builtForZoneCount != zoneCount
                     || builtForHasWarehouse != hasWarehouse;

        if (dirty)
        {
            Rebuild(properties, influenceManager, foundingBoat, hasWarehouse, previewWorldPos);

            builtForProperties = properties;
            builtForCenterCell = centerCell;
            builtForBoatCell = boatCell;
            builtForZoneCount = zoneCount;
            builtForHasWarehouse = hasWarehouse;
        }

        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
        builtForProperties = null;
        builtForCenterCell = new Vector3Int(int.MinValue, 0, int.MinValue);
    }

    private void SetVisible(bool visible)
    {
        isShown = visible;
        if (validObject != null) validObject.SetActive(visible);
        if (invalidObject != null) invalidObject.SetActive(visible);
        if (boundaryLineObject != null) boundaryLineObject.SetActive(visible);
        if (gridLineObject != null) gridLineObject.SetActive(visible);
    }

    #endregion

    #region Mesh building

    private void Rebuild(BuildingProperties properties, InfluenceManager influenceManager, Unit foundingBoat, bool hasWarehouse, Vector3? previewWorldPos)
    {
        var validVerts = new List<Vector3>();
        var validTris = new List<int>();
        var invalidVerts = new List<Vector3>();
        var invalidTris = new List<int>();

        var boundaryLinesVerts = new List<Vector3>();
        var boundaryLinesIndices = new List<int>();

        var gridLinesVerts = new List<Vector3>();
        var gridLinesIndices = new List<int>();

        BuildingData data = properties.buildingData;
        bool isHarbor = InfluenceManager.IsHarborBuilding(properties);
        bool isQuayBuilding = data != null && data.requiresQuayFoundation;

        TerrainSampleCache heightField = ResolveTerrainHeightField();
        QuaySystem quay = QuaySystem.GetOrCreate(gridSystem);
        float quayTop = quay != null ? quay.TopElevationLocal : 0f;

        // Anno 2070 Dynamic Candidate Influence mode
        if (previewWorldPos.HasValue)
        {
            Vector3 worldCenter = previewWorldPos.Value;
            Vector3Int centerCellCoords = gridSystem.WorldToCell(worldCenter);
            Vector2Int footprint = GridSystem.GetFootprint(
                BuildingProperties.ResolveSize(properties, data),
                properties.transform.rotation);

            InfluenceEvaluationResult eval = PlacementInfluenceEvaluator.EvaluateCandidateInfluence(
                gridSystem,
                centerCellCoords,
                footprint,
                properties);

            var candidateSet = new HashSet<Vector2Int>();

            // 1. Build valid (green) cell quads
            foreach (Vector2Int coord in eval.validCells)
            {
                Cell cell = gridSystem.GetCell(coord.x, coord.y);
                if (cell == null) continue;
                candidateSet.Add(coord);
                AddCellQuad(validVerts, validTris, cell, isQuayBuilding);
                AddCellGridBorders(gridLinesVerts, gridLinesIndices, coord.x, coord.y, cell, heightField, isQuayBuilding, quayTop);
            }

            // 2. Build invalid (red) cell quads
            foreach (Vector2Int coord in eval.invalidCells)
            {
                Cell cell = gridSystem.GetCell(coord.x, coord.y);
                candidateSet.Add(coord);

                // If cell is null (outside island array), synthesize a flat quad at 0
                if (cell == null)
                {
                    AddSyntheticCellQuad(invalidVerts, invalidTris, coord.x, coord.y, 0f);
                }
                else
                {
                    AddCellQuad(invalidVerts, invalidTris, cell, isQuayBuilding);
                    AddCellGridBorders(gridLinesVerts, gridLinesIndices, coord.x, coord.y, cell, heightField, isQuayBuilding, quayTop);
                }
            }

            // 3. Build stepped boundary outline for all candidate cells (stepped pixelated circle perimeter)
            BuildSteppedPerimeterLines(candidateSet, boundaryLinesVerts, boundaryLinesIndices, heightField, isQuayBuilding, quayTop);
        }
        else
        {
            // Static / Settlement boat fallback mode
            if (TryGetShadedRegion(influenceManager, foundingBoat, isHarbor, hasWarehouse,
                                   out int minX, out int minZ, out int maxX, out int maxZ))
            {
                int shaded = 0;
                var consideredSet = new HashSet<Vector2Int>();

                for (int x = minX; x <= maxX && shaded <= maxShadedCells; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        Cell cell = gridSystem.GetCell(x, z);
                        if (cell == null) continue;

                        Vector3 worldPos = gridSystem.transform.TransformPoint(cell.localCenter);
                        if (!IsInConsideredRegion(influenceManager, foundingBoat, isHarbor, hasWarehouse, worldPos)) continue;

                        if (++shaded > maxShadedCells) break;

                        consideredSet.Add(new Vector2Int(x, z));

                        bool ok = PlacementRules.EvaluateFootprint(gridSystem, new Vector3Int(x, 0, z),
                                                                   properties.buildingSize, data, out _);

                        if (ok)
                        {
                            ok = PlacementRules.EvaluateInfluence(influenceManager, isHarbor, worldPos, gridSystem, out _, out _);
                        }

                        if (!ok && cell.IsUnderwater && !isQuayBuilding) continue;

                        if (ok)
                        {
                            AddCellQuad(validVerts, validTris, cell, isQuayBuilding);
                        }
                        else
                        {
                            AddCellQuad(invalidVerts, invalidTris, cell, isQuayBuilding);
                        }

                        AddCellGridBorders(gridLinesVerts, gridLinesIndices, x, z, cell, heightField, isQuayBuilding, quayTop);
                    }
                }

                BuildSteppedPerimeterLines(consideredSet, boundaryLinesVerts, boundaryLinesIndices, heightField, isQuayBuilding, quayTop);
            }
        }

        ApplyMesh(ref validMesh, validVerts, validTris, ref validObject, ref validMaterial, validColor, "Placement Valid");
        ApplyMesh(ref invalidMesh, invalidVerts, invalidTris, ref invalidObject, ref invalidMaterial, invalidColor, "Placement Invalid");
        ApplyLineMesh(ref boundaryLineMesh, boundaryLinesVerts, boundaryLinesIndices, ref boundaryLineObject, ref boundaryLineMaterial, boundaryLineColor, "Placement Stepped Boundary");
        ApplyLineMesh(ref gridLineMesh, gridLinesVerts, gridLinesIndices, ref gridLineObject, ref gridLineMaterial, gridLineColor, "Placement Grid Lines");
    }

    /// <summary>
    /// Constructs the exact stepped / pixelated perimeter lines surrounding the candidate region.
    /// Checks the 4 orthogonal edges of every candidate cell; if an adjacent cell is outside the
    /// evaluated influence set, that edge forms the outer boundary segment.
    /// </summary>
    private void BuildSteppedPerimeterLines(
        HashSet<Vector2Int> candidateCells,
        List<Vector3> vertices,
        List<int> indices,
        TerrainSampleCache heightField,
        bool isQuayBuilding,
        float quayTop)
    {
        if (candidateCells == null || candidateCells.Count == 0) return;

        float o = heightOffset + 0.015f; // Lift boundary line slightly above cell quads

        foreach (Vector2Int coord in candidateCells)
        {
            int x = coord.x;
            int z = coord.y;
            Cell cell = gridSystem.GetCell(x, z);

            float c00 = GetCornerElevation(heightField, cell, x, z, isQuayBuilding, quayTop) + o;
            float c10 = GetCornerElevation(heightField, cell, x + 1, z, isQuayBuilding, quayTop) + o;
            float c11 = GetCornerElevation(heightField, cell, x + 1, z + 1, isQuayBuilding, quayTop) + o;
            float c01 = GetCornerElevation(heightField, cell, x, z + 1, isQuayBuilding, quayTop) + o;

            Vector3 p00 = new Vector3(x, c00, z);
            Vector3 p10 = new Vector3(x + 1f, c10, z);
            Vector3 p11 = new Vector3(x + 1f, c11, z + 1f);
            Vector3 p01 = new Vector3(x, c01, z + 1f);

            // South edge (z - 1)
            if (!candidateCells.Contains(new Vector2Int(x, z - 1)))
            {
                AddLineSegment(vertices, indices, p00, p10);
            }

            // East edge (x + 1)
            if (!candidateCells.Contains(new Vector2Int(x + 1, z)))
            {
                AddLineSegment(vertices, indices, p10, p11);
            }

            // North edge (z + 1)
            if (!candidateCells.Contains(new Vector2Int(x, z + 1)))
            {
                AddLineSegment(vertices, indices, p11, p01);
            }

            // West edge (x - 1)
            if (!candidateCells.Contains(new Vector2Int(x - 1, z)))
            {
                AddLineSegment(vertices, indices, p01, p00);
            }
        }
    }

    private void AddCellGridBorders(
        List<Vector3> vertices,
        List<int> indices,
        int x,
        int z,
        Cell cell,
        TerrainSampleCache heightField,
        bool isQuayBuilding,
        float quayTop)
    {
        float o = heightOffset + 0.008f;

        float c00 = GetCornerElevation(heightField, cell, x, z, isQuayBuilding, quayTop) + o;
        float c10 = GetCornerElevation(heightField, cell, x + 1, z, isQuayBuilding, quayTop) + o;
        float c11 = GetCornerElevation(heightField, cell, x + 1, z + 1, isQuayBuilding, quayTop) + o;
        float c01 = GetCornerElevation(heightField, cell, x, z + 1, isQuayBuilding, quayTop) + o;

        Vector3 p00 = new Vector3(x, c00, z);
        Vector3 p10 = new Vector3(x + 1f, c10, z);
        Vector3 p11 = new Vector3(x + 1f, c11, z + 1f);
        Vector3 p01 = new Vector3(x, c01, z + 1f);

        AddLineSegment(vertices, indices, p00, p10);
        AddLineSegment(vertices, indices, p10, p11);
        AddLineSegment(vertices, indices, p11, p01);
        AddLineSegment(vertices, indices, p01, p00);
    }

    private float GetCornerElevation(TerrainSampleCache heightField, Cell cell, int x, int z, bool isQuayBuilding, float quayTop)
    {
        if (cell != null && isQuayBuilding && cell.IsUnderwater)
        {
            return quayTop;
        }

        if (heightField == null)
        {
            return cell != null ? cell.height : 0f;
        }

        int samples = heightField.VisualSamplesPerCell;
        int clampedX = Mathf.Clamp(x * samples, 0, (gridSystem.gridSize) * samples);
        int clampedZ = Mathf.Clamp(z * samples, 0, (gridSystem.gridSize) * samples);

        return heightField.GetHeight(clampedX, clampedZ);
    }

    private static void AddLineSegment(List<Vector3> vertices, List<int> indices, Vector3 start, Vector3 end)
    {
        int first = vertices.Count;
        vertices.Add(start);
        vertices.Add(end);
        indices.Add(first);
        indices.Add(first + 1);
    }

    private bool TryGetShadedRegion(InfluenceManager influenceManager, Unit foundingBoat, bool isHarbor, bool hasWarehouse,
                                    out int minX, out int minZ, out int maxX, out int maxZ)
    {
        minX = minZ = maxX = maxZ = 0;

        if (isHarbor && !hasWarehouse)
        {
            if (foundingBoat == null) return false;

            return TryGetCircleRegion(foundingBoat.transform.position,
                                      InfluenceManager.FoundingRangeOf(foundingBoat),
                                      out minX, out minZ, out maxX, out maxZ);
        }

        if (influenceManager == null)
        {
            maxX = gridSystem.gridSize - 1;
            maxZ = gridSystem.gridSize - 1;
            return maxX >= 0 && maxZ >= 0;
        }

        bool any = false;
        foreach (InfluenceZone zone in influenceManager.Zones)
        {
            if (zone == null) continue;
            if (!TryGetCircleRegion(zone.Center, zone.Radius, out int zMinX, out int zMinZ, out int zMaxX, out int zMaxZ)) continue;

            if (!any)
            {
                minX = zMinX; minZ = zMinZ; maxX = zMaxX; maxZ = zMaxZ;
                any = true;
            }
            else
            {
                minX = Mathf.Min(minX, zMinX);
                minZ = Mathf.Min(minZ, zMinZ);
                maxX = Mathf.Max(maxX, zMaxX);
                maxZ = Mathf.Max(maxZ, zMaxZ);
            }
        }

        return any;
    }

    private bool TryGetCircleRegion(Vector3 worldCenter, float radius, out int minX, out int minZ, out int maxX, out int maxZ)
    {
        Vector3Int center = gridSystem.WorldToCell(worldCenter);
        int cellRadius = Mathf.CeilToInt(radius / Mathf.Max(0.0001f, gridSystem.cellSize)) + 1;

        minX = Mathf.Max(0, center.x - cellRadius);
        minZ = Mathf.Max(0, center.z - cellRadius);
        maxX = Mathf.Min(gridSystem.gridSize - 1, center.x + cellRadius);
        maxZ = Mathf.Min(gridSystem.gridSize - 1, center.z + cellRadius);

        return minX <= maxX && minZ <= maxZ;
    }

    private bool IsInConsideredRegion(InfluenceManager influenceManager, Unit foundingBoat, bool isHarbor, bool hasWarehouse, Vector3 worldPos)
    {
        if (isHarbor && !hasWarehouse)
        {
            if (foundingBoat == null) return false;

            Vector3 cellFlat = worldPos; cellFlat.y = 0f;
            Vector3 boatFlat = foundingBoat.transform.position; boatFlat.y = 0f;
            return Vector3.Distance(cellFlat, boatFlat) <= InfluenceManager.FoundingRangeOf(foundingBoat);
        }

        if (influenceManager == null) return true;

        foreach (InfluenceZone zone in influenceManager.Zones)
        {
            if (zone != null && zone.ContainsPoint(worldPos)) return true;
        }
        return false;
    }

    private void AddCellQuad(List<Vector3> vertices, List<int> triangles, Cell cell, bool isQuayBuilding)
    {
        float inset = cellInset * gridSystem.cellSize;
        float y = cell.height;

        if (isQuayBuilding && cell.IsUnderwater)
        {
            QuaySystem quay = QuaySystem.GetOrCreate(gridSystem);
            if (quay != null) y = quay.TopElevationLocal;
        }

        y += heightOffset;

        float x0 = cell.cellPosition.x + inset;
        float x1 = cell.cellPosition.x + 1f - inset;
        float z0 = cell.cellPosition.z + inset;
        float z1 = cell.cellPosition.z + 1f - inset;

        int first = vertices.Count;
        vertices.Add(new Vector3(x0, y, z0));
        vertices.Add(new Vector3(x0, y, z1));
        vertices.Add(new Vector3(x1, y, z1));
        vertices.Add(new Vector3(x1, y, z0));

        triangles.Add(first);
        triangles.Add(first + 1);
        triangles.Add(first + 2);
        triangles.Add(first);
        triangles.Add(first + 2);
        triangles.Add(first + 3);
    }

    private void AddSyntheticCellQuad(List<Vector3> vertices, List<int> triangles, int x, int z, float y)
    {
        float inset = cellInset * gridSystem.cellSize;
        y += heightOffset;

        float x0 = x + inset;
        float x1 = x + 1f - inset;
        float z0 = z + inset;
        float z1 = z + 1f - inset;

        int first = vertices.Count;
        vertices.Add(new Vector3(x0, y, z0));
        vertices.Add(new Vector3(x0, y, z1));
        vertices.Add(new Vector3(x1, y, z1));
        vertices.Add(new Vector3(x1, y, z0));

        triangles.Add(first);
        triangles.Add(first + 1);
        triangles.Add(first + 2);
        triangles.Add(first);
        triangles.Add(first + 2);
        triangles.Add(first + 3);
    }

    private TerrainSampleCache ResolveTerrainHeightField()
    {
        MapGrid mg = GetComponent<MapGrid>();
        if (mg == null || mg.TerrainSource == null || mg.generationSettings == null) return null;

        return mg.TerrainSource.GetOrCreateSampleCache(mg.generationSettings.visualSamplesPerCell);
    }

    private void ApplyMesh(ref Mesh mesh, List<Vector3> vertices, List<int> triangles,
                           ref GameObject target, ref Material material, Color color, string label)
    {
        if (target == null)
        {
            target = new GameObject(label);
            target.transform.SetParent(transform, false);
            target.AddComponent<MeshFilter>();

            MeshRenderer renderer = target.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            material = OverlayMaterial.Create(color);
            renderer.sharedMaterial = material;
        }

        OverlayMaterial.SetColor(material, color);

        if (mesh == null)
        {
            mesh = new Mesh { name = label };
            mesh.indexFormat = IndexFormat.UInt32;
            target.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        mesh.Clear();
        if (vertices.Count > 0)
        {
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }
    }

    private void ApplyLineMesh(ref Mesh mesh, List<Vector3> vertices, List<int> indices,
                               ref GameObject target, ref Material material, Color color, string label)
    {
        if (target == null)
        {
            target = new GameObject(label);
            target.transform.SetParent(transform, false);
            target.AddComponent<MeshFilter>();

            MeshRenderer renderer = target.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            material = OverlayMaterial.Create(color);
            renderer.sharedMaterial = material;
        }

        OverlayMaterial.SetColor(material, color);

        if (mesh == null)
        {
            mesh = new Mesh { name = label };
            mesh.indexFormat = IndexFormat.UInt32;
            target.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        mesh.Clear();
        if (vertices.Count > 0)
        {
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
        }
    }

    #endregion
}
