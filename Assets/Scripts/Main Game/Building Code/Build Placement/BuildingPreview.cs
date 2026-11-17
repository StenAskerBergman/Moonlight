using UnityEngine;
using UnityEngine.Rendering;

public class BuildingPreview : MonoBehaviour
{
    // Inspector Settings and Adjustments
    // And all the variable used in the code

    [SerializeField] private bool localCanPlace;
    [SerializeField] private LayerMask groundLayer;
    public GameObject buildingPrefab;
    public Island currentIsland;
    public GridSystem gridSystem;
    [Space]
    public SnapMode snapMode = SnapMode.Grid;
    public Vector3 offset = new Vector3(5, 0, 5);
    [SerializeField] private float offsetY;
    public float size = 1f;

    // Mats (legacy fallbacks if custom hologram shader is absent)
    [SerializeField] private Material truePlacement;
    [SerializeField] private Material falsePlacement;

    [Header("Facing Arrow")]
    [Tooltip("Turn a HARBOR blueprint so its front points away from the island, out to sea. " +
             "Inland buildings ignore this and keep whatever rotation the player gives them.")]
    [SerializeField] private bool faceAwayFromIsland = true;
    [SerializeField] private float arrowLength = 5f;
    [SerializeField] private float arrowHeight = 0.14f;
    [SerializeField] private Color arrowValidColor = new Color(0.25f, 1f, 0.35f, 0.85f);
    [SerializeField] private Color arrowInvalidColor = new Color(1f, 0.25f, 0.25f, 0.85f);

    private static readonly Color ValidBaseColor = new Color(0.14f, 0.94f, 0.38f, 0.62f);
    private static readonly Color ValidRimColor = new Color(0.55f, 1.0f, 0.72f, 1.0f);
    private static readonly Color UnaffordableBaseColor = new Color(0.98f, 0.68f, 0.14f, 0.64f);
    private static readonly Color UnaffordableRimColor = new Color(1.0f, 0.86f, 0.35f, 1.0f);
    private static readonly Color BlockedBaseColor = new Color(0.98f, 0.16f, 0.14f, 0.64f);
    private static readonly Color BlockedRimColor = new Color(1.0f, 0.42f, 0.38f, 1.0f);

    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int RimColorProperty = Shader.PropertyToID("_RimColor");

    private BuildingRotator rotator;
    private QuayFoundationPreview quayPreview;
    private GameObject facingArrow;
    private Mesh facingArrowMesh;
    private Material facingArrowMaterial;
    private Material hologramMaterial;
    private MeshRenderer[] cachedPreviewRenderers = System.Array.Empty<MeshRenderer>();
    private MaterialPropertyBlock _propertyBlock;
    private MaterialPropertyBlock propertyBlock => _propertyBlock ??= new MaterialPropertyBlock();

    private Color currentBaseTint = ValidBaseColor;
    private Color currentRimTint = ValidRimColor;

    private GridSystem visibleBuildGrid;
    private Island centerCachedIsland;
    private Vector3 cachedIslandCenter;

    // Building Data
    public Island BoundIsland { get; private set; }
    public Unit BoundBoat { get; private set; }
    public bool IsBoundToIsland => BoundIsland != null;

    public void BindToIsland(Island island, Unit boat = null)
    {
        BoundIsland = island;
        BoundBoat = boat;
        currentIsland = island;
        if (island != null)
        {
            UpdateGridSystem(island.GetComponentInChildren<GridSystem>());
            transform.SetParent(island.transform);
        }
    }

    public BuildingData buildingData;
    public BuildingProperties buildingProperties;
    private BuildingRequirements buildingRequirements;

    public enum SnapMode
    {
        Grid,
        Deposit
    }

    #region Start + OnDestroy

    private void Start()
    {
        rotator = GetComponent<BuildingRotator>();

        buildingData = GetBuildingData();
        buildingProperties = GetPropertiesData();

        // Blueprints for buildings without art would otherwise be invisible, leaving nothing
        // for hologram rendering. Built after buildingData is resolved so the stand-in matches
        // the real footprint.
        BuildingPlaceholderModel.Ensure(gameObject, withCollider: false, withLabel: false);
        CachePreviewRenderers();

        buildingRequirements = FindObjectOfType<BuildingRequirements>();
        if (buildingRequirements != null)
        {
            buildingRequirements.SetRequirements(this);
        }

        if (IslandManager.instance != null)
        {
            IslandManager.instance.OnGridSystemDetected += OnGridSystemDetected;
            IslandManager.instance.OnPlayerEnterIsland += OnPlayerEnterIsland;
        }
    }

    private void OnDestroy()
    {
        SetVisibleBuildGrid(null);
        if (IslandManager.instance != null)
        {
            IslandManager.instance.OnGridSystemDetected -= OnGridSystemDetected;
            IslandManager.instance.OnPlayerEnterIsland -= OnPlayerEnterIsland;
        }
        if (hologramMaterial != null) Destroy(hologramMaterial);
        if (facingArrowMaterial != null) Destroy(facingArrowMaterial);
        if (facingArrowMesh != null) Destroy(facingArrowMesh);
    }

    #endregion

    #region Events + GridSystem

    private void OnPlayerEnterIsland(Island island)
    {
        if (IsBoundToIsland) return;
        currentIsland = island;
    }

    private void OnGridSystemDetected(GridSystem detectedGridSystem)
    {
        UpdateGridSystem(detectedGridSystem);
    }

    public void UpdateGridSystem(GridSystem newGridSystem)
    {
        gridSystem = newGridSystem;
        SetVisibleBuildGrid(newGridSystem);
    }

    private void SetVisibleBuildGrid(GridSystem newGridSystem)
    {
        if (visibleBuildGrid == newGridSystem) return;

        if (visibleBuildGrid != null)
        {
            visibleBuildGrid.SetBuildGridVisible(false);
        }

        visibleBuildGrid = newGridSystem;
    }

    #endregion

    #region Render Related

    public void SetPreviewMaterial(bool canPlace)
    {
        SetPlacementVisualState(canPlace, canPlace);
    }

    public void SetPlacementVisualState(bool isPlacementValid, bool isAffordable)
    {
        localCanPlace = isPlacementValid && isAffordable;

        if (IsHarborBlueprint())
        {
            EnsureFacingArrow();
            if (facingArrow != null && !facingArrow.activeSelf) facingArrow.SetActive(true);
        }
        else if (facingArrow != null && facingArrow.activeSelf)
        {
            facingArrow.SetActive(false);
        }

        if (!isPlacementValid)
        {
            currentBaseTint = BlockedBaseColor;
            currentRimTint = BlockedRimColor;
        }
        else if (!isAffordable)
        {
            currentBaseTint = UnaffordableBaseColor;
            currentRimTint = UnaffordableRimColor;
        }
        else
        {
            currentBaseTint = ValidBaseColor;
            currentRimTint = ValidRimColor;
        }

        if (cachedPreviewRenderers == null || cachedPreviewRenderers.Length == 0)
        {
            CachePreviewRenderers();
        }

        Material holoMat = ResolveHologramMaterial(localCanPlace);
        for (int i = 0; i < cachedPreviewRenderers.Length; i++)
        {
            MeshRenderer r = cachedPreviewRenderers[i];
            if (r == null) continue;
            if (facingArrow != null && r.gameObject == facingArrow) continue;

            if (holoMat != null && r.sharedMaterial != holoMat)
            {
                r.sharedMaterial = holoMat;
            }

            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorProperty, currentBaseTint);
            propertyBlock.SetColor(RimColorProperty, currentRimTint);
            r.SetPropertyBlock(propertyBlock);
        }

