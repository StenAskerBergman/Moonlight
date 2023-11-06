[System.Serializable]
public class ResourceRequirement : BuildingRequirement
{
    public string requiredResource;

    public override bool IsSatisfied()
    {
        // Logic to check if the required resource is available on the island.
        return true;
    }
}
