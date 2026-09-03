using UnityEngine;
using System.Collections;
using System.Collections.Generic;
//using UnityEditor.SceneManagement;

public class BaseStorageManager : StorageManager
{
    public BaseStorage baseStorage { get; private set; } // Making it publicly readable but only settable within the class.


    // C# Constructor
    public BaseStorageManager(Storage storage) : base(storage) { }

    // Unity never calls a constructor with arguments, and declaring only the one above
    // removes the implicit parameterless constructor - which BuildingPlacer needs, because
    // it falls back to AddComponent<BaseStorageManager>() for an island that has none.
    public BaseStorageManager() { }

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
    // Logic for affordability check - returns true if the player can afford the building.
    // BuildingCost owns the "what does this cost" question, including what an unassigned
    // CostData means, so this no longer reaches through to costData itself.
    public bool CanAffordBuilding(BuildingCost buildingCost)
    {
        if (buildingCost == null) return true;

        Dictionary<ItemData, int> costItems;
        if (!buildingCost.TryGetCosts(out costItems)) return true;

        if (baseStorage == null)
        {
            Debug.LogWarning($"{name}: no BaseStorage to pay from, refusing the purchase.");
            return false;
        }

        Dictionary<ItemData, int> currentItems = baseStorage.GetAllItems();
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
        Dictionary<ItemData, int> costItems;
        if (buildingCost == null || !buildingCost.TryGetCosts(out costItems)) return;
        if (baseStorage == null) return;

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
