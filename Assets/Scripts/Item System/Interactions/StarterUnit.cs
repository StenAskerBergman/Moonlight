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
        
        // Now initialize the slot with the starting item
        foreach (var slot in inventoryUI.inventorySlots)
        {
            slot.InitializeSlot(startingItemData, startingQuantity);
        }
    }

    void AssignStartingItem()
    {
        // Logic to assign the starting item to the boat/unit
    }

}
