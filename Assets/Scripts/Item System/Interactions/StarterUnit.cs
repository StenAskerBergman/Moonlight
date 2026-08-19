using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StarterUnit : MonoBehaviour
{
    ItemData startingItemData;
    int startingQuantity;
    UnitInventoryUI inventoryUI;

    void Start()
    {
        // Example start method in the boat/unit script
        AssignStartingItem();
        
        // Now add the starting item to the unit's inventory
        UnitInventory unitInv = GetComponent<UnitInventory>();
        if (unitInv != null && startingItemData != null && startingQuantity > 0)
        {
            unitInv.AddItem(startingItemData, startingQuantity, "StarterUnit");
        }
        else if (inventoryUI != null && inventoryUI.unitInventory != null && startingItemData != null && startingQuantity > 0)
        {
            inventoryUI.unitInventory.AddItem(startingItemData, startingQuantity, "StarterUnit");
        }
    }

    void AssignStartingItem()
    {
        // Logic to assign the starting item to the boat/unit
    }

}
