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

    private ResourceManager resourceManager;

    public Building(Enums.BuildingType buildingType, int id, ResourceManager resourceManager)
    {
        this.BuildingId = id;
        this.BuildingType = buildingType;
        isSeedBuilding = false;
        currentSeedType = Enums.SeedType.None;
        this.resourceManager = resourceManager;
    }

    public int GetResourceCount(Enums.Resource resource)
    {
        int count = 0;
        if (Resources != null)
        {
            foreach (Enums.Resource r in Resources)
            {
                if (r == resource)
                {
                    count++;
                }
            }
        }
        return count;
    }


    public void AddResourceToBulding(Enums.Resource resource)
    {
        if (Resources == null)
        {
            Resources = new List<Enums.Resource>();
        }
        Resources.Add(resource);
    }

    public void RemoveResource(int islandId, Enums.Resource resource, int amount)
    {
        Island island = IslandManager.instance.GetIsland(islandId);
        if (island != null)
        {
            island.RemoveResourceFromBuilding(this, resource, amount);
        }
    }



    public void RemoveMaterial(int islandId, Enums.Material material, int amount)
    {
        IslandStorage islandStorage = resourceManager.GetIslandStorage(islandId);
        if (islandStorage != null)
        {
            islandStorage.RemoveMaterial(material, amount);
        }
    }

    public void RemoveGood(int islandId, Enums.Good good, int amount)
    {
        IslandStorage islandStorage = resourceManager.GetIslandStorage(islandId);
        if (islandStorage != null)
        {
            islandStorage.RemoveGood(good, amount);
        }
    }

    // remove a resource from a building
    public void RemoveResourceFromBuilding(Island island, Building building, Enums.Resource resource, int amount)
    {
        island.RemoveResourceFromBuilding(building, resource, amount);
    }


}
