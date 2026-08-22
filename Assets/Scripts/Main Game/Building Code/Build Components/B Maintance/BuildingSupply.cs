using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingSupply : MonoBehaviour
{
    private Building building;

    // TODO: assign in the Inspector per building prefab - the resources this
    // building needs on hand to keep operating.
    [SerializeField] private List<ResourceRequirement> requiredSupplies = new List<ResourceRequirement>();

    private void Awake()
    {
        building = GetComponent<Building>();
    }

    public bool HasRequiredSupplies()
    {
        if (requiredSupplies == null || requiredSupplies.Count == 0) return true;
        if (building == null || building.buildingInventory == null) return true;

        foreach (ResourceRequirement requirement in requiredSupplies)
        {
            if (requirement == null) continue;

            // TODO: ResourceRequirement.requiredResource is matched against ItemEnums.ResourceType
            // by name until supplies get a proper ResourceType field of their own.
            if (!System.Enum.TryParse(requirement.requiredResource, out ItemEnums.ResourceType resourceType))
            {
                continue;
            }

            int available = building.buildingInventory.GetResourceCount(resourceType);
            if (!requirement.IsSatisfiedBy(available))
            {
                return false;
            }
        }

        return true;
    }

    public void ConsumeSupplies()
    {
        if (requiredSupplies == null) return;
        if (building == null || building.buildingInventory == null) return;

        foreach (ResourceRequirement requirement in requiredSupplies)
        {
            if (requirement == null || requirement.amount <= 0) continue;

            if (!System.Enum.TryParse(requirement.requiredResource, out ItemEnums.ResourceType resourceType))
            {
                continue;
            }

            for (int i = 0; i < requirement.amount; i++)
            {
                building.buildingInventory.RemoveResourceFromBuilding(resourceType);
            }
        }
    }

    // Called from BuildingCondition's periodic check.
    public void CheckSupplyState()
    {
        if (building == null) return;

        if (!HasRequiredSupplies())
        {
            building.SetState(BuildingEnums.BuildingState.Paused);
        }
        else if (building.CurrentState == BuildingEnums.BuildingState.Paused)
        {
            building.SetState(BuildingEnums.BuildingState.Active);
        }
    }
}
