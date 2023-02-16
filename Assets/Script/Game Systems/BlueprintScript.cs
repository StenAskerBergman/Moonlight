using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlueprintScript : MonoBehaviour
{

	// SP
	RaycastHit hit; // Creates a hit varible
	public Material red; // Can't Build
	public Material blue; // Can Build
	private float BuildHeight = 1f; // Height Debug For Preview
	public bool CanBuild = false; // Building Checker
	public float RotationSpeed; // Building Rotation Speed 
	float PlacementRotation;
	public GameObject Model;
	public bool RotationMode = false;
	private float ProjectionLocation = 1f;

	public GameObject currentlySelected; // Object that will be placed

	public GameObject[] hotbar = new GameObject[9];  
	// public GameObject buildingManager;
	public void SetSelected(int index)
	{

		currentlySelected = hotbar[index];
		Debug.Log(hotbar);
	}

	[SerializeField] private LayerMask mask; //On the top of you code. SerializeField make your variable visible in the inspector

	void Start()
	{
		// Starts the Placement Systems
		SelectionSystem.placing = true;
	}



	void Update() // Update is called once per frame
	{
		/*Physics.OverlapBox(transform.position, transform.position, transform.rotation)
			{
			
			}*/

		for (int i = 0; i < 9; i++)
		{
			if (Input.GetKeyDown(KeyCode.Alpha1 + i)) // can use GetKey/GetKeyUp/etc
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

				Debug.Log(hit.transform.gameObject.tag);

				// Start

				if (Input.GetMouseButtonDown(0) && CanBuild == true)
				{
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


					if (hit.transform.gameObject.tag == "notStackable") // should be in Middle step
					{
						gameObject.GetComponent<MeshRenderer>().material = red;
					}
					else
					{
						gameObject.GetComponent<MeshRenderer>().material = blue;

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

						// Cost Checker
						if (currentlySelected.GetComponent<BuildingCost>().GetValue() <= Bank.BM) // && if this condition is true
						{
							// Transaction
							Bank.BM = Bank.BM - currentlySelected.GetComponent<BuildingCost>().GetValue(); // then cost + build
							
							// Spawn Height
							Vector3 ProjectionPOS = transform.position;
							ProjectionPOS.y -= ProjectionLocation;
							transform.position = ProjectionPOS;

							// Instantiate 
							GameObject currentlySpawned = Instantiate(currentlySelected, ProjectionPOS, transform.rotation); // Instantiate(GameObject, (hit.point)transform.position, transform.rotation)

							// Find the "Building Manager" GameObject using its name
							GameObject buildingManager = GameObject.Find("Building Manager");

							// Check if the GameObject was found
							if (buildingManager != null) {
								// Set the parent of currentlySelected to buildingManager
								currentlySpawned.transform.SetParent(buildingManager.transform);
							} else {
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
}