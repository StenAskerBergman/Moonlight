using System.Collections.Generic;
using UnityEngine;

public class BuildingInventory : MonoBehaviour
{
    #region Inventory Variables

    #region Fetched Data

    // Fetch Build Data 
    public BuildingData data;

    // Fetch Inventory Data 
    public BuildingInventoryData inventoryData;

    // List of Resources 
    public List<ItemEnums.ResourceType> Resources { get; private set; } = new List<ItemEnums.ResourceType>();

    #endregion

    #region Private Data

    // Building Inventory level
    private int currentInventory;

    #endregion

    #endregion

    #region Default Methods

    private void Awake()
    {
        currentInventory = inventoryData != null ? inventoryData.inventoryCapacity : 30;
        Building building = GetComponent<Building>();
        if (building != null && building.buildingInventory == null) building.buildingInventory = this;
    }

    #endregion

    #region Local Inventory Related Methods

    // Get Resource Count
    public int GetResourceCount(ItemEnums.ResourceType resource)
    {
        int count = 0;
        foreach (ItemEnums.ResourceType r in Resources)
        {
            if (r == resource)
            {
                count++;
            }
        }
        return count;
    }

    #region Add/Remove Items From Building

    // Add Resource To Building
    public void AddResourceToBuilding(ItemEnums.ResourceType resource)
    {
        if (Resources.Count < currentInventory) Resources.Add(resource);
    }

    public int FreeCapacity => Mathf.Max(0, currentInventory - Resources.Count);

    public int AddResourceToBuilding(ItemEnums.ResourceType resource, int amount)
    {
        int accepted = Mathf.Min(Mathf.Max(0, amount), FreeCapacity);
        for (int i = 0; i < accepted; i++) Resources.Add(resource);
        return accepted;
    }

    public bool TryRemoveResource(ItemEnums.ResourceType resource, int amount)
    {
        if (amount <= 0 || GetResourceCount(resource) < amount) return false;
        for (int i = 0; i < amount; i++) Resources.Remove(resource);
        return true;
    }

    // Removes Resource To Building
    public void RemoveResourceFromBuilding(ItemEnums.ResourceType resource)
    {
        Resources.Remove(resource);
    }

    #endregion

        #region Remove Methods 

        // Using either string or int for ID

        #region String ID

        // Removes Resource from the island (needs proper linking with Island system)
        public void RemoveResource(string islandID, ItemEnums.ResourceType resource, int amount)
        {
            // TODO: Fetch Island via IslandManager and remove the resource from it.
        }

        // Removes Material (needs proper linking with Island system)
        public void RemoveMaterial(string islandID, ItemEnums.MaterialType material, int amount)
        {
            // TODO: Fetch Island via IslandManager, get its storage and remove the material from it.
        }

        // Removes Goods (needs proper linking with Island system)
        public void RemoveGood(string islandID, ItemEnums.GoodType good, int amount)
        {
            // TODO: Fetch Island via IslandManager, get its storage and remove the good from it.
        }
        #endregion

        #region Int ID
        // Removes Resource from the island (needs proper linking with Island system)
        public void RemoveResource(int islandId, ItemEnums.ResourceType resource, int amount)
        {
            // TODO: Fetch Island via IslandManager and remove the resource from it.
        }

        // Removes Material (needs proper linking with Island system)
        public void RemoveMaterial(int islandId, ItemEnums.MaterialType material, int amount)
        {
            // TODO: Fetch Island via IslandManager, get its storage and remove the material from it.
        }

        // Removes Goods (needs proper linking with Island system)
        public void RemoveGood(int islandId, ItemEnums.GoodType good, int amount)
        {
            // TODO: Fetch Island via IslandManager, get its storage and remove the good from it.
        }
        #endregion

        #endregion

    #endregion
}
