// InventoryBehaviour.cs

public abstract class InventoryBehaviour
{
    public abstract void AddItem(Item item); // Just an example method
    public abstract void RemoveItem(Item item);
    // ... any other methods you think all inventories should implement
}

public class IslandInventoryBehaviour : InventoryBehaviour
{
    private BuildingInventoryData referenceInventory; // This is the other island inventory this one refers to

    public override void AddItem(Item item)
    {
        // Add the item to the reference inventory instead of this one
        referenceInventory.AddItem(item);
    }

    public override void RemoveItem(Item item)
    {
        // Remove the item from the reference inventory
        referenceInventory.RemoveItem(item);
    }
}


// ... More implementations for other inventory types
public abstract class BuildingInventoryBehaviour
{
    protected IslandInventory mainInventory; // Reference to the island's primary inventory

    public BuildingInventoryBehaviour(IslandInventory islandInventory)
    {
        mainInventory = islandInventory;
    }

    public abstract void AddItem(ItemData itemData, int amount);
    public abstract void RemoveItem(ItemData itemData, int amount);
}

public class DepotInventoryBehaviour : BuildingInventoryBehaviour
{
    public DepotInventoryBehaviour(IslandInventory islandInventory) : base(islandInventory) { }

    public override void AddItem(ItemData itemData, int amount)
    {
        // For example, maybe a depot can't add certain items or has a different behavior
        mainInventory.AddItem(itemData, amount);
    }

    public override void RemoveItem(ItemData itemData, int amount)
    {
        mainInventory.RemoveItem(itemData, amount);
    }
}

public class HarbourInventoryBehaviour : BuildingInventoryBehaviour
{
    public HarbourInventoryBehaviour(IslandInventory islandInventory) : base(islandInventory) { }

    public override void AddItem(ItemData itemData, int amount)
    {
        // Maybe the harbour has a fee or some other logic when adding items
        mainInventory.AddItem(itemData, amount);
    }

    public override void RemoveItem(ItemData itemData, int amount)
    {
        // Here, you'd handle exporting items, calculating trade fees, etc.
        mainInventory.RemoveItem(itemData, amount);
    }
}
