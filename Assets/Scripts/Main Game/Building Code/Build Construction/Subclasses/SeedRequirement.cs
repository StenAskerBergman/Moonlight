using UnityEngine;


[CreateAssetMenu(fileName = "Seed Requirement", menuName = "Building/Requirements/Seed")] //[System.Serializable]
public class SeedRequirement : BuildingRequirement
{
    public string requiredSeed;

    public override bool IsSatisfied()
    {
        // Logic to check if the required seed is present on the island.
        // For example purposes, always returning true. Replace with your actual logic.
        return true;
    }
}
