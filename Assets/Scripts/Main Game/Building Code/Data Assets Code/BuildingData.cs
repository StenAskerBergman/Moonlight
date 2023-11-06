// Start - BuildingData.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Main File Goal:
// Hold static universal data, universally for all buildings & built entities ingame 

[CreateAssetMenu(fileName = "New Building Data", menuName = "Data/Building/Building Data")]
public class BuildingData : ScriptableObject
{
    // Basic data for the building
    public string buildingName, buildingDescription, buildingType;
    public Vector3 buildingSize;
    public string[] buildingTags;

    // List of requirements for the building
    public List<BuildingRequirement> BuildingRequirements = new List<BuildingRequirement>();
}

// End - BuildingData.cs