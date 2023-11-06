using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Author: Sten
// Purpose: Manage Building
public class BuildingManager : MonoBehaviour
{
    public BuildingData buildingData;

    public bool AreBuildingRequirementsMet()
    {
        foreach (BuildingRequirement req in buildingData.BuildingRequirements)
        {
            if (!req.IsSatisfied())
                return false;
        }
        return true;
    }
}
