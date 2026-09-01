using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Road Definition", menuName = "Moonlight/Road Definition")]
public sealed class RoadDefinition : ScriptableObject
{
    [Tooltip("Roads in the same connection family join visually. Use the same value for compatible upgrades.")]
    [SerializeField] private string connectionFamily = "basic";
    [Tooltip("Additional families this road may join. Compatibility only needs to be declared by either definition.")]
    [SerializeField] private List<string> compatibleConnectionFamilies = new List<string>();

    [Header("Double-road context")]
    [SerializeField] private bool supportsParallelDoubleRoad;
    [Tooltip("Only directly adjacent roads with the same non-empty double family coordinate their median visuals.")]
    [SerializeField] private string doubleRoadFamily;

    [Header("Authored visuals")]
    [Tooltip("Rules are checked in order. Masks use N=1, E=2, S=4, W=8. Author one orientation; rotation matching supplies the rest.")]
    [SerializeField] private List<RoadVisualRule> visualRules = new List<RoadVisualRule>();

    public bool SupportsParallelDoubleRoad => supportsParallelDoubleRoad;
    public string DoubleRoadFamily => doubleRoadFamily;
    public IReadOnlyList<RoadVisualRule> VisualRules => visualRules;

    public bool ConnectsTo(RoadDefinition other)
    {
        if (other == null) return false;
        if (ReferenceEquals(this, other) || connectionFamily == other.connectionFamily) return true;
        return compatibleConnectionFamilies.Contains(other.connectionFamily)
            || other.compatibleConnectionFamilies.Contains(connectionFamily);
    }

    public bool FormsDoubleRoadWith(RoadDefinition other)
    {
        return other != null
            && supportsParallelDoubleRoad
            && other.supportsParallelDoubleRoad
            && !string.IsNullOrWhiteSpace(doubleRoadFamily)
            && doubleRoadFamily == other.doubleRoadFamily;
    }
}

[Serializable]
public sealed class RoadVisualRule
{
    [Range(0, 15)] public int connectionMask;
    [Range(0, 15), Tooltip("0 for a normal rule. Double-road rules identify which side contains a parallel partner.")]
    public int parallelMask;
    public GameObject prefab;
    [Tooltip("Additional authored Y rotation before automatic mask rotation.")]
    public float yRotation;
}
