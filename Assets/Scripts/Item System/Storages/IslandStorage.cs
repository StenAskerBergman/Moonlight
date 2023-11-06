using UnityEngine;

public class IslandStorage : Storage
{
    // Island-specific methods and properties.

    // For instance, if there are any unique behaviors, you can override the base methods:

    public override void AddItem(ItemData itemData, int quantity)
    {
        base.AddItem(itemData, quantity);
        // Custom logic for Island storage if needed...
    }

    // ... any other specific functionality or overrides for the Island storage.
}
