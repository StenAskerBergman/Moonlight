using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Owns per-island GPU placement / influence overlay masks and binds them onto both
/// the terrain shader (TerrainSplatNormal) and a depth-tested coastal ocean sheet
/// (CoastalPlacementOverlay).
///
/// Channel layout (RGBA32, Point, linear):
///   R: Territory (settled zones or founding boat reach)
///   G: Active influence / buildability disc
///   B: Cell buildability (valid green vs blocked red)
///   A: Footprint (128) & orthogonal connected road highlight (255)
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GridSystem))]
public sealed class PlacementOverlayMaskUpdater : MonoBehaviour
{
    private const byte ByteOff = 0;
    private const byte ByteFull = 255;
    private const byte ByteFootprint = 128;
    private const byte ByteRoad = 255;

    private static readonly int PlacementMaskProperty = Shader.PropertyToID("_PlacementMask");
    private static readonly int PlacementOverlayStateProperty = Shader.PropertyToID("_PlacementOverlayState");
    private static readonly int PlacementGridParamsProperty = Shader.PropertyToID("_PlacementGridParams");

    private GridSystem gridSystem;
    private MapGrid mapGrid;
    private MeshRenderer terrainRenderer;
    private QuaySystem quaySystem;

    private Texture2D maskTexture;
    private Color32[] pixelBuffer;
    private int bufferGridSize;

    private GameObject coastalSheetObject;
    private Mesh coastalMesh;
    private Material coastalMaterial;
    private MeshRenderer coastalRenderer;
    private float builtCoastalWaterY = float.NaN;
    private int builtCoastalGridSize = -1;
    private float builtCoastalCellSize = -1f;

    private MaterialPropertyBlock sharedPropertyBlock;
    private MaterialPropertyBlock SharedPropertyBlock => sharedPropertyBlock ??= new MaterialPropertyBlock();

    // Dirty tracking keys (avoids re-uploading identical grid frames during static hover)
    private bool isShowing;
    private BuildingProperties cachedProperties;
    private Vector3Int cachedCenterCell = new Vector3Int(int.MinValue, 0, int.MinValue);
    private Vector3Int cachedOriginCell = new Vector3Int(int.MinValue, 0, int.MinValue);
    private Vector2Int cachedFootprint;
    private Vector3Int cachedBoatCell = new Vector3Int(int.MinValue, 0, int.MinValue);
    private int cachedZoneCount = -1;
    private bool cachedHasWarehouse;
    private bool cachedCanCommit;
    private int cachedRoadCount = -1;
    private int cachedRoadHash;
    private InfluenceZone cachedStaticZone;

    private readonly HashSet<Vector2Int> cachedFootprintCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> cachedRoadCells = new HashSet<Vector2Int>();

    public static PlacementOverlayMaskUpdater GetOrCreate(GridSystem grid)
    {
        if (grid == null) return null;
        PlacementOverlayMaskUpdater updater = grid.GetComponent<PlacementOverlayMaskUpdater>();
        if (updater == null)
        {
            updater = grid.gameObject.AddComponent<PlacementOverlayMaskUpdater>();
        }
        return updater;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (gridSystem == null) gridSystem = GetComponent<GridSystem>();
        if (mapGrid == null) mapGrid = GetComponent<MapGrid>();
        if (terrainRenderer == null) terrainRenderer = GetComponent<MeshRenderer>();
        if (quaySystem == null) quaySystem = QuaySystem.GetOrCreate(gridSystem);
    }

    private void OnDestroy()
    {
        if (maskTexture != null) Destroy(maskTexture);
        if (coastalMesh != null) Destroy(coastalMesh);
        if (coastalMaterial != null) Destroy(coastalMaterial);
        if (coastalSheetObject != null) Destroy(coastalSheetObject);
    }

