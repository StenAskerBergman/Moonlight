using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Transport : MonoBehaviour
{
    private int capacity;
    private Dictionary<ItemEnums.ResourceType, int> resources = new Dictionary<ItemEnums.ResourceType, int>();


    public Transport(int capacity)
    {
        this.capacity = capacity;
    }
    
    public bool AddResource(ItemEnums.ResourceType resource, int amount)
    {
        if (resources.ContainsKey(resource))
        {
            int currentAmount = resources[resource];
            int newAmount = currentAmount + amount;
            if (newAmount <= capacity)
            {
                resources[resource] = newAmount;
                return true;
            }
        }
        else
        {
            if (amount <= capacity)
            {
                resources.Add(resource, amount);
                return true;
            }
        }

        return false;
    }

    public bool RemoveResource(ItemEnums.ResourceType resource, int amount)
    {
        if (resources.ContainsKey(resource))
        {
            int currentAmount = resources[resource];
            int newAmount = currentAmount - amount;
            if (newAmount >= 0)
            {
                resources[resource] = newAmount;
                return true;
            }
        }
        return false;
    }
}


// Maybe Legacy?´Maybe Not, Not Sure!

// Initialize transports
//transports.Add(TransportEnums.TransportType.Boat, new Transport(10)); // Change the capacity as needed
//transports.Add(TransportEnums.TransportType.Airplane, new Transport(20));
//transports.Add(TransportEnums.TransportType.Truck, new Transport(15));



/*
public void TransferResource(int sourceIslandID, int targetIslandID, TransportEnums.TransportType transportType, ItemEnums.ResourceType resource, int amount)
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
*/