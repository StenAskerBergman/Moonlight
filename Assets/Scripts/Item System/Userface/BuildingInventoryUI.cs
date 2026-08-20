using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingInventoryUI : InventoryUserface
{
    // The rest of this class is still unimplemented, so there is no display context to
    // return yet. When buildings get a displayable inventory, give this class its own
    // serialized field and return it here. It has no unit members to implement,
    // because a building has no unit inventory.
    protected override Inventory DisplayedInventory => null;

    public override void SetInventory(Inventory newInventory)
    {
        Debug.LogWarning($"{name}: BuildingInventoryUI.SetInventory is not implemented yet.");
    }
/*

    protected override void Start()
    {
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
    */
}
