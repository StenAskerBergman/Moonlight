using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingCost : MonoBehaviour
{
    public int cost; // Cost of the building
    public Enums.Resource resourceType; // The type of resource needed to build the building
    
    public int GetValue()
    {
        return cost;
    }

    public Enums.Resource GetResourceType()
    {
        return resourceType;
    }

}
