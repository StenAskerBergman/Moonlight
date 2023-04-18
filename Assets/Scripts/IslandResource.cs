using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandResource : MonoBehaviour
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
    public int StartAmount = 10000;

    // Current Amount
    public int resource1Amount = 100;
    public int resource2Amount = 100;
    public int resource3Amount = 100;
    IslandResourceManager islandResourceManager;

    public void Start(){
        // Start Values
        resource1Amount = resource1Amount + StartAmount;
        resource2Amount = resource2Amount + StartAmount;
        resource3Amount = resource3Amount + StartAmount;
    }


    // Method to add resources to the island
    public void AddResource(Enums.Resource resource, int amount)
    {
        switch (resource)
        {
            case Enums.Resource.Resource1:
                resource1Amount += amount;
                break;
            case Enums.Resource.Resource2:
                resource2Amount += amount;
                break;
            case Enums.Resource.Resource3:
                resource3Amount += amount;
                break;
            // Add more cases for other resources as needed
        }
    }

    // Method to remove resources from the island
    public bool RemoveResource(Enums.Resource resource, int amount)
    {
        switch (resource)
        {
            case Enums.Resource.Resource1:
                if (resource1Amount >= amount)
                {
                    resource1Amount -= amount;
                    return true;
                }
                break;
            case Enums.Resource.Resource2:
                if (resource2Amount >= amount)
                {
                    resource2Amount -= amount;
                    return true;
                }
                break;
            case Enums.Resource.Resource3:
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
    public int GetResourceAmount(Enums.Resource resource)
    {
        switch (resource)
        {
            case Enums.Resource.Resource1:
                return resource1Amount;
            case Enums.Resource.Resource2:
                return resource2Amount;
            case Enums.Resource.Resource3:
                return resource3Amount;
            // Add more cases for other resources as needed
        }
        return 0;
    }

    // More Methods
}