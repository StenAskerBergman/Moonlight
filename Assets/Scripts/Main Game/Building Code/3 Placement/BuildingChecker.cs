using UnityEngine;

public class BuildingChecker : MonoBehaviour
{
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask UI;
    public bool canPlace;
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
            UpdateBuildsite(); // Determines Location              

            // currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
            Debug.Log("Update: " + canPlace);

            // Click to Set the Selected Building Preview at Mouse Location...
            if (Input.GetMouseButtonDown(0))
            {

                // Check CanPlace for "place-ability"...
                if (IC == true || canPlace == true) // Either Require True 
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
    public void Buildsite(){

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
            private void Conditions(){

            }

            // Requirements
            private void Requirements(){

            }    
    
            // price
            private void Price(){

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
            Vector3 newPos = hit.point;
            
            if (currentBuildingPreview == null)
            {
                Debug.Log("currentBuildingPreview is null");
                canPlace = false;
                currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator - you don't want a future false positive
                currentBuildingPreview.SetRendererEnabled(false);
                return;
            }

            currentBuildingPreview.UpdateGridSystem(gridSystem);

            GridSystem newGridSystem = GetCurrentIslandGridSystem(currentIsland);
            if (newGridSystem == null)
            {
                Debug.LogWarning("newGridSystem is null");
                canPlace = false;
                currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator - you don't want a future false positive
                currentBuildingPreview.SetRendererEnabled(true);
                return;
            }
            currentBuildingPreview.gridSystem = newGridSystem;

            // Checks currentBuildingPreview for GridSystem
            if (currentBuildingPreview.gridSystem == null)
            {
                Debug.Log("currentBuildingPreview.gridSystem is null");
                canPlace = false;
                currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
                currentBuildingPreview.SetRendererEnabled(true);
                return;
            }

            // Enable the renderer if the parent object is present
            if (currentBuildingPreview.transform.parent != null)
            {
                currentBuildingPreview.SetRendererEnabled(true);

                // currentIsland.LogBounds(); // Debugg Log for Bounds
            }
            else
            {
                // Disable the renderer and return early if there is no parent object
                currentBuildingPreview.SetRendererEnabled(false);
                currentBuildingPreview.SetPreviewMaterial(false); // Placement Indicator - you don't want a future false positive
                return;
            }
            
            // old 
            // newPos = currentBuildingPreview.gridSystem.GetNearestPointOnGrid(newPos);
            
            // new
            newPos = newGridSystem.GetNearestPointOnGrid(newPos);

            
            Cell cell = gridSystem.GetCellAtWorldPosition(newPos);
            

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

                if (cell.isBlocked != true)
                {

                    if (cell.building != null)
                    {
                        canPlace = false;
                        Debug.Log("Cell is occupied by a building." + canPlace);
                        currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
                        return;
                    }
          
                    
                    Debug.Log("Cell is Open, not occupied.");
                    currentBuildingPreview.transform.position = newPos;

                    // Check if the building can be placed at the new position
                    BuildingProperties buildingProperties = currentBuildingPreview.GetBuildingPrefab().GetComponent<BuildingProperties>();

                    if (buildingProperties == null)
                    {
                        Debug.Log("buildingProperties is null");
                        return;
                    }

                    Vector3 buildingSize = buildingProperties.buildingSize;

                    canPlace = currentBuildingPreview.gridSystem.CanPlaceAtPosition(newPos, buildingSize);

                    Debug.LogFormat("<color=pink>UpdateBuildsite - canPlace: </color>" + canPlace);
                    if (gridSystem.CanPlaceAtPosition(newPos, buildingSize) == true)
                    {
                        canPlace = gridSystem.CanPlaceAtPosition(newPos, buildingSize);
                        currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
                    }
                    else
                    {
                        canPlace = false;
                        currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
                    }
                    
                }
                else
                {
                    canPlace = false;
                    currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
                    Debug.Log("Cell is Blocked.");
                }
                 
            }
            else
            {
                //Debug.Log("No cell found at the given world position." + newPos);
                canPlace = false;
                currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
                return;
            }
            // Still hit the Ground layer, but no cells found
            //Debug.Log("Still hit the Ground layer, but no cells found");
            canPlace = false;
            currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
        }
        else
        {
            // Mouse Mouse off, Island after its selected
            // Disable the renderer if the raycast does not hit the ground layer
            if (currentBuildingPreview != null)
            {
                Debug.Log("No Ground Layer");
                canPlace = false;
                currentBuildingPreview.SetPreviewMaterial(canPlace); // Placement Indicator
                currentBuildingPreview.SetRendererEnabled(true); 
                return;
                // Note:
                // This Determines if you can move mouse off island
                // and if the building retains it's last selected
                // location on the island for the building preview
            }

            // No Parent Land Found Yet!
            canPlace = false;
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

}