    public void ApplyPlacement(
        Island island,
        BuildingProperties properties,
        Unit foundingBoat,
        Vector3? previewWorldPos,
        PlacementValidationResult validation,
        Quaternion? previewRotation)
    {
        ResolveReferences();
        if (gridSystem == null || gridSystem.gridSize <= 0) return;

        InfluenceManager influenceManager = PlacementRules.GetInfluenceManager(island);
        bool hasWarehouse = influenceManager != null && influenceManager.HasWarehouse;
        int zoneCount = influenceManager != null ? influenceManager.ActiveZoneCount : 0;

        BuildingData data = properties != null ? properties.buildingData : null;
        bool isHarbor = InfluenceManager.IsHarborBuilding(properties);
        float emitterRadius = BuildingProperties.ResolveInfluenceRadius(properties, data, 0f);
        bool isZoneEmitter = isHarbor || emitterRadius > 0f || (properties != null && properties.GetComponent<InfluenceZone>() != null);

        Vector2Int footprint = previewRotation.HasValue
            ? GridSystem.GetFootprint(BuildingProperties.ResolveSize(properties, data), previewRotation.Value)
            : GridSystem.GetFootprint(BuildingProperties.ResolveSize(properties, data));

        Vector3Int centerCell = previewWorldPos.HasValue
            ? gridSystem.WorldToCell(previewWorldPos.Value)
            : new Vector3Int(int.MinValue, 0, int.MinValue);

        Vector3Int originCell = previewWorldPos.HasValue
            ? gridSystem.GetFootprintOrigin(previewWorldPos.Value, footprint)
            : new Vector3Int(int.MinValue, 0, int.MinValue);

        Vector3Int boatCell = foundingBoat != null
            ? gridSystem.WorldToCell(foundingBoat.transform.position)
            : new Vector3Int(int.MinValue, 0, int.MinValue);

        bool canCommit = validation != null && validation.CanCommit;
        int roadCount = validation != null ? validation.ConnectedRoadCells.Count : 0;
        int roadHash = ComputeRoadHash(validation);

        bool dirty = !isShowing
                     || cachedStaticZone != null
                     || cachedProperties != properties
                     || cachedCenterCell != centerCell
                     || cachedOriginCell != originCell
                     || cachedFootprint != footprint
                     || cachedBoatCell != boatCell
                     || cachedZoneCount != zoneCount
                     || cachedHasWarehouse != hasWarehouse
                     || cachedCanCommit != canCommit
                     || cachedRoadCount != roadCount
                     || cachedRoadHash != roadHash;

        if (dirty)
        {
            EnsureMaskBuffer(gridSystem.gridSize);

            RebuildPlacementPixels(
                properties,
                data,
                influenceManager,
                foundingBoat,
                hasWarehouse,
                isHarbor,
                isZoneEmitter,
                emitterRadius,
                previewWorldPos,
                originCell,
                centerCell,
                footprint,
                validation);

            maskTexture.SetPixels32(pixelBuffer);
            maskTexture.Apply(false, false);

            cachedStaticZone = null;
            cachedProperties = properties;
            cachedCenterCell = centerCell;
            cachedOriginCell = originCell;
            cachedFootprint = footprint;
            cachedBoatCell = boatCell;
            cachedZoneCount = zoneCount;
            cachedHasWarehouse = hasWarehouse;
            cachedCanCommit = canCommit;
            cachedRoadCount = roadCount;
            cachedRoadHash = roadHash;
        }

        isShowing = true;
        bool showDiscGrid = isZoneEmitter;
        PushShaderParameters(
            enabled: true,
            dimStrength: 0.58f,
            showDiscGrid: showDiscGrid,
            footprintValid: canCommit);
    }

    public void ApplyStaticZone(Island island, InfluenceZone zone)
    {
        ResolveReferences();
        if (gridSystem == null || gridSystem.gridSize <= 0 || zone == null)
        {
            Clear();
            return;
        }

        InfluenceManager influenceManager = PlacementRules.GetInfluenceManager(island);
        int zoneCount = influenceManager != null ? influenceManager.ActiveZoneCount : 0;
        Vector3Int centerCell = gridSystem.WorldToCell(zone.Center);

        bool dirty = !isShowing
                     || cachedStaticZone != zone
                     || cachedCenterCell != centerCell
                     || cachedZoneCount != zoneCount;

        if (dirty)
        {
            EnsureMaskBuffer(gridSystem.gridSize);
            RebuildStaticZonePixels(influenceManager, zone);
            maskTexture.SetPixels32(pixelBuffer);
            maskTexture.Apply(false, false);

            cachedStaticZone = zone;
            cachedProperties = null;
            cachedCenterCell = centerCell;
            cachedZoneCount = zoneCount;
        }

        isShowing = true;
        PushShaderParameters(
            enabled: true,
            dimStrength: 0.45f,
            showDiscGrid: true,
            footprintValid: true);
    }

