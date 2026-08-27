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

            buildingRequirements = FindObjectOfType<BuildingRequirements>();
            buildingRequirements.SetRequirements(this);   // Set the building requirements for the preview

            IslandManager.instance.OnGridSystemDetected += OnGridSystemDetected;
            IslandManager.instance.OnPlayerEnterIsland += OnPlayerEnterIsland;
        }
        private void OnDestroy()
        {
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
        gridSystem = detectedGridSystem;
    }

    public void UpdateGridSystem(GridSystem newGridSystem)
    {
        gridSystem = newGridSystem;
    }

    #endregion

    #region Render Related

    // Preview Color & Render Methods
    public void SetPreviewMaterial(bool canPlace)
    {
        localCanPlace = canPlace;
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        Material targetMaterial = canPlace ? truePlacement : falsePlacement;

        foreach (MeshRenderer renderer in renderers)
        {
            renderer.material = targetMaterial;
        }
    }

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
            foreach (var mb in _clonedVisuals.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
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

                if (Physics.Raycast(ray, out hit, 1000f, groundLayer))
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
        BuildingData buildingData = this.buildingPrefab.GetComponent<BuildingProperties>().buildingData;
        return buildingData;
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