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
                Dictionary<ItemData, int> costItems;
                if (buildingCostPrefab != null && boatInv != null && buildingCostPrefab.TryGetCosts(out costItems))
                {
                    Dictionary<ItemData, int> boatItems = boatInv.GetAllItems();
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

        // Credits are the other half of the price. Bank charges them from its
        // OnBuildingPlaced handler, which only runs once the building already exists, so
        // without this the balance simply went negative and nothing could refuse it.
        if (!CanAffordCredits(buildingCostPrefab))
        {
            buildingChecker.CancelBuilding();
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

        // Credits come out here, in the same method that just verified they were there.
        ChargeCredits(buildingCost);

        if (isUnsettledIsland && foundingBoat != null)
        {
            UnitInventory boatInv = foundingBoat.GetComponent<UnitInventory>();
            Dictionary<ItemData, int> paidCostItems;
            if (buildingCost != null && boatInv != null && buildingCost.TryGetCosts(out paidCostItems))
            {
                foreach (var kvp in paidCostItems)
                {
                    if (kvp.Key != null && kvp.Value > 0)
                    {
                        boatInv.RemoveItem(kvp.Key, kvp.Value);
                    }
                }
            }

            // --- WIRING SETTLEMENT.CS ---
            // Trigger the boat's Settlement behavior to execute cargo transfer
            // now that the warehouse cost has been paid.
            Settlement boatSettlement = foundingBoat.GetComponent<Settlement>();
            if (boatSettlement != null)
            {
                boatSettlement.CompleteSettlement(currentBaseStorageManager);
            }
            // ----------------------------
        }
        else if (buildingCost != null && currentBaseStorageManager != null)
        {
            DeductCosts(buildingCost, currentBaseStorageManager); 
        }
        // Resolved here rather than read from the placer's own gridSystem field, which
        // nothing assigns during this flow. It was null, so MarkGridCells threw and took
        // the rest of placement down with it - including the influence zone registration
        // below, which is what actually settles the island.
        GridSystem placementGrid = buildingPreview.gridSystem;
        if (placementGrid == null && islandTransform != null)
        {
            placementGrid = islandTransform.GetComponent<GridSystem>()
                         ?? islandTransform.GetComponentInChildren<GridSystem>();
        }

        // One footprint and one origin, resolved through the grid's own convention, then
        // used both for the reserved cells and for the quay. Deriving them twice is how
        // the quay came to sit half a cell away from the building it belongs to.
        Vector2Int footprint = GridSystem.GetFootprint(
            BuildingProperties.ResolveSize(
                buildingProperties,
                buildingProperties != null ? buildingProperties.buildingData : null),
            buildingInstance.transform.rotation);

        if (buildingProperties != null
            && buildingProperties.buildingData != null
            && buildingProperties.buildingData.requiresQuayFoundation
            && placementGrid != null)
        {
            QuaySystem quay = QuaySystem.GetOrCreate(placementGrid);
            Vector3 quayPosition = buildingInstance.transform.position;
            quayPosition.y = quay.TopElevationWorld;
            buildingInstance.transform.position = quayPosition;

            Building quayOwner = buildingInstance.GetComponent<Building>();
            quay.RegisterAutomaticFoundation(
                quayOwner,
                placementGrid.GetFootprintOrigin(buildingInstance.transform.position, footprint),
                footprint,
                buildingProperties.buildingData.quayFoundationPadding);
        }

        MarkGridCells(buildingInstance, footprint, placementGrid);

        // Lead the placement thud by one perceptible instant with material pushed out from
        // every edge of the footprint. Quay/offshore construction uses suspended sediment;
        // ordinary terrain uses a heavier one-second dirt displacement.
        BuildingPlacementImpact.Play(
            buildingInstance,
            placementGrid,
            footprint,
            IsUnderwaterPlacement(buildingProperties, placementGrid, buildingInstance.transform.position));

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

    private static bool IsUnderwaterPlacement(
        BuildingProperties properties,
        GridSystem placementGrid,
        Vector3 position)
    {
        BuildingData data = properties != null ? properties.buildingData : null;
        if (data != null && data.requiresQuayFoundation) return true;

        string buildingType = data != null ? data.buildingType : null;
        if (buildingType == BuildingEnums.BuildingType.OffShore.ToString()
            || buildingType == BuildingEnums.BuildingType.DeepSea.ToString())
        {
            return true;
        }

        Cell cell = placementGrid != null ? placementGrid.GetCellAtWorldPosition(position) : null;
        return cell != null && cell.IsUnderwater;
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
        if (buildingPreview == null)
        {
            Debug.LogError("BuildingPreview component not found.");
            return false;
        }

        // Validate the island the blueprint is actually standing on. This used to ask
        // IslandManager for whatever island happened to be in front of the camera, so a
        // perfectly good site was rejected purely because of the angle the player was
        // looking from - and it always failed outright since IslandManager.islands is
        // never populated.
        Island currentIsland = buildingPreview.currentIsland;

        if (currentIsland == null && islandTransform != null)
        {
            currentIsland = islandTransform.GetComponent<Island>()
                         ?? islandTransform.GetComponentInParent<Island>();
        }

        if (currentIsland == null && islandManager != null)
        {
            currentIsland = islandManager.GetIslandInFrontOfCamera(Camera.main);
        }

        if (currentIsland == null)
        {
            Debug.Log("No island found for the building site.");
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

        // Buildings whose art has not been made yet get a generated stand-in model, so a new
        // building is never an invisible object on the island. Does nothing once real art exists.
        BuildingPlaceholderModel.Ensure(buildingInstance);

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

    /// <summary>
    /// Takes the building's credit price. The affordability gate above has already run, so
    /// this is expected to succeed; a refusal here means something spent the balance
    /// between the check and the build and is worth hearing about.
    /// </summary>
    private void ChargeCredits(BuildingCost buildingCost)
    {
        if (buildingCost == null || bank == null) return;

        int price = buildingCost.GetPrice();
        if (price <= 0) return;

        if (!bank.TrySpend(price, buildingCost.GetBuildingName()))
        {
            Debug.LogWarning("Balance changed between approving and building " +
                             buildingCost.GetBuildingName() + " - it was not charged.", buildingCost);
        }
    }

    /// <summary>
    /// Whether the player can pay this building's credit price. Answers true when there is
    /// no bank or no price, so a project that has not wired credits up yet is not blocked
    /// from building anything.
    /// </summary>
    private bool CanAffordCredits(BuildingCost buildingCost)
    {
        if (buildingCost == null) return true;

        if (bank == null) bank = FindObjectOfType<Bank>();
        if (bank == null) return true;

        int price = buildingCost.GetPrice();
        if (bank.CanAfford(price)) return true;

        Debug.Log($"Not enough credits for {buildingCost.GetBuildingName()}: " +
                  $"{price} needed, {bank.Balance} available.");
        return false;
    }

    private void DeductCosts(BuildingCost buildingCost, BaseStorageManager currentBaseStorageManager)
    {
        // Use the local BaseStorageManager directly
        if (!currentBaseStorageManager.DeductBuildingCosts(buildingCost, bank))
        {
            Debug.LogError("Failed to deduct costs for building.");
        }
    }


    private void MarkGridCells(GameObject buildingInstance, Vector2Int footprint, GridSystem placementGrid)
    {
        if (placementGrid == null)
        {
            Debug.LogWarning("BuildingPlacer: no GridSystem for the placed building - its cells were not reserved.");
            return;
        }

        Vector3Int origin = placementGrid.GetFootprintOrigin(buildingInstance.transform.position, footprint);
        Building building = buildingInstance.GetComponent<Building>();

        for (int x = 0; x < footprint.x; x++)
        {
            for (int z = 0; z < footprint.y; z++)
            {
                // Cell indices are grid-local and a cell's centre is index + 0.5. This
                // used to multiply the index by cellSize and use it as a WORLD position,
                // ignoring the island's own transform, so it reserved cells belonging to
                // whatever happened to sit at those world coordinates.
                placementGrid.MarkCellAsOccupied(
                    placementGrid.GetCellCenterWorld(origin.x + x, origin.z + z), building);
            }
        }
    }
}
