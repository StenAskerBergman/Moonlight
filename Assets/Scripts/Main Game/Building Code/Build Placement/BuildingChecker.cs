using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BuildingChecker : MonoBehaviour
{
    #region Variables
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask UI;

    public bool IC = false;
    [SerializeField] private BuildingPreview currentBuildingPreview;

    // True while a building is being positioned. Other placement modes (roads)
    // read this so two of them can't both act on the same click.
    public bool IsPlacingBuilding => currentBuildingPreview != null;
    [SerializeField] private BuildingPlacer buildingPlacer;
    private Island currentIsland;
    private GridSystem gridSystem;
    private GridSystem currentGridSystem;

    public static BuildingChecker instance;

    // Refs 1: 
    [Header("Island Related")]
    [Space(8)]
    // public IslandItems islandItems;  // Relies on Legacy method - Needs to be replaced
    public IslandPower islandPower;
    public IslandEcology islandEcology;


    // Refs 2
    private Inventory playerInventory;
    private Inventory islandInventory;
    private BuildingRequirements buildingRequirements;
    private Vector3 targetPosition;
    private BuildingData _BuildData;
    
    // Tracks the boat currently providing settlement range for the active preview
    private Unit currentSettlingBoat;
    private Unit hoveredBoat;
    #endregion

    #region Awake + Start + OnDestroy

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        canPlace = false;
        
        // Subscribe to event for the current island.
        IslandManager.instance.OnPlayerHoverIsland += OnCurrentIslandChanged;
        IslandManager.instance.OnPlayerEnterIsland += OnCurrentIslandChanged;

    }

    private void OnDestroy()
    {
        // Unsubscribes on Destruction
        IslandManager.instance.OnPlayerHoverIsland -= OnCurrentIslandChanged;
        IslandManager.instance.OnPlayerEnterIsland -= OnCurrentIslandChanged;

    }
    #endregion

    #region On Current Island Changed
    private void OnCurrentIslandChanged(Island island)
    {
        if (island == null)
        {
            Debug.Log("Island = Null");
            return;
        }
        currentIsland = island;
        currentGridSystem = island.GetComponent<GridSystem>();

        // Add a null check for currentBuildingPreview
        if (currentBuildingPreview != null)
        {
            currentBuildingPreview.gridSystem = currentGridSystem;
        }
        else
        {
            // Debug.LogWarning("currentBuildingPreview is null");
            return;
        }

        // Get the system refernces from the island
        islandPower = island.GetComponent<IslandPower>();
        islandEcology = island.GetComponent<IslandEcology>();
        islandInventory = island.GetComponent<Inventory>();
        GetCurrentIslandGridSystem(currentIsland);
    }
    #endregion

    #region StartPlacingBuilding Method
    // Starts this UpdateBuildingSite...
    public void StartPlacingBuilding(BuildingPreview buildingPreview)
    {

        // Assign the new buildingPreview to currentBuildingPreview
        currentBuildingPreview = buildingPreview;

        // Check if the BuildingPreview object has a parent
        if (currentBuildingPreview.transform.parent == null)
        {
            //Debug.Log("Awaiting Parent..."); // BuildingPreview object has no parent. 
            return;
        }

        if (currentBuildingPreview.transform.parent != null)
        {
            // Adopt BuildingPreview object.
            Debug.Log("BuildingPreview Adopted By " + currentBuildingPreview.transform.parent.name);

            GetCurrentIslandGridSystem(currentIsland);
            currentBuildingPreview.SetPreviewMaterial(canPlace); // Start Update the color of the building preview based on canPlace value

            Debug.Log("Parent Found!"); // BuildingPreview object has parent.


            // Get the grid system from the parent object of the BuildingPreview
            gridSystem = currentBuildingPreview.transform.parent.GetComponent<GridSystem>();

            if (gridSystem != null)
            {
                currentBuildingPreview.UpdateGridSystem(gridSystem);
            }
            else
            {
                Debug.Log("Grid system is null. Make sure the grid system is assigned before starting to place a new building.");
            }
        }
    }
    #endregion

    #region Update Method
    private void Update()
    {
        // Check if there is a Building Preview Active...
        if (currentBuildingPreview != null)
        {
            hoveredBoat = ResolveSettlingBoat();
            
            UpdateBuildsite();
            
            currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator

            // Update the visual settlement ring to follow whatever boat we evaluated
            UpdateSettlingBoatRing(hoveredBoat);

            // Shade the grid so the player can see where this building may stand before
            // clicking, rather than discovering it from a refused click. Rebuilds only
            // when the building, the vessel's cell, or the island's influence changes.
            PlacementValidityOverlay.Show(gridSystem, currentIsland, GetPreviewProperties(), hoveredBoat);

            InputCheck();
        }
        else
        {
            UpdateSettlingBoatRing(null); // Ensure it's off if no preview
            PlacementValidityOverlay.HideAll();
        }
    }
    #endregion

    #region Base Requirement Methods

    // A - Nobody Remembers the Past something you will learn one day,
    // along the way to the end of your life.
    private BaseStorageManager FetchBaseStorageManager()
    {

        if (currentIsland == null)
        {
            Debug.Log("No current island found.");
            canPlace = false;
            return null;
        }

        // Assuming you can retrieve the appropriate BaseStorageManager for the current island:
        BaseStorageManager currentIslandStorageManager = currentIsland.GetBaseStorageManager();

        // Null Check
        if (currentIslandStorageManager == null)
        {
            Debug.Log("Failed to retrieve BaseStorageManager for current island.");
            canPlace = false;
            return null;
        }

        return currentIslandStorageManager;
    }

    #endregion

    #region  InputCheck + HandleBuildingPlacement Method

    public bool isVerified;


    //TODO: this method to return with infomation regarding the request made
    //SOLV: out string str_Conditions_Status - Would be cool!
    //ISUE: but I fail to remember how InputCheck reads the "actual" conditions 


    private void InputCheck() 
    {
        // Left-click actions
        if (Input.GetMouseButtonDown(0))
        {
            if (buildingRequirements == null)
            {
                buildingRequirements = GetComponent<BuildingRequirements>();
            }

            if (buildingRequirements != null)
            {
                isVerified = buildingRequirements.Verify();
            }
            else
            {
                isVerified = true;
            }

            // Scenario Cases 

            // Can Place && building requirements is Verified
            if (IC || (canPlace && isVerified))
            {
                HandleBuildingPlacement();
            }
            else if (!canPlace)
            {
                Debug.LogFormat("<color=red>Cant place building - Reason: canPlace: {0}</color>", canPlace);
                CancelClick();
            }
            else if (!isVerified)
            {
                Debug.LogFormat("<color=red>Cant place building - Reason: isVerified: {0}</color>", isVerified);
                CancelClick();
            }
            else
            {
                Debug.LogError("Unknown Error: CanPlace is null");
                CancelClick();
            }
        }

        // Right-click actions
        if (Input.GetMouseButtonDown(1))
        {
            CancelBuilding();
        }
    }

    private void HandleBuildingPlacement()
    {
        Debug.LogFormat("<color=yellow>WARNING: attempting placement - {0}</color>", currentBuildingPreview?.ToString() ?? "null");

        if (currentBuildingPreview != null && currentBuildingPreview.transform.parent != null)
        {
            if (currentBuildingPreview == null && currentBuildingPreview.transform.parent == null)
            { 
                Debug.LogError("Error - Reason: currentBuildingPreview or parent is null");
                return;  
            }

            FetchBaseStorageManager();

            Debug.LogFormat("<color=green>placing building - {0}</color>", canPlace);
            Debug.Log("Building Placer: " + buildingPlacer);
            Debug.Log("Current Building Preview: " + currentBuildingPreview);

            buildingPlacer.PlaceBuilding(currentBuildingPreview, currentBuildingPreview.transform.parent); 
        }
        else
        {
            Debug.LogFormat("<color=red>ERROR: building placement failed - {0}</color>", currentBuildingPreview?.ToString() ?? "null");
            CancelClick();
        }
    }
    #endregion 

    #region Grid Related

    // Update Methods for getting the current selected island grid system
    private GridSystem GetCurrentIslandGridSystem(Island currentIsland)
        {
            // You can replace this with your method for getting the current island's GridSystem
            // For example, if you have an island manager that keeps track of the current island 
            // you can get the GridSystem from there

            if (currentIsland == null)
            {
                Debug.LogWarning("GetCurrentIslandGridSystem: currentIsland is null");
                return null;
            }

            GridSystem gridSystem = currentIsland.GetComponent<GridSystem>();
            currentBuildingPreview.gridSystem = gridSystem;
            return currentBuildingPreview.gridSystem;
        }

        // Minor
        public void UpdateGridSystem(GridSystem newGridSystem)
        {
            gridSystem = newGridSystem;
        }
        
        // How to Use:
        // GridSystem CurrentGrid = GetGridSystem();
        
        private GridSystem GetGridSystem() 
        {
            // Null Check
            if (gridSystem == null)
            {
                Debug.LogWarning("GetGridSystem: currentIsland is null");
                return null;
            }

            return gridSystem;
        }

    #endregion

    #region Update Buildsite
    public bool canPlace; // Can Place Building
    private void UpdateBuildsite()
    {
        // Runs in Update the overall Method

        /* Method Description
         
            // Script Task List
                // 1. Gather a Building Location
                // 2. Determines Build-ability (CanBuild)
                // 3. Determines Compatability (isVerified)

            // Return
                // Location (Position Cell) 
                // CanBuild - (True/False)
                // isVerified - (True/False)
        */

        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // Triggers are ignored deliberately. Each island carries a 40-unit-tall trigger
        // box on the Ground layer for hover detection; hitting that instead of the terrain
        // put the blueprint in mid-air well away from the cursor.
        if (Physics.Raycast(ray, out hit, 1000f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            // Creates a New Position... 
            Vector3 newPos = hit.point;

            #region Default Null Checks

                // Simple Null Ref Check ... on currentBuildingPreview 
                if (currentBuildingPreview == null)
                {
                    // There aint no building preview to place
                    Debug.Log("currentBuildingPreview is null");
                    canPlace = false;
                    currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator - you don't want a future false positive
                    currentBuildingPreview.SetRendererEnabled(false);
                    return;
                }

                // *Not if* currentBuildingPreview is not Null
                // *Since* currentBuildingPreview is not Null I can... update it to the new grid system

                // lesser side note: This is needed because, if the user drags a building from one island to another then we need to account for that

                currentBuildingPreview.UpdateGridSystem(gridSystem);
                GridSystem newGridSystem = GetCurrentIslandGridSystem(currentIsland);

                if (newGridSystem == null)
                {
                    Debug.LogWarning("newGridSystem is null");
                    currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator - you don't want a future false positive
                    currentBuildingPreview.SetRendererEnabled(true);
                    return;
                }

                currentBuildingPreview.gridSystem = newGridSystem;

                // Null Check for currentBuildingPreview for GridSystem
                if (currentBuildingPreview.gridSystem == null)
                {
                    // No Grid System to place the building on
                    Debug.Log("currentBuildingPreview.gridSystem is null");
                    currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
                    currentBuildingPreview.SetRendererEnabled(true);
                    return;
                } 

                // Enable the renderer if the parent object is present
                if (currentBuildingPreview.transform.parent != null)
                {
                    currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator - you don't want a future false positive
                    currentBuildingPreview.SetRendererEnabled(true);

                    // currentIsland.LogBounds(); // Debugg Log for Bounds
                }
                else
                {
                    // Disable the renderer and return early if there is no parent object
                    currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator - you don't want a future false positive
                    currentBuildingPreview.SetRendererEnabled(false);
                    return;
                }

                //if (buildingRequirements != null)
                //{
                    


                //}
                //else
                //{
                //    Debug.Log("buildingRequirements is null");
                //    return;
                //}

            #endregion
            
            // Default Null Checks for Systems are done
            // Now the actual System Logics can start

            // Get the nearest Position to Cursor for 
            // Cell Placement 

            // Everything below reads through newGridSystem, the one just resolved and
            // null checked. It used to snap through newGridSystem and then look the cell
            // up through the gridSystem FIELD, which is only ever assigned when the
            // preview happens to be parented to a GridSystem. Whenever it was null this
            // line threw every frame, UpdateBuildsite bailed before evaluating anything,
            // and the click had nothing to act on.
            gridSystem = newGridSystem;

            newPos = newGridSystem.GetNearestPointOnGrid(newPos);
            Cell cell = newGridSystem.GetCellAtWorldPosition(newPos);

            // Placement Logic Check 

            // Checks for cells
            if (cell != null)
            {

                // First,
                // ensure that the primary cell where you are trying to place the building is not blocked or occupied.
                if (cell.isBlocked || cell.isOccupied)
                {
                    canPlace = false;
                    currentBuildingPreview.SetPreviewMaterial(canPlace);
                    Debug.Log(cell.isBlocked ? "Cell is Blocked." : "Cell is occupied, not Open.");
                    return; // If the primary cell is blocked or occupied, then exit early.
                }
                // If Not, Continue...

                // Second,
                // If the cell is free, check for the entire footprint of the building & check Additional Properties and requirements
                BuildingProperties buildingProperties = currentBuildingPreview.GetBuildingPrefab().GetComponent<BuildingProperties>();
                
                // Null Check for Building Properties.
                if (buildingProperties == null)
                {
                    canPlace = false;
                    Debug.LogError("buildingProperties is null");
                    return; // If the building properties are Null, then exit Early.
                }


                // Third,
                // Re-decide from scratch every frame. canPlace used to only ever be
                // assigned false anywhere in the class, so the blueprint stayed red
                // forever and no building could be placed.
                Vector3Int gridPosition = newGridSystem.WorldToCell(newPos);

                // The per-cell footprint rules live in PlacementRules now. They used to be
                // written out inline here, where nothing else could read them - which meant
                // the placement overlay would have had to re-implement them and could
                // silently disagree with what the click actually enforces.
                canPlace = PlacementRules.EvaluateFootprint(
                    newGridSystem,
                    gridPosition,
                    buildingProperties.buildingSize,
                    buildingProperties.buildingData,
                    out _);

                if (canPlace)
                {
                    InfluenceManager influenceManager = PlacementRules.GetInfluenceManager(currentIsland, createIfMissing: true);

                    // Shared with BuildInteraction and the action bar so all three agree
                    // on what counts as the founding building. The old inline test
                    // required a BuildingData asset, which the Depot does not have, so
                    // the first harbor was judged an ordinary building and demanded
                    // island influence that cannot exist before it is built.
                    bool isWarehouse = InfluenceManager.IsHarborBuilding(buildingProperties);

                    canPlace = PlacementRules.EvaluateInfluence(influenceManager, isWarehouse, newPos, newGridSystem,
                                                               out Unit foundingBoat, out _);

                    // If this is the first warehouse on an unsettled island, verify boat cargo.
                    // Cargo is a property of the vessel rather than of the site, so the overlay
                    // deliberately does not shade cells by it - every cell would fail alike.
                    if (canPlace && isWarehouse && influenceManager != null && !influenceManager.HasWarehouse && foundingBoat != null)
                    {
                        canPlace = BoatCarriesCost(foundingBoat);
                    }
                }

                currentBuildingPreview.SetPreviewMaterial(canPlace);
                Debug.LogFormat("<color=pink>UpdateBuildsite - canPlace: </color>" + canPlace);
            }
            else
            {
                canPlace = false;
                currentBuildingPreview.SetPreviewMaterial(canPlace);
                return;
            }
        }
        else
        {
            if (currentBuildingPreview != null)
            {
                Debug.Log("No Ground Layer");
                canPlace = false;
                currentBuildingPreview.SetPreviewMaterial(false);
                currentBuildingPreview.SetRendererEnabled(false);
                return;
            }

            canPlace = false;
            return;
        }
    }
    #endregion

    /// <summary>
    /// The BuildingProperties of whatever blueprint is currently out, or null. The
    /// overlay needs the footprint and building data, which live on the prefab.
    /// </summary>
    private BuildingProperties GetPreviewProperties()
    {
        GameObject prefab = currentBuildingPreview != null ? currentBuildingPreview.GetBuildingPrefab() : null;
        return prefab != null ? prefab.GetComponent<BuildingProperties>() : null;
    }

    /// <summary>
    /// Whether the founding vessel is carrying what the harbor costs. Extracted from
    /// UpdateBuildsite so the site rules there read as site rules.
    /// </summary>
    private bool BoatCarriesCost(Unit foundingBoat)
    {
        BuildingCost costComponent = currentBuildingPreview.GetBuildingPrefab()?.GetComponent<BuildingCost>();
        if (costComponent == null || costComponent.costData == null) return true;

        UnitInventory boatInv = foundingBoat.GetComponent<UnitInventory>();
        if (boatInv == null) return true;

        Dictionary<ItemData, int> boatItems = boatInv.GetAllItems();
        Dictionary<ItemData, int> costItems = costComponent.costData.GetCostItemsDictionary();

        foreach (var kvp in costItems)
        {
            if (kvp.Key == null || kvp.Value <= 0) continue;

            if (!boatItems.ContainsKey(kvp.Key) || boatItems[kvp.Key] < kvp.Value) return false;
        }

        return true;
    }

    /// <summary>
    /// The vessel whose influence circle is shown while a harbor blueprint is out. It is
    /// anchored to the selected boat rather than to the hovered cell, so the circle stays
    /// up for the whole placement instead of blinking off over every invalid tile.
    /// </summary>
    private Unit ResolveSettlingBoat()
    {
        if (currentBuildingPreview == null) return null;
        if (!InfluenceManager.IsHarborBuilding(currentBuildingPreview.GetBuildingPrefab())) return null;

        Unit selected = InfluenceManager.GetSelectedPlayerBoat();
        if (selected != null) return selected;

        return InfluenceManager.GetNearestPlayerBoat(currentBuildingPreview.transform.position);
    }

    private void UpdateSettlingBoatRing(Unit newBoat)
    {
        if (currentSettlingBoat != newBoat)
        {
            if (currentSettlingBoat != null)
            {
                currentSettlingBoat.GetComponent<Settlement>()?.CancelSettlement();
            }
            currentSettlingBoat = newBoat;
            if (currentSettlingBoat != null)
            {
                currentSettlingBoat.GetComponent<Settlement>()?.BeginSettlement();
            }
        }
    }

    #region Cancel Building Methods
    public void CancelBuilding()
    {
        UpdateSettlingBoatRing(null);
        PlacementValidityOverlay.HideAll();
        if (currentBuildingPreview != null)
        {
            if (currentBuildingPreview.gameObject != null)
            {
                Destroy(currentBuildingPreview.gameObject);
            }
            currentBuildingPreview = null;
        }
    }

    public void CancelClick()
    {                     
        if (Input.GetMouseButtonDown(1))
        {
            CancelBuilding();
        }
    }
    #endregion

}
