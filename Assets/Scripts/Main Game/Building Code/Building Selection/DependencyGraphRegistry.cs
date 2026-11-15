using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves runtime supply/production producer-consumer links between buildings on the same island
/// so selection highlights clearly indicate upstream suppliers and downstream consumers.
/// </summary>
public static class DependencyGraphRegistry
{
    private static readonly List<ItemEnums.ResourceType> ScratchNeeded = new List<ItemEnums.ResourceType>();

    public static void QueryDependencies(
        Building selected,
        List<Building> outSuppliers,
        List<Building> outConsumers)
    {
        outSuppliers?.Clear();
        outConsumers?.Clear();
        if (selected == null) return;

        Island selectedIsland = ResolveIsland(selected);

        ScratchNeeded.Clear();
        BuildingSupply selectedSupply = selected.GetComponent<BuildingSupply>();
        if (selectedSupply != null)
        {
            selectedSupply.FillRequiredResources(ScratchNeeded);
        }

        IBuildingProduction selectedProduction = selected.GetComponent<IBuildingProduction>();
        bool selectedProduces = selectedProduction != null;
        ItemEnums.ResourceType producedType = selectedProduces ? selectedProduction.GetProducedResource() : default;

        if (ScratchNeeded.Count == 0 && !selectedProduces) return;

        Building[] allBuildings = Object.FindObjectsOfType<Building>();
        var otherNeeds = new List<ItemEnums.ResourceType>(4);

        for (int i = 0; i < allBuildings.Length; i++)
        {
            Building other = allBuildings[i];
            if (other == null || other == selected) continue;
            if (other.CurrentState == BuildingEnums.BuildingState.Destroyed) continue;

            if (selectedIsland != null)
            {
                Island otherIsland = ResolveIsland(other);
                if (otherIsland != null && otherIsland != selectedIsland) continue;
            }

            if (outSuppliers != null && ScratchNeeded.Count > 0)
            {
                IBuildingProduction otherProd = other.GetComponent<IBuildingProduction>();
                if (otherProd != null && ScratchNeeded.Contains(otherProd.GetProducedResource()))
                {
                    outSuppliers.Add(other);
                }
            }

            if (outConsumers != null && selectedProduces)
            {
                BuildingSupply otherSupply = other.GetComponent<BuildingSupply>();
                if (otherSupply != null)
                {
                    otherNeeds.Clear();
                    otherSupply.FillRequiredResources(otherNeeds);
                    if (otherNeeds.Contains(producedType))
                    {
                        outConsumers.Add(other);
                    }
                }
            }
        }
    }

    private static Island ResolveIsland(Building building)
    {
        if (building == null) return null;
        BuildingProperties props = building.GetComponent<BuildingProperties>();
        if (props != null && props.currentIsland != null) return props.currentIsland;
        return building.GetComponentInParent<Island>();
    }
}
