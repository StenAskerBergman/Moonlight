using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Main File Goal:
// Hold static universal data, universally for all buildings & built entities ingame 

[CreateAssetMenu(fileName = "New Building Data", menuName = "Building Data")]
public class BuildingData : ScriptableObject
{
    // Building Qualities, Conditions & Requirement
    public ConditionEnums.ConditionType[] Conditions;
    public RequirementEnums.RequirementType[] Requirements;
    public Vector3 buildingSize;
        
    // Building Text
    public string buildingName, buildingDescription, buildingType;
    public string[] buildingTags;

    // Building Materials, Shaders & Textures
    public ItemEnums.MaterialType material;

}
