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
            if (islandTransform.GetComponent<Island>() != null)
            {
                currentBaseStorageManager = FetchBaseStorageManager(islandTransform.GetComponent<Island>().ID);
            }
            if (currentBaseStorageManager == null)
            {
                currentBaseStorageManager = islandTransform.GetComponent<BaseStorageManager>();
                if (currentBaseStorageManager == null)
                {
                    currentBaseStorageManager = islandTransform.gameObject.AddComponent<BaseStorageManager>();
                }
            }
        }

        // Get Building Cost from the preview prefab before instantiating
        BuildingCost buildingCostPrefab = buildingPreview != null && buildingPreview.buildingPrefab != null 
            ? buildingPreview.buildingPrefab.GetComponent<BuildingCost>() 
            : null;

        InfluenceManager influenceManager = islandTransform.GetComponent<InfluenceManager>();
        bool isUnsettledIsland = influenceManager == null || !influenceManager.HasWarehouse;
        Unit foundingBoat = null;

        // Logic for checking if the player can afford the building
        if (isUnsettledIsland)
        {
            foundingBoat = InfluenceManager.GetNearestPlayerBoat(buildingPreview.transform.position);
            if (foundingBoat != null)
            {
                UnitInventory boatInv = foundingBoat.GetComponent<UnitInventory>();
                if (buildingCostPrefab != null && buildingCostPrefab.costData != null && boatInv != null)
                {
                    Dictionary<ItemData, int> boatItems = boatInv.GetAllItems();
                    Dictionary<ItemData, int> costItems = buildingCostPrefab.costData.GetCostItemsDictionary();
                    foreach (var kvp in costItems)
                    {
                        if (kvp.Key != null && kvp.Value > 0)
                        {
                            if (!boatItems.ContainsKey(kvp.Key) || boatItems[kvp.Key] < kvp.Value)
                            {
                                Debug.Log("Boat does not have enough resources to found harbor!");
                                buildingChecker.CancelBuilding();
                                return;
                            }
                        }
                    }
                }
            }
        }
        else
        {
            if (buildingCostPrefab != null && currentBaseStorageManager != null && !currentBaseStorageManager.CanAffordBuilding(buildingCostPrefab))
            {
                Debug.Log("Not Enough Resources in Island Storage!");
                buildingChecker.CancelBuilding();
                return;
            }
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

        if (isUnsettledIsland && foundingBoat != null)
        {
            UnitInventory boatInv = foundingBoat.GetComponent<UnitInventory>();
            if (buildingCost != null && buildingCost.costData != null && boatInv != null)
            {
                Dictionary<ItemData, int> costItems = buildingCost.costData.GetCostItemsDictionary();
                foreach (var kvp in costItems)
                {
                    if (kvp.Key != null && kvp.Value > 0)
                    {
                        boatInv.RemoveItem(kvp.Key, kvp.Value);
                    }
                }
            }
        }
        else if (buildingCost != null && currentBaseStorageManager != null)
        {
            DeductCosts(buildingCost, currentBaseStorageManager); 
        }
        MarkGridCells(buildingInstance, buildingProperties.buildingSize);

        // Register Influence Zone (Phase 3)
        InfluenceZone zone = buildingInstance.GetComponent<InfluenceZone>();
        if (zone != null)
        {
            InfluenceManager manager = islandTransform.GetComponent<InfluenceManager>();
            if (manager == null)
            {
                manager = islandTransform.gameObject.AddComponent<InfluenceManager>();
            }
            manager.RegisterZone(zone);
        }

        InitializePlacedBuilding(buildingInstance);

        buildingChecker.CancelBuilding();
    }

    /// <summary>
    /// Puts a freshly placed building onto the construction -> Active path.
    ///
    /// Building.CurrentState defaults to UnderConstruction, and BuildingProductionController
    /// and BuildingOutput both refuse to run until it reaches Active. Nothing else in the
    /// placement flow performed that transition, so every placed producer sat inert. Adding
    /// ConstructionSite here (it creates its own BuildingSimulation) means prefabs do not
    /// each have to remember to carry the construction components.
    /// </summary>
    private void InitializePlacedBuilding(GameObject buildingInstance)
    {
        if (buildingInstance == null) return;

        // ConstructionSite.Start runs autoBuildOnStart on the frame after placement and
        // drives progress to 1.0, which is what flips BuildingSimulation - and through it
        // the legacy Building component - to Active.
        if (buildingInstance.GetComponent<ConstructionSite>() == null)
        {
            buildingInstance.AddComponent<ConstructionSite>();
        }
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

    private void DeductCosts(BuildingCost buildingCost, BaseStorageManager currentBaseStorageManager)
    {
        // Use the local BaseStorageManager directly
        if (!currentBaseStorageManager.DeductBuildingCosts(buildingCost, bank))
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

