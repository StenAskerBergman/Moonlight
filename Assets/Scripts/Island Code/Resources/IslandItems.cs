using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandItems : MonoBehaviour
{

    /*
        Add Enum for resource start amounts
        Create a Randomizer Function that
        decideds what value a island gets
        once it's added.

        If its a player island randomize
        differently than the others. And
        based it of pre-defined settings.

        add options to manipulate and ctrl
        the randomness in favour for what
        we want it to produce.

        > Create More resoureful options - Easy
        > Create Less resoureful options - Normal
        > Create None resoureful options - Hard
        
    */
    
    /*  Resourcefulness Enum Name Options 

        -Empty	
        -Little	
        -Some	
        -Okay	
        -Decent	
        -Good	
        -Lots	
        -High	
        -Giga	
        -Unreal
          
          or

        Barren Atoll
        Rocky Outcropping
        Arid Plateau
        Resource-Poor Island
        Sparse Terrain
        Low-Yield Peninsula
        Meager Coastline
        Modestly Abundant Isle
        Fertile Grounds
        Resource-Rich Archipelago
    */

        // Starting Amount
        public int Resource_StartAmount = 10000;
        public int Material_StartAmount = 10;
        public int Good_StartAmount = 10;

    // Current Resource Amount
        public int resource0Amount = 100;
        public int resource1Amount = 100;
        public int resource2Amount = 100;
        public int resource3Amount = 100;

        // Current Material Amount
        public int material1Amount = 40;
        public int material2Amount = 40;
        public int material3Amount = 40;

        // Current Good Amount
        public int good1Amount = 100;
        public int good2Amount = 100;
        public int good3Amount = 100;

    IslandItemManager islandItemManager;

    public void Start(){

        // Start Values
        resource0Amount = resource0Amount + Resource_StartAmount;
        resource1Amount = resource1Amount + Resource_StartAmount;
        resource2Amount = resource2Amount + Resource_StartAmount;
        resource3Amount = resource3Amount + Resource_StartAmount;

        material1Amount = material1Amount + Material_StartAmount;
        material2Amount = material2Amount + Material_StartAmount;
        material3Amount = material3Amount + Material_StartAmount;


        // No Goods to start With!
        /*
        good1Amount = good1Amount + Good_StartAmount;
        good2Amount = good2Amount + Good_StartAmount;
        good3Amount = good3Amount + Good_StartAmount;
        */
    }


    // Method to add resources to the island
    public void AddResource(ItemEnums.ResourceType resource, int amount)
    {
        switch (resource)
        {
            case ItemEnums.ResourceType.Resource1:
                resource1Amount += amount;
                break;
            case ItemEnums.ResourceType.Resource2:
                resource2Amount += amount;
                break;
            case ItemEnums.ResourceType.Resource3:
                resource3Amount += amount;
                break;
            // Add more cases for other resources as needed
        }
    }

    // Method to remove resources from the island
    public bool RemoveResource(ItemEnums.ResourceType resource, int amount)
    {
        switch (resource)
        {
            case ItemEnums.ResourceType.Resource1:
                if (resource1Amount >= amount)
                {
                    resource1Amount -= amount;
                    return true;
                }
                break;
            case ItemEnums.ResourceType.Resource2:
                if (resource2Amount >= amount)
                {
                    resource2Amount -= amount;
                    return true;
                }
                break;
            case ItemEnums.ResourceType.Resource3:
                if (resource3Amount >= amount)
                {
                    resource3Amount -= amount;
                    return true;
                }
                break;
            // Add more cases for other resources as needed
        }
        return false;
    }

    // Method to get the current amount of a specific resource
    public int GetResourceAmount(ItemEnums.ResourceType resource)
    {
        switch (resource)
        {
            case ItemEnums.ResourceType.Resource1:
                return resource1Amount;
            case ItemEnums.ResourceType.Resource2:
                return resource2Amount;
            case ItemEnums.ResourceType.Resource3:
                return resource3Amount;
                // Add more cases for other resources as needed
        }
        return 0;
    }

    // Method to get the current amount of a specific material
    public int GetMaterialAmount(ItemEnums.MaterialType material)
    {
        switch (material)
        {
            case ItemEnums.MaterialType.Material1:
                return material1Amount;
            case ItemEnums.MaterialType.Material2:
                return material2Amount;
            case ItemEnums.MaterialType.Material3:
                return material3Amount;
                // Add more cases for other resources as needed
        }
        return 0;
    }

    // Method to get the current amount of a specific good
    public int GetGoodAmount(ItemEnums.GoodType good)
    {
        switch (good)
        {
            case ItemEnums.GoodType.Good1:
                return material1Amount;
            case ItemEnums.GoodType.Good2:
                return material2Amount;
            case ItemEnums.GoodType.Good3:
                return material3Amount;
                // Add more cases for other resources as needed
        }
        return 0;
    }


    //// Method to get the current amount of a specific item
    //public int GetItemsAmount(ItemEnums.ItemType itemType,
    //                          ItemEnums.ResourceType resourceType = ItemEnums.ResourceType.None,
    //                          ItemEnums.MaterialType materialType = ItemEnums.MaterialType.None,
    //                          ItemEnums.GoodType goodType = ItemEnums.GoodType.None)
    //{
    //    switch (itemType)
    //    {
    //        case ItemEnums.ItemType.ResourceItem:
    //            switch (resourceType)
    //            {
    //                case ItemEnums.ResourceType.Resource1:
    //                    return resource1Amount;
    //                case ItemEnums.ResourceType.Resource2:
    //                    return resource2Amount;
    //                case ItemEnums.ResourceType.Resource3:
    //                    return resource3Amount;
    //                    // Add more cases for other resources as needed
    //            }
    //            break;

    //        case ItemEnums.ItemType.MaterialItem:
    //            switch (materialType)
    //            {
    //                case ItemEnums.MaterialType.Material1:
    //                    return material1Amount;
    //                case ItemEnums.MaterialType.Material2:
    //                    return material2Amount;
    //                case ItemEnums.MaterialType.Material3:
    //                    return material3Amount;
    //                    // Add more cases for other materials as needed
    //            }
    //            break;

    //        case ItemEnums.ItemType.GoodItem:
    //            switch (goodType)
    //            {
    //                case ItemEnums.GoodType.Good1:
    //                    return good1Amount;
    //                case ItemEnums.GoodType.Good2:
    //                    return good2Amount;
    //                case ItemEnums.GoodType.Good3:
    //                    return good3Amount;
    //                    // Add more cases for other goods as needed
    //            }
    //            break;
    //    }
    //    return 0;
    //}


    //// Method to get the current amount of a specific resource
    //public int GetItemsAmount(ItemEnums.ItemType itemType, ItemEnums.ResourceType resourceType || ItemEnums.MaterialType materialType || ItemEnums.GoodType goodType)
    //{
    //    switch (itemType)
    //    {
    //        case ItemEnums.ItemType.ResourceItem:
    //            switch (resourceType)
    //            {
    //                case ItemEnums.ResourceType.Resource1:
    //                    return resource1Amount;
    //                case ItemEnums.ResourceType.Resource2:
    //                    return resource2Amount;
    //                case ItemEnums.ResourceType.Resource3:
    //                    return resource3Amount;
    //                    // Add more cases for other resources as needed
    //            }
    //            break;

    //        case ItemEnums.ItemType.MaterialItem:
    //            switch (materialType)
    //            {
    //                case ItemEnums.ResourceType.Material1:
    //                    return material1Amount;
    //                case ItemEnums.ResourceType.Material2:
    //                    return material2Amount;
    //                case ItemEnums.ResourceType.Material3:
    //                    return material3Amount;
    //                    // Add more cases for other resources as needed
    //            }
    //            // Add switch cases for material items
    //            break;

    //        case ItemEnums.ItemType.GoodItem:
    //            switch (goodsType)
    //            {
    //                case ItemEnums.ResourceType.Good1:
    //                    return good1Amount;
    //                case ItemEnums.ResourceType.Good2:
    //                    return good2Amount;
    //                case ItemEnums.ResourceType.Good3:
    //                    return good3Amount;
    //                    // Add more cases for other resources as needed
    //            }
    //            // Add switch cases for good items
    //            break;
    //    }
    //    return 0;
    //}


    // More Methods
}