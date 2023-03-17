using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingProperties : MonoBehaviour
{    
	// Grid Refs
		public GridSystem gridSystem;
		public BuildingProperties buildingProperties;
        
    // Size of building in grid cells
    public int width = 1;
    public int length = 1;

    // Whether or not the building can be placed outside of the grid
    public bool canBePlacedOutsideGrid = false;

    public Vector3 size; // Size of the building
    public bool canBuildOutsideGrid = true; // Whether the building can be placed outside of the grid

    public void Start(){
        
        buildingProperties = GetComponent<BuildingProperties>();
		gridSystem = FindObjectOfType<GridSystem>();

    }
    public bool IsEmptyAtPosition(Vector3 position, GameObject islandGameObject)
    {
        // Check if the position is within the bounds of the grid
        if (!canBuildOutsideGrid)
        {
            float minX = gridSystem.transform.position.x - gridSystem.gridSize / 2f;
            float maxX = gridSystem.transform.position.x + gridSystem.gridSize / 2f;
            float minZ = gridSystem.transform.position.z - gridSystem.gridSize / 2f;
            float maxZ = gridSystem.transform.position.z + gridSystem.gridSize / 2f;

            if (position.x < minX || position.x > maxX || position.z < minZ || position.z > maxZ)
            {
                return false;
            }
        }

        if (!buildingProperties.canBePlacedOutsideGrid)
        {
            float minX = gridSystem.transform.position.x - (gridSystem.cellSize * width) / 2f;
            float maxX = gridSystem.transform.position.x + (gridSystem.cellSize * width) / 2f;
            float minZ = gridSystem.transform.position.z - (gridSystem.cellSize * length) / 2f;
            float maxZ = gridSystem.transform.position.z + (gridSystem.cellSize * length) / 2f;

            if (position.x < minX || position.x > maxX || position.z < minZ || position.z > maxZ)
            {
                return false;
            }
        }

        // Check if the position is too close to the edge of the island
        float borderDistance = 2f; // Adjust as needed
        Vector2 position2D = new Vector2(position.x, position.z);
        Vector2 islandCenter2D = new Vector2(islandGameObject.transform.position.x, islandGameObject.transform.position.z);
        float distanceFromCenter = Vector2.Distance(position2D, islandCenter2D);

        // Determine the border of the island by adding the border distance to the radius of the island
        float islandRadius = islandGameObject.transform.localScale.x / 2f;
        float borderRadius = islandRadius + borderDistance;

        // Check if the position is inside the island's border
        if (distanceFromCenter < borderRadius)
        {
            return false;
        }

        // Cast a sphere at the position with a radius of 0.1 units
        Collider[] colliders = Physics.OverlapSphere(position, 0.1f);

        // Check if any of the colliders are tagged as "Building"
        foreach (Collider collider in colliders)
        {
            if (collider.gameObject.CompareTag("Building"))
            {
                return false;
            }
        }

        // If no colliders are found or if the colliders found are not tagged as "Building", return true
        return true;
    }
}
