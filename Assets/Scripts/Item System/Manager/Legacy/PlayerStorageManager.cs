using System.Collections.Generic;
using System;
using UnityEngine;

public class PlayerStorageManager : MonoBehaviour
{
    private Dictionary<int, PlayerIslandInventory> islandStorages = new Dictionary<int, PlayerIslandInventory>();

    public int CurrentIslandID { get; private set; }

    #region Start/Destroy + OnPlayerEnter Methods
    private void Start()
    {
        // No need to initialize storage here as we will initialize them as we encounter new islands

        // Subscribe to the event from the IslandManager
        IslandManager.instance.OnPlayerEnterIsland += OnPlayerEnterIsland;
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent potential memory leaks
        IslandManager.instance.OnPlayerEnterIsland -= OnPlayerEnterIsland;
    }

    private void OnPlayerEnterIsland(Island island)
    {
        // Update the current island ID
        CurrentIslandID = island.id;

        // If this is the first time encountering this island, initialize its storage
        if (!islandStorages.ContainsKey(CurrentIslandID))
        {
            islandStorages[CurrentIslandID] = new PlayerIslandInventory();
        }
    }
    #endregion

    #region +/- Item Methods

    // Resource
    public bool AddResource(ItemEnums.ResourceType resource, int amount)
    {
        if (islandStorages.TryGetValue(CurrentIslandID, out PlayerIslandInventory storage))
        {
            return storage.AddResource(resource, amount);
        }
        return false;
    }

    public bool RemoveResource(ItemEnums.ResourceType resource, int amount)
    {
        if (islandStorages.TryGetValue(CurrentIslandID, out PlayerIslandInventory storage))
        {
            return storage.RemoveResource(resource, amount);
        }
        return false;
    }

            // Materials
            public bool AddMaterials(ItemEnums.MaterialType material, int amount)
            {
                if (islandStorages.TryGetValue(CurrentIslandID, out PlayerIslandInventory storage))
                {
                    return storage.AddMaterial(material, amount);
                }
                return false;
            }

            public bool RemoveMaterials(ItemEnums.MaterialType material, int amount)
            {
                if (islandStorages.TryGetValue(CurrentIslandID, out PlayerIslandInventory storage))
                {
                    return storage.RemoveMaterial(material, amount);
                }
                return false;
            }

                    // Goods
                    public bool AddGoods(ItemEnums.GoodType good, int amount)
                    {
                        if (islandStorages.TryGetValue(CurrentIslandID, out PlayerIslandInventory storage))
                        {
                            return storage.AddGood(good, amount);
                        }
                        return false;
                    }
                    public bool RemoveGood(ItemEnums.GoodType good, int amount)
                    {
                        if (islandStorages.TryGetValue(CurrentIslandID, out PlayerIslandInventory storage))
                        {
                            return storage.RemoveGood(good, amount);
                        }
                        return false;
                    }

    #endregion

    [System.Serializable]
    private class PlayerIslandInventory
    {
        private Dictionary<ItemEnums.ResourceType, int> resources = new Dictionary<ItemEnums.ResourceType, int>();
        private Dictionary<ItemEnums.MaterialType, int> materials = new Dictionary<ItemEnums.MaterialType, int>();
        private Dictionary<ItemEnums.GoodType, int> goods = new Dictionary<ItemEnums.GoodType, int>();

        #region Resource methods
        public bool AddResource(ItemEnums.ResourceType resource, int amount)
        {
            if (resources.ContainsKey(resource))
            {
                resources[resource] += amount;
                return true;
            }
            else
            {
                resources.Add(resource, amount);
                return true;
            }
        }

        public bool RemoveResource(ItemEnums.ResourceType resource, int amount)
        {
            if (resources.ContainsKey(resource) && resources[resource] >= amount)
            {
                resources[resource] -= amount;
                return true;
            }
            return false;
        }
        #endregion

        #region Material methods
        public bool AddMaterial(ItemEnums.MaterialType material, int amount)
        {
            if (materials.ContainsKey(material))
            {
                materials[material] += amount;
                return true;
            }
            else
            {
                materials.Add(material, amount);
                return true;
            }
        }

        public bool RemoveMaterial(ItemEnums.MaterialType material, int amount)
        {
            if (materials.ContainsKey(material) && materials[material] >= amount)
            {
                materials[material] -= amount;
                return true;
            }
            return false;
        }
        #endregion

        #region Good methods
        public bool AddGood(ItemEnums.GoodType good, int amount)
        {
            if (goods.ContainsKey(good))
            {
                goods[good] += amount;
                return true;
            }
            else
            {
                goods.Add(good, amount);
                return true;
            }
        }

        public bool RemoveGood(ItemEnums.GoodType good, int amount)
        {
            if (goods.ContainsKey(good) && goods[good] >= amount)
            {
                goods[good] -= amount;
                return true;
            }
            return false;
        }
        #endregion
    }
}
