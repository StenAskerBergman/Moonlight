using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Building : MonoBehaviour
{
    public int MonthlyReturn { get; set; }
    public int BuildingId { get; set; }
    public Enums.BuildingType BuildingType { get; set; } = default;
    public List<Enums.Resource> Resources { get; set; } = new List<Enums.Resource>();
    public bool isSeedBuilding { get; set; }
    public Enums.SeedType currentSeedType { get; set; } = Enums.SeedType.None;

    // List of Building Systems
    public BuildingInv buildingInventory; // Local Building Inventory 

    // List of Building Data Files
    public BuildingData buildingData;   // Universal Building Data


    private ResourceManager resourceManager;

    public Building(Enums.BuildingType buildingType, int id, ResourceManager resourceManager)
    {
        this.BuildingId = id;
        this.BuildingType = buildingType;
        isSeedBuilding = false;
        currentSeedType = Enums.SeedType.None;
        this.resourceManager = resourceManager;
    }

}

