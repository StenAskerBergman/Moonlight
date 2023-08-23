using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Island : MonoBehaviour
{

    // Variables
        public int id;
        public string islandName;
        public Bounds bounds;
        public Enums.IslandType islandType;
        public GameObject islandObject { get; set; }

    // Refs

        IslandResourceManager islandResourceManager;
        MapManager mapManager;


    // Island resources
        IslandResource islandResource;

    public void Start(){
        islandResource = gameObject.GetComponent<IslandResource>();
        islandResourceManager = gameObject.GetComponent<IslandResourceManager>();
        bounds.center = transform.position;
        bounds.extents = new Vector3(5.0f, 5.0f, 5.0f); // Change these values to your desired size


        // MapManager As Island Parent
        //mapManager = FindObjectOfType<MapManager>(); // locate the amount of islands to be generated
        //this.transform.SetParent(mapManager.transform);
        //LogBounds(); // Bounds Debugging
    }

    public void LogBounds()
    {
        Renderer renderer = GetComponent<Renderer>();
        Debug.Log($"Island: {islandName + "id: " + id} Bounds: {renderer.bounds}");       // island names
        //Debug.Log($"Island: {islandName + "id: " + id} Set Bounds center: {bounds.center}");      // island centers
        //Debug.Log($"Island: {islandName + "id: " + id} Set Bounds extents: {bounds.extents}");     // island extents
    }
    public Island(Enums.IslandType type)
    {
        // Initialize the required fields
        this.islandType = type;
        //buildings = new List<Building>();
        //Resource = new Dictionary<Enums.Resource, int>();
        //Material = new Dictionary<Enums.Material, int>();
        //Good = new Dictionary<Enums.Good, int>();

    }

    // list of buildings on the island
    public List<Building> buildings = new List<Building>();

    // list of Resource on the island
    //public Dictionary<Enums.Resource, int> Resource = new Dictionary<Enums.Resource, int>();

    // list of Material on the island
    //public Dictionary<Enums.Material, int> Material = new Dictionary<Enums.Material, int>();

    // list of Good on the island
    //private Dictionary<Enums.Good, int> Good = new Dictionary<Enums.Good, int>();

    // add a building to the island
    public void AddBuilding(Building building)
    {
        //buildings.Add(building);
    }

    // remove a building from the island
    public void RemoveBuilding(Building building)
    {
        //buildings.Remove(building);
    }

    // add a resource to the island
    // add a resource to the island
    public void AddResource(Enums.Resource resource, int amount)
    {
        islandResource.AddResource(resource, amount);
    }

    // remove a resource from the island
    public bool RemoveResource(Enums.Resource resource, int amount)
    {
        return islandResource.RemoveResource(resource, amount);
    }

    // get the amount of a resource on the island
    public int GetResourceCount(Enums.Resource resource)
    {
        return islandResource.GetResourceAmount(resource);
    }

    // get the amount of a material on the island
    // public int GetMaterialCount(Enums.Material material)
    // {
    //     return islandResource.GetMaterialAmount(material);
    // }

    // // get the amount of a good on the island
    // public int GetGoodCount(Enums.Good good)
    // {
    //     return islandResource.GetGoodAmount(good);
    // }
    // remove a resource from a building on the island - doesn't make any sense to me

    // public bool RemoveResourceFromBuilding(Building building, Enums.Resource resource, int amount)
    // {
    //     int count = building.GetResourceCount(resource);
    //     if (count >= amount)
    //     {
    //         for (int i = 0; i < amount; i++)
    //         {
    //             building.Resources.Remove(resource);
    //         }
    //         return true;
    //     }
    //     return false;
    // }


}
