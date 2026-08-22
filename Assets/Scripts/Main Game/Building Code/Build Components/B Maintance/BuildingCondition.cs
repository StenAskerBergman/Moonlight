using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingCondition : MonoBehaviour
{
    private Building building;
    private BuildingSupply buildingSupply;
    private BuildingProperties buildingProperties;

    [SerializeField] private float checkInterval = 2f;
    private float timeSinceLastCheck;

    private void Awake()
    {
        building = GetComponent<Building>();
        buildingSupply = GetComponent<BuildingSupply>();
        buildingProperties = GetComponent<BuildingProperties>();
    }

    private void OnEnable()
    {
        RoadPlacer.OnRoadPlaced += HandleRoadNetworkChanged;
        RoadPlacer.OnRoadRemoved += HandleRoadNetworkChanged;
    }

    private void OnDisable()
    {
        RoadPlacer.OnRoadPlaced -= HandleRoadNetworkChanged;
        RoadPlacer.OnRoadRemoved -= HandleRoadNetworkChanged;
    }

    // Any road placed/removed anywhere could affect this building's road-access
    // requirement, so just re-check conditions immediately rather than waiting
    // for the next timed pass.
    private void HandleRoadNetworkChanged(Cell changedCell)
    {
        CheckConditions();
    }

    // Update is called once per frame
    void Update()
    {
        timeSinceLastCheck += Time.deltaTime;
        if (timeSinceLastCheck < checkInterval) return;

        timeSinceLastCheck = 0f;
        CheckConditions();
    }

    private void CheckConditions()
    {
        if (building == null || building.buildingData == null) return;

        if (building.CurrentState == BuildingEnums.BuildingState.UnderConstruction
            || building.CurrentState == BuildingEnums.BuildingState.Destroyed)
        {
            return;
        }

        Cell ownCell = GetOwnCell();

        bool requirementsMet = true;
        foreach (BuildingRequirement requirement in building.buildingData.BuildingRequirements)
        {
            if (requirement == null) continue;

            if (requirement is GridRequirement gridRequirement)
            {
                gridRequirement.SetTargetCell(ownCell);
            }

            if (!requirement.IsSatisfied())
            {
                requirementsMet = false;
                break;
            }
        }

        if (!requirementsMet)
        {
            building.SetState(BuildingEnums.BuildingState.Paused);
        }
        else if (building.CurrentState == BuildingEnums.BuildingState.Paused)
        {
            building.SetState(BuildingEnums.BuildingState.Active);
        }

        buildingSupply?.CheckSupplyState();
    }

    private Cell GetOwnCell()
    {
        if (buildingProperties == null || buildingProperties.gridSystem == null) return null;

        return buildingProperties.gridSystem.GetCellAtWorldPosition(transform.position);
    }
}
