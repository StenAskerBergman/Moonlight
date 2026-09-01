using UnityEngine;

public class BuildingPreview : MonoBehaviour
{    
    // Building Data
    // public BuildingData buildingData; 

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
    [SerializeField]private float offsetY; // Can Add offsetY variable
    public float size = 1f;
    
    // Mats.
    [SerializeField] private Material truePlacement;
    [SerializeField] private Material falsePlacement;

    [Header("Facing Arrow")]
    [Tooltip("Turn the blueprint so its front points away from the island, out to sea.")]
    [SerializeField] private bool faceAwayFromIsland = true;
    [SerializeField] private float arrowLength = 5f;
    [SerializeField] private float arrowHeight = 2.5f;
    [SerializeField] private Color arrowValidColor = new Color(0.25f, 1f, 0.35f, 0.85f);
    [SerializeField] private Color arrowInvalidColor = new Color(1f, 0.25f, 0.25f, 0.85f);

    private GameObject facingArrow;
    private Material facingArrowMaterial;
    private GridSystem visibleBuildGrid;
    private Island centerCachedIsland;
    private Vector3 cachedIslandCenter;

    // Building Data
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

            buildingData = GetBuildingData();
            buildingProperties = GetPropertiesData();

            // Blueprints for buildings without art would otherwise be invisible, leaving nothing
            // for SetPreviewMaterial to tint green/red. Built after buildingData is resolved so
            // the stand-in matches the real footprint.
            BuildingPlaceholderModel.Ensure(gameObject, withCollider: false);

            buildingRequirements = FindObjectOfType<BuildingRequirements>();
            if (buildingRequirements != null)
            {
                buildingRequirements.SetRequirements(this);   // Set the building requirements for the preview
            }

            IslandManager.instance.OnGridSystemDetected += OnGridSystemDetected;
            IslandManager.instance.OnPlayerEnterIsland += OnPlayerEnterIsland;
        }
        private void OnDestroy()
        {
            SetVisibleBuildGrid(null);
            IslandManager.instance.OnGridSystemDetected -= OnGridSystemDetected;
            IslandManager.instance.OnPlayerEnterIsland -= OnPlayerEnterIsland;
        }

    #endregion
    
    #region Events + GridSystem
    
    private void OnPlayerEnterIsland(Island island)
    {
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
        if (visibleBuildGrid != null)
        {
            visibleBuildGrid.SetBuildGridVisible(true);
        }
    }

    #endregion

    #region Render Related

    // Preview Color & Render Methods
    public void SetPreviewMaterial(bool canPlace)
    {
        localCanPlace = canPlace;

        EnsureFacingArrow();

        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        Material targetMaterial = canPlace ? truePlacement : falsePlacement;

        foreach (MeshRenderer renderer in renderers)
        {
            // The arrow is signage, not part of the silhouette, so it keeps its own
            // material and only follows the valid/invalid colour.
            if (facingArrow != null && renderer.gameObject == facingArrow) continue;

            renderer.material = targetMaterial;
        }

        OverlayMaterial.SetColor(facingArrowMaterial, canPlace ? arrowValidColor : arrowInvalidColor);
    }

    #region Facing Arrow

    private void EnsureFacingArrow()
    {
        if (facingArrow != null) return;

        facingArrow = new GameObject("Placement Facing Arrow");
        facingArrow.transform.SetParent(transform, false);
        facingArrow.transform.localPosition = new Vector3(0f, arrowHeight, 0f);

        facingArrow.AddComponent<MeshFilter>().sharedMesh = BuildArrowMesh(arrowLength);

        MeshRenderer arrowRenderer = facingArrow.AddComponent<MeshRenderer>();
        arrowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        arrowRenderer.receiveShadows = false;

        facingArrowMaterial = OverlayMaterial.Create(arrowValidColor);
        arrowRenderer.sharedMaterial = facingArrowMaterial;
    }

    /// <summary>
    /// A flat arrow lying in the XZ plane pointing along local +Z, so it reads as the
    /// direction the building faces when seen from the game's overhead camera.
    /// </summary>
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

    /// <summary>
    /// Centre of the island the blueprint is over, so "outwards" can be resolved as
    /// "away from the island", which for a harbor means facing the open water.
    /// </summary>
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

        Vector3 outward = transform.position - GetIslandCenter(currentIsland);
        outward.y = 0f;

        if (outward.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(outward.normalized, Vector3.up);
        }
    }

    #endregion

    public void SetRendererColor(Color color)
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            renderer.material.color = color;
        }
    }


    public void SetRendererEnabled(bool isEnabled)
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            renderer.enabled = isEnabled;
        }
    }

    #endregion

    private GameObject _clonedVisuals;

    // BUILDING PREFAB RELATED
    public void SetBuildingPrefab(GameObject buildingPrefab)
    {
        this.buildingPrefab = buildingPrefab;
        if (currentIsland != null)
        {
            transform.SetParent(currentIsland.transform); // Set Parent
        }

        // Generate 1:1 visual silhouette clone from target building prefab
        if (buildingPrefab != null)
        {
            if (_clonedVisuals != null)
            {
                Destroy(_clonedVisuals);
            }

            _clonedVisuals = Instantiate(buildingPrefab, transform.position, transform.rotation, transform);
            _clonedVisuals.name = $"{buildingPrefab.name}_GhostSilhouette";

            // Strip logic, physics, and audio from the visual ghost
            foreach (var col in _clonedVisuals.GetComponentsInChildren<Collider>(true)) Destroy(col);
            var mbs = _clonedVisuals.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = mbs.Length - 1; i >= 0; i--)
            {
                if (mbs[i] != null) Destroy(mbs[i]);
            }
            foreach (var aud in _clonedVisuals.GetComponentsInChildren<AudioSource>(true)) Destroy(aud);

            // Hide the default placeholder cube renderer so only target geometry shows
            MeshRenderer baseRenderer = GetComponent<MeshRenderer>();
            if (baseRenderer != null) baseRenderer.enabled = false;
        }

        SetPreviewMaterial(localCanPlace);
    }


    public GameObject GetBuildingPrefab()
    {
        return buildingPrefab;
    }

    // Update Method Starts
    public void Update()
    {
        #region Hovering Mechanics 
            
        // Assigns Island
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
        
        // Assigns Parent
            if (currentIsland != null)
            {
                transform.SetParent(currentIsland.transform);

                RaycastHit hit;
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                // Same reason as BuildingChecker: the island's tall hover trigger sits on
                // the Ground layer and would otherwise catch the ray before the terrain.
                if (Physics.Raycast(ray, out hit, 1000f, groundLayer, QueryTriggerInteraction.Ignore))
                {
                    Vector3 newPos = hit.point;
                    newPos.y = offsetY;

                    if (currentIsland != null)
                    {
                        switch (snapMode)
                        {
                            case SnapMode.Grid:
                                newPos = gridSystem.GetNearestPointOnGrid(newPos);
                                break;
                            case SnapMode.Deposit:
                                newPos = gridSystem.GetNearestDepositPosition(newPos);
                                break;
                        }
                    }

                    transform.position = newPos;
                    FaceOutwards();
                }
            }
            else
            {
                UpdateGridSystem(null);
                transform.SetParent(null);
            }
        #endregion
    }
    // Update Method Ends

    // Get Building Data 
    public BuildingData GetBuildingData()
    {
        // Both may legitimately be absent: not every building prefab carries a
        // BuildingData asset, and a null here means "no data driven rules", not a fault.
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

    // Get Building Properties
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
