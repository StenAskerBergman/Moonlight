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
        Refined,  // Refind Material
        Default   // Default Material ( For Debugging )
    }

    public enum ResourceType
    {
        Resource1,
        Resource2,
        Resource3,

        Oil,
        Coal,
        Wood,
        Sand,
        Gravel,
        Iron_Ore,
        Copper_Ore,
        Uranium_Ore,
        
        Lobster,
        Fish,
        Crab,
        None
    }

    public enum MaterialType
    {
        // Default
        Material0, // Building Moduls
        Material1, // Building Tools

        // Tycoon
        
            // Tyc 1
            Material2, // Tyc
            Material3, // Tyc
        
            // Tyc 2
            Material4, // Tyc
            Material5, // Tyc


        // Ecology

            // Eco 1
            Material6, // Eco
            Material7, // Eco

            // Eco 2
            Material8, // Eco
            Material9, // Eco

        // Technocrats
            // Tier 1
            Material10, // Sci
            // Tier 2    
            Material11, // Sci
            // Tier 3
            Material12, // Sci

        // Rest Below 

        Moduls,
        Tools,
    
        Concrete,
        Wood,
        
        Plastics,
        Glass,
        
        Carbon,
        None
    }

    public enum GoodType
    {
        // Default
        Good1,
        Good2,
        Good3,

        // Tycoon
        T_Good1,
        T_Good2,
        T_Good3,

        T_Good4,
        T_Good5,
        T_Good6,

        // Eco
        E_Good1,
        E_Good2,
        E_Good3,

        E_Good4,
        E_Good5,
        E_Good6,

        // Science
        S_Good1,
        S_Good2,
        S_Good3,

        S_Good4,
        S_Good5,
        S_Good6,


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

        None
    }

    public enum SeedType
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
