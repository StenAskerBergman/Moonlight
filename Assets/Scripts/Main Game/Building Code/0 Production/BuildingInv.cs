using System.Collections.Generic;
using System.Collections;
using System.Resources;
using UnityEngine;

public class BuildingInv : MonoBehaviour
{
    // BuildingInventory Class
    // BuildingInv short for BuildingInventory
    #region Inventory Variables

    #region Fetched Data

    // Fetch Build Data 
    public BuildingData data;

    // Fetch Inventory Data 
    // public BuildingInventoryData inventoryData;

    // Fetch List of Resources 
    public List<Enums.Resource> Resources { get; set; } = new List<Enums.Resource>();

    #endregion

    #region Private Data
    
    // Building Inventory level
    private int currentInventory;

    #endregion


    #endregion

    #region Default Methods

    void Awake()
    {
        //currentInventory = inventoryData.inventorySize;
    }
    void Start()
    {

    }
    void Update()
    {

    }
    #endregion

    #region Inventory Related Methods

    // Get Resource Count
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

    //// Add Resource To Building
    //public void AddResourceToBuilding(Enums.Resource resource)
    //{
    //    if (Resources == null)
    //    {
    //        Resources = new List<Enums.Resource>();
    //    }
    //    Resources.Add(resource);
    //}

    //// Remove a resource from a building
    //public void RemoveResourceFromBuilding(Island island, Building building, Enums.Resource resource, int amount)
    //{
    //    island.RemoveResourceFromBuilding(building, resource, amount);
    //}

    //// Removes Resource
    //public void RemoveResource(int islandId, Enums.Resource resource, int amount)
    //{
    //    Island island = IslandManager.instance.GetIsland(islandId);
    //    if (island != null)
    //    {
    //        island.RemoveResourceFromBuilding(this, resource, amount);
    //    }
    //}

    //// Removes Material
    //public void RemoveMaterial(int islandId, Enums.Material material, int amount)
    //{
    //    IslandStorage islandStorage = resourceManager.GetIslandStorage(islandId);
    //    if (islandStorage != null)
    //    {
    //        islandStorage.RemoveMaterial(material, amount);
    //    }
    //}

    //// Removes Goods
    //public void RemoveGood(int islandId, Enums.Good good, int amount)
    //{
    //    IslandStorage islandStorage = resourceManager.GetIslandStorage(islandId);
    //    if (islandStorage != null)
    //    {
    //        islandStorage.RemoveGood(good, amount);
    //    }
    //}



    #endregion

}
