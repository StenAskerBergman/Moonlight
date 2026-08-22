public enum ResourceNodeType
{
    None,
    // Land
    Mine,           // Mountain/MountainPeak cells
    ForestGrove,    // Forest cells (if present, else high-density Land)
    // River
    RiverBank,      // Land cells directly adjacent to River cells
    LakeMouth,      // RiverMouth cells where river meets Water/Lake
    // Coast
    CoastalFishery, // Beach cells
    CoastalDock,    // Beach cells adjacent to Shallow water
    // Underwater
    OreSeabed,      // Plateau cells (deep seabed mineral deposits)
    HydrothermalVent, // Abyssal cells
}
