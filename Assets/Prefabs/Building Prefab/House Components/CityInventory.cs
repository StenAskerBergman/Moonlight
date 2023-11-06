using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CityInventory : MonoBehaviour
{
    private Dictionary<string, int> inventory = new Dictionary<string, int>();

    public void AddGood(string goodName, int amount)
    {
        if (!inventory.ContainsKey(goodName))
            inventory[goodName] = 0;

        inventory[goodName] += amount;
    }

    public bool RemoveGood(string goodName, int amount)
    {
        if (!inventory.ContainsKey(goodName) || inventory[goodName] < amount)
            return false; // Not enough goods

        inventory[goodName] -= amount;
        return true;
    }

    public int GetGoodAmount(string goodName)
    {
        return inventory.ContainsKey(goodName) ? inventory[goodName] : 0;
    }
}
