using UnityEngine;

public class BuildingChecker : MonoBehaviour
{
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask UI;
    public bool canPlace, canPlace2;
    public bool IC = false;
    [SerializeField] private BuildingPreview currentBuildingPreview;
    [SerializeField] private BuildingPlacer buildingPlacer;
    private Island currentIsland;
    private GridSystem gridSystem;

    public static BuildingChecker instance;

    // Refs 1: 
    [Header("Island Related")]
    [Space(8)]
    public IslandResource islandResource;
    public IslandPower islandPower;
    public IslandEcology islandEcology;


    // Refs 2
    private PlayerMaterialManager playerMaterialManager;
    private IslandResourceManager islandResourceManager;

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
        canPlace2 = false;
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

    private void OnCurrentIslandChanged(Island island)
    {

        if (island == null)
        {
            Debug.Log("Island = Null");
            return;
        }
        currentIsland = island;
        GridSystem currentGridSystem = island.GetComponent<GridSystem>();

        // Add a null check for currentBuildingPreview
        if (currentBuildingPreview != null)
        {
            currentBuildingPreview.gridSystem = currentGridSystem;
        }
        else
        {
            Debug.LogWarning("currentBuildingPreview is null");
            return;
        }

        islandResource = island.GetComponent<IslandResource>();
        islandPower = island.GetComponent<IslandPower>();
        islandEcology = island.GetComponent<IslandEcology>();
        GetCurrentIslandGridSystem(currentIsland);
        islandResourceManager = island.GetComponent<IslandResourceManager>();
    }


    // Starts this Script...
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

    void Update()
    {

        // Check if there is a Building Preview Active...
        if (currentBuildingPreview != null)
        {
            // There is a Building Preview Active...

            /* Future stuff
                // Logic Desc - Goal
                // Determine where the player wants to build
                // Determine Location Suitability
                // Additional Post Construction Extras

                // To do - Not Yet
                // UpdateBuildsite(x);              // For Determine Location 
                // CheckBuildsite(x,y);             // For Determine Suitability
                // ConstructBuildsite(x,y,z);       // For Determine Construction        

                // Logic Desc - Current
                // Does all in one method
            */

            // Doing - Now
            UpdateBuildsite();  // Determines Location              

            // currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator

            Debug.Log("Update: " + canPlace);


            // Click to Build - Building Method - Click to Set the Selected Building Preview at Mouse Location...
            if (Input.GetMouseButtonDown(0))
            {
                // Check CanPlace for "place-ability"...
                if (IC == true || canPlace == true)// || canPlace2 == true) // Either Require True 
                {
                    // IDEA: Add a Parent check? ... avoid nullref
                    // Place Building - Construct BuildSite...

                    Debug.LogFormat("<color=green>placing building - {0}</color>", canPlace); // Green
                    buildingPlacer.PlaceBuilding(currentBuildingPreview, currentBuildingPreview.transform.parent); // Requires Parent! 

                }
                else if (IC == false && canPlace == false) // Both Require False
                {
                    Debug.LogFormat("<color=red>Cant place building - {0}</color>", canPlace); // Red
                    // currentBuildingPreview.SetPreviewMaterial(canPlace); // Update the color of the building preview based on canPlace value

                }

            }

            // Click to Cancel
            if (Input.GetMouseButtonDown(1))
            {
                CancelBuilding();
            }
        }
    }

    #region Grid Related
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


    public void UpdateGridSystem(GridSystem newGridSystem)
    {
        gridSystem = newGridSystem;
    }
    #endregion

    #region Construction Methods 

    // ( Conditions Requirements Price ) 

    // Buildsite 
    public void Buildsite()
    {

        // Construct Building & Checks Island for Needs
        // Construct Building & Checks Grid System for Needs
        // Construct Building & Checks Cell Location for Needs


        // then...
        // Finishes or Cancelled

    }
    // Check Buildsite
    public void CheckBuildsite()
    {

        // Checks Island
        // Checks Grid System
        // Checks Cell Location


        // then...

    }

