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
    private Bank bank;
    private readonly PlacementValidationResult latestValidation = new PlacementValidationResult();

    public PlacementValidationResult LatestValidation => latestValidation;
    public BuildingPreview ActivePreview => currentBuildingPreview;
    public Island ActiveIsland => currentIsland;
    public GridSystem ActiveGrid => gridSystem;
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
        bank = FindObjectOfType<Bank>();
        
        // Subscribe to event for the current island.
        if (IslandManager.instance != null)
        {
            IslandManager.instance.OnPlayerHoverIsland += OnCurrentIslandChanged;
            IslandManager.instance.OnPlayerEnterIsland += OnCurrentIslandChanged;
        }
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
        if (currentBuildingPreview != null && currentBuildingPreview.IsBoundToIsland)
        {
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

        if (currentBuildingPreview != null && currentBuildingPreview.IsBoundToIsland)
        {
            currentIsland = currentBuildingPreview.BoundIsland;
            if (currentIsland != null && currentBuildingPreview.transform.parent == null)
            {
                currentBuildingPreview.transform.SetParent(currentIsland.transform);
            }
        }

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
            
            currentBuildingPreview.SetPlacementVisualState(
                latestValidation.IsPlacementValid,
                latestValidation.IsAffordable);

            // Update the visual settlement ring to follow whatever boat we evaluated
            UpdateSettlingBoatRing(hoveredBoat);

            BuildingProperties previewProps = GetPreviewProperties();
            GameObject previewPrefab = currentBuildingPreview != null ? currentBuildingPreview.GetBuildingPrefab() : null;
            Vector3? previewPos = currentBuildingPreview != null ? (Vector3?)currentBuildingPreview.transform.position : null;
            Quaternion? previewRot = currentBuildingPreview != null ? (Quaternion?)currentBuildingPreview.transform.rotation : null;

            PlacementValidityOverlay.Show(
                gridSystem,
                currentIsland,
                previewProps,
                hoveredBoat,
                previewPos,
                latestValidation,
                previewRot);
            PlacementDepositBeacons.Show(gridSystem, previewProps != null ? previewProps.buildingData : null);
            PlacementCursorCostHUD.UpdateReadout(
                previewPrefab,
                previewPrefab != null ? previewPrefab.GetComponent<BuildingCost>() : null,
                latestValidation,
                bank,
                islandInventory,
                hoveredBoat);

            InputCheck();
        }
        else
        {
            UpdateSettlingBoatRing(null); // Ensure it's off if no preview
            PlacementValidityOverlay.HideAll();
            PlacementDepositBeacons.Hide();
            PlacementCursorCostHUD.Hide();
        }
    }
    #endregion

    #region Base Requirement Methods

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

            // PlacementEvaluator validates terrain, footprint road perimeter, influence,
            // credits, goods, and non-grid BuildingRequirements every frame into canPlace.
            isVerified = true;

            if (IC || (canPlace && isVerified))
            {
                HandleBuildingPlacement();
            }
            else
            {
                string reason = latestValidation != null && latestValidation.FailureReason != PlacementFailureReason.None
                    ? latestValidation.FormatReasonLabel()
                    : $"canPlace={canPlace}, isVerified={isVerified}";
                Debug.Log($"<color=red>Cannot place building: {reason}</color>");
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

            // Storage is resolved by BuildingPlacer from the island the blueprint stands on.
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

            // Snapped with the blueprint's own footprint so the cell this check reads is
            // the cell the blueprint is actually standing on. GetNearestPointOnGrid is the
            // 1x1 case of this and still answers identically for single-cell buildings.
            Vector2Int previewFootprint = currentBuildingPreview.GetFootprint();
            newPos = newGridSystem.SnapFootprintToGrid(newPos, previewFootprint);
            Vector3Int gridOrigin = newGridSystem.GetFootprintOrigin(newPos, previewFootprint);

            GameObject prefab = currentBuildingPreview.GetBuildingPrefab();
            BuildingProperties buildingProperties = prefab != null ? prefab.GetComponent<BuildingProperties>() : null;
            if (buildingProperties == null)
            {
                canPlace = false;
                latestValidation.Reset();
                latestValidation.FailureReason = PlacementFailureReason.InvalidTerrain;
                currentBuildingPreview.SetPreviewMaterial(false);
                Debug.LogError("buildingProperties is null");
                return;
            }

            if (bank == null) bank = FindObjectOfType<Bank>();
            islandInventory = currentIsland != null ? currentIsland.GetComponent<Inventory>() : null;

            Vector3 evaluatedWorldPos = newPos;

            var request = new AllocationRequest
            {
                Prefab = prefab,
                Properties = buildingProperties,
                Data = buildingProperties.buildingData,
                Cost = prefab.GetComponent<BuildingCost>(),
                Grid = newGridSystem,
                Island = currentIsland,
                WorldPosition = evaluatedWorldPos,
                GridOrigin = gridOrigin,
                Footprint = previewFootprint,
                IslandGoods = islandInventory,
                Bank = bank,
                PreferredBoat = hoveredBoat
            };

            PlacementEvaluator.Evaluate(in request, latestValidation);
            canPlace = latestValidation.CanCommit;
            currentBuildingPreview.SetPlacementVisualState(
                latestValidation.IsPlacementValid,
                latestValidation.IsAffordable);
        }
        else
        {
            if (currentBuildingPreview != null)
            {
                canPlace = false;
                latestValidation.Reset();
                currentBuildingPreview.SetPlacementVisualState(false, false);
                currentBuildingPreview.SetRendererEnabled(false);
                PlacementDepositBeacons.Hide();
                PlacementCursorCostHUD.Hide();
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

    public Unit ActiveSettlingBoat => currentSettlingBoat != null ? currentSettlingBoat : ResolveSettlingBoat();

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
        PlacementDepositBeacons.Hide();
        PlacementCursorCostHUD.Hide();
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
