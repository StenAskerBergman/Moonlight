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
    [SerializeField] private Color validColor = new Color(0.2f, 1f, 0.35f, 0.28f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.25f);

    [Tooltip("Lifts the shading clear of the terrain so it does not z-fight the ground.")]
    [SerializeField, Min(0f)] private float heightOffset = 0.02f;

    [Tooltip("Shrinks each shaded quad so individual cells stay readable as tiles.")]
    [SerializeField, Range(0f, 0.4f)] private float cellInset = 0.06f;

    [Tooltip("Hard ceiling on shaded cells, so an enormous reach cannot stall a frame.")]
    [SerializeField, Min(64)] private int maxShadedCells = 20000;

    private GridSystem gridSystem;

    private GameObject validObject;
    private GameObject invalidObject;
    private Mesh validMesh;
    private Mesh invalidMesh;
    private Material validMaterial;
    private Material invalidMaterial;

    // What the currently drawn meshes were built from. The rules only change when one of
    // these does, so a static blueprint over a static boat costs nothing per frame.
    private BuildingProperties builtForProperties;
    private Vector3Int builtForBoatCell = new Vector3Int(int.MinValue, 0, int.MinValue);
    private int builtForZoneCount = -1;
    private bool builtForHasWarehouse;
    private bool isShown;

    private static readonly List<PlacementValidityOverlay> ActiveOverlays = new List<PlacementValidityOverlay>();

    #region Static entry points

    /// <summary>
    /// Draws the overlay for one island's grid and hides it everywhere else, so dragging
    /// a blueprint between islands cannot leave stale shading behind.
    /// </summary>
    public static void Show(GridSystem gridSystem, Island island, BuildingProperties properties, Unit foundingBoat)
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
        overlay.Display(island, properties, foundingBoat);
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
        if (validMaterial != null) Destroy(validMaterial);
        if (invalidMaterial != null) Destroy(invalidMaterial);
    }

    #region Display

    private void Display(Island island, BuildingProperties properties, Unit foundingBoat)
    {
        InfluenceManager influenceManager = PlacementRules.GetInfluenceManager(island);
        bool hasWarehouse = influenceManager != null && influenceManager.HasWarehouse;
        int zoneCount = influenceManager != null ? influenceManager.ActiveZoneCount : 0;

        // The founding vessel is what makes the valid region move, so its cell is part of
        // the rebuild key. Sub-cell drift is not worth a rebuild.
        Vector3Int boatCell = foundingBoat != null
            ? gridSystem.WorldToCell(foundingBoat.transform.position)
            : new Vector3Int(int.MinValue, 0, int.MinValue);

        bool dirty = !isShown
                     || builtForProperties != properties
                     || builtForBoatCell != boatCell
                     || builtForZoneCount != zoneCount
                     || builtForHasWarehouse != hasWarehouse;

        if (dirty)
        {
            Rebuild(properties, influenceManager, foundingBoat, hasWarehouse);

            builtForProperties = properties;
            builtForBoatCell = boatCell;
            builtForZoneCount = zoneCount;
            builtForHasWarehouse = hasWarehouse;
        }

        SetVisible(true);
    }

    private void Hide()
    {
        SetVisible(false);
        builtForProperties = null;
    }

    private void SetVisible(bool visible)
    {
        isShown = visible;
        if (validObject != null) validObject.SetActive(visible);
        if (invalidObject != null) invalidObject.SetActive(visible);
    }

    #endregion

    #region Mesh building

    private void Rebuild(BuildingProperties properties, InfluenceManager influenceManager, Unit foundingBoat, bool hasWarehouse)
    {
        var validVerts = new List<Vector3>();
        var validTris = new List<int>();
        var invalidVerts = new List<Vector3>();
        var invalidTris = new List<int>();

        BuildingData data = properties.buildingData;
        bool isHarbor = InfluenceManager.IsHarborBuilding(properties);

        if (TryGetShadedRegion(influenceManager, foundingBoat, isHarbor, hasWarehouse,
                               out int minX, out int minZ, out int maxX, out int maxZ))
        {
            int shaded = 0;

            for (int x = minX; x <= maxX && shaded <= maxShadedCells; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Cell cell = gridSystem.GetCell(x, z);
                    if (cell == null) continue;

                    Vector3 worldPos = gridSystem.transform.TransformPoint(cell.localCenter);

                    // Outside the region under consideration the cell is simply not part
                    // of this decision, so it is left unshaded rather than painted red.
                    if (!IsInConsideredRegion(influenceManager, foundingBoat, isHarbor, hasWarehouse, worldPos)) continue;

                    if (++shaded > maxShadedCells) break;

                    bool ok = PlacementRules.EvaluateFootprint(gridSystem, new Vector3Int(x, 0, z),
                                                               properties.buildingSize, data, out _);

                    if (ok)
                    {
                        ok = PlacementRules.EvaluateInfluence(influenceManager, isHarbor, worldPos, gridSystem, out _, out _);
                    }

                    bool isQuayBuilding = data != null && data.requiresQuayFoundation;

                    // Do not paint sunken red quads on deep water / seabed floor far from coast
                    if (!ok && cell.IsUnderwater && !isQuayBuilding) continue;

                    if (ok) AddCellQuad(validVerts, validTris, cell, isQuayBuilding);
                    else AddCellQuad(invalidVerts, invalidTris, cell, isQuayBuilding);
                }
            }
        }

        ApplyMesh(ref validMesh, validVerts, validTris, ref validObject, ref validMaterial, validColor, "Placement Valid");
        ApplyMesh(ref invalidMesh, invalidVerts, invalidTris, ref invalidObject, ref invalidMaterial, invalidColor, "Placement Invalid");
    }

    /// <summary>
    /// The grid-index window worth testing. Founding a harbor is bounded by the vessel's
    /// reach; everything else by the island's existing influence zones. Both are far
    /// smaller than the island, which is what keeps a full rebuild cheap.
    /// </summary>
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
            // No influence system on this island - the whole grid is the decision.
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

    /// <summary>
    /// The bounding box above is square; this trims it back to the actual circle so the
    /// shaded patch reads as the same range ring the player is already looking at.
    /// </summary>
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

        // Cells are stored by array index and physically span local [x, x+1).
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

    #endregion
}
