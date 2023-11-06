using static Enums;

[System.Serializable]
public class Need
{
    public string needName;
    public float consumptionSpeed;
    public float currentSatisfaction; // 0 to 1
    public Demographics relatedDemographic;
    public bool IsSatisfied { get => currentSatisfaction >= 0.75f; } // Example: A need is satisfied if its satisfaction is >= 75%

    public void ConsumeResource(float amount)
    {
        // Reduce the resource based on consumptionSpeed and increase satisfaction
    }

    public void UpdateSatisfaction()
    {
        // Update the current satisfaction based on resources and other factors
    }
}
