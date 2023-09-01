using UnityEngine;

public class BuildingSelector : MonoBehaviour
{
    #region Variables
    [SerializeField] private BuildingChecker buildingChecker;
    [SerializeField] private GameObject buildingPreviewPrefab;
    [SerializeField] private Vector3 buildingPlacementSize;

    public GameObject previewPrefab;
    private BuildingPreview currentPreview;
    #endregion

    #region Start Method
    void Start()
    {
        buildingChecker = FindObjectOfType<BuildingChecker>();
        if (buildingChecker == null)
        {
            Debug.LogError("BuildingChecker not found in the scene.");
        }
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
    /// Method used on buttons via onclick event
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
            return;
        }

        // Set the correct gridSystem for the new BuildingPreview
        GridSystem currentIslandGridSystem = GetCurrentIslandGridSystem();
        if (currentIslandGridSystem != null)
        {
            bp.UpdateGridSystem(currentIslandGridSystem);
        }

        bp.SetBuildingPrefab(buildingPrefab);
        buildingChecker.StartPlacingBuilding(bp);

        // Set current preview object
        currentPreview = bp;
    }
    #endregion

    #region Cancel Preview
    /// <summary>
    /// Cancels the current Preview Building, used by the player
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
