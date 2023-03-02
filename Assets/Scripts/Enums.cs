using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enums : MonoBehaviour
{
    public enum IslandType
    {
        None,
        Tropical,
        Arctic,
        Desert,
        Volcanic,
        Forest,
        Swamp,
        Mountainous,
        Industrial,
    }
    public enum BuildingType
    {
        // Debug Type
        Default, // Just for Debugging

        // Unique
        SiteBased, // Requires Site
        
        // Normal
        LandBased,      // On Land
        OnShore,        // On Beach
        OffShore,       // On Shallows
        
            // Below Sea
            DeepSea,        // On Plateau

                // Above Land
                Orbital,        // In Atmosphere
                Space,          // In Space
                LunarBased,     // On Moon

    }
    public enum ResourceType
    {
        Resource, // Unobtained resources
        Material, // Obtained materials
        Good // Final goods
    }

    public enum Resource
    {
        Resource1,
        Resource2,
        Resource3,
    }

    public enum Material
    {
        Material1,
        Material2,
        Material3,
    }

    public enum Good
    {
        Good1,
        Good2,
        Good3,
    }

    public enum SeedType
    {
        None,
        SeedType1,
        SeedType2,
        SeedType3
    }

    public enum TransportType
    {
        Boat,
        Airplane,
        Truck
    }
}