    public void Clear()
    {
        if (!isShowing) return;
        isShowing = false;
        cachedStaticZone = null;
        cachedProperties = null;
        cachedCenterCell = new Vector3Int(int.MinValue, 0, int.MinValue);

        PushShaderParameters(
            enabled: false,
            dimStrength: 0f,
            showDiscGrid: false,
            footprintValid: true);
    }

    private void RebuildPlacementPixels(
        BuildingProperties properties,
        BuildingData data,
        InfluenceManager influenceManager,
        Unit foundingBoat,
        bool hasWarehouse,
        bool isHarbor,
        bool isZoneEmitter,
        float emitterRadius,
        Vector3? previewWorldPos,
        Vector3Int originCell,
        Vector3Int centerCell,
        Vector2Int footprint,
        PlacementValidationResult validation)
    {
        int n = gridSystem.gridSize;
        float cellSize = Mathf.Max(0.0001f, gridSystem.cellSize);

        cachedFootprintCells.Clear();
        if (previewWorldPos.HasValue)
        {
            for (int fx = 0; fx < footprint.x; fx++)
            {
                for (int fz = 0; fz < footprint.y; fz++)
                {
                    cachedFootprintCells.Add(new Vector2Int(originCell.x + fx, originCell.z + fz));
                }
            }
        }

        cachedRoadCells.Clear();
        if (validation != null)
        {
            for (int i = 0; i < validation.ConnectedRoadCells.Count; i++)
            {
                Cell roadCell = validation.ConnectedRoadCells[i];
                if (roadCell != null)
                {
                    cachedRoadCells.Add(new Vector2Int(roadCell.cellPosition.x, roadCell.cellPosition.z));
                }
            }
        }

        bool useBoatReach = isHarbor && !hasWarehouse && foundingBoat != null;
        float boatRange = useBoatReach ? InfluenceManager.FoundingRangeOf(foundingBoat) : 0f;
        float boatRangeSq = boatRange * boatRange;
        Vector3 boatPos = foundingBoat != null ? foundingBoat.transform.position : Vector3.zero;

        bool evaluateDisc = isZoneEmitter && (previewWorldPos.HasValue || useBoatReach);
        float discRadius = emitterRadius > 0f ? emitterRadius : PlacementInfluenceEvaluator.DefaultInfluenceRadius;
        float discRadiusSq = discRadius * discRadius;

        // For an unsettled harbor placement, evaluate legality across the founding boat's reach
        // centered on the vessel when no harbor disc overrides it, matching Anno's coastal founding ring.
        Vector3 discCenterWorld = previewWorldPos ?? boatPos;
        Vector3Int discCenterCell = gridSystem.WorldToCell(discCenterWorld);

        for (int z = 0; z < n; z++)
        {
            int rowOffset = z * n;
            for (int x = 0; x < n; x++)
            {
                Cell cell = gridSystem.GetCell(x, z);
                if (cell == null)
                {
                    pixelBuffer[rowOffset + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                Vector3 worldPos = gridSystem.transform.TransformPoint(cell.localCenter);

                // 1. Territory R
                bool inTerritory = false;
                if (hasWarehouse && influenceManager != null)
                {
                    inTerritory = IsInsideExistingTerritory(influenceManager, worldPos);
                }
                else if (useBoatReach)
                {
                    float bdx = worldPos.x - boatPos.x;
                    float bdz = worldPos.z - boatPos.z;
                    inTerritory = (bdx * bdx + bdz * bdz) <= boatRangeSq;
                }

                // 2. Influence Disc G
                bool inDisc = false;
                if (evaluateDisc)
                {
                    if (useBoatReach && !previewWorldPos.HasValue)
                    {
                        inDisc = inTerritory;
                    }
                    else if (useBoatReach)
                    {
                        // Show green/red across boat founding reach OR candidate harbor disc
                        float cdx = (x - discCenterCell.x) * cellSize;
                        float cdz = (z - discCenterCell.z) * cellSize;
                        bool insideCandidateDisc = (cdx * cdx + cdz * cdz) <= discRadiusSq;
                        inDisc = inTerritory || insideCandidateDisc;
                    }
                    else
                    {
                        float cdx = (x - discCenterCell.x) * cellSize;
                        float cdz = (z - discCenterCell.z) * cellSize;
                        inDisc = (cdx * cdx + cdz * cdz) <= discRadiusSq;
                    }
                }

                // 3. Validity B (checked for disc cells cleanly)
                bool cellValid = false;
                if (inDisc)
                {
                    if (useBoatReach)
                    {
                        bool inBoatRadius = inTerritory;
                        cellValid = inBoatRadius && IsHarborSiteCellUsable(cell, data);
                    }
                    else
                    {
                        cellValid = inTerritory && PlacementInfluenceEvaluator.IsCellUsableBuildable(cell, data);
                    }
                }

                // 4. Footprint & Road Pip A
                var coord = new Vector2Int(x, z);
                byte tagA = ByteOff;
                if (cachedFootprintCells.Contains(coord))
                {
                    tagA = ByteFootprint;
                }
                else if (cachedRoadCells.Contains(coord))
                {
                    tagA = ByteRoad;
                }

                pixelBuffer[rowOffset + x] = new Color32(
                    inTerritory ? ByteFull : ByteOff,
                    inDisc ? ByteFull : ByteOff,
                    cellValid ? ByteFull : ByteOff,
                    tagA);
            }
        }
    }

    private void RebuildStaticZonePixels(InfluenceManager influenceManager, InfluenceZone selectedZone)
    {
        int n = gridSystem.gridSize;
        float radiusSq = selectedZone.Radius * selectedZone.Radius;
        Vector3 zoneCenter = selectedZone.Center;

        for (int z = 0; z < n; z++)
        {
            int rowOffset = z * n;
            for (int x = 0; x < n; x++)
            {
                Cell cell = gridSystem.GetCell(x, z);
                if (cell == null)
                {
                    pixelBuffer[rowOffset + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                Vector3 worldPos = gridSystem.transform.TransformPoint(cell.localCenter);
                bool inTerritory = influenceManager != null && IsInsideExistingTerritory(influenceManager, worldPos);

                float dx = worldPos.x - zoneCenter.x;
                float dz = worldPos.z - zoneCenter.z;
                bool inDisc = (dx * dx + dz * dz) <= radiusSq;

                bool valid = inDisc && PlacementInfluenceEvaluator.IsCellUsableBuildable(cell, null);

                pixelBuffer[rowOffset + x] = new Color32(
                    (inTerritory || inDisc) ? ByteFull : ByteOff,
                    inDisc ? ByteFull : ByteOff,
                    valid ? ByteFull : ByteOff,
                    ByteOff);
            }
        }
    }

    private static bool IsInsideExistingTerritory(InfluenceManager manager, Vector3 worldPos)
    {
        foreach (InfluenceZone zone in manager.Zones)
        {
            if (zone != null && zone.ContainsPoint(worldPos)) return true;
        }
        return false;
    }

    private static bool IsHarborSiteCellUsable(Cell cell, BuildingData data)
    {
        if (cell == null || cell.isBlocked || cell.isOccupied || cell.isDeposit) return false;

        if (data != null && data.requiresQuayFoundation)
        {
            return QuaySystem.IsLegalQuayTerrain(cell.currentTerrainType);
        }

        return PlacementInfluenceEvaluator.IsCellUsableBuildable(cell, data);
    }

    private void EnsureMaskBuffer(int gridSize)
    {
        if (maskTexture == null || maskTexture.width != gridSize || maskTexture.height != gridSize)
        {
            if (maskTexture != null) Destroy(maskTexture);

            maskTexture = new Texture2D(gridSize, gridSize, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                name = $"{name} PlacementMask",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
        }

        int total = gridSize * gridSize;
        if (pixelBuffer == null || bufferGridSize != gridSize)
        {
            pixelBuffer = new Color32[total];
            bufferGridSize = gridSize;
        }
    }

    private void PushShaderParameters(bool enabled, float dimStrength, bool showDiscGrid, bool footprintValid)
    {
        ResolveReferences();
        if (gridSystem == null) return;

        float waterY = quaySystem != null ? quaySystem.WaterSurfaceLocal : 0f;
        var state = new Vector4(enabled ? 1f : 0f, dimStrength, showDiscGrid ? 1f : 0f, footprintValid ? 1f : 0f);
        var gridParams = new Vector4(gridSystem.gridSize, Mathf.Max(0.0001f, gridSystem.cellSize), waterY, Time.time);
        Texture mask = maskTexture != null ? (Texture)maskTexture : Texture2D.blackTexture;

        if (terrainRenderer != null)
        {
            MaterialPropertyBlock block = SharedPropertyBlock;
            terrainRenderer.GetPropertyBlock(block);
            block.SetTexture(PlacementMaskProperty, mask);
            block.SetVector(PlacementOverlayStateProperty, state);
            block.SetVector(PlacementGridParamsProperty, gridParams);
            terrainRenderer.SetPropertyBlock(block);
        }

        SyncCoastalSheet(enabled, waterY, mask, state, gridParams);
    }

    private void SyncCoastalSheet(bool enabled, float waterY, Texture mask, Vector4 state, Vector4 gridParams)
    {
        if (!enabled)
        {
            if (coastalSheetObject != null && coastalSheetObject.activeSelf)
            {
                coastalSheetObject.SetActive(false);
            }
            return;
        }

        EnsureCoastalSheet(waterY);
        if (coastalSheetObject == null) return;

        if (!coastalSheetObject.activeSelf) coastalSheetObject.SetActive(true);

        if (coastalRenderer != null)
        {
            MaterialPropertyBlock block = SharedPropertyBlock;
            coastalRenderer.GetPropertyBlock(block);
            block.SetTexture(PlacementMaskProperty, mask);
            block.SetVector(PlacementOverlayStateProperty, state);
            block.SetVector(PlacementGridParamsProperty, gridParams);
            coastalRenderer.SetPropertyBlock(block);
        }
    }

    private void EnsureCoastalSheet(float waterY)
    {
        if (coastalSheetObject == null)
        {
            coastalSheetObject = new GameObject("Coastal Placement Overlay");
            coastalSheetObject.transform.SetParent(transform, false);
            coastalSheetObject.AddComponent<MeshFilter>();
            coastalRenderer = coastalSheetObject.AddComponent<MeshRenderer>();
            coastalRenderer.shadowCastingMode = ShadowCastingMode.Off;
            coastalRenderer.receiveShadows = false;

            Shader coastalShader = Shader.Find("Moonlight/Placement/CoastalOverlay");
            if (coastalShader == null) coastalShader = Shader.Find("Universal Render Pipeline/Unlit");
            coastalMaterial = new Material(coastalShader) { hideFlags = HideFlags.DontSave };
            coastalRenderer.sharedMaterial = coastalMaterial;
        }

        int n = gridSystem.gridSize;
        float cs = Mathf.Max(0.0001f, gridSystem.cellSize);
        float overlayY = waterY + 0.025f;

        if (coastalMesh == null
            || !Mathf.Approximately(builtCoastalWaterY, overlayY)
            || builtCoastalGridSize != n
            || !Mathf.Approximately(builtCoastalCellSize, cs))
        {
            if (coastalMesh == null)
            {
                coastalMesh = new Mesh { name = "CoastalPlacementOverlayMesh" };
                coastalSheetObject.GetComponent<MeshFilter>().sharedMesh = coastalMesh;
            }

            // Cover island grid + generous apron so territory shroud over sea extends beyond chunk borders
            float span = n * cs;
            float margin = span * 0.5f;
            float minCoord = -margin;
            float maxCoord = span + margin;

            coastalMesh.Clear();
            coastalMesh.vertices = new[]
            {
                new Vector3(minCoord, overlayY, minCoord),
                new Vector3(minCoord, overlayY, maxCoord),
                new Vector3(maxCoord, overlayY, maxCoord),
                new Vector3(maxCoord, overlayY, minCoord)
            };
            coastalMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            coastalMesh.RecalculateNormals();
            coastalMesh.RecalculateBounds();

            builtCoastalWaterY = overlayY;
            builtCoastalGridSize = n;
            builtCoastalCellSize = cs;
        }
    }

    private static int ComputeRoadHash(PlacementValidationResult validation)
    {
        if (validation == null || validation.ConnectedRoadCells.Count == 0) return 0;

        unchecked
        {
            int hash = 17;
            for (int i = 0; i < validation.ConnectedRoadCells.Count; i++)
            {
                Cell c = validation.ConnectedRoadCells[i];
                if (c == null) continue;
                hash = hash * 31 + c.cellPosition.x;
                hash = hash * 31 + c.cellPosition.z;
            }
            return hash;
        }
    }
}
