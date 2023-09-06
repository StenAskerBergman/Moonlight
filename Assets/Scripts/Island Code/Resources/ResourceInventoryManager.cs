using System.Collections.Generic;

public class ResourceInventoryManager
{
    private Dictionary<Enums.IslandType, IslandStorage> inventories;

    public ResourceInventoryManager()
    {
        inventories = new Dictionary<Enums.IslandType, IslandStorage>();
    }

    public void AddIslandStorage(Enums.IslandType island, IslandStorage inventory)
    {
        inventories[island] = inventory;
    }

    public IslandStorage GetIslandStorage(Enums.IslandType island)
    {
        if (inventories.ContainsKey(island))
        {
            return inventories[island];
        }
        return null;
    }

    public bool TryGetIslandStorage(Enums.IslandType island, out IslandStorage islandStorage)
    {
        return inventories.TryGetValue(island, out islandStorage);
    }
}
