using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingPreview : MonoBehaviour
{
    [SerializeField] private GameObject buildingPrefab;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float raycastMaxDistance = 100f;
    private BuildingRotator buildingRotator;
    private GridSystem currentGridSystem;
    private bool isBlueprint;

    private void Start()
    {
        buildingRotator = GetComponent<BuildingRotator>();
        IslandManager.instance.OnGridSystemDetected += SetCurrentGridSystem;
    }

    private void OnDestroy()
    {
        IslandManager.instance.OnGridSystemDetected -= SetCurrentGridSystem;
    }


    private void Update()
    {   
        
        MovePreviewToMousePosition();

        if (isBlueprint)
        {
            // Handle blueprint-specific behaviors (e.g., checking affordability, updating visuals)
        }
        else
        {
            PlaceBuilding();
        }
    }

    public void SetAsBlueprint(bool value)
    {
        isBlueprint = value;
    }

    private void MovePreviewToMousePosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, raycastMaxDistance, groundLayer))
        {
            Vector3 snappedPosition = currentGridSystem.GetNearestPointOnGrid(hit.point);
            transform.position = snappedPosition;
        }
    }

    private void PlaceBuilding()
    {
        if (Input.GetMouseButtonDown(0))
        {
            GameObject placedBuilding = Instantiate(buildingPrefab, transform.position, buildingRotator.transform.rotation);
            placedBuilding.transform.SetParent(currentGridSystem.transform);
        }
    }

    private void SetCurrentGridSystem(GridSystem gridSystem)
    {
        currentGridSystem = gridSystem;
    }
}