using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Helpers for ordering an agent between stacked NavMesh layers.
///
/// Because the layers are joined by NavMeshLinks of the same agent type, moving
/// between them is an ordinary SetDestination - the planner finds the nearest
/// usable dive or climb link on its own. There is no "switch the agent to another
/// mode" step, and deliberately so: an agent-type switch would let a unit change
/// layer anywhere, and would leave the planner unable to route *through* a
/// transition when a destination on the far layer is what was actually ordered.
/// </summary>
public static class NavLayerTransit
{
    /// <summary>
    /// Orders the agent onto the layer identified by <paramref name="area"/>,
    /// directly below or above where it currently is. Returns false when that layer
    /// does not exist here - shallow water for a dive, a mountain for a climb -
    /// which is the natural place for a "cannot dive here" response.
    /// </summary>
    public static bool MoveToLayer(NavMeshAgent agent, float targetHeight, int area,
                                   float tolerance = 8f)
    {
        if (agent == null || !agent.isOnNavMesh) return false;
        if (!IsAreaAllowed(agent, area)) return false;

        Vector3 here = agent.transform.position;

        NavMeshHit hit;
        if (!SampleLayer(agent, new Vector3(here.x, targetHeight, here.z), area, tolerance, out hit))
            return false;

        return agent.SetDestination(hit.position);
    }

    /// <summary>True if the layer identified by <paramref name="area"/> exists at this XZ.</summary>
    public static bool LayerExistsHere(NavMeshAgent agent, float targetHeight, int area,
                                       float tolerance = 8f)
    {
        if (agent == null) return false;

        Vector3 here = agent.transform.position;
        NavMeshHit hit;
        return SampleLayer(agent, new Vector3(here.x, targetHeight, here.z), area, tolerance, out hit);
    }

    /// <summary>
    /// Whether this unit is permitted on an area at all. A hull that cannot dive
    /// simply clears the Dive bit in its agent's Area Mask, and every capability
    /// check below reduces to this - no per-unit branching required.
    /// </summary>
    public static bool IsAreaAllowed(NavMeshAgent agent, int area)
    {
        return agent != null && (agent.areaMask & (1 << area)) != 0;
    }

    /// <summary>
    /// The NavMesh area the agent is currently standing on, or -1 if it is not on a
    /// NavMesh. Useful for "is this submarine submerged" style questions without
    /// tracking state separately.
    /// </summary>
    public static int CurrentArea(NavMeshAgent agent)
    {
        if (agent == null || !agent.isOnNavMesh) return -1;

        NavMeshHit hit;
        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask,
        };

        if (!NavMesh.SamplePosition(agent.transform.position, out hit, 2f, filter)) return -1;

        // NavMeshHit.mask is a one-bit area mask; turn it back into an index.
        for (int area = 0; area < 32; area++)
        {
            if ((hit.mask & (1 << area)) != 0) return area;
        }

        return -1;
    }

    private static bool SampleLayer(NavMeshAgent agent, Vector3 probe, int area,
                                    float tolerance, out NavMeshHit hit)
    {
        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,

            // Restricted to the one area so the probe cannot snap back onto the layer
            // the unit is already on - the layers overlap in XZ and differ only in
            // height, so an unfiltered sample picks whichever is nearest.
            areaMask = 1 << area,
        };

        return NavMesh.SamplePosition(probe, out hit, tolerance, filter);
    }
}
