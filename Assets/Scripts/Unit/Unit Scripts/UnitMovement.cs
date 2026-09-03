using UnityEngine;
using UnityEngine.AI;

public enum MoveType
{
    None,
    Aircraft,
    Landcraft,
    Watercraft,
    Hovercraft,
    Submersible,
}

/// <summary>
/// NavMesh placement and configuration for a unit.
/// Movement orders themselves are owned by the command system
/// (PlayerUnitOrderDispatcher -> UnitCommandExecutor -> MoveCommand);
/// this component supplies the agent, its travel medium, and NavMesh recovery.
/// </summary>
public class UnitMovement : MonoBehaviour
{
    // Responsibilities: Unit Moving

    public UnityEngine.AI.NavMeshAgent agent;              // Agent Pre Settings

    public LayerMask TravelMedium; // Medium for Travel — set by MovementProfile
    NavMeshHit closestHit;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // With a runtime-baked map the NavMesh usually does not exist yet at Start,
        // so a failure here is expected rather than fatal - RuntimeNavMeshBaker will
        // tell us when there is something to land on.
        TryPlaceOnNavMesh();
    }

    private void OnEnable()
    {
        RuntimeNavMeshBaker.OnNavMeshBaked += HandleNavMeshBaked;
    }

    private void OnDisable()
    {
        RuntimeNavMeshBaker.OnNavMeshBaked -= HandleNavMeshBaked;
    }

    // Action is void, TryPlaceOnNavMesh returns bool, so the event needs a wrapper.
    // A named method rather than a lambda so OnDisable can actually unsubscribe it.
    private void HandleNavMeshBaked()
    {
        TryPlaceOnNavMesh();
    }

    /// <summary>
    /// Snaps the unit onto the nearest point of its agent type's NavMesh and enables
    /// the agent. Safe to call repeatedly; it is the recovery path for units that
    /// spawned before the map was baked.
    /// </summary>
    public bool TryPlaceOnNavMesh()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent == null) return false;

        // Already placed - nothing to recover.
        if (agent.enabled && agent.isOnNavMesh) return true;

        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };

        if (NavMesh.SamplePosition(gameObject.transform.position, out closestHit, 500f, filter))
        {
            gameObject.transform.position = closestHit.position;
            agent.enabled = true;
            return true;
        }

        agent.enabled = false; // No NavMesh to stand on yet; retry when one is baked.
        return false;
    }

    /// <summary>
    /// Sends the agent straight to a destination, bypassing the command queue.
    /// Used by autonomous behaviours (e.g. trade routes) that drive the unit themselves.
    /// </summary>
    public void SetDirectDestination(Vector3 destination)
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(destination);
        }
    }
}
