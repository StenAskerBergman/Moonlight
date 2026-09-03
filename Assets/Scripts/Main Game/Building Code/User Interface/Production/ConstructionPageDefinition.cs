using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Construction Page", menuName = "Moonlight/Construction/Page Definition")]
public sealed class ConstructionPageDefinition : ScriptableObject
{
    public Enums.Faction Faction;
    public ConstructionSection Section = ConstructionSection.Production;
    public ProductionLineDefinition[] ProductionLines = Array.Empty<ProductionLineDefinition>();

    public IEnumerable<string> ValidateDefinition()
    {
        var lineIds = new HashSet<string>();

        if (ProductionLines == null) yield break;

        foreach (ProductionLineDefinition line in ProductionLines)
        {
            if (line == null)
            {
                yield return "Page contains a null production line.";
                continue;
            }

            if (string.IsNullOrWhiteSpace(line.Id))
                yield return "A production line has no Id.";
            else if (!lineIds.Add(line.Id))
                yield return $"Duplicate production line Id '{line.Id}'.";

            var nodeIds = new HashSet<string>();
            if (line.Nodes != null)
            {
                foreach (ProductionNodeDefinition node in line.Nodes)
                {
                    if (node == null)
                    {
                        yield return $"Line '{line.Id}' contains a null node.";
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(node.Id))
                        yield return $"Line '{line.Id}' contains a node with no Id.";
                    else if (!nodeIds.Add(node.Id))
                        yield return $"Line '{line.Id}' has duplicate node Id '{node.Id}'.";
                }
            }

            if (line.Connections == null) continue;

            foreach (ProductionConnectionDefinition connection in line.Connections)
            {
                if (connection == null)
                {
                    yield return $"Line '{line.Id}' contains a null connection.";
                    continue;
                }

                if (!nodeIds.Contains(connection.FromNodeId) || !nodeIds.Contains(connection.ToNodeId))
                {
                    yield return $"Line '{line.Id}' connection '{connection.FromNodeId}' -> " +
                                 $"'{connection.ToNodeId}' references a missing node.";
                }
            }
        }
    }
}
