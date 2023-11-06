using System.Collections;
using System.Collections.Generic;
using System.Resources;
using UnityEngine;

public class Building : MonoBehaviour
{
    public int MonthlyReturn { get; set; }
    public int BuildingId { get; set; }
    public BuildingEnums.BuildingType BuildingType { get; set; } = default;
    public List<ItemEnums.ResourceType> Resources { get; set; } = new List<ItemEnums.ResourceType>();
    public bool isSeedBuilding { get; set; }
    public ItemEnums.SeedType currentSeedType { get; set; } = ItemEnums.SeedType.None;

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
        }
    }
}