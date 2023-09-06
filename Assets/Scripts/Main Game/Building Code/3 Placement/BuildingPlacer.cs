using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class BuildingPlacer : MonoBehaviour
{
    private Island currentIsland;
    public GridSystem gridSystem;
    [SerializeField] private Bank bank;
    [SerializeField] private IslandManager islandManager;
    [SerializeField] private BuildingChecker buildingChecker;
    public delegate void OnConfirmPlacement(GameObject previewObject);
    public event OnConfirmPlacement ConfirmPlacement;

    // Can Afford
    private bool canAfford;

    // Resource Related
    [SerializeField] private IslandItemManager islandItemManager;
    [SerializeField] private IslandItems islandItems;

    // Requirement Related
    [SerializeField] private RequirementEnums.RequirementType requirementType;
    [SerializeField] private RequirementEnums.RequirementType[] req;
    
    private void Start()
    {
        IslandManager.instance.OnGridSystemDetected += OnGridSystemDetected;
        IslandManager.instance.OnPlayerEnterIsland += OnPlayerEnterIsland;

        buildingChecker = FindObjectOfType<BuildingChecker>();
        if (buildingChecker == null)
        {
            Debug.LogError("BuildingChecker not found in the scene.");
        }
    }

    private void OnDestroy()
    {
        IslandManager.instance.OnGridSystemDetected -= OnGridSystemDetected;
        IslandManager.instance.OnPlayerEnterIsland -= OnPlayerEnterIsland;
    }

    private void OnPlayerEnterIsland(Island island)
    {
        currentIsland = island;
        islandItemManager = currentIsland.GetComponent<IslandItemManager>(); 
        islandItems = currentIsland.GetComponent<IslandItems>(); 
    }

    private void OnGridSystemDetected(GridSystem detectedGridSystem)
    {
        gridSystem = detectedGridSystem;
        islandItemManager = currentIsland.GetComponent<IslandItemManager>();
        islandItems = currentIsland.GetComponent<IslandItems>(); 
    }

    private Vector3 CalculateOffset(Vector3 buildingSize)
    {
        float offsetX = (buildingSize.x % 2 == 0) ? gridSystem.cellSize / 2 : 0;
        float offsetZ = (buildingSize.z % 2 == 0) ? gridSystem.cellSize / 2 : 0;

        return new Vector3(offsetX, 0, offsetZ);
    }

    // Sets Req Bools
    private bool ReqShore, ReqSea, ReqSub, ReqLand, ReqOther;

    public void PlaceBuilding(BuildingPreview buildingPreview, Transform islandTransform)
    {
        // Add debug information
        // Debug.Log("PlaceBuilding() called.");

        if (buildingChecker.canPlace || buildingChecker.IC)
        {
            // Building Placement Logic - Section
            
            // Minor Verfication Area

            // Check if there is a island to build on infront of you or not
            Camera mainCamera = Camera.main;
            Island currentIsland = islandManager.GetIslandInFrontOfCamera(mainCamera);

            if (currentIsland == null)
            {
                buildingPreview.SetPreviewMaterial(false); // Start Update the color of the building preview based on canPlace value

                Debug.Log("No island found in front of the camera.");
                return;
            }

            // Reset the Preview Material if needed
            buildingPreview.SetPreviewMaterial(buildingChecker.canPlace);

            if (buildingPreview == null)
            {
                Debug.LogError("BuildingPreview component not found.");
                return;
            }

            if (buildingPreview != null)
            {
                buildingPreview.transform.SetParent(islandTransform);
                buildingPreview.currentIsland = currentIsland;
                buildingPreview.gridSystem = currentIsland.GetComponentInChildren<GridSystem>();
            }

            // building placement logic - Starts Here...

            // Sets Target Position
            Vector3 targetPosition = buildingPreview.transform.position;

            // Sets Target Rotation
            Quaternion targetRotation = buildingPreview.transform.rotation;

            // Sets Builds Size Values
            Vector3 buildingSize = buildingPreview.GetBuildingPrefab().GetComponent<BuildingProperties>().buildingSize;

            // Sets Builds Type
            RequirementEnums.RequirementType[] req = buildingPreview.GetBuildingPrefab().GetComponent<RequirementEnums>().Requirements;

            // Check Build Requirements
            if (req != null)
            {
                foreach (RequirementEnums.RequirementType requirement in req)
                {
                    switch (requirement)
                    {
                        case RequirementEnums.RequirementType.ReqShore:
                            // Handle shore requirement
                            ReqShore = true;

                            // False
                            ReqSea = false;
                            ReqSub = false;
                            ReqLand = false;
                            ReqOther = false;

                            break;

                        case RequirementEnums.RequirementType.ReqSea:
                            // Handle sea requirement
                            ReqSea = true;

                            // False
                            ReqShore = false;
                            ReqSub = false;
                            ReqLand = false;
                            ReqOther = false;


                            break;

                        case RequirementEnums.RequirementType.ReqSub:
                            // Handle sea requirement
                            ReqSub = true;

                            // False
                            ReqShore = false;
                            ReqSea = false;
                            ReqLand = false;
                            ReqOther = false;

                            break;

                        case RequirementEnums.RequirementType.ReqLand:
                            // Handle land requirement
                            ReqLand = true;

                            // False
                            ReqShore = false;
                            ReqSea = false;
                            ReqSub = false;
                            ReqOther = false;

                            break;

                        default:
                            // Handle any other requirement type
                            ReqOther = true;

                            // False
                            ReqShore = false;
                            ReqSea = false;
                            ReqSub = false;
                            ReqLand = false;
                            break;
                    }
                }
            }

            if (buildingChecker.canPlace) // aka. -> if (gridSystem.CanPlaceAtPosition(targetPosition, buildingSize) == true) // Additional Check Before Building
            {
                // If true...
                // Place the building
                
                // Instantiate the actual building prefab with the correct parent
                GameObject buildingInstance = Instantiate(buildingPreview.buildingPrefab, targetPosition, targetRotation, islandTransform); // (buildingPreview.buildingPrefab, targetPosition, Quaternion.identity, islandTransform);

                // Set the current island and gridSystem in the BuildingProperties component
                BuildingProperties buildingProperties = buildingInstance.GetComponent<BuildingProperties>();

                if (buildingProperties != null)
                {
                    buildingProperties.currentIsland = buildingPreview.currentIsland;
                    buildingProperties.gridSystem = buildingPreview.gridSystem;
                }

                // Update the bank balance and island resources using the BuildingCost script
                BuildingCost buildingCost = buildingInstance.GetComponent<BuildingCost>(); // Get Building Cost

                if (buildingCost != null)
                {
                    // Cost was found
                    //Debug.Log("BuildingCost component found."); 

                    Bank bank = FindObjectOfType<Bank>(); // Get Player Bank
                    if (bank != null)
                    {
                        // Bank was 
                        // Debug.Log("Bank component found."); 

                        // Use AddIncome with a negative value to place price
                        // bank.AddIncome(-buildingCost.GetPrice()); 
                        // Weird to pay before checking affordability 

                        // Use RemoveResource to Remove Resources from Island
                        currentIsland.RemoveResource(buildingCost.GetResourceType(), buildingCost.GetPrice());

                        // issue: there is only one price! for one good! jezus, fix this please!

                        // Sets local price based of the One price preset
                        int buildPrice = buildingCost.GetPrice();

                        // Gets local island resource count
                        int resourceCount = currentIsland.GetResourceCount(buildingCost.GetResourceType());

                        // Balance Check 
                        if (resourceCount > 0 || resourceCount >= buildPrice) { canAfford = true; } else { canAfford = false; }

                        if (canAfford)
                        {
                            // We Can Afford it!

                            // Use AddIncome with a negative value to place price
                            bank.AddIncome(-buildingCost.GetPrice()); 

                            // use Remove Resource to remove resource from island
                            currentIsland.RemoveResource(buildingCost.GetResourceType(), buildingCost.GetPrice());
                            
                            // "Still" Placing the 
                            // Mark all cells that the building will cover as occupied.
                            Vector3Int gridPosition = gridSystem.WorldToCell(targetPosition);
                            for (int x = 0; x < buildingSize.x; x++)
                            {
                                for (int z = 0; z < buildingSize.z; z++)
                                {
                                    int targetX = gridPosition.x + x;
                                    int targetZ = gridPosition.z + z;

                                    Vector3 targetCellWorldPosition = new Vector3(targetX * gridSystem.cellSize, 0, targetZ * gridSystem.cellSize);
                                    gridSystem.MarkCellAsOccupied(targetCellWorldPosition);  // Mark each cell as occupied
                                }
                            }
                            // Next Step Initialize Building!
                            // Success Building is Now placed! 
                        } 
                        else
                        {
                            // We can't Afford it!
                            Debug.Log("Not Enough Resources!");
                            Destroy(buildingInstance);
                            buildingChecker.CancelBuilding();
                        }

                    }
                    else
                    {   
                        // isn't Placing the Building
                        Debug.LogError("Bank component not found."); // Add this line
                        buildingChecker.CancelBuilding();
                    }
                }    
                else
                {
                    // Add this line
                    Debug.Log("BuildingCost component not found."); 
                }
                // Destroy the preview object and reset the reference in BuildingMover
                buildingChecker.CancelBuilding();
            }
            else // gridSystem.CanPlaceAtPosition(targetPosition, buildingSize) == false
            { 
                // If false
                
                    // Position Out of Bounds
                    // Invalid Position in Bounds

                    // Missing Resource Globally
                        // Not enough money

                    // Missing Resource Locally
                        // Not Enough Resource 
                        // Not Enough Space
                        // Not Enough Power
                        // Not Enough Eco
                        // Not Enough People

                //Debug.Log("Not Enough Space");
                Debug.LogError("Error: Can't Place Building (Reason: Unknown)");
                Debug.Log("CanPlaceAtPosition: " + gridSystem.CanPlaceAtPosition(targetPosition, buildingSize));
                return;
            }

        }
        else
        {      
            // Destroy the preview object and reset the reference in BuildingMover
            buildingChecker.CancelBuilding();
            Debug.Log("Cannot place building.");
            return;
        }



        // Destroy the preview object and reset the reference in BuildingMover
        buildingChecker.CancelBuilding();
    }

}

    

