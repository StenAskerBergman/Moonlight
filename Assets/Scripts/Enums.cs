using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enums : MonoBehaviour
{
    public enum Moods
    {
        Happy,
        Pleased,
        Content,
        Displeased,
        Frustrated,
        Angry,
        Leave
    }

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
    public enum RequirementType
    {
        ReqShore,
        ReqSea,
        ReqSub,
        ReqLand,
        ReqOther
    }
    public enum ConditionType
    {
        // Bool
        Condition_Resource_Exist,        // Condition Island Resource Exists 
        Condition_Power_Exist,           // Condition Island Power Exists 

        // Ints
        Condition_Resource_Has,          // Condition Island Resource is Above X Amount
        Condition_Power_Has,             // Condition Island Power is Above X Amount
        Condition_Ecology_Positive,      // Condition Island Ecology is Positive X Amount
        Condition_Ecology_Negative,      // Condition Island Ecology is Negative X Amount
        Condition_Ecology_Level,         // Condition Island Ecology is Above Level X 
        Condition_Buildings,             // Condition Island Buildings Within Range
        Condition_Seed_Type,             // Condition Island has Seeds Type X 
    }
    public enum BuildingType
    {
        // Debug Type
        Default,        // Just for Debugging

        // Unique
        SiteBased,      // Requires Site
        
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

    public RequirementType[] Requirements = new RequirementType[]
    {
            RequirementType.ReqShore,   // Requires Shoreline
            RequirementType.ReqSea,     // Requires Above at Sea
            RequirementType.ReqSub,     // Requires Above Submerged
            RequirementType.ReqLand,    // Requires Only Land
            RequirementType.ReqOther    // Requires Edge Cases
    };
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
