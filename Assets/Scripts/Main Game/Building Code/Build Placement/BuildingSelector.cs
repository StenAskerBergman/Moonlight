using UnityEngine;

public class BuildingSelector : MonoBehaviour
{
    #region Variables
    [SerializeField] private BuildingChecker buildingChecker;
    [SerializeField] private BuildingRequirements buildingRequirements;
    [SerializeField] private GameObject buildingPreviewPrefab;
    [SerializeField] private Vector3 buildingPlacementSize;

    public GameObject previewPrefab;
    private BuildingPreview currentPreview;
    #endregion

    #region Start Method
    void Start()
    {
        // Find the BuildingChecker 
        buildingChecker = FindObjectOfType<BuildingChecker>();
        if (buildingChecker == null)
        {
            Debug.LogError("BuildingChecker not found in the scene.");
        }
        
        // Find the BuildingRequirements
        buildingRequirements = FindObjectOfType<BuildingRequirements>();
        if (buildingRequirements == null)
        {
            Debug.LogError("BuildingRequirements not found in the scene.");
        }

        // With Both the BuildingChecker & BuildingRequirements in the scene
        // We can now start the Building Selection & Placement Process
    }
    #endregion

    #region Grid Methods
    private GridSystem GetCurrentIslandGridSystem()
    {
        Island currentIsland = IslandManager.instance.GetIslandInFrontOfCamera(Camera.main);
        if (currentIsland != null)
        {
            return currentIsland.GetComponentInChildren<GridSystem>();
        }
        return null;
    }
    #endregion

    #region Selector Method
    
    /// <summary>
    /// Method used on buttons via On Click event
    /// </summary>
    /// <param name="buildingPrefab"></param>
    
    public void SpawnPreview(GameObject buildingPrefab)
    {
        // Destroy any current preview object
        CancelPreview();

        GameObject previewObject = Instantiate(buildingPreviewPrefab);
        BuildingPreview bp = previewObject.GetComponent<BuildingPreview>();

        if (bp == null)
        {
            Debug.LogError("BuildingPreview component not found on the building preview prefab.");
            CancelPreview();
            return;
        }

        // Set the correct gridSystem for the new BuildingPreview
        GridSystem currentIslandGridSystem = GetCurrentIslandGridSystem();
        if (currentIslandGridSystem != null)
        {
            bp.UpdateGridSystem(currentIslandGridSystem);
        }

        bp.SetBuildingPrefab(buildingPrefab);       // Set the building prefab that the preview represents
        // buildingRequirements.SetRequirements(bp);   // Set the building requirements for the preview
        buildingChecker.StartPlacingBuilding(bp);   // Start the process of placing the building 

        // Set current preview object
        currentPreview = bp;
        previewPrefab = buildingPrefab;
    }

    #endregion

    #region Cancel Preview
    /// <summary>
    /// Cancels the current Preview Building, used by the player <br> 
    /// </br> Used by BuildingSelector.cs, BuildingChecker.cs, and BuildingPreview.cs
    /// </summary>
    public void CancelPreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview.gameObject);
            currentPreview = null;
        }
    }
    #endregion
}