// 
    
    // // Update the building preview's parent and current island
    // BuildingPreview buildingPreview = previewObject.GetComponent<BuildingPreview>();
    
    // public void PlaceBuildingAtPosition(GameObject previewObject)
    // {
    //     Camera mainCamera = Camera.main;
    //     Island currentIsland = islandManager.GetIslandInFrontOfCamera(mainCamera);
    //     if (currentIsland == null)
    //     {
    //         Debug.LogWarning("No island found in front of the camera.");
    //         return;
    //     }

    //     Transform islandTransform = currentIsland.transform;

    //     // Update the building preview's parent and current island
    //     BuildingPreview buildingPreview = previewObject.GetComponent<BuildingPreview>();
    //     if (buildingPreview != null)
    //     {
    //         buildingPreview.transform.SetParent(islandTransform);
    //         buildingPreview.currentIsland = currentIsland;
    //         buildingPreview.gridSystem = currentIsland.GetComponentInChildren<GridSystem>();
    //     }

    //     // Pass the BuildingPreview component instead of the GameObject
    //     PlaceBuilding(buildingPreview, islandTransform);
    // }
    
    // --

    // With MonthlyCost + MonthlyYield
    // public void PlaceBuilding(BuildingPreview buildingPreview, Transform islandTransform)
    // {
    //     if (buildingPreview == null)
    //     {
    //         Debug.LogError("BuildingPreview component not found.");
    //         return;
    //     }

    //     Vector3 targetPosition = buildingPreview.transform.position;

    //     // Instantiate the actual building prefab with the correct parent
    //     GameObject buildingInstance = Instantiate(buildingPreview.buildingPrefab, targetPosition, Quaternion.identity, islandTransform);

    //     // Set the current island and gridSystem in the BuildingProperties component
    //     BuildingProperties buildingProperties = buildingInstance.GetComponent<BuildingProperties>();
    //     if (buildingProperties != null)
    //     {
    //         buildingProperties.currentIsland = buildingPreview.currentIsland;
    //         buildingProperties.gridSystem = buildingPreview.gridSystem;
    //     }

    //     // Call the BuildingPlaced() method in the BuildingCost script
    //     BuildingCost buildingCost = buildingInstance.GetComponent<BuildingCost>();
    //     if (buildingCost != null)
    //     {
    //         buildingCost.BuildingPlaced();
    //     }

    //     // Destroy the preview object and reset the reference in BuildingMover
    //     buildingMover.CancelBuilding();
    // }   
    
    // public void PlaceBuilding(BuildingPreview buildingPreview, Transform islandTransform)
    // {
    //     if (buildingPreview == null)
    //     {
    //         Debug.LogError("BuildingPreview component not found.");
    //         return;
    //     }

    //     Vector3 targetPosition = buildingPreview.transform.position;

    //     // Instantiate the actual building prefab with the correct parent
    //     GameObject buildingInstance = Instantiate(buildingPreview.buildingPrefab, targetPosition, Quaternion.identity, islandTransform);

    //     // Set the current island and gridSystem in the BuildingProperties component
    //     BuildingProperties buildingProperties = buildingInstance.GetComponent<BuildingProperties>();
    //     if (buildingProperties != null)
    //     {
    //         buildingProperties.currentIsland = buildingPreview.currentIsland;
    //         buildingProperties.gridSystem = buildingPreview.gridSystem;
    //     }

    //     // Get the BuildingCost component from the instantiated building
    //     // Call the BuildingPlaced() method in the BuildingCost script
    //     BuildingCost buildingCost = buildingInstance.GetComponent<BuildingCost>();
        
    //     // Call the AddBuilding method from the Bank script to add the new building to the Bank's Building List
    //     bank.AddBuilding(buildingCost.GetBuildingName(), buildingCost.GetCost(), buildingCost.GetExpense());
        
    //     // Call the AddBuilding method from the GridSystem script to add the new building to the GridSystem's Building List
    //     buildingPreview.gridSystem.AddBuilding(buildingCost);

    //     if (buildingCost != null)
    //     {
    //         buildingCost.BuildingPlaced();
    //     }

    //     // Destroy the preview object and reset the reference in BuildingMover
    //     buildingMover.CancelBuilding();
    // }

