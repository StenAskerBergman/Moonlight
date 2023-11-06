using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingInventoryUI : InventoryUIBase
{

    protected override void Start()
    {
        maxUISlots = 5;  // Assuming buildings can have up to 5 slots. Adjust as needed.
        base.Start();
    }

    // Not sure how we can display both qualities from a building in the ui
    //protected override string FormatItemDisplay(ItemData item, int quantity)
    //{
    //    // Just an example format specific to buildings
    //    return $"{item.displayName} (Using: {quantity} units)";
    //}

    // Override any methods as needed to provide specific functionality for buildings
    protected override string FormatItemDisplay(ItemData itemData, int quantity)
    {
        // Customize how the item is displayed for a building
        return $"{itemData.displayName} (Producing: {itemData.productionRate} / hr)";
    }

}
