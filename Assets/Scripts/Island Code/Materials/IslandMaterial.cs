using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandMaterial : MonoBehaviour
{
    // Starting Amount
    public int StartAmount = 1000;

    // Current Amount
    public int material1Amount = 100;
    public int material2Amount = 100;
    public int material3Amount = 100;

    IslandItemManager islandItemManager;

    public void Start()
    {
        // Start Values
        material1Amount = material1Amount + StartAmount;
        material2Amount = material2Amount + StartAmount;
        material3Amount = material3Amount + StartAmount;
    }


    // Method to add materials to the island
    public void AddMaterial(ItemEnums.MaterialType material, int amount)
    {
        switch (material)
        {
            case ItemEnums.MaterialType.Material1:
                material1Amount += amount;
                break;
            case ItemEnums.MaterialType.Material2:
                material2Amount += amount;
                break;
            case ItemEnums.MaterialType.Material3:
                material3Amount += amount;
                break;
                // Add more cases for other materials as needed
        }
    }

    // Method to remove materials from the island
    public bool RemoveMaterial(ItemEnums.MaterialType material, int amount)
    {
        switch (material)
        {
            case ItemEnums.MaterialType.Material1:
                if (material1Amount >= amount)
                {
                    material1Amount -= amount;
                    return true;
                }
                break;
            case ItemEnums.MaterialType.Material2:
                if (material2Amount >= amount)
                {
                    material2Amount -= amount;
                    return true;
                }
                break;
            case ItemEnums.MaterialType.Material3:
                if (material3Amount >= amount)
                {
                    material3Amount -= amount;
                    return true;
                }
                break;
                // Add more cases for other materials as needed
        }
        return false;
    }

    // Method to get the current amount of a specific material
    public int GetMaterialAmount(ItemEnums.MaterialType material)
    {
        switch (material)
        {
            case ItemEnums.MaterialType.Material1:
                return material1Amount;
            case ItemEnums.MaterialType.Material2:
                return material2Amount;
            case ItemEnums.MaterialType.Material3:
                return material3Amount;
                // Add more cases for other materials as needed
        }
        return 0;
    }

}
