using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Transport : MonoBehaviour
{
    private int capacity;
    private Dictionary<Enums.Resource, int> resources = new Dictionary<Enums.Resource, int>();

    public Transport(int capacity)
    {
        this.capacity = capacity;
    }
    public bool AddResource(Enums.Resource resource, int amount)
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

    public bool RemoveResource(Enums.Resource resource, int amount)
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
