using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Grid Type Requirement", menuName = "Building/Requirements/Grid")]
public class GridRequirement : BuildingRequirement
{
    public GridType gridType;
    public enum GridType { island, plataeu, coastal, shore, other }; // Add more when ya need

    public override bool IsSatisfied()
    {
        // Check if the grid requirement is satisfied
        // ... (implement your logic here)
        return true;
    }
}