using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Main File Goal:
// Hold static universal data, universally for all buildings & built entities ingame 

[CreateAssetMenu(fileName = "New Building Data", menuName = "Building Data")]
public class BuildingData : ScriptableObject
{
    // Building Qualities, Conditions & Requirement
    public Vector3 buildingSize;
    public Enums.ConditionType[] Conditions;
    public Enums.RequirementType[] Requirements;
    

    public int expense, cost, price;  
    
    // Building Text
    public string buildingName, buildingDescription, buildingType;

    // Building Materials, Shaders & Textures
    public Material material;


    // The cost of various building material
    public int[] costResource;
    
    [SerializeField] public int costResource1; 
    [SerializeField] public int costResource2; 
    [SerializeField] public int costResource3; 
    [SerializeField] public int costResource4; 

    [SerializeField] public int costResource5;
    [SerializeField] public int costResource6;
    [SerializeField] public int costResource7;
    [SerializeField] public int costResource8;
    [SerializeField] public int costResource9;
    [SerializeField] public int costResource10;
}
