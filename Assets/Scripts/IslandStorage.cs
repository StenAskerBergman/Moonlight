using System.Collections.Generic;
using UnityEngine;
using System;

public class IslandStorage : MonoBehaviour
{
    public Enums.IslandType Island { get; set; }

    public Dictionary<Enums.Resource, int> GetAllResources()
    {
        return Resource;
    }
    public Dictionary<Enums.Material, int> GetAllMaterials()
    {
        return Material;
    }
    public Dictionary<Enums.Good, int> GetAllGoods()
    {
        return Good;
    }
    private Dictionary<Enums.Material, int> Material = new Dictionary<Enums.Material, int>();
    private Dictionary<Enums.Good, int> Good = new Dictionary<Enums.Good, int>();
    private Dictionary<Enums.Resource, int> Resource = new Dictionary<Enums.Resource, int>();

    private Transport transportComponent;

    private Dictionary<Enums.IslandType, ResourceInventory> inventories = new Dictionary<Enums.IslandType, ResourceInventory>();
    private Dictionary<Enums.IslandType, GameObject> islandObjects = new Dictionary<Enums.IslandType, GameObject>();

    private void Awake()
    {
        foreach (Enums.IslandType island in Enum.GetValues(typeof(Enums.IslandType)))
        {
            GameObject islandObject = GameObject.Find(island.ToString());
            if (islandObject != null)
            {
                islandObjects.Add(island, islandObject);

                IslandStorage islandStorage = islandObject.GetComponent<IslandStorage>();
                if (islandStorage != null)
                {
                    inventories.Add(island, islandStorage.GetIslandStorage(island));
                }
            }
        }

        if (islandObjects.TryGetValue(Island, out GameObject gameObject) && gameObject != null)
        {
            Island = this.Island;
        }

        transportComponent = GetComponent<Transport>();
    }

    // public int GetResourceCount(int islandId, Enums.Resource resource)
    // {
    //     Enums.IslandType islandType = (Enums.IslandType)islandId;
    //     ResourceInventory resourceInventory = GetIslandStorage(islandType);
    //     if (resourceInventory != null)
    //     {
    //         int count = resourceInventory.GetResourceCount(resource);
    //         Debug.Log(string.Format("{0} on island {1}: {2}", resource, islandId, count));
    //         return count;
    //     }

    //     return 0;
    // }



// Has Section
    // has a resource to the islandStorage
    public bool HasResource(Enums.Resource resource)
    {
        return Resource.ContainsKey(resource);
    }
    // has a material to the islandStorage
    public bool HasMaterial(Enums.Material material)
    {
        return Material.ContainsKey(material);
    }
    // has a Good to the islandStorage
    public bool HasGood(Enums.Good good)
    {
        return Good.ContainsKey(good);
    }
// bool Add Section
    // add a resource to the island
    public bool AddResource(Enums.Resource resource, int amount)
    {
        if (Resource.ContainsKey(resource))
        {
            Resource[resource] += amount;
            return true;
        }
        else
        {
            Resource.Add(resource, amount);
            return true;
        }
        return false;
    }
    // add a material to the islandStorage
    public bool AddMaterial(Enums.Material material, int amount)
    {
        if (Material.ContainsKey(material))
        {
            Material[material] += amount;
            return true;
        }
        else
        {
            Material.Add(material, amount);
            return true;
        }
        //return false;
    }

    // add a good to the islandStorage
    public bool AddGood(Enums.Good good, int amount)
    {
        if (Good.ContainsKey(good))
        {
            Good[good] += amount;
            return true;
        }
        else
        {
            Good.Add(good, amount);
            return true;
        }
        //return false;
    }

// Add Section
    // add a resource to the island
    /*
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
    // add a material to the islandStorage
    public void AddMaterial(Enums.Material material, int amount)
    {
        if (Material.ContainsKey(material))
        {
            Material[material] += amount;
        }
        else
        {
            Material.Add(material, amount);
        }
    }

    // add a good to the islandStorage
    public void AddGood(Enums.Good good, int amount)
    {
        if (Good.ContainsKey(good))
        {
            Good[good] += amount;
        }
        else
        {
            Good.Add(good, amount);
        }
    }*/
// Get Section
    // Get Storage
    public ResourceInventory GetIslandStorage(Enums.IslandType island)
    {
        if (inventories.TryGetValue(island, out ResourceInventory inventory))
        {
            return inventory;
        }
        return null;
    }
    
    // Get Island / Get IslandType
    public Enums.IslandType GetIslandType()
    {
        return this.Island;
    }
// Bool Remove Section
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
    // remove a material from the islandStorage
    public bool RemoveMaterial(Enums.Material material, int amount)
    {
        if (Material.ContainsKey(material) && Material[material] >= amount)
        {
            Material[material] -= amount;
            return true;
        }
        return false;
    }

    // remove a good from the islandStorage
    public bool RemoveGood(Enums.Good good, int amount)
    {
        if (Good.ContainsKey(good) && Good[good] >= amount)
        {
            Good[good] -= amount;
            return true;
        }
        return false;
    }
}