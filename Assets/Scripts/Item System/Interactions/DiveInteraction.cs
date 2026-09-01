using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Submarine dive/surface orders.
///
/// The old TODO list here asked for checks on hull type, cooldown and whether the
/// water is deep enough. Two of those are no longer this script's job:
///
///  - "is this hull rated to dive" is the Dive bit in the agent's Area Mask. Clear
///    it and the planner stops offering dive routes at all.
///  - "is the water deep enough" is whether the deep layer exists at this XZ.
///    StackedNavMeshLayers only builds it over deep seabed, so the question answers
///    itself when the sample fails.
///
/// What remains here is the order itself and the cooldown.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class DiveInteraction : MonoBehaviour, IDiveable
{
    [Tooltip("Height of the deep-sea layer. Must match the Sub - Deep band.")]
    [SerializeField] private float deepHeight = -30f;

    [Tooltip("Height of the surface layer.")]
    [SerializeField] private float surfaceHeight = 0f;

    [Tooltip("Seconds before this hull can change depth again.")]
    [SerializeField] private float cooldown = 8f;

    [Tooltip("How far from the target layer the unit may be and still reach it.")]
    [SerializeField] private float sampleTolerance = 8f;

    /// <summary>Raised when an order is refused, with the reason. Hook UI to this.</summary>
    public event System.Action<string> OnDiveRefused;

    /// <summary>Raised after a successful dive or surface order, with the new submerged state.</summary>
    public event System.Action<bool> OnDiveStateChanged;

    private NavMeshAgent agent;
    private NavLinkTraversal traversal;
    private float readyAt;

    /// <summary>True while the submarine is on the deep layer.</summary>
    public bool IsSubmerged
    {
        get { return NavLayerTransit.CurrentArea(agent) == NavAreas.DeepSea; }
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        traversal = GetComponent<NavLinkTraversal>();
    }

    /// <summary>
    /// Whether a dive order would be accepted right now: rated hull, off cooldown,
    /// not already down, and deep water beneath. UI polls this to show or hide the
    /// dive button, so it must stay cheap and side-effect free.
    /// </summary>
    public bool CanDive()
    {
        if (agent == null || !agent.isOnNavMesh) return false;
        if (traversal != null && traversal.IsTraversing) return false;
        if (Time.time < readyAt) return false;
        if (IsSubmerged) return false;
        if (!NavLayerTransit.IsAreaAllowed(agent, NavAreas.Dive)) return false;

        return NavLayerTransit.LayerExistsHere(agent, deepHeight, NavAreas.DeepSea, sampleTolerance);
    }

    /// <summary>The mirror of <see cref="CanDive"/>, for a surface button.</summary>
    public bool CanSurface()
    {
        if (agent == null || !agent.isOnNavMesh) return false;
        if (traversal != null && traversal.IsTraversing) return false;
        if (Time.time < readyAt) return false;
        if (!IsSubmerged) return false;

        return NavLayerTransit.LayerExistsHere(agent, surfaceHeight, NavAreas.Ocean, sampleTolerance);
    }

    public bool Dive()
    {
        if (!Ready("dive")) return false;

        if (IsSubmerged)
        {
            OnDiveRefused?.Invoke("Already submerged.");
            return false;
        }

        if (!NavLayerTransit.IsAreaAllowed(agent, NavAreas.Dive))
        {
            OnDiveRefused?.Invoke("This hull is not rated to dive.");
            return false;
        }

        if (!NavLayerTransit.MoveToLayer(agent, deepHeight, NavAreas.DeepSea, sampleTolerance))
        {
            OnDiveRefused?.Invoke("The water here is not deep enough to dive.");
            return false;
        }

        readyAt = Time.time + cooldown;
        OnDiveStateChanged?.Invoke(true);
        return true;
    }

    public bool Surface()
    {
        if (!Ready("surface")) return false;

        if (!IsSubmerged)
        {
            OnDiveRefused?.Invoke("Already surfaced.");
            return false;
        }

        if (!NavLayerTransit.MoveToLayer(agent, surfaceHeight, NavAreas.Ocean, sampleTolerance))
        {
            OnDiveRefused?.Invoke("No surface route from here.");
            return false;
        }

        readyAt = Time.time + cooldown;
        OnDiveStateChanged?.Invoke(false);
        return true;
    }

    private bool Ready(string verb)
    {
        if (traversal != null && traversal.IsTraversing)
        {
            OnDiveRefused?.Invoke($"Cannot {verb} mid-transit.");
            return false;
        }

        if (Time.time < readyAt)
        {
            OnDiveRefused?.Invoke($"Cannot {verb} for another {readyAt - Time.time:F1}s.");
            return false;
        }

        return true;
    }
}
