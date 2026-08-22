[System.Serializable]
public class ResourceRequirement : BuildingRequirement
{
    public string requiredResource;

    // Used by per-unit build costs (e.g. BuildInteraction) where the requirement is
    // checked against a carried inventory rather than the island-wide placement rules
    // this base class was originally written for.
    public ItemData item;
    public int amount = 1;

    public override bool IsSatisfied()
    {
        // Logic to check if the required resource is available on the island.
        return true;
    }

    public bool IsSatisfiedBy(int availableQuantity)
    {
        return availableQuantity >= amount;
    }
}
