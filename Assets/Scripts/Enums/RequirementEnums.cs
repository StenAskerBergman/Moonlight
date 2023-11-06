using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RequirementEnums : MonoBehaviour
{

    // This dictionary will map each RequirementType to its respective subtypes
    private Dictionary<RequirementType, List<object>> requirementTypeMap = new Dictionary<RequirementType, List<object>>();

    private void Awake()
    {
        // Initialize each RequirementType with its respective subtypes
        requirementTypeMap.Add(RequirementType.Zone, new List<object>(System.Enum.GetValues(typeof(RequirementSubTypeZone)).OfType<object>()));
        requirementTypeMap.Add(RequirementType.Grid, new List<object>(System.Enum.GetValues(typeof(RequirementSubTypeGrid)).OfType<object>()));
        requirementTypeMap.Add(RequirementType.Cell, new List<object>(System.Enum.GetValues(typeof(RequirementSubTypeCell)).OfType<object>()));
        requirementTypeMap.Add(RequirementType.Item, new List<object>(System.Enum.GetValues(typeof(RequirementSubTypeItem)).OfType<object>()));
        requirementTypeMap.Add(RequirementType.Seed, new List<object>(System.Enum.GetValues(typeof(RequirementSubTypeSeed)).OfType<object>()));
    }


    // Get all subtypes for a specific RequirementType
    public List<object> GetSubTypesForRequirement(RequirementType reqType)
    {
        if (requirementTypeMap.ContainsKey(reqType))
        {
            return requirementTypeMap[reqType];
        }
        return null;
    }

    // Enum to define the main types of requirements
    public enum RequirementType
    {
        Zone,
        Grid,
        Cell,
        Item,
        Seed
    }

    public enum RequirementSubTypeZone
    {

        // Specific Zones for Buildings

        Active, // Active zone for a Building
        Inactive, // Inactive zone for a Building

        GambleZone, // Gambling zone for a casino Building
        InformationZone, // Information zone for a Information Building

        CityZone,  // City zone for a citizens Building
        DepotZone, // Depot zone for a Collector Building

        FarmZone,  // Farm zone for a Farm Building
        RangeZone, // Range zone for a Range Building

        TradeZone, // Trade zone for a harbour Building
        TradeReach, // Trade reach of a harbour Building

        Control, // Region control of a player

        Wind, // Wind zone for a Wind Turbine Building
        Weather, // Weather zone for a Weather Station Building

        Trash, // Trash zone for a Trash Building
        Civilan, // Civilan zone for a Civilan Building

        None, // No Zone Required   

    }

    public enum RequirementSubTypeGrid
    {
        // Specific Grids for Buildings

        // Sea
        SeaGrid, // Sea grid for Sea based Buildings
        UnderwaterGrid, // Underwater grid for Underwater Buildings
        SubmergedGrid, // Submerged grid for Submerged Buildings
        PlataeuGrid, // Plataeu grid for Plataeuo Buildings

        // Land
        LandGrid, // Land grid for Land based Buildings 
        ShoreGrid, // Shore grid for Shore based Buildings
        IslandGrid, // Island grid for Island based Buildings

        // Air
        SkyGrid, // Sky grid for Sky based Buildings
        AirGrid, // Air grid for Air based Buildings

        // Space
        SpaceGrid, // Space grid for Space based Buildings
        OrbitGrid, // Orbit grid for Orbit based Buildings

        None, // No Grid Required

    }

    public enum RequirementSubTypeCell
    {
        ReqShore,
        ReqSea,
        ReqSub,
        ReqLand,
        ReqOther,
        None
    }

    public enum RequirementSubTypeItem
    {
        ReqA,
        ReqB,
        ReqC,
        ReqD,
        ReqE,

        Req_Iron,
        Req_Copper,
        Req_Stone,
        Req_Wood,
        Req_Food,
        Req_Uranium,
        Req_Oil,
        Req_Gold,
        Req_Silver,
        Req_Diamond,
        Req_Gems,
        Req_Coal,
        Req_Sand,
        Req_Glass,
        Req_Bricks,
        Req_Cement,
        Req_Concrete,
        Req_Steel,
        Req_Bronze,
        Req_Brass,
        Req_Aluminum,
        Req_Titanium,
        Req_Plastic,
        Req_Ceramic,
        Req_Paper,
    }

    public enum RequirementSubTypeSeed
    {
        None,

        SeedType1,
        SeedType2,
        SeedType3,

        Sallad,
        Spices,
        Coffee,
        Fruit,
        Suger,
        Rice,
        Tea,
    }
    
}
