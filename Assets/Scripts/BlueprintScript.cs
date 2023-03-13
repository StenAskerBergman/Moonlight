using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BlueprintScript : MonoBehaviour
{
    // Handle the logic for the building placement & preview script
		RaycastHit hit; // Creates a hit varible
	
	// Grid Refs
		public GridSystem gridSystem;
		public BuildingProperties buildingProperties;

    // Preview Material
		public Material red; // Can't Build
		public Material blue; // Cannot Build

    // Build Zone Material
		public Material green; // Build Zone

    // Preview Building Variables
		private float BuildHeight = 1f; // Height Debug For Preview
		public bool CanBuild = false; // Building Checker
		private int RotationAngleAmount = 90;
		private float PlacementRotation; // Building Placement Rotation
		public GameObject Model; // Not implmented: Spesific Preview Building
		public bool RotationMode = true; // Determins Rotation Mode
		private float ProjectionLocation = 1f; // Normalize Spawn location
		public GameObject currentlySelected; // Object - Building selected && that will be placed

    // Refs 1
        private IslandResource islandResource;
        private IslandPower islandPower;
        private IslandEcology islandEcology;

    // Refs 2
        private PlayerMaterialManager playerMaterialManager;
        private IslandResourceManager islandResourceManager;
        private Island currentIsland;

	// Layer Refs
		[SerializeField] private LayerMask gridLayer;
		[SerializeField] private LayerMask mask; 

	// Text Refs
		public Text Material1Text; // Fetches UI text
		public Text Material2Text; // Fetches UI text
	
	// Building Refs
    public GameObject[] hotbar = new GameObject[9];  // Hotbar of Buildings

    // public GameObject buildingManager;
 		public bool CostCheck;
		
    public void SetSelected(int index)
    {
        currentlySelected = hotbar[index];
        Debug.Log(hotbar);
    }

    public void Start(){
        
        buildingProperties = GetComponent<BuildingProperties>();
		gridSystem = FindObjectOfType<GridSystem>();

		// Get the PlayerMaterialManager from the GameManager
		playerMaterialManager = GameManager.instance.playerMaterialManager;

		//Get the UI text components for Materials
		//Material1Text = GameObject.Find("").GetComponent<Text>();
		//Material2Text = GameObject.Find("").GetComponent<Text>();

		// Enables Rotation for a building
		RotationMode = true;

		// Starts the Placement Systems
		SelectionSystem.placing = true;
	}

	void Update() 
	{	

		//Physics.OverlapBox(hit.point, currentlySelected.GetComponent<BuildingProperties>().GetSize(), transform.rotation);
		
		// Get Current Island 
			// Get Fertilities
			// Get Resource Count
			// Get Material Count
			// Get Goods Count

		// Update the UI with the current resource counts
			//Material1Text.text = "" + Material1Count;
			//Material2Text.text = "" + Material2Count;
		
		// For Loop Building Array -- Not Effective implementation 
		for (int i = 0; i < 9; i++)
		{
			if (Input.GetKeyDown(KeyCode.Alpha1 + i)) // can use GetKey - GetKeyUp/etc
			{
				// use i to figure out which key is pressed 

					currentlySelected = hotbar[i]; 	// use i as index indicator
					
					// or

					SetSelected(i); // use-case of method below
			}
		}

		// Ray Cast
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition); 
		


		// Preview building positioning
		if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, gridLayer))
		{
			// Grid System Detection
			GridSystem detectedGridSystem = hit.collider.GetComponent<GridSystem>();
			if (detectedGridSystem != null)
			{
				gridSystem = detectedGridSystem;
			}


			Vector3 hitPoint = SnapToGrid(hit.point);
			transform.position = hitPoint;

			// Placement Check
			if (buildingProperties.canBePlacedOutsideGrid || !gridSystem.IsEmptyAtPosition(hitPoint))
			{
				CanBuild = true;
				gameObject.GetComponent<MeshRenderer>().material = green;
			}
			else
			{
				CanBuild = false;
				gameObject.GetComponent<MeshRenderer>().material = red;
			}

		}

		// Final building positioning
		if (Physics.Raycast (ray, out hit, mask)) {	
				
				Vector3 hitPoint = SnapToGrid(hit.point);
				transform.position = hitPoint;

				// Building Ui Button Clicked & Desired Building to be Built is Selected 

				// Instantiate Preview Building -- Make it Based of the current selection
				
				PreviewScript(); // Blue Print Building -- Preview Building -- Building Indicator
				
				//Debug.Log(hit.transform.gameObject.tag); // What are we hitting?

				#region TODO: Condition Checkers && Cost Checkers  
				// Condition Checkers
					// Resource Check -- Checks the availability before allowing player to build
					//int cost = currentlySelected.GetComponent<BuildingCost>().GetValue();
					//Enums.Resource resourceType = currentlySelected.GetComponent<BuildingCost>().GetResourceType();

				// Cost Checker 1
					//int buildingCost = currentlySelected.GetComponent<BuildingCost>().GetValue();
					//bool canBuild = playerMaterialManager.CheckResourceAvailability(Enums.IslandType.None, resourceType, buildingCost);
				
				// Cost Checker 2
					// Cost Reference
					// Island currentIsland = (Island)Enum.Parse(typeof(Island), GameManager.instance.currentIsland.GetType().IslandID);
					//if (CostCheck == true)//Cost Condition currentlySelected.GetComponent<BuildingCost>().GetValue() <= playerMaterialManager.GetResourceCount(Enums.IslandType.None, currentlySelected.GetComponent<BuildingCost>().resourceType)) 
					//{

					// Remove resources used for building from playerMaterialManager
						// int cost = currentlySelected.GetComponent<BuildingCost>().GetValue();
						// playerMaterialManager.RemoveResource(currentlySelected.GetComponent<BuildingCost>().resourceType, cost);

					//playerMaterialManager.RemoveResource(GameManager.instance.currentIsland.GetType(), currentlySelected.GetComponent<BuildingCost>().resourceType, cost);


										
				// // Todo: Implement a method to showcase build zone availability to the player
				
				// bool buildZone = false;

				// if (buildZone) {
					// 	// Set material to green (build zone)
					// 	gameObject.GetComponent<MeshRenderer>().material = green;
					// } else {
				// 	// Set material to red (cannot build)
					// 	gameObject.GetComponent<MeshRenderer>().material = red;
					// }

				#endregion
				
				#region Rotate Building
				// Rotational System -- Flat Based : Quadral 90° - Newer
					float scrollDirection = Input.GetAxis("Mouse ScrollWheel") * 10.0f; // adjust scaling factor as needed
					
					if(scrollDirection > 0)
					{
						PlacementRotation += Mathf.FloorToInt(scrollDirection) * RotationAngleAmount;
					}
					else if(scrollDirection < 0)
					{
						PlacementRotation -= Mathf.FloorToInt(Mathf.Abs(scrollDirection)) * RotationAngleAmount;
					}
					
					transform.rotation = Quaternion.AngleAxis(PlacementRotation, Vector3.down);

				// Rotational System -- Flat Based : Quadral 90° - New 
				/*
					PlacementRotation += Mathf.FloorToInt(Input.GetAxis("Mouse ScrollWheel")) * RotationAngleAmount; // Angle Amount
					Debug.Log(Input.GetAxis("Mouse ScrollWheel"));
					transform.rotation = Quaternion.AngleAxis(PlacementRotation, Vector3.down); // Angle Selection
				*/
				// Rotational System -- Float Based : Fluid 1% - Old 
					// PlacementRotation += Input.GetAxis("Mouse X") * RotationSpeed * -Time.deltaTime; // Angle Amount
					// transform.rotation = Quaternion.AngleAxis(PlacementRotation, Vector3.down); // Angle Selection
				#endregion



					
				// 2nd Stackability Check -- If Building can be placed on another building
				/*
					if (hit.transform.gameObject.CompareTag("notStackable")==false) {
						
						// Set material to red (cannot build)
						gameObject.GetComponent<MeshRenderer>().material = red;
						Debug.Log("Checking Stackability: Not Stackable");

					} else {

						// Set material to blue (can build)
						gameObject.GetComponent<MeshRenderer>().material = blue;
						Debug.Log("Checking Stackability: Stackable");
					}
				*/

				// Left Click Down: Start 
				if (Input.GetMouseButtonDown(0) && CanBuild == true)
				{
					Debug.Log("Mouse Down");
					RotationMode = true;
					// While Rotating...
					// Debug.Log("0");
					// I'm not too sure what you want to do here? 
					// I think you already have the preview set up so you might not need this
				
					// Left Click Release: End - Success
					if (Input.GetMouseButtonUp(0) && CanBuild == true) // if button released...
					{	
						Debug.Log("Mouse Up");
						// Right Click Down: Cancel
						if (Input.GetMouseButtonDown(1))
						{
							Destroy(gameObject);				// Destorys Preview
							RotationMode = false;				// Stops Rotation
							SelectionSystem.placing = false;	// Stops Placing
						}

			
						//Spawn Height
						Vector3 ProjectionPOS = transform.position;
						ProjectionPOS.y -= ProjectionLocation;
						transform.position = ProjectionPOS;

						// Instantiate 
						GameObject currentlySpawned = Instantiate(currentlySelected, ProjectionPOS, transform.rotation);

						// Find the "Building Manager" GameObject using its name
						GameObject buildingManager = GameObject.Find("Building Manager");
						// NOTE: THIS WILL NEED A UPDATE TO REFLECT WHAT ISLAND IS BEING BUILT ON
						// IslandID, or ID  or something to place the building on the correct island

						// Check if the GameObject was found
						if (buildingManager != null)
						{
							// Set the parent of currentlySelected to buildingManager
							currentlySpawned.transform.SetParent(buildingManager.transform);
							
							// Destroy Preview -- Stops Placing + Rotation -- (inside) Selection System Mode
							Debug.Log(currentlySelected + ": Placed"); 
							Destroy(gameObject);	
							SelectionSystem.placing = false; 
							RotationMode = false;

						} else {
							
							// Game Breaking Bug
							Debug.LogError("Could not find the 'Building Manager' GameObject");
						}
			

					} else {
				
						// CanBuild == False:
						Debug.Log("not enough resources for " + currentlySelected); 	// Console: Error Message
						
						// End - Canceled - Stops Preview & Placement System
						if (Input.GetMouseButtonDown(1))
						{
							RotationMode = false;				// Stops Rotation
							SelectionSystem.placing = false;	// Stops Placing
							Destroy(gameObject);				// Destorys Preview
						}
					}
				}

				// Right Click Cancel
				if (Input.GetMouseButton(1))
				{
					RotationMode = false;				// Stops Rotation
					SelectionSystem.placing = false;	// Stops Placing
					Destroy(gameObject);				// Destorys Preview
				}
				
			}
		}

	// Preview: Positioning, Visuals and More!
	private Vector3 SnapToGrid(Vector3 position)
    {
		// Get the dimensions of the grid
		float minX = gridSystem.transform.position.x - (gridSystem.gridSize * gridSystem.cellSize) / 2f;
		float maxX = gridSystem.transform.position.x + (gridSystem.gridSize * gridSystem.cellSize) / 2f;
		float minZ = gridSystem.transform.position.z - (gridSystem.gridSize * gridSystem.cellSize) / 2f;
		float maxZ = gridSystem.transform.position.z + (gridSystem.gridSize * gridSystem.cellSize) / 2f;


		// Clamp the position to the bounds of the grid
		position.x = Mathf.Clamp(position.x, minX, maxX);
		position.z = Mathf.Clamp(position.z, minZ, maxZ);

		// Round the position to the nearest cell
		int x = Mathf.FloorToInt(position.x / gridSystem.cellSize) * (int)gridSystem.cellSize;
		int y = Mathf.RoundToInt(position.y);
		int z = Mathf.FloorToInt(position.z / gridSystem.cellSize) * (int)gridSystem.cellSize;

        return new Vector3(x, y, z);
    }
	private bool IsEmptyAtPosition(Vector3 position)
	{
		
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
	
	public float cellSize = 1f; // match the Actual cell size

	void PreviewScript()
	{
		// Get the position of the mouse pointer on the grid
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, gridLayer))
		{
			// Round the position of the mouse pointer to the nearest grid cell
			Vector3 hitPoint = hit.point;
			hitPoint.x = Mathf.Round(hitPoint.x / cellSize) * cellSize;
			hitPoint.z = Mathf.Round(hitPoint.z / cellSize) * cellSize;

			// Check if the cell is empty
			if (IsEmptyAtPosition(hitPoint))
			{
				// Set the position of the preview building to the rounded position
				transform.position = hitPoint;
			}
		}
	}
}