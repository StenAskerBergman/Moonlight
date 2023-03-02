using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance;
    private Dictionary<Enums.Resource, int> resources = new Dictionary<Enums.Resource, int>();
    private Dictionary<int, IslandStorage> islandResources = new Dictionary<int, IslandStorage>();

    private Dictionary<Enums.TransportType, Transport> transports = new Dictionary<Enums.TransportType, Transport>();
    private Dictionary<GameObject, IslandStorage> islandStorages = new Dictionary<GameObject, IslandStorage>();
    private Dictionary<int, GameObject> islandObjects = new Dictionary<int, GameObject>();

    private bool isInitialized = false;
    //public bool shouldLogResourceAmounts = false;
    //public bool shouldLogForEachIsland = false;
    //public bool shouldLog = true;

    void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        /*if (shouldLog)
        {
            if (shouldLogForEachIsland) // B: One Line Option
            {
                // Log resource amounts for each island separately
                foreach (int islandId in Enum.GetValues(typeof(int)))
                {
                    if (islandResources.TryGetValue(islandId, out IslandStorage islandStorage))
                    {
                        string logMessage = islandId.ToString();
                        foreach (Enums.Resource resource in Enum.GetValues(typeof(Enums.Resource)))
                        {
                            int count = islandStorage.GetResourceCount(resource);
                            logMessage += ", " + resource.ToString() + ": " + count;
                        }
                        Debug.Log(logMessage);
                    }
                }
            }
            else // C: One Island Line Option
            {
                // Log resource amounts on each island
                string logString = "Resource amounts: ";
                foreach (int islandId in islandResources.Keys)
                {
                    logString += $"{islandId.ToString()} ";
                    foreach (Enums.Resource resource in Enum.GetValues(typeof(Enums.Resource)))
                    {
                        logString += $"{resource.ToString()}: {islandResources[islandId].GetResourceCount(resource)} ";
                    }
                }
                Debug.Log(logString);
            }
        }*/
    }

    private void Start()
    {
        // Create and add IslandStorage objects for each island
        IslandStorage island1Storage = new IslandStorage();
        IslandStorage island2Storage = new IslandStorage();
        IslandStorage island3Storage = new IslandStorage();

        foreach (int islandId in Enum.GetValues(typeof(Enums.Resource)))
        {
            GameObject islandObject = GameObject.Find(islandId.ToString());
            if (islandObject != null)
            {
                islandObjects.Add(islandId, islandObject);

                IslandStorage islandStorage = islandObject.GetComponent<IslandStorage>();
                if (islandStorage != null)
                {
                    islandStorages.Add(islandObject, islandStorage);
                    islandResources.Add(islandId, islandStorage);
                }
            }
        }

    // Initialize transports
    transports.Add(Enums.TransportType.Boat, new Transport(10)); // Change the capacity as needed
    transports.Add(Enums.TransportType.Airplane, new Transport(20));
    transports.Add(Enums.TransportType.Truck, new Transport(15));

    isInitialized = true;
}




    public void TransferResource(int sourceIslandID, int targetIslandID, Enums.TransportType transportType, Enums.Resource resource, int amount)
    {
        Debug.Log("TransferResource called with sourceIslandID: " + sourceIslandID + ", targetIslandID: " + targetIslandID + ", transportType: " + transportType + ", resource: " + resource + ", amount: " + amount);

        if (islandResources.ContainsKey(sourceIslandID) && islandResources.ContainsKey(targetIslandID) && transports.ContainsKey(transportType))
        {
            IslandStorage source = islandResources[sourceIslandID];
            IslandStorage target = islandResources[targetIslandID];
            Transport transport = transports[transportType];

            if (source.RemoveResource(resource, amount))
            {
                transport.AddResource(resource, amount);
            }

            if (target.AddResource(resource, amount))
            {
                transport.RemoveResource(resource, amount);
            }
        }
    }

    /*
    public int GetResourceCount(int islandId, Enums.Resource resource)
    {
        if (islandResources.TryGetValue(islandId, out IslandStorage islandResource) && islandResource != null)
        {
            int count = islandResource.GetIslandStorage(resource);
            Debug.Log(string.Format("{0} on island {1}: {2}", resource, islandId, count));
            return count;
        }

        return 0;
    }


    public bool CheckResourceAvailability(int islandId, Enums.Resource resource, int amount)
    {
        if (islandResources.TryGetValue(islandId, out IslandStorage islandStorage))
        {
            //int count = islandStorage.GetResourceCount(resource);
            //return count >= amount;
        }

        return false;
    }
    */


    public bool CanBuildCheck(int islandId, Enums.Resource resource)
    {
        if (islandResources.ContainsKey(islandId))
        {
            IslandStorage islandStorage = islandResources[islandId];
            return islandStorage.HasResource(resource);
        }
        return false;
    }


    public bool AddResource(Enums.Resource resource, int amount)
    {
        if (resources.ContainsKey(resource))
        {
            resources[resource] += amount;
        }
        else
        {
            resources.Add(resource, amount);
        }

        return true;
    }

    public bool RemoveResource(Enums.Resource resource, int amount)
    {
        if (resources.ContainsKey(resource) && resources[resource] >= amount)
        {
            resources[resource] -= amount;
            return true;
        }

        return false;
    }

    public bool AddResourceToIsland(int islandId, Enums.Resource resource, int amount)
    {
        IslandStorage storage = GetIslandStorage(islandId);
        if (storage != null && storage.AddResource(resource, amount))
        {
            return true;
        }
        return false;
    }

    public bool RemoveResourceFromIsland(int islandId, Enums.Resource resource, int amount)
    {
        IslandStorage storage = GetIslandStorage(islandId);
        if (storage != null && storage.RemoveResource(resource, amount))
        {
            return true;
        }
        return false;
    }

    public IslandStorage GetIslandStorage(int islandId)
    {
        if (islandResources.ContainsKey(islandId))
        {
            return islandResources[islandId];
        }
        return null;
    }

    public void AddIsland(int islandId)
    {
        if (!islandStorages.ContainsKey(islandObjects[islandId]))
        {
            IslandStorage newIslandStorage = islandObjects[islandId].GetComponent<IslandStorage>();
            islandStorages.Add(islandObjects[islandId], newIslandStorage);

            // Add the IslandStorage to the islandResources dictionary
            if (!islandResources.ContainsKey(islandId))
            {
                islandResources.Add(islandId, newIslandStorage);
            }
        }
    }
}
