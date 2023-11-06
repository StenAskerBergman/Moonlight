using System.Collections;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

public class BaseStorageManager : StorageManager
{
    public BaseStorage baseStorage { get; private set; } // Making it publicly readable but only settable within the class.


    // C# Constructor
    public BaseStorageManager(Storage storage) : base(storage)
    {
        this.baseStorage = baseStorage;
    }

    // Unity Constructor
    private void Awake()
    {
        baseStorage = GetComponent<BaseStorage>();
        if (!baseStorage)
        {
            baseStorage = gameObject.AddComponent<BaseStorage>();
        }
        if (!storage)
        {
            storage = baseStorage;
        }
    }
    // Logic for affordability check - returns true if the player can afford the building
    public bool CanAffordBuilding(BuildingCost buildingCost)
    {
        Dictionary<ItemData, int> currentItems = baseStorage.GetAllItems();
        Dictionary<ItemData, int> costItems = buildingCost.costData.GetCostItemsDictionary();

        foreach (var item in costItems)
        {
            if (!currentItems.ContainsKey(item.Key) || currentItems[item.Key] < item.Value)
            {
                return false;
            }
        }
        return true;
    }

    public bool DeductBuildingCosts(BuildingCost buildingCost, Bank bankReference)
    {
        // Here, we directly use the local baseStorage to deduct costs
        if (!CanAffordBuilding(buildingCost))
        {
            return false;
        }

        DeductCosts(buildingCost);
        return true;
    }

    public void DeductCosts(BuildingCost buildingCost)
    {
        Dictionary<ItemData, int> costItems = buildingCost.costData.GetCostItemsDictionary();
        foreach (var item in costItems)
        {
            baseStorage.RemoveItem(item.Key, item.Value);
        }
    }

    //  Capacity Related
    public void AddBonusCapacityFromStructure(int bonusCapacity)
    {
        baseStorage.AddBonusCapacityFromStructure(bonusCapacity);
    }

    public void RemoveBonusCapacityFromStructure(int bonusCapacity)
    {
        baseStorage.RemoveBonusCapacityFromStructure(bonusCapacity);
    }

    public void AddOtherEnhancementsSize(int enhancementSize)
    {
        baseStorage.AddOtherEnhancementsSize(enhancementSize);
    }

    // This function checks if we can add a specific quantity of items to the base storage
    public override bool CanAddItem(ItemData itemData, int quantity)
    {
        return baseStorage.HasCapacityForItems(quantity);
    }

    // Register to the full capacity event of the BaseStorage
    public void RegisterToOnFullCapacityEvent(BaseStorage.OnFullCapacity method)
    {
        baseStorage.onFullCapacityEvent += method;
    }

    // Unregister from the full capacity event of the BaseStorage
    public void UnregisterFromOnFullCapacityEvent(BaseStorage.OnFullCapacity method)
    {
        baseStorage.onFullCapacityEvent -= method;
    }
}
