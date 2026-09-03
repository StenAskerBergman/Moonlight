using System;
using UnityEngine;

public enum ConstructionSection
{
    Production,
    Infrastructure,
    Ornaments,
}

public enum ProductionConnectionType
{
    Horizontal,
    MergeFromAbove,
    MergeFromBelow,
    VerticalJoin,
}

[Serializable]
public sealed class ProductionNodeDefinition
{
    [Tooltip("Stable identifier used by connections within this production line.")]
    public string Id;

    public BuildingData BuildingData;

    [Tooltip("Optional authored label. Falls back to BuildingData.buildingName, then Id.")]
    public string DisplayName;

    [Tooltip("Optional placeholder/override while building art is unavailable.")]
    public Sprite Icon;

    [Min(0)] public int Column;
    [Min(0)] public int Row;
    public PopulationUnlock UnlockCondition;
}

[Serializable]
public sealed class ProductionConnectionDefinition
{
    public string FromNodeId;
    public string ToNodeId;
    public ProductionConnectionType Type;

    [Tooltip("0-1 position of the vertical junction between the source and target columns.")]
    [Range(0.1f, 0.9f)] public float JunctionPosition = 0.5f;
}

[Serializable]
public sealed class ProductionLineDefinition
{
    public string Id;
    public string DisplayName;
    public Sprite OutputIcon;

    [Tooltip("Civilisation page that owns this line. This is independent of its population unlock gate.")]
    public PopulationClass Tier;

    public PopulationUnlock UnlockCondition;
    public ProductionNodeDefinition[] Nodes = Array.Empty<ProductionNodeDefinition>();
    public ProductionConnectionDefinition[] Connections = Array.Empty<ProductionConnectionDefinition>();

    public ProductionNodeDefinition FindNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId) || Nodes == null) return null;

        foreach (ProductionNodeDefinition node in Nodes)
        {
            if (node != null && string.Equals(node.Id, nodeId, StringComparison.Ordinal))
            {
                return node;
            }
        }

        return null;
    }
}
