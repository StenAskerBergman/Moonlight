using UnityEngine;

/// <summary>
/// Central access point for global data registries in Moonlight.
/// Decouples systems from hardcoded asset paths or closed C# enums.
/// </summary>
public static class GameRegistries
{
    public static readonly Registry<BuildingData> Buildings = new Registry<BuildingData>("Buildings");
    public static readonly Registry<ItemData> Items = new Registry<ItemData>("Items");
    public static readonly Registry<UnitDefinition> Units = new Registry<UnitDefinition>("Units");

    /// <summary>
    /// Resets registries (useful for testing or hot-reload).
    /// </summary>
    public static void ResetForTesting()
    {
        Buildings.Clear();
        Items.Clear();
        Units.Clear();
    }
}
