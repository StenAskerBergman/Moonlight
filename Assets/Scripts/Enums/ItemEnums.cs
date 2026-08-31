using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemEnums : MonoBehaviour
{
    public enum ItemType
    {
        ResourceItem, // Unobtained resources
        MaterialItem, // Obtained materials
        GoodItem,     // Final goods
        
        None,       // Nothing Fits
        Consumable, // For Consumables
        Seed,       // For Island Seeds

        Raw,      // Raw Material
        Refined,  // Refined Material
        Default   // Default Material ( For Debugging )
    }

    public enum ResourceType
    {
        RawGravel,
        RawCoal,

        IronOre,
        RawWood,

        RawSand,
        CopperOre,
        RawOil,
        UraniumOre,
        
        RawLobster,
        RawFish,
        RawCrab,
        Grain,
        Plantain,
        None
    }

    public enum MaterialType
    {
        // Default
        BuildingModules,
    
        Coppar,

        // Tier 1
        Tools,

        // Tier 2
        Concrete, 
        Wood,

        // Tier 3
        Glass,
        Steel,
        

        // Tier 4
        Carbon,
        Kerosene,
        None
    }

    public enum GoodType
    {
        Tea,
        Rice,
        Fish,
        Sushi,
        Booze,
        Phones,
        Robots,
        Plastics,
        Hamburgers,
        Electronics,
        Grain,
        Plantain,

        None
    }

    public enum SeedType
    {
        Sallad,
        Spices,
        Coffee,
        Fruit,
        Suger,
        Rice,
        Tea,

        None
    }
}
