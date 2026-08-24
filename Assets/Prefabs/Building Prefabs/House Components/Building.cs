using System;
using System.Collections;
using System.Collections.Generic;
using System.Resources;
using UnityEngine;

public class Building : MonoBehaviour, ISelectable
{
    public int MonthlyReturn { get; set; }
    public int BuildingId { get; set; }
    public BuildingEnums.BuildingType BuildingType { get; set; } = default;
    public List<ItemEnums.ResourceType> Resources { get; set; } = new List<ItemEnums.ResourceType>();
    public bool isSeedBuilding { get; set; }
    public ItemEnums.SeedType currentSeedType { get; set; } = ItemEnums.SeedType.None;

    public BuildingEnums.BuildingState CurrentState { get; private set; } = BuildingEnums.BuildingState.UnderConstruction;
    public static event Action<Building, BuildingEnums.BuildingState> OnBuildingStateChanged;

    public void SetState(BuildingEnums.BuildingState newState)
    {
        if (CurrentState == newState) return;
        CurrentState = newState;
        OnBuildingStateChanged?.Invoke(this, newState);
    }

    public ItemData compatibleSeed;

    // Inventory Systems
    public BuildingInventory buildingInventory; // Building Inventory 
    public IslandInventory islandInventory; // Island Inventory 

    // Building Data
    public BuildingData buildingData;   

    public Building(BuildingEnums.BuildingType buildingType, int id ) // , ResourceManager resourceManager) // Local Legacy Code
    {
        this.BuildingId = id;
        this.BuildingType = buildingType;
        isSeedBuilding = false;
        currentSeedType = ItemEnums.SeedType.None;
        // this.resourceManager = resourceManager; // Local Legacy Code
    }

    public bool IsCompatibleWithSeeds(ItemData seedOne, ItemData seedTwo, ItemData seedThree)
    {
        if(seedOne || seedTwo || seedThree == null) return false;
            // Logic to determine if this building can
            // produce based on the provided seed item

        return true;
    }
    public bool IsCompatibleWithSeed(ItemData seed)
    {
        // Logic to determine if this building can
        // produce based on the provided seed item
        if (compatibleSeed == seed)
        {
            return true;
        } // else ...
        
        Debug.Log(compatibleSeed+" Not detected!");

        return false;
    }

    public void SeedActivate(ItemData seed)
    {
        // Alter production based on the seed.
        // Example:
        if (compatibleSeed == seed)
        {
            var productionController = GetComponent<BuildingProductionController>();
            // productionController.SetProducedResource(seed.associatedResource);
            // productionController.SetProductionRate(seed.boostedProductionRate);

            SetState(BuildingEnums.BuildingState.Active);
        }
    }

    // ISelectable — driven by BuildingSelections.ClickSelect/DeselectAll (see
    // Main Game/Building Code/Building Selection/). Highlight is a stencil-outline
    // silhouette drawn over every renderer under this GameObject (see
    // SelectionOutlineTarget/SelectionOutlineRendererFeature), so it works across
    // modular building hierarchies without combining meshes or relying on a
    // hand-authored ring prefab.
    public bool Selected { get; private set; }

    private SelectionOutlineTarget outline;

    // Lazily fetched/added so building prefabs don't each need the component
    // hand-placed — SelectionOutlineTarget carries no prefab-specific setup.
    private SelectionOutlineTarget Outline =>
        outline ??= GetComponent<SelectionOutlineTarget>()
                   ?? gameObject.AddComponent<SelectionOutlineTarget>();

    public void OnSelect()
    {
        Selected = true;
        Outline.SetSelected(true);
    }

    // Call after upgrades/modules structurally add or remove renderers under this
    // building, so the selection outline picks up the change immediately rather than
    // waiting on the next direct-child hierarchy edit (see SelectionOutlineTarget).
    public void RefreshOutlineRenderers()
    {
        Outline.RefreshRenderers();
    }

    public void OnDeselect()
    {
        Selected = false;
        Outline.SetSelected(false);
    }
}