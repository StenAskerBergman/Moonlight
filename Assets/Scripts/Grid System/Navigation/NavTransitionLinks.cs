using UnityEngine;
using UnityEngine.AI;

using NavMeshLink = Unity.AI.Navigation.NavMeshLink;

/// <summary>
/// Stitches the stacked NavMesh layers together with NavMeshLinks, which is what
/// turns "dive" and "lift off" into things the path planner can route *through*
/// rather than commands a player has to issue by hand.
///
/// The constraint that shapes all of this: a NavMeshLink carries exactly one
/// agentTypeID, so a link can only ever join two layers of the same agent type.
/// There is no cross-agent-type traversal in Unity. That is why Submarine owns
/// both the surface and deep layers, and Aircraft owns the apron and all three
/// altitude bands - a dive is Submarine-to-Submarine, a take-off is
/// Aircraft-to-Aircraft.
///
/// Links are placed on a coarse grid wherever both layers actually exist at that
/// XZ, so a submarine can only dive where the seabed is deep enough and an
/// aircraft can only climb where there is sky above it. Capability is then pure
/// area masking: a hull that cannot dive clears the Dive bit and the planner stops
/// offering it dive routes, with no branching in the movement code.
/// </summary>
public class NavTransitionLinks : MonoBehaviour
{
    [System.Serializable]
    public class LinkSet
    {
        [Tooltip("Name for the hierarchy and the logs, e.g. Dive or Lift Off.")]
        public string label = "Dive";

        [Tooltip("Agent type name from Navigation > Agents. Both ends must be this type.")]
        public string agentTypeName = NavAgentTypes.Submarine;

        [Tooltip("Height of the lower layer, and the area that identifies it.")]
        public float lowerHeight = -30f;
        public int lowerArea = NavAreas.DeepSea;

        [Tooltip("Height of the upper layer, and the area that identifies it.")]
        public float upperHeight = 0f;
        public int upperArea = NavAreas.Ocean;

        [Tooltip("Area assigned to the link itself. Its cost comes from Navigation > Areas.")]
        public int linkArea = NavAreas.Dive;

        [Tooltip("-1 uses the link area's own cost, which is normally what you want.")]
        public int costModifier = -1;

        [Tooltip("Off means the link is one-way, lower to upper. Use two one-way sets " +
                 "with different costs to make climbing dearer than descending.")]
        public bool bidirectional = true;

        [Tooltip("Distance between candidate transition points. Smaller means more " +
                 "places to dive or take off, and more links to keep.")]
        public float spacing = 50f;

        [Tooltip("Width of the link corridor.")]
        public float width = 6f;

        [Tooltip("How far a candidate point may be from each layer and still count.")]
        public float sampleTolerance = 4f;
    }

    [SerializeField]
    private LinkSet[] linkSets =
    {
        new LinkSet
        {
            label = "Dive", agentTypeName = NavAgentTypes.Submarine,
            lowerHeight = -30f, lowerArea = NavAreas.DeepSea,
            upperHeight = 0f, upperArea = NavAreas.Ocean,
            linkArea = NavAreas.Dive, bidirectional = true, spacing = 40f, width = 6f,
        },
        new LinkSet
        {
            label = "Lift Off", agentTypeName = NavAgentTypes.Aircraft,
            lowerHeight = 0f, lowerArea = NavAreas.Walkable,
            upperHeight = 25f, upperArea = NavAreas.LowAltitude,
            linkArea = NavAreas.LowAltitude, bidirectional = true, spacing = 100f, width = 8f,
        },
        new LinkSet
        {
            label = "Climb Low-Mid", agentTypeName = NavAgentTypes.Aircraft,
            lowerHeight = 25f, lowerArea = NavAreas.LowAltitude,
            upperHeight = 50f, upperArea = NavAreas.MidAltitude,
            linkArea = NavAreas.MidAltitude, bidirectional = true, spacing = 100f, width = 8f,
        },
        new LinkSet
        {
            label = "Climb Mid-High", agentTypeName = NavAgentTypes.Aircraft,
            lowerHeight = 50f, lowerArea = NavAreas.MidAltitude,
            upperHeight = 75f, upperArea = NavAreas.HighAltitude,
            linkArea = NavAreas.HighAltitude, bidirectional = true, spacing = 100f, width = 8f,
        },
    };