        if (facingArrowMaterial != null)
        {
            OverlayMaterial.SetColor(facingArrowMaterial, localCanPlace ? arrowValidColor : arrowInvalidColor);
        }
    }

    private Material ResolveHologramMaterial(bool canCommit)
    {
        if (hologramMaterial == null)
        {
            Shader holoShader = Shader.Find("Moonlight/Placement/BuildingBlueprint");
            if (holoShader != null)
            {
                hologramMaterial = new Material(holoShader)
                {
                    name = "RuntimeBuildingBlueprintHologram",
                    hideFlags = HideFlags.DontSave
                };
            }
        }

        return hologramMaterial != null ? hologramMaterial : (canCommit ? truePlacement : falsePlacement);
    }

    private void CachePreviewRenderers()
    {
        var all = GetComponentsInChildren<MeshRenderer>(true);
        var filtered = new System.Collections.Generic.List<MeshRenderer>(all.Length);
        MeshRenderer rootCubeRenderer = _clonedVisuals != null ? GetComponent<MeshRenderer>() : null;

        for (int i = 0; i < all.Length; i++)
        {
            MeshRenderer r = all[i];
            if (r == null) continue;
            if (facingArrow != null && r.gameObject == facingArrow) continue;
            if (r == rootCubeRenderer) continue;
            if (r.GetComponent<PlaceholderLabelBillboard>() != null) continue;

            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            filtered.Add(r);
        }

        cachedPreviewRenderers = filtered.ToArray();
    }

    private bool IsHarborBlueprint()
    {
        if (buildingData != null && buildingData.requiresQuayFoundation) return true;

        return InfluenceManager.IsHarborBuilding(buildingProperties);
    }

    #region Facing Arrow

    private void EnsureFacingArrow()
    {
        if (facingArrow != null) return;

        facingArrow = new GameObject("Placement Facing Arrow");
        facingArrow.transform.SetParent(transform, false);
        facingArrow.transform.localPosition = new Vector3(0f, arrowHeight, 0f);

        facingArrowMesh = BuildArrowMesh(arrowLength);
        facingArrow.AddComponent<MeshFilter>().sharedMesh = facingArrowMesh;

        MeshRenderer arrowRenderer = facingArrow.AddComponent<MeshRenderer>();
        arrowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        arrowRenderer.receiveShadows = false;

        facingArrowMaterial = OverlayMaterial.Create(arrowValidColor);
        // Depth-tested over coastal water & deck so arrow doesn't draw straight through building hulls.
        if (facingArrowMaterial.HasProperty("_ZTest"))
        {
            facingArrowMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
        }
        facingArrowMaterial.renderQueue = (int)RenderQueue.Transparent + 55;
        arrowRenderer.sharedMaterial = facingArrowMaterial;
    }

    private static Mesh BuildArrowMesh(float length)
    {
        length = Mathf.Max(1f, length);

        float shaftLength = length * 0.6f;
        float shaftHalfWidth = length * 0.09f;
        float headHalfWidth = length * 0.22f;

        Vector3[] vertices =
        {
            new Vector3(-shaftHalfWidth, 0f, 0f),
            new Vector3( shaftHalfWidth, 0f, 0f),
            new Vector3( shaftHalfWidth, 0f, shaftLength),
            new Vector3(-shaftHalfWidth, 0f, shaftLength),

            new Vector3(-headHalfWidth, 0f, shaftLength),
            new Vector3( headHalfWidth, 0f, shaftLength),
            new Vector3(0f, 0f, length),
        };

        int[] triangles =
        {
            0, 3, 2,
            0, 2, 1,
            4, 6, 5,
        };

        Mesh mesh = new Mesh { name = "PlacementFacingArrow" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private Vector3 GetIslandCenter(Island island)
    {
        if (island == centerCachedIsland) return cachedIslandCenter;

        Collider[] colliders = island.GetComponentsInChildren<Collider>();
        Vector3 center = island.transform.position;

        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
            center = bounds.center;
        }

        centerCachedIsland = island;
        cachedIslandCenter = center;
        return center;
    }

    private void FaceOutwards()
    {
        if (!faceAwayFromIsland || currentIsland == null) return;
        if (!IsHarborBlueprint()) return;

        Vector3 outward = transform.position - GetIslandCenter(currentIsland);
        outward.y = 0f;
        if (outward.sqrMagnitude <= 0.0001f) return;

        Quaternion facing = Quaternion.LookRotation(outward.normalized, Vector3.up);

        if (rotator != null)
        {
            rotator.SetBaseRotation(facing);
            return;
        }

        transform.rotation = facing;
    }

    #endregion

    public void SetRendererColor(Color color)
    {
        if (cachedPreviewRenderers == null || cachedPreviewRenderers.Length == 0) CachePreviewRenderers();
        foreach (MeshRenderer renderer in cachedPreviewRenderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorProperty, color);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    public void SetRendererEnabled(bool isEnabled)
    {
        if (cachedPreviewRenderers == null || cachedPreviewRenderers.Length == 0) CachePreviewRenderers();
        foreach (MeshRenderer renderer in cachedPreviewRenderers)
        {
            if (renderer != null) renderer.enabled = isEnabled;
        }
    }

    #endregion

    private GameObject _clonedVisuals;

    public void SetBuildingPrefab(GameObject buildingPrefab)
    {
        this.buildingPrefab = buildingPrefab;
        buildingData = GetBuildingData();
        buildingProperties = GetPropertiesData();

        if (currentIsland != null)
        {
            transform.SetParent(currentIsland.transform);
        }

        if (buildingPrefab != null)
        {
            if (_clonedVisuals != null)
            {
                Destroy(_clonedVisuals);
            }

            _clonedVisuals = Instantiate(buildingPrefab, transform.position, transform.rotation, transform);
            _clonedVisuals.name = $"{buildingPrefab.name}_GhostSilhouette";

            foreach (var col in _clonedVisuals.GetComponentsInChildren<Collider>(true)) Destroy(col);
            var mbs = _clonedVisuals.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = mbs.Length - 1; i >= 0; i--)
            {
                if (mbs[i] != null) Destroy(mbs[i]);
            }
            foreach (var aud in _clonedVisuals.GetComponentsInChildren<AudioSource>(true)) Destroy(aud);

            MeshRenderer baseRenderer = GetComponent<MeshRenderer>();
            if (baseRenderer != null) baseRenderer.enabled = false;
        }

        CachePreviewRenderers();
        SetPreviewMaterial(localCanPlace);
    }

    public GameObject GetBuildingPrefab()
    {
        return buildingPrefab;
    }

    public void Update()
    {
        #region Hovering Mechanics

        if (!IsBoundToIsland)
        {
            Island hoveredIsland = IslandManager.instance.GetHoveredIsland();
            if (hoveredIsland != null)
            {
                currentIsland = hoveredIsland;
                UpdateGridSystem(currentIsland.GetComponentInChildren<GridSystem>());
            }
            else
            {
                Island islandForBuildingPreview = IslandManager.instance.GetIslandForBuildingPreview(this);
                if (islandForBuildingPreview != null)
                {
                    currentIsland = islandForBuildingPreview;
                    UpdateGridSystem(currentIsland.GetComponentInChildren<GridSystem>());
                }
            }
        }
        else
        {
            currentIsland = BoundIsland;
            if (gridSystem == null && currentIsland != null)
            {
                UpdateGridSystem(currentIsland.GetComponentInChildren<GridSystem>());
            }
        }

        if (currentIsland != null)
        {
            if (transform.parent != currentIsland.transform)
            {
                transform.SetParent(currentIsland.transform);
            }

            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit, 1000f, groundLayer, QueryTriggerInteraction.Ignore))
            {
                Vector3 newPos = hit.point;
                newPos.y = offsetY;

                if (gridSystem != null)
                {
                    switch (snapMode)
                    {
                        case SnapMode.Grid:
                            newPos = gridSystem.SnapFootprintToGrid(hit.point, GetFootprint());

                            if (buildingData != null
                                && buildingData.requiresQuayFoundation
                                && !PlacementRules.IsPlateauCellAt(gridSystem, newPos))
                            {
                                newPos.y = QuaySystem.GetOrCreate(gridSystem).TopElevationWorld;
                            }
                            break;
                        case SnapMode.Deposit:
                            newPos = gridSystem.GetNearestDepositPosition(newPos);
                            break;
                    }
                }

                transform.position = newPos;
                FaceOutwards();
                UpdateQuayFoundationPreview();
            }
        }
        else
        {
            UpdateGridSystem(null);
            transform.SetParent(null);
        }
        #endregion
    }

    private void UpdateQuayFoundationPreview()
    {
        bool wantsQuay = buildingData != null
            && buildingData.requiresQuayFoundation
            && gridSystem != null
            && !PlacementRules.IsPlateauCellAt(gridSystem, transform.position);

        if (!wantsQuay)
        {
            if (quayPreview != null) quayPreview.Hide();
            return;
        }

        if (quayPreview == null)
        {
            quayPreview = gameObject.AddComponent<QuayFoundationPreview>();
        }

        Vector2Int footprint = GetFootprint();
        quayPreview.Show(
            gridSystem,
            gridSystem.GetFootprintOrigin(transform.position, footprint),
            footprint,
            buildingData.quayFoundationPadding,
            ResolveHologramMaterial(localCanPlace),
            currentBaseTint,
            currentRimTint);
    }

    public Vector2Int GetFootprint()
    {
        return GridSystem.GetFootprint(
            BuildingProperties.ResolveSize(buildingProperties, buildingData),
            transform.rotation);
    }

    public BuildingData GetBuildingData()
    {
        BuildingProperties properties = this.buildingPrefab != null
            ? this.buildingPrefab.GetComponent<BuildingProperties>()
            : null;

        return properties != null ? properties.buildingData : null;
    }

    public BuildingData GetBuildingData2()
    {
        BuildingProperties _buildingProperties = GetPropertiesData();
        BuildingData buildingData = _buildingProperties.buildingData;
        return buildingData;
    }

    public BuildingProperties GetPropertiesData()
    {
        BuildingProperties buildingProperties = this.buildingPrefab.GetComponent<BuildingProperties>();
        return buildingProperties;
    }

    public BuildingPreview GetBuildingPreview()
    {
        return this;
    }

    public Vector3 GetBuildingPosition()
    {
        return transform.position;
    }

    public void TransferValuesToFinalBuilding(BuildingProperties finalBuildingProperties)
    {
        if (finalBuildingProperties != null)
        {
            finalBuildingProperties.currentIsland = currentIsland;
            finalBuildingProperties.gridSystem = gridSystem;
        }
    }
}
