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
        if (inventoryUI != null)
        {
            var slots = (inventoryUI.itemSlots != null && inventoryUI.itemSlots.Length > 0)
                ? inventoryUI.itemSlots
                : (inventoryUI.inventorySlots != null ? inventoryUI.inventorySlots.ToArray() : null);

            if (slots != null)
            {
                foreach (var slot in slots)
                {
                    if (slot != null)
                    {
                        slot.InitializeSlot(startingItemData, startingQuantity);
                    }
                }
            }
        }
    }

    void AssignStartingItem()
    {
        // Logic to assign the starting item to the boat/unit
    }

}