    [Tooltip("Width and depth of the area searched for transition points. Should cover the map.")]
    [SerializeField] private float mapSize = 1000f;

    [Tooltip("Centre of that area.")]
    [SerializeField] private Vector3 mapCentre = Vector3.zero;

    [Tooltip("Log how many links each set produced.")]
    [SerializeField] private bool logLinks = false;

    private const string RootName = "Generated Nav Links";

    /// <summary>
    /// Rebuilds every link set for one agent type. Must run *after* both layers have
    /// baked - a link needs NavMesh at each end to attach to, so building it against
    /// an unbaked layer silently produces a link that connects nothing.
    /// </summary>
    public int BuildLinks(string agentTypeName)
    {
        if (!NavAgentTypes.Exists(agentTypeName)) return 0;

        int agentTypeID = NavAgentTypes.Id(agentTypeName);
        Transform root = EnsureRoot();

        int total = 0;
        for (int i = 0; i < linkSets.Length; i++)
        {
            LinkSet set = linkSets[i];
            if (set.agentTypeName != agentTypeName) continue;

            // Replace rather than accumulate - a rebake after a terraform must not
            // leave links hanging over water that is no longer deep enough.
            Transform stale = root.Find(set.label);
            if (stale != null) DestroyImmediate(stale.gameObject);

            GameObject setRoot = new GameObject(set.label);
            setRoot.transform.SetParent(root, false);

            int built = BuildSet(set, agentTypeID, setRoot.transform);
            total += built;

            if (logLinks)
            {
                Debug.Log($"<color=cyan>NavTransitionLinks:</color> '{set.label}' " +
                          $"({agentTypeName}) built {built} link(s).");
            }
        }

        return total;
    }

    private int BuildSet(LinkSet set, int agentTypeID, Transform parent)
    {
        NavMeshQueryFilter lowerFilter = new NavMeshQueryFilter
        {
            agentTypeID = agentTypeID,
            areaMask = 1 << set.lowerArea,
        };

        NavMeshQueryFilter upperFilter = new NavMeshQueryFilter
        {
            agentTypeID = agentTypeID,
            areaMask = 1 << set.upperArea,
        };

        float spacing = Mathf.Max(set.spacing, 1f);
        float half = mapSize * 0.5f;
        int built = 0;

        for (float z = -half; z <= half; z += spacing)
        {
            for (float x = -half; x <= half; x += spacing)
            {
                Vector3 column = new Vector3(mapCentre.x + x, 0f, mapCentre.z + z);

                NavMeshHit low;
                if (!NavMesh.SamplePosition(new Vector3(column.x, set.lowerHeight, column.z),
                                            out low, set.sampleTolerance, lowerFilter)) continue;

                NavMeshHit high;
                if (!NavMesh.SamplePosition(new Vector3(column.x, set.upperHeight, column.z),
                                            out high, set.sampleTolerance, upperFilter)) continue;

                CreateLink(parent, set, agentTypeID, low.position, high.position, built);
                built++;
            }
        }

        return built;
    }

    private static void CreateLink(Transform parent, LinkSet set, int agentTypeID,
                                   Vector3 lower, Vector3 upper, int index)
    {
        GameObject go = new GameObject($"{set.label} {index}");
        go.transform.SetParent(parent, false);
        go.transform.position = lower;

        NavMeshLink link = go.AddComponent<NavMeshLink>();
        link.agentTypeID = agentTypeID;
        link.area = set.linkArea;
        link.bidirectional = set.bidirectional;
        link.costModifier = set.costModifier;
        link.width = set.width;

        // Endpoints are local to the link's transform.
        link.startPoint = Vector3.zero;
        link.endPoint = upper - lower;
    }

    private Transform EnsureRoot()
    {
        Transform existing = transform.Find(RootName);
        if (existing != null) return existing;

        GameObject go = new GameObject(RootName);
        go.transform.SetParent(transform, false);
        return go.transform;
    }
}
