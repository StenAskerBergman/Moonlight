using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingPlacer : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private Bank bank;
    [SerializeField] private BuildingChecker buildingChecker;
    [SerializeField] private BaseStorageManager baseStorageManager;
    [SerializeField] private BuildingRequirements buildingRequirements;

    [Header("Inventories")]
    [SerializeField] private Inventory islandInventory;
    [SerializeField] private Inventory playerInventory;

    private bool canAfford;
    private string playerBaseID;
    private Island currentIsland;
    private GridSystem gridSystem;
    private IslandManager islandManager;

    public delegate void OnConfirmPlacement(GameObject previewObject);
    public event OnConfirmPlacement ConfirmPlacement;

    private void Start()
    {
        InitializeBuildingPlacer();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void InitializeBuildingPlacer()
    {
        islandManager = IslandManager.instance;
        islandManager.OnGridSystemDetected += OnGridSystemDetected;
        islandManager.OnPlayerEnterIsland += OnPlayerEnterIsland;

        EnsureDependenciesAreSet();
    }

    private void EnsureDependenciesAreSet()
    {
        buildingRequirements = buildingRequirements ?? GetComponent<BuildingRequirements>();
        buildingChecker = buildingChecker ?? GetComponent<BuildingChecker>();
    }

    private void UnsubscribeFromEvents()
    {
        islandManager.OnGridSystemDetected -= OnGridSystemDetected;
        islandManager.OnPlayerEnterIsland -= OnPlayerEnterIsland;
    }

    private void OnPlayerEnterIsland(Island island)
    {
        currentIsland = island;
        UpdateIslandInventory();
    }

    private void OnGridSystemDetected(GridSystem detectedGridSystem)
    {
        gridSystem = detectedGridSystem;
        UpdateIslandInventory();
    }

    private void UpdateIslandInventory()
    {
        if (currentIsland == null || string.IsNullOrEmpty(playerBaseID)) return;
        islandInventory = currentIsland.GetPlayerBaseInventory(playerBaseID);
        // if (islandInventory == null) return;
    }

    private Vector3 CalculateOffset(Vector3 buildingSize)
    {
        float offsetX = IsEven(buildingSize.x) ? gridSystem.cellSize / 2 : 0;
        float offsetZ = IsEven(buildingSize.z) ? gridSystem.cellSize / 2 : 0;
        return new Vector3(offsetX, 0, offsetZ);
    }

    private bool IsEven(float value)
    {
        return value % 2 == 0;
    }

    // A
    private BaseStorageManager FetchBaseStorageManager()
    {

        if(currentIsland == null)
        {
            Debug.Log("No current island found.");
            return null;
        }

        // Assuming you can retrieve the appropriate BaseStorageManager for the current island:
        BaseStorageManager currentIslandStorageManager = currentIsland.GetBaseStorageManager();

        // Null Check
        if (currentIslandStorageManager == null)
        {
            Debug.Log("Failed to retrieve BaseStorageManager for current island.");
            return null;
        }

        return currentIslandStorageManager;
    }
    // B 
    private BaseStorageManager FetchBaseStorageManager(string baseID)
    {
        if (currentIsland == null)
        {
            Debug.Log("No current island found.");
            return null;
        }

        BaseStorageManager baseStorageManager = currentIsland.GetBaseStorageManagerForID(baseID);

        if (baseStorageManager == null)
        {
            Debug.Log("Failed to retrieve BaseStorageManager for baseID: " + baseID);
            return null;
        }

        return baseStorageManager;
    }

    // Logic for placing the building
    public void PlaceBuilding(BuildingPreview buildingPreview, Transform islandTransform)
    {
        // Logic for checking if the placement is valid
        if (!IsValidPlacement(buildingPreview, islandTransform))
        {
            Debug.Log("Invalid Placement!");
            return;
        }

        // Fetch Storage Manager  A
        BaseStorageManager currentBaseStorageManager = FetchBaseStorageManager();
        if (currentBaseStorageManager == null)
        {
            Debug.Log("Attempt A: No BaseStorageManager found.");
            // Fetch Storage Manager B
            currentBaseStorageManager = FetchBaseStorageManager(islandTransform.GetComponent<Island>().ID);
            if (currentBaseStorageManager == null)
            {
                Debug.Log("Attempt B: No BaseStorageManager found.");
                return;

            }
            return;
        }

        // Spawn Building
        GameObject buildingInstance = InstantiateBuilding(buildingPreview, islandTransform);
        if (buildingInstance == null)
        {
            Debug.LogError("Failed to instantiate building.");
            return;
        }

        BuildingProperties buildingProperties = SetBuildingProperties(buildingInstance, buildingPreview);
        BuildingCost buildingCost = buildingInstance.GetComponent<BuildingCost>();

        // Logic for checking if the player can afford the building
        if (!currentBaseStorageManager.CanAffordBuilding(buildingCost))
        {
            Debug.Log("Not Enough Resources!");
            buildingChecker.CancelBuilding();
            return;
        }

        // DeductCosts(buildingCost, currentBaseStorageManager); // Keep Future for Reference
        DeductCosts(buildingCost); 
        MarkGridCells(buildingInstance, buildingProperties.buildingSize);

        // Next Step: Initialize Building!
        buildingChecker.CancelBuilding();
    }
    public bool CheckIfCanAffordBuilding(BuildingCost buildingCost)
    {
        return baseStorageManager.CanAffordBuilding(buildingCost);
    }


    private bool IsValidPlacement(BuildingPreview buildingPreview, Transform islandTransform)
    {
        // Logic for checking if the island exists and if the building preview is valid
        Camera mainCamera = Camera.main;
        Island currentIsland = islandManager.GetIslandInFrontOfCamera(mainCamera);
        
        if (currentIsland == null)
        {
            Debug.Log("No island found in front of the camera.");
            return false;
        }

        if (buildingPreview == null)
        {
            Debug.LogError("BuildingPreview component not found.");
            return false;
        }

        // Other validations...
        return true;
    }


    private GameObject InstantiateBuilding(BuildingPreview buildingPreview, Transform islandTransform)
    {
        // Logic for instantiating the building
        Vector3 targetPosition = buildingPreview.transform.position;
        Quaternion targetRotation = buildingPreview.transform.rotation;
        GameObject buildingInstance = Instantiate(buildingPreview.buildingPrefab, targetPosition, targetRotation, islandTransform);
        return buildingInstance;
    }


    private BuildingProperties SetBuildingProperties(GameObject buildingInstance, BuildingPreview buildingPreview)
    {
        // Logic for setting building properties
        BuildingProperties buildingProperties = buildingInstance.GetComponent<BuildingProperties>();
        buildingProperties.currentIsland = buildingPreview.currentIsland;
        buildingProperties.gridSystem = buildingPreview.gridSystem;
        return buildingProperties;
    }

    public List<ItemCost> costs; // Represents the cost of items required for the building

    public List<ItemCost> GetAllCostItems()
    {
        return costs;
    }

    // Might need this later for reference / if I gotta redo this or something pops up
    //private void DeductCosts(BuildingCost buildingCost, BaseStorageManager baseStorageManager, Bank bank)
    //{
    //    baseStorageManager.DeductBuildingCosts(buildingCost, baseStorageManager.baseStorage, bank);
    //}

    private void DeductCosts(BuildingCost buildingCost)
    {
        // Use the local BaseStorageManager directly
        if (!baseStorageManager.DeductBuildingCosts(buildingCost, bank))
        {
            Debug.LogError("Failed to deduct costs for building.");
        }
    }


    private void MarkGridCells(GameObject buildingInstance, Vector3 buildingSize)
    {
        // Logic for marking grid cells
        Vector3 targetPosition = buildingInstance.transform.position;
        Vector3Int gridPosition = gridSystem.WorldToCell(targetPosition);
        for (int x = 0; x < buildingSize.x; x++)
        {
            for (int z = 0; z < buildingSize.z; z++)
            {
                int targetX = gridPosition.x + x;
                int targetZ = gridPosition.z + z;
                Vector3 targetCellWorldPosition = new Vector3(targetX * gridSystem.cellSize, 0, targetZ * gridSystem.cellSize);
                gridSystem.MarkCellAsOccupied(targetCellWorldPosition, buildingInstance.GetComponent<Building>());
            }
        }
    }
}

