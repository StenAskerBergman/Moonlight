using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingPlacer : MonoBehaviour
{
    private Island currentIsland;
    public GridSystem gridSystem;
    [SerializeField] private Bank bank;
    [SerializeField] private IslandManager islandManager;
    [SerializeField] private BuildingChecker buildingChecker;
    public delegate void OnConfirmPlacement(GameObject previewObject);
    public event OnConfirmPlacement ConfirmPlacement;
    [SerializeField] private IslandResourceManager islandResourceManager;
    [SerializeField] private IslandResource islandResource;

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
        islandResourceManager = currentIsland.GetComponent<IslandResourceManager>(); 
        islandResource = currentIsland.GetComponent<IslandResource>(); 
    }

    private void OnGridSystemDetected(GridSystem detectedGridSystem)
    {
        gridSystem = detectedGridSystem;
        islandResourceManager = currentIsland.GetComponent<IslandResourceManager>();
        islandResource = currentIsland.GetComponent<IslandResource>(); 
    }



    public void PlaceBuilding(BuildingPreview buildingPreview, Transform islandTransform)
    {
        // Add debug information
        // Debug.Log("PlaceBuilding() called.");

        if (buildingChecker.canPlace || buildingChecker.IC)
        {
            // Your building placement logic here
            
            Camera mainCamera = Camera.main;
            Island currentIsland = islandManager.GetIslandInFrontOfCamera(mainCamera);

            if (currentIsland == null)
            {
                Debug.Log("No island found in front of the camera.");
                return;
            }

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


            // Sets Target Position
            Vector3 targetPosition = buildingPreview.transform.position;

            // Sets the actual size values
            Vector3 buildingSize = buildingPreview.GetBuildingPrefab().GetComponent<BuildingProperties>().buildingSize;

            if (gridSystem.CanPlaceAtPosition(targetPosition, buildingSize) == true)
            {
                // If true...
                // Place the building

                // Instantiate the actual building prefab with the correct parent
                GameObject buildingInstance = Instantiate(buildingPreview.buildingPrefab, targetPosition, Quaternion.identity, islandTransform);

                // Set the current island and gridSystem in the BuildingProperties component
                BuildingProperties buildingProperties = buildingInstance.GetComponent<BuildingProperties>();

                if (buildingProperties != null)
                {
                    buildingProperties.currentIsland = buildingPreview.currentIsland;
                    buildingProperties.gridSystem = buildingPreview.gridSystem;
                }

                // Update the bank balance and island resources using the BuildingCost script
                BuildingCost buildingCost = buildingInstance.GetComponent<BuildingCost>();
                if (buildingCost != null)
                {
                    Debug.Log("BuildingCost component found."); // Cost was found

                    Bank bank = FindObjectOfType<Bank>();
                    if (bank != null)
                    {
                        // is Placing the Building
                        Debug.Log("Bank component found."); // Bank was found
                        
                        bank.AddIncome(-buildingCost.GetPrice()); // Use AddIncome with a negative value to place price
                        currentIsland.RemoveResource(buildingCost.GetResourceType(), buildingCost.GetPrice());
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
                    Debug.Log("BuildingCost component not found."); // Add this line
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

