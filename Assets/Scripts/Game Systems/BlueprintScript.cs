using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BlueprintScript : MonoBehaviour
{
    // Handle the logic for the building placement & preview script
    RaycastHit hit; // Creates a hit varible

    // Preview Material
    public Material red; // Can't Build
    public Material blue; // Cannot Build

    // Build Zone Material
    public Material green; // Build Zone

    // Preview Building Variables
    private float BuildHeight = 1f; // Height Debug For Preview
    public bool CanBuild = false; // Building Checker
    public float RotationSpeed; // Building Rotation Speed 
    float PlacementRotation; // Building Placement Rotation
    public GameObject Model; // Not implmented: Spesific Preview Building
    public bool RotationMode = false; // Determins Preview Rotation
    private float ProjectionLocation = 1f; // Normalize Spawn location
    public GameObject currentlySelected; // Object - Building selected && that will be placed
    public PlayerResourceManager playerResourceManager; // Fetches PlayerResourceManager
    public Text woodCountText; // Fetches UI text
    public Text stoneCountText; // Fetches UI text
	
    public GameObject[] hotbar = new GameObject[9];  // Hotbar of Buildings
    // public GameObject buildingManager;
    public void SetSelected(int index)
    {
        currentlySelected = hotbar[index];
        Debug.Log(hotbar);
    }

    [SerializeField] private LayerMask mask; //On the top of you code. SerializeField make your variable visible in the inspector

	void Start()
	{
		// Get the PlayerResourceManager from the GameManager
		playerResourceManager = GameManager.instance.playerResourceManager;

		// Get the UI text components for wood and stone
		// woodCountText = GameObject.Find("WoodCount").GetComponent<Text>();
		// stoneCountText = GameObject.Find("StoneCount").GetComponent<Text>();

		// Starts the Placement Systems
		SelectionSystem.placing = true;
	}

	//[SerializeField] private LayerMask mask; //On the top of you code. SerializeField make your variable visible in the inspector

/*
	void Update() 
	{
		//Physics.OverlapBox(hit.point, currentlySelected.GetComponent<BuildingProperties>().GetSize(), transform.rotation);
		
		// Get the current resource counts from the PlayerResourceManager for the Main island
		int woodCount = playerResourceManager.GetResourceCount(Enums.IslandType.None, Enums.Resource.Resource1);
		int stoneCount = playerResourceManager.GetResourceCount(Enums.IslandType.None, Enums.Resource.Resource2);

		// Update the UI with the current resource counts
		woodCountText.text = "Wood: " + woodCount;
		stoneCountText.text = "Stone: " + stoneCount;

		for (int i = 0; i < 9; i++)
		{
			if (Input.GetKeyDown(KeyCode.Alpha1 + i)) // can use GetKey - GetKeyUp/etc
			{
				// use i to figure out which key is pressed -- use i as your index
				currentlySelected = hotbar[i];
				// or
				SetSelected(i); // use-case of method below
			}
		}

		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition); // Ray

		if (Physics.Raycast (ray, out hit, mask))
			{
				PreviewScript();
				// Debug.Log(hit.transform.gameObject.tag);

				// Start - After click...
				if (Input.GetMouseButtonDown(0) && CanBuild == true)
				{
					// While Rotating..

					// I'm not too sure what you want to do here? 
					// I think you already have the preview set up so you might not need this
				}

				if (hit.transform.gameObject.tag == "notStackable")
				{
					gameObject.GetComponent<MeshRenderer>().material = red;

				}

				// Middle
				if (Input.GetMouseButton(0))
				{

					if (hit.transform.gameObject.tag != "notStackable")
					{
						// Check resource availability before allowing player to build
						int cost = currentlySelected.GetComponent<BuildingCost>().GetValue();
						Enums.Resource resourceType = currentlySelected.GetComponent<BuildingCost>().GetResourceType();

						int buildingCost = currentlySelected.GetComponent<BuildingCost>().GetValue();
						bool canBuild = false; //playerResourceManager.CheckResourceAvailability(Enums.IslandType.None, resourceType, buildingCost);



						if (canBuild) {
							// Set material to green (build zone)
							gameObject.GetComponent<MeshRenderer>().material = green;
						} else {
							// Set material to red (cannot build)
							gameObject.GetComponent<MeshRenderer>().material = red;
						}

						// SeizeTrackingMode
						RotationMode = true;

						// Rotational System
						PlacementRotation += Input.GetAxis("Mouse X") * RotationSpeed * -Time.deltaTime; // Angle Amount
						transform.rotation = Quaternion.AngleAxis(PlacementRotation, Vector3.down); // Angle Selection
					}
				}

				// Blue Active Colour Assigner
				if (hit.transform.gameObject.tag != "notStackable")
				{
					gameObject.GetComponent<MeshRenderer>().material = blue;

					// End - Success
					if (Input.GetMouseButtonUp(0) && CanBuild == true) // if button released
					{
						// Enables Rotation for a building
						//RotationMode = true;
						
						if (Input.GetMouseButton(1))
						{
							Destroy(gameObject);
							RotationMode = false;
							SelectionSystem.placing = false;
						}

						// Cost Checker
						Island currentIsland = (Island)Enum.Parse(typeof(Island), GameManager.instance.currentIsland.GetType().Name);
						if (currentlySelected.GetComponent<BuildingCost>().GetValue() <= playerResourceManager.GetResourceCount(Enums.IslandType.None, currentlySelected.GetComponent<BuildingCost>().resourceType)) 
						{

							// Remove resources used for building from playerResourceManager
							int cost = currentlySelected.GetComponent<BuildingCost>().GetValue();
							playerResourceManager.RemoveResource(currentlySelected.GetComponent<BuildingCost>().resourceType, cost);

							//playerResourceManager.RemoveResource(GameManager.instance.currentIsland.GetType(), currentlySelected.GetComponent<BuildingCost>().resourceType, cost);

							// Spawn Height
							Vector3 ProjectionPOS = transform.position;
							ProjectionPOS.y -= ProjectionLocation;
							transform.position = ProjectionPOS;

							// Instantiate 
							GameObject currentlySpawned = Instantiate(currentlySelected, ProjectionPOS, transform.rotation);

							// Find the "Building Manager" GameObject using its name
							GameObject buildingManager = GameObject.Find("Building Manager");

							// Check if the GameObject was found
							if (buildingManager != null)
							{
								// Set the parent of currentlySelected to buildingManager
								currentlySpawned.transform.SetParent(buildingManager.transform);
							}
							else
							{
								Debug.LogError("Could not find the 'Building Manager' GameObject");
							}

							// Stops Preview & Placement System
							Destroy(gameObject); // Destroy Preview
							SelectionSystem.placing = false; // Selection System Mode
						}
						else
						{
							Debug.Log("not enough resources for " + currentlySelected); // Console: Error Message
							Destroy(gameObject); // Destroy Preview
							RotationMode = false;
							SelectionSystem.placing = false; // Selection System Mode
						}



					}
				}

				// End - Canceled
				if (Input.GetMouseButton(1))
				{
					Destroy(gameObject);
					RotationMode = false;
					SelectionSystem.placing = false;
				}
			}
		}
	
	void PreviewScript()
    {
		if (RotationMode == false)
		{ 
		// Preview Code
		gameObject.transform.position = hit.point;
		Vector3 position = transform.position;

		// Spawn Height Code
		position.y += BuildHeight;
		transform.position = position;
		}
	}
*/}