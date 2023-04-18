using UnityEngine;

public class BuildingSelector : MonoBehaviour
{
    [SerializeField] private BuildingChecker buildingChecker;
    [SerializeField] private GameObject buildingPreviewPrefab;
    
    private BuildingPreview currentPreview;

    void Start()
    {
        buildingChecker = FindObjectOfType<BuildingChecker>();
        if (buildingChecker == null)
        {
            Debug.LogError("BuildingMover not found in the scene.");
        }
    }

    private GridSystem GetCurrentIslandGridSystem()
    {
        Island currentIsland = IslandManager.instance.GetIslandInFrontOfCamera(Camera.main);
        if (currentIsland != null)
        {
            return currentIsland.GetComponentInChildren<GridSystem>();
        }
        return null;
    }

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


    public void CancelPreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview.gameObject);
            currentPreview = null;
        }
    }
}
