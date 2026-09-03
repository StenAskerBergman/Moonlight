using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Road Definition", menuName = "Moonlight/Road Definition")]
public sealed class RoadDefinition : ScriptableObject
{
    [Header("Visual language")]
    [SerializeField] private RoadVisualStyle visualStyle = RoadVisualStyle.CityRoad;
    [Range(0f, 1f), Tooltip("Controls deterministic stains, patching, and faded surface variation without changing road gameplay.")]
    [SerializeField] private float wear = 0.45f;

    [Tooltip("Roads in the same connection family join visually. Use the same value for compatible upgrades.")]
    [SerializeField] private string connectionFamily = "basic";
    [Tooltip("Additional families this road may join. Compatibility only needs to be declared by either definition.")]
    [SerializeField] private List<string> compatibleConnectionFamilies = new List<string>();

    [Header("Double-road context")]
    [SerializeField] private bool supportsParallelDoubleRoad;
    [Tooltip("Only directly adjacent roads with the same non-empty double family coordinate their median visuals.")]
    [SerializeField] private string doubleRoadFamily;

    [Header("Bridges")]
    [Tooltip("Allows this road to occupy a short, straight River, Stream, Shallow, or Water span bounded by road-buildable shore on both sides.")]
    [SerializeField] private bool supportsBridges = true;
    [Min(1), Tooltip("Maximum number of consecutive water cells this road may bridge.")]
    [SerializeField] private int maxBridgeSpan = 6;
    [Min(0f), Tooltip("Raises bridge visuals above the water cell height.")]
    [SerializeField] private float bridgeDeckHeight = 0.35f;
    [Tooltip("Optional bridge rules, using the same N=1, E=2, S=4, W=8 masks as normal road visuals. Falls back to RoadPlacer's bridge prefab.")]
    [SerializeField] private List<RoadVisualRule> bridgeVisualRules = new List<RoadVisualRule>();

    [Header("Authored visuals")]
    [Tooltip("Rules are checked in order. Masks use N=1, E=2, S=4, W=8. Author one orientation; rotation matching supplies the rest.")]
    [SerializeField] private List<RoadVisualRule> visualRules = new List<RoadVisualRule>();

    public bool SupportsParallelDoubleRoad => supportsParallelDoubleRoad;
    public string DoubleRoadFamily => doubleRoadFamily;
    public IReadOnlyList<RoadVisualRule> VisualRules => visualRules;
    public bool SupportsBridges => supportsBridges;
    public int MaxBridgeSpan => Mathf.Max(1, maxBridgeSpan);
    public float BridgeDeckHeight => Mathf.Max(0f, bridgeDeckHeight);
    public IReadOnlyList<RoadVisualRule> BridgeVisualRules => bridgeVisualRules;
    public RoadVisualStyle VisualStyle => visualStyle;
    public float Wear => Mathf.Clamp01(wear);

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

public enum RoadVisualStyle
{
    CityRoad,
    Highway,
    TycoonHighway,
    EcoHighway,
    TechHighway
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
