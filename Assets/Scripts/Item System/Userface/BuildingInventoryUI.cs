using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingInventoryUI : InventoryUserface
{
    // A building UI has no unit and, as yet, no display context of its own - the rest
    // of this class is still unimplemented. These are stubbed explicitly rather than
    // inherited so the decision stays visible: when buildings get a displayable
    // inventory, give this class its own serialized field and return it here.
    // Nothing below the UI layer changes; item authority stays with the storage
    // services either way.
    protected override Inventory DisplayedInventory => null;
    protected override UnitInventory DisplayedUnitInventory => null;

    public override void SetInventory(Inventory newInventory)
    {
        Debug.LogWarning($"{name}: BuildingInventoryUI.SetInventory is not implemented yet.");
    }

    public override void SetUnitInventory(UnitInventory newUnitInventory)
    {
        Debug.LogWarning($"{name}: BuildingInventoryUI has no unit inventory to display.");
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
