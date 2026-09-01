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

    // Lazily added for the same reason as the outline: no building prefab needs the
    // component placed by hand.
    private BuildingHighlighter highlighter;
    private BuildingHighlighter Highlighter =>
        highlighter ??= GetComponent<BuildingHighlighter>()
                       ?? gameObject.AddComponent<BuildingHighlighter>();

    // Ground ring under the selected building, the building-side equivalent of the
    // circle Unit.OnSelect enables on its first child.
    private BuildingSelectionRing selectionRing;
    private BuildingSelectionRing SelectionRing =>
        selectionRing ??= GetComponent<BuildingSelectionRing>()
                         ?? gameObject.AddComponent<BuildingSelectionRing>();

    public void OnSelect()
    {
        Selected = true;
        Highlighter.SetHighlight(BuildingHighlight.Selected);
        SelectionRing.SetVisible(true);
    }

    // Call after upgrades/modules structurally add or remove renderers under this
    // building, so the selection outline picks up the change immediately rather than
    // waiting on the next direct-child hierarchy edit (see SelectionOutlineTarget).
    public void RefreshOutlineRenderers()
    {
        Outline.RefreshRenderers();
        Highlighter.RefreshOverlays();
        SelectionRing.Refresh();
    }

    public void OnDeselect()
    {
        Selected = false;
        SelectionRing.SetVisible(false);

        // Only the blue "you clicked this" state is cleared here. Green influence is
        // owned by BuildingHighlightController, which decides it from the selection as a
        // whole - clearing it from the deselected building would fight that.
        if (Highlighter.State == BuildingHighlight.Selected)
        {
            Highlighter.SetHighlight(BuildingHighlight.None);
        }
    }

    private void OnDestroy()
    {
        if (BuildingSelections.Instance != null && BuildingSelections.Instance.SelectedBuilding == this)
        {
            BuildingSelections.Instance.DeselectAll();
        }

        ReleaseOccupiedCells();
    }

    private void ReleaseOccupiedCells()
    {
        Island island = GetComponentInParent<Island>();
        GridSystem grid = island != null ? island.GetComponent<GridSystem>() : GetComponentInParent<GridSystem>();
        if (grid != null)
        {
            int size = grid.gridSize;
            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    Cell cell = grid.GetCell(x, z);
                    if (cell != null && cell.occupyingBuilding == this)
                    {
                        cell.ReleaseCell();
                    }
                }
            }
        }
    }
}