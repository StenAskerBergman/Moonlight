using UnityEngine;

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
    [SerializeField]private float offsetY; // Can Add offsetY variable
    public float size = 1f;
    [SerializeField] private Material truePlacement;
    [SerializeField] private Material falsePlacement;

    public enum SnapMode
    {
        Grid,
        Deposit
    }

    private void Start()
    {
        IslandManager.instance.OnGridSystemDetected += OnGridSystemDetected;
        IslandManager.instance.OnPlayerEnterIsland += OnPlayerEnterIsland;
    }
    private void OnDestroy()
    {
        IslandManager.instance.OnGridSystemDetected -= OnGridSystemDetected;
        IslandManager.instance.OnPlayerEnterIsland -= OnPlayerEnterIsland;
    }
    private void OnPlayerEnterIsland(Island island)
    {
        currentIsland = island;
    }

    private void OnGridSystemDetected(GridSystem detectedGridSystem)
    {
        gridSystem = detectedGridSystem;
    }

    #region Color & Render

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

    #endregion


    public void SetRendererEnabled(bool isEnabled)
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            renderer.enabled = isEnabled;
        }
    }


    // BUILDING PREFAB RELATED
    public void SetBuildingPrefab(GameObject buildingPrefab)
    {
        this.buildingPrefab = buildingPrefab;
        if (currentIsland != null)
        {
            transform.SetParent(currentIsland.transform); // Set Parent
        }
        // Debug.Log("Building prefab set to: " + buildingPrefab.name); // Debug line
    }

    public GameObject GetBuildingPrefab()
    {
        return buildingPrefab;
    }
    public void UpdateGridSystem(GridSystem newGridSystem)
    {
        gridSystem = newGridSystem;
    }
    
    // Update Method Starts
public void Update()
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
}
    
    // Update Method Ends


    public void TransferValuesToFinalBuilding(BuildingProperties finalBuildingProperties)
    {
        if (finalBuildingProperties != null)
        {
            finalBuildingProperties.currentIsland = currentIsland;
            finalBuildingProperties.gridSystem = gridSystem;
        }
    }
}