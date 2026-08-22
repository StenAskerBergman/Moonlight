using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingConditions : MonoBehaviour
{
    private Building building;
    private BuildingSupply buildingSupply;

    private void Awake()
    {
        building = GetComponent<Building>();
        buildingSupply = GetComponent<BuildingSupply>();
    }

    public List<string> GetUnmetConditions()
    {
        List<string> unmetConditions = new List<string>();

        if (building == null || building.buildingData == null) return unmetConditions;

        foreach (BuildingRequirement requirement in building.buildingData.BuildingRequirements)
        {
            if (requirement == null) continue;

            if (!requirement.IsSatisfied())
            {
                // TODO: give BuildingRequirement subclasses a human-readable label instead
                // of falling back to the type name.
                unmetConditions.Add($"Missing requirement: {requirement.GetType().Name}");
            }
        }

        if (buildingSupply != null && !buildingSupply.HasRequiredSupplies())
        {
            unmetConditions.Add("Missing required supplies");
        }

        return unmetConditions;
    }
}
