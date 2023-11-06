using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Zone Requirement", menuName = "Building/Requirements/Grid")]
public class ZoneRequirement : BuildingRequirement
{
    public string zoneName;

    public override bool IsSatisfied()
    {
        // Check if the zone requirement is satisfied
        // ... (implement your logic here)
        return true;
    }
}

