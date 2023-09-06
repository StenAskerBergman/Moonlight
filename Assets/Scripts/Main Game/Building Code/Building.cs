using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Building : MonoBehaviour
{
    public int MonthlyReturn { get; set; }
    public int BuildingId { get; set; }
    public BuildingEnums.BuildingType BuildingType { get; set; } = default;
    public List<ItemEnums.ResourceType> Resources { get; set; } = new List<ItemEnums.ResourceType>();
    public bool isSeedBuilding { get; set; }
    public ItemEnums.SeedType currentSeedType { get; set; } = ItemEnums.SeedType.None;

    // List of Building Systems
    public BuildingInventory buildingInventory; // Building Inventory 

    // List of Building Data Files
    public BuildingData buildingData;   // Universal Building Data

    // public IslandInventory islandInventory; // Island Inventory 

    private ResourceManager resourceManager;

    public Building(BuildingEnums.BuildingType buildingType, int id, ResourceManager resourceManager)
    {
        this.BuildingId = id;
        this.BuildingType = buildingType;
        isSeedBuilding = false;
        currentSeedType = ItemEnums.SeedType.None;
        this.resourceManager = resourceManager;
    }

}

