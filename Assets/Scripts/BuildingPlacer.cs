using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingPlacer : MonoBehaviour
{
    // Refs 1
        private IslandResource islandResource;
        private IslandPower islandPower;
        private IslandEcology islandEcology;

    // Refs 2
        private PlayerMaterialManager playerMaterialManager;
        private IslandResourceManager islandResourceManager;
        private Island currentIsland;
    
    // Refs 3
        public GridSystem gridSystem; // Reference to the GridSystem component
        private GameObject buildingPreview; // The preview object of the building being placed
        private bool canPlaceBuilding; // Whether a building can be placed at the current position


    void Update(){

        islandResource = currentIsland.GetComponent<IslandResource>();
        islandPower = currentIsland.GetComponent<IslandPower>();
        islandEcology = currentIsland.GetComponent<IslandEcology>();
        islandResourceManager = currentIsland.GetComponent<IslandResourceManager>(); 
        
        // Check for building placement input
        if (Input.GetMouseButtonDown(0) && canPlaceBuilding)
        {
            // Place the building at the snapped position
            Vector3 snappedPos = gridSystem.SnapToGrid(transform.position);
            Instantiate(buildingPreview, snappedPos, Quaternion.identity);

            // Reset the building preview
            Destroy(buildingPreview);
            buildingPreview = null;
            canPlaceBuilding = false;
        }    
    }
    public void StartPlacingBuilding(GameObject buildingPrefab)
    {
        // Instantiate the building preview
        buildingPreview = Instantiate(buildingPrefab);

        // Disable collisions on the preview object
        Collider[] colliders = buildingPreview.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        // Update the canPlaceBuilding flag based on the current position of the building preview
        if (buildingPreview != null)
        {
            Vector3 pos = buildingPreview.transform.position;
            canPlaceBuilding = gridSystem.IsEmptyAtPosition(pos);
        }
    }
    
}