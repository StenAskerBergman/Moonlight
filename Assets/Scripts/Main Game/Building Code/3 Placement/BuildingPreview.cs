using UnityEngine;

public class BuildingPreview : MonoBehaviour
{
    // Inspector Settings and Adjustments 
    // And all the variable used in the code

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


    // public Vector3 GetNearestPointOnGrid(Vector3 position)
    // {
    //     position -= offset;

    //     int xCount = Mathf.RoundToInt(position.x / size);
    //     int yCount = Mathf.RoundToInt(position.y / size);
    //     int zCount = Mathf.RoundToInt(position.z / size);

    //     Vector3 result = new Vector3(
    //         (float)xCount * size,
    //         (float)yCount * size,
    //         (float)zCount * size);

    //     result += offset;
    //     result.y += offsetY; // Set the Y value of the result to offsetY

    //     return result;
    // }

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

    // Preview Color & Render Methods
    
    public void SetPreviewMaterial(bool canPlace)
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        Material targetMaterial = canPlace ? falsePlacement : truePlacement;

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
    // Guide: How to implement SetRendererColor Method
    // Color previewColor = canPlace ? Color.green : Color.red; // Old
    // currentBuildingPreview.SetRendererColor(previewColor);

    //public void SetRendererColorByBool(bool canPlace, Color color)
    //{
    //    Color previewColor = canPlace ? Color.green : Color.red;
    //    MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
    //    foreach (MeshRenderer renderer in renderers)
    //    {
    //        renderer.material.color = color;
    //    }
    //}

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