using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Island : MonoBehaviour
{


    // Variables
    public int id;
    public Bounds bounds;
    public Enums.IslandType islandType;
    public string islandName;
    public GameObject islandObject { get; set; }
    //public GameObject gameObject { get; set; }

    public Island(Enums.IslandType type)
    {
        // Initialize the required fields
        this.islandType = type;
        buildings = new List<Building>();
        Resource = new Dictionary<Enums.Resource, int>();
        Material = new Dictionary<Enums.Material, int>();
        Good = new Dictionary<Enums.Good, int>();
    }
    // list of buildings on the island
    public List<Building> buildings = new List<Building>();

    // list of Resource on the island
    public Dictionary<Enums.Resource, int> Resource = new Dictionary<Enums.Resource, int>();

    // list of Material on the island
    public Dictionary<Enums.Material, int> Material = new Dictionary<Enums.Material, int>();

    // list of Good on the island
    private Dictionary<Enums.Good, int> Good = new Dictionary<Enums.Good, int>();

    // add a building to the island
    public void AddBuilding(Building building)
    {
        buildings.Add(building);
    }

    // remove a building from the island
    public void RemoveBuilding(Building building)
    {
        buildings.Remove(building);
    }

    // add a resource to the island
    public void AddResource(Enums.Resource resource, int amount)
    {
        if (Resource.ContainsKey(resource))
        {
            Resource[resource] += amount;
        }
        else
        {
            Resource.Add(resource, amount);
        }
    }

    // remove a resource from the island
    public bool RemoveResource(Enums.Resource resource, int amount)
    {
        if (Resource.ContainsKey(resource) && Resource[resource] >= amount)
        {
            Resource[resource] -= amount;
            return true;
        }
        return false;
    }
    // remove a resource from a building on the island - doesn't make any sense to me
    public bool RemoveResourceFromBuilding(Building building, Enums.Resource resource, int amount)
    {
        int count = building.GetResourceCount(resource);
        if (count >= amount)
        {
            for (int i = 0; i < amount; i++)
            {
                building.Resources.Remove(resource);
            }
            return true;
        }
        return false;
    }
    
    // get the amount of a resource on the island
    public int GetResourceCount(Enums.Resource resource)
    {
        if (Resource.ContainsKey(resource))
        {
            return Resource[resource];
        }
        return 0;
    }

    // get the amount of a material on the island
    public int GetMaterialCount(Enums.Material material)
    {
        if (Material.ContainsKey(material))
        {
            return Material[material];
        }
        return 0;
    }

    // get the amount of a good on the island
    public int GetGoodCount(Enums.Good good)
    {
        if (Good.ContainsKey(good))
        {
            return Good[good];
        }
        return 0;
    }
    
}