    // Construction 
    public void Construction()
    {
        Price();            // Price for Construction 
        Conditions();       // Conditions for Construction
        Requirements();     // Requirements for Construction
    }


    // Conditions
    private void Conditions()
    {

    }

    // Requirements
    private void Requirements()
    {

    }

    // price
    private void Price()
    {

    }

    #endregion

    #region  Construction GetValue 

    private void GetPrice()
    {

        //return price;         // Get Price 
    }
    private void GetRequirements()
    {
        //return requirements;  // Get Requirements 
    }
    private void GetConditions()
    {
        //return Conditions;    // Get Conditions
    }

    #endregion

    private void UpdateBuildsite()
    {
        // Runs in Update the overall Method

        /* Method Description
         
            // Script Task List
                // 1. Gather a Building Location
                // 2. Determine Build-ability
                // 3. Determine Compatability

            // Return
                // Location (Position Cell) 
                // CanBuild

        */

        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out hit, 1000f, groundLayer))
        {
            // Creates a New Position... 
            Vector3 newPos = hit.point;

            // Simple Null Ref Check ... on currentBuildingPreview 
            if (currentBuildingPreview == null)
            {
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

            // Checks currentBuildingPreview for GridSystem
            if (currentBuildingPreview.gridSystem == null)
            {
                Debug.Log("currentBuildingPreview.gridSystem is null");
                currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
                currentBuildingPreview.SetRendererEnabled(true);
                return;
            }

            // Enable the renderer if the parent object is present
            if (currentBuildingPreview.transform.parent != null)
            {
                currentBuildingPreview.SetRendererEnabled(true);
                currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator - you don't want a future false positive

                // currentIsland.LogBounds(); // Debugg Log for Bounds
            }
            else
            {
                // Disable the renderer and return early if there is no parent object
                currentBuildingPreview.SetRendererEnabled(false);
                currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator - you don't want a future false positive
                return;
            }

            newPos = newGridSystem.GetNearestPointOnGrid(newPos);
            Cell cell = gridSystem.GetCellAtWorldPosition(newPos);

            // Checks for cells
            if (cell != null)
            {
                /* Future Ideas 
                  
                    switch (cell.status)
                    {
                        case status.isFull:
                            // Handle isFull case
                            break;

                        case status.isNull:
                            // Handle isNull case
                            break;

                        case status.isWater:
                            // Handle isWater case
                            break;

                        default:
                            // Handle default case (if any)
                            break;
                    }
                 
                 */
                Debug.Log("Checking - Cell x:" + cell.position.x + ", Cell z:" + cell.position.z);
                if (cell.isBlocked != true)
                {
                    if (cell.isOccupied != true)
                    {

                        if (cell.building != null)
                        {
                            canPlace = false;
                            Debug.Log("Cell is occupied by a building. - canPlace:" + canPlace);
                            currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
                            return;
                        }


                        Debug.Log("Cell is Open, not occupied. - canPlace:" + canPlace);
                        currentBuildingPreview.transform.position = newPos;

                        // Check if the building can be placed at the new position
                        BuildingProperties buildingProperties = currentBuildingPreview.GetBuildingPrefab().GetComponent<BuildingProperties>();

                        if (buildingProperties == null)
                        {
                            canPlace = false;
                            Debug.LogError("buildingProperties is null"); // Game Breaker
                            return;
                        }

                        Vector3 buildingSize = buildingProperties.buildingSize; // Retains the correct size configurements

                        Vector3Int gridPosition = gridSystem.WorldToCell(newPos); // Moves the model to the correct location

                        canPlace = true; // Since Size & Location was retained & not blocked or filled allow Building

                        // Semi Works

                        for (int x = 0; x < buildingSize.x; x++)
                        {
                            for (int z = 0; z < buildingSize.z; z++)
                            {
                                int targetX = gridPosition.x + x;
                                int targetZ = gridPosition.z + z;

                                Vector3 targetCellWorldPosition = new Vector3(targetX * gridSystem.cellSize, 0, targetZ * gridSystem.cellSize);
                                Cell targetCell = gridSystem.GetCellAtPosition(targetCellWorldPosition);

                                if (targetCell == null || targetCell.isBlocked || targetCell.isOccupied || targetCell.building != null)
                                {
                                    canPlace = false;
                                    break;
                                }
                            }

                            if (!canPlace)
                            {
                                break;
                            }
                        }


                        currentBuildingPreview.SetPreviewMaterial(canPlace);
                        currentBuildingPreview.transform.position = newPos;

                        // Asking the Grid System to perform a Check
                        canPlace = currentBuildingPreview.gridSystem.CanPlaceAtPosition(newPos, buildingSize);  // Duel Check
                        canPlace2 = currentBuildingPreview.gridSystem.CanPlaceAtPosition(newPos, buildingSize); // Duel Check

                        // Works 
                        if (gridSystem.CanPlaceAtPosition(newPos, buildingSize) == true)
                        {
                            canPlace = gridSystem.CanPlaceAtPosition(newPos, buildingSize);  // Duel Check
                            canPlace2 = gridSystem.CanPlaceAtPosition(newPos, buildingSize); // Duel Check
                            currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
                        }
                        else
                        {
                            currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
                        }
                        Debug.LogFormat("<color=pink>UpdateBuildsite - 1 canPlace: </color>" + canPlace);
                        Debug.LogFormat("<color=pink>UpdateBuildsite - 2 canPlace: </color>" + canPlace2);

                    }
                    else
                    {
                        // Cell isOccupied

                        canPlace = false;
                        currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
                        Debug.Log("Cell is occupied, not Open. - canPlace:" + canPlace);
                        Debug.Log("Cell is occupied at position ( Cell x:" + cell.position.x + ", Cell z:" + cell.position.z + ")");
                        // ref 1: See refrence below for more
                    }
                }
                else
                {
                    // Cell isBlocked

                    canPlace = false;
                    currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
                    Debug.Log("Cell is Blocked.");

                }


            }
            else
            {
                //Debug.Log("No cell found at the given world position." + newPos);
                currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
                return;
            }
            // Still hit the Ground layer, but no cells found
            //Debug.Log("Still hit the Ground layer, but no cells found");
            currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
        }
        else
        {
            // Mouse Mouse off, Island after its selected
            // Disable the renderer if the raycast does not hit the ground layer
            if (currentBuildingPreview != null)
            {
                Debug.Log("No Ground Layer");

                // If you do want the building to be insta cancelled
                // CancelBuilding(); // Only Issue is... You won't ever Reach the island

                // If You don't want the building to be insta cancelled
                //currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
                currentBuildingPreview.SetPreviewMaterial(false);// canPlace == true && canPlace2 == true); // Placement Indicator

                currentBuildingPreview.SetRendererEnabled(true);
                return;

                // Note:
                // This Determines if you can move mouse off island
                // and if the building retains it's last selected
                // location on the island for the building preview
            }

            // No Parent Land Found Yet!
            currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
            currentBuildingPreview.SetRendererEnabled(false);
            Debug.Log("No Parents.");
            return;
        }
    }

    // void IndicationColor(){
    //     // Update the color of the building preview based on canPlace value
    //     Color previewColor = canPlace ? Color.green : Color.red;
    //     currentBuildingPreview.SetRendererColor(previewColor);
    // }

    public void CancelBuilding()
    {
        if (currentBuildingPreview != null)
        {
            if (currentBuildingPreview.gameObject != null)
            {
                Destroy(currentBuildingPreview.gameObject);
            }
            currentBuildingPreview = null;
        }

        // No current building preview to cancel

    }

    // Ref 1: 
    //
    // Here is the place for Dockyard Code
    //
    // if()
    // {
    //      if a building canbe placed onto
    //      another building than it needs
    //      to be coded and added here to
    //      this section.
    // }

}
