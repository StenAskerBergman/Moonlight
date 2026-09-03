// Start - BuildingData.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Main File Goal:
// Hold static universal data, universally for all buildings & built entities ingame 

[CreateAssetMenu(fileName = "New Building Data", menuName = "Data/Building/Building Data")]
public class BuildingData : ScriptableObject, IIdentifiable
{
    [Header("Identity")]
    [Tooltip("Namespaced identifier (e.g. 'core:worker_resident', 'core:coastal_warehouse', 'modname:custom_refinery').")]
    [SerializeField] private string identifier = "core:building";

    public Identifier Id => !string.IsNullOrEmpty(identifier) 
        ? new Identifier(identifier) 
        : new Identifier($"core:{name.ToLowerInvariant().Replace(' ', '_')}");

    // Basic data for the building
    public string buildingName, buildingDescription, buildingType;
    public Vector3 buildingSize;
    public string[] buildingTags;

    [Header("Harbor Foundation")]
    [Tooltip("Builds this footprint on the island's shared quay platform. Quay buildings may span beach, coast, shallow water, and water cells.")]
    public bool requiresQuayFoundation;

    [Tooltip("Cells of open deck the quay adds around the footprint on every side. The " +
             "building stands ON the dock rather than being the dock, so 0 would leave it " +
             "flush with the retaining wall, with nowhere to walk, moor or decorate.")]
    [Min(0)] public int quayFoundationPadding = 2;

    // Resource node the building must sit on top of (e.g. a Mine on Mountain cells).
    // None means no deposit is required.
    public ResourceNodeType requiredNodeType = ResourceNodeType.None;

    // List of requirements for the building
    public List<BuildingRequirement> BuildingRequirements = new List<BuildingRequirement>();
}

// End - BuildingData.cs
