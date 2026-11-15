using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingPlacer : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private Bank bank;
    [SerializeField] private BuildingChecker buildingChecker;
    [SerializeField] private BuildingRequirements buildingRequirements;

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
        if (islandManager != null)
        {
            islandManager.OnGridSystemDetected += OnGridSystemDetected;
            islandManager.OnPlayerEnterIsland += OnPlayerEnterIsland;
        }
        if (bank == null) bank = FindObjectOfType<Bank>();

        EnsureDependenciesAreSet();
    }

    private void EnsureDependenciesAreSet()
    {
        buildingRequirements = buildingRequirements ?? GetComponent<BuildingRequirements>();
        buildingChecker = buildingChecker ?? GetComponent<BuildingChecker>();
    }

    private void UnsubscribeFromEvents()
    {
        if (islandManager != null)
        {
            islandManager.OnGridSystemDetected -= OnGridSystemDetected;
            islandManager.OnPlayerEnterIsland -= OnPlayerEnterIsland;
        }
    }

    private void OnPlayerEnterIsland(Island island)
    {
        currentIsland = island;
    }

    private void OnGridSystemDetected(GridSystem detectedGridSystem)
    {
        gridSystem = detectedGridSystem;
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

    // Logic for placing the building
    public void PlaceBuilding(BuildingPreview buildingPreview, Transform islandTransform)
    {
        if (buildingPreview == null || buildingPreview.buildingPrefab == null)
        {
            Debug.LogError("BuildingPreview component or prefab not found.");
            return;
        }

        // Validate the island the blueprint is actually standing on.
        Island placedIsland = buildingPreview.currentIsland;
        if (placedIsland == null && islandTransform != null)
        {
            placedIsland = islandTransform.GetComponent<Island>()
                        ?? islandTransform.GetComponentInParent<Island>();
        }
        if (placedIsland == null && islandManager != null)
        {
            placedIsland = islandManager.GetIslandInFrontOfCamera(Camera.main);
        }
        if (placedIsland == null)
        {
            Debug.Log("No island found for the building site.");
            return;
        }

        Transform resolvedIslandTransform = islandTransform != null ? islandTransform : placedIsland.transform;

        // Resolved here rather than read from the placer's own gridSystem field, which
        // nothing assigns during this flow.
        GridSystem placementGrid = buildingPreview.gridSystem;
        if (placementGrid == null && resolvedIslandTransform != null)
        {
            placementGrid = resolvedIslandTransform.GetComponent<GridSystem>()
                         ?? resolvedIslandTransform.GetComponentInChildren<GridSystem>();
        }
        if (placementGrid == null)
        {
            Debug.LogWarning("BuildingPlacer: no GridSystem found for placement.");
            return;
        }

        if (bank == null) bank = FindObjectOfType<Bank>();
        Inventory islandGoods = placedIsland.GetComponent<Inventory>();
        GameObject prefab = buildingPreview.buildingPrefab;
        BuildingProperties prefabProps = prefab.GetComponent<BuildingProperties>();
        BuildingData prefabData = prefabProps != null ? prefabProps.buildingData : null;
        BuildingCost buildingCostPrefab = prefab.GetComponent<BuildingCost>();

        // One footprint and one origin resolved through the grid's own convention.
        Vector2Int footprint = GridSystem.GetFootprint(
            BuildingProperties.ResolveSize(prefabProps, prefabData),
            buildingPreview.transform.rotation);
        Vector3 snappedPos = placementGrid.SnapFootprintToGrid(buildingPreview.transform.position, footprint);
        Vector3Int gridOrigin = placementGrid.GetFootprintOrigin(snappedPos, footprint);

        Unit preferredBoat = buildingChecker != null ? buildingChecker.ActiveSettlingBoat : null;
        PlacementValidationResult validation = PlacementEvaluator.Evaluate(new AllocationRequest
        {
            Prefab = prefab,
            Properties = prefabProps,
            Data = prefabData,
            Cost = buildingCostPrefab,
            Grid = placementGrid,
            Island = placedIsland,
            WorldPosition = snappedPos,
            GridOrigin = gridOrigin,
            Footprint = footprint,
            IslandGoods = islandGoods,
            Bank = bank,
            PreferredBoat = preferredBoat
        });

        if (!validation.CanCommit)
        {
            Debug.Log($"Placement refused ({validation.FailureReason}): {validation.FormatReasonLabel()}");
            // Keep preview active so the player sees the red blocked state and reason instead of losing build mode.
            return;
        }

        // Verify & charge both credits and island/boat goods atomically BEFORE instantiating.
        if (buildingCostPrefab != null &&
            !buildingCostPrefab.TryPayAtomic(islandGoods, bank, validation.FoundingBoat, validation.IsUnsettledHarbor))
        {
            Debug.LogWarning($"Payment declined atomically for {buildingCostPrefab.GetBuildingName()}.");
            return;
        }

        if (validation.IsUnsettledHarbor && validation.FoundingBoat != null)
        {
            // Trigger the boat's Settlement behavior to execute cargo transfer
            // now that the harbor cost has been paid.
            Settlement boatSettlement = validation.FoundingBoat.GetComponent<Settlement>();
            if (boatSettlement != null)
            {
                boatSettlement.CompleteSettlement(islandGoods);
            }
        }

        // Spawn Building
        GameObject buildingInstance = InstantiateBuilding(buildingPreview, resolvedIslandTransform, snappedPos);
        if (buildingInstance == null)
        {
            Debug.LogError("Failed to instantiate building.");
            return;
        }

        BuildingProperties buildingProperties = SetBuildingProperties(buildingInstance, placedIsland, placementGrid);

        if (buildingProperties != null
            && buildingProperties.buildingData != null
            && buildingProperties.buildingData.requiresQuayFoundation)
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

        // Register Influence Zone
        InfluenceZone zone = buildingInstance.GetComponent<InfluenceZone>();
        if (zone != null)
        {
            InfluenceManager manager = resolvedIslandTransform.GetComponent<InfluenceManager>()
                                    ?? placedIsland.GetComponent<InfluenceManager>()
                                    ?? placedIsland.gameObject.AddComponent<InfluenceManager>();
            manager.RegisterZone(zone);
        }

        InitializePlacedBuilding(buildingInstance);
        ConfirmPlacement?.Invoke(buildingInstance);

        if (buildingChecker != null) buildingChecker.CancelBuilding();

        // Founding hands the player their new settlement: the construction window opens so
        // they can start spending the cargo that was just unloaded.
        if (validation.IsUnsettledHarbor)
        {
            StartCoroutine(OpenConstructionWindowAfterFounding());
        }
    }

    /// <summary>
    /// Opens the construction window ("HUD (Bot Building Window)", wired to
    /// UnitSelections.builderUIPanel). Before this, the only way in was the action bar's
    /// Build button, and nothing opened it after founding.
    ///
    /// Waits out the frame of the placement click, so that same click cannot reach the UI
    /// as a click on empty ground and close the window straight away.
    /// </summary>
    private System.Collections.IEnumerator OpenConstructionWindowAfterFounding()
    {
        yield return null;
        yield return null;

        GameObject window = UnitSelections.Instance != null ? UnitSelections.Instance.builderUIPanel : null;
        if (window == null)
        {
            Debug.LogWarning("Founding: no construction window to open - UnitSelections.builderUIPanel is not assigned.");
            yield break;
        }

        window.SetActive(true);
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

    private GameObject InstantiateBuilding(BuildingPreview buildingPreview, Transform islandTransform, Vector3 targetPosition)
    {
        Quaternion targetRotation = buildingPreview.transform.rotation;
        GameObject buildingInstance = Instantiate(buildingPreview.buildingPrefab, targetPosition, targetRotation, islandTransform);

        // Buildings whose art has not been made yet get a generated stand-in model, so a new
        // building is never an invisible object on the island. Does nothing once real art exists.
        BuildingPlaceholderModel.Ensure(buildingInstance);

        return buildingInstance;
    }

    private BuildingProperties SetBuildingProperties(
        GameObject buildingInstance,
        Island placedIsland,
        GridSystem placementGrid)
    {
        BuildingProperties buildingProperties = buildingInstance.GetComponent<BuildingProperties>();
        if (buildingProperties != null)
        {
            buildingProperties.currentIsland = placedIsland;
            buildingProperties.gridSystem = placementGrid;
        }
        return buildingProperties;
    }

    public List<ItemCost> costs; // Represents the cost of items required for the building

    public List<ItemCost> GetAllCostItems()
    {
        return costs;
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
