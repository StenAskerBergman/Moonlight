public enum ResourceNodeType
{
    None = 0,
    // Land
    Mine = 1,           // Mountain/MountainPeak cells
    ForestGrove = 2,    // Forest cells (if present, else high-density Land)
    // River
    RiverBank = 3,      // Land cells directly adjacent to River cells
    LakeMouth = 4,      // RiverMouth cells where river meets Water/Lake
    // Coast
    CoastalFishery = 5, // Beach cells
    // Value 6 was the retired CoastalDock shoreline-start marker.
    // Underwater
    OreSeabed = 7,      // Plateau cells (deep seabed mineral deposits)
    HydrothermalVent = 8, // Abyssal cells
    CrudeOil = 9,       // 3x3 extraction sites on buildable underwater plateaus
}
