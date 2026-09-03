using System.Collections;
using System.Collections.Generic;
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

public class UnitMovement : MonoBehaviour
{
    // Responsibilities: Unit Moving

    public Camera cam;                      // Player Ray Camera
    public UnityEngine.AI.NavMeshAgent agent;              // Agent Pre Settings
    
    public LayerMask TravelMedium; // Medium for Travel — set by MovementProfile
    public float StopFactor = 0.5f;         // Agent Stop Factor 
    NavMeshHit closestHit;

    // Queue for Movement Orders
    private Queue<Vector3> destinationQueue = new Queue<Vector3>();

    void Start()
    {
        cam = Camera.main;
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


    void Update()
    {
        ProcessMovementOrders();
    }

    public bool HasQueuedOrders => destinationQueue != null && destinationQueue.Count > 0;

    public void ClearQueue()
    {
        destinationQueue?.Clear();
    }

    public void SetDirectDestination(Vector3 destination)
    {
        destinationQueue?.Clear();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(destination);
        }
    }

    // PMO
    private void ProcessMovementOrders()
    {
        if (destinationQueue.Count == 0)
        {
            return;
        }

        // If No Nav Mesh Exists, Stop and Return until there is one
        if (!agent.isOnNavMesh)
        {
            // Do nothing - Stop Agent
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
            destinationQueue.Clear();
            // Debug.Log("Agent is not on Nav Mesh - PMO");
            return;
        }
        else
        {
            // Do Something - Move Agent
            agent.isStopped = false;
        }

        // If we're not at a destination, and there's another in the queue, set the next destination
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + StopFactor && destinationQueue.Count > 0)
        {
            Vector3 nextDestination = destinationQueue.Dequeue();
            agent.SetDestination(nextDestination);
        }
    }

    // HMO
    private void HandleMovementOrder()
    {
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, TravelMedium, QueryTriggerInteraction.Ignore))
            {
                NavMeshHit navHit;
                // Check if the hit point is close enough to a point on the NavMesh, or find the nearest valid point
                var filter = new NavMeshQueryFilter
                {
                    agentTypeID = agent.agentTypeID,
                    areaMask = agent.areaMask
                };

                if (NavMesh.SamplePosition(hit.point, out navHit, 1.0f, filter) ||
                    NavMesh.SamplePosition(hit.point, out navHit, 500f, filter))
                {
                    Vector3 validPoint = navHit.position;
                    //Debug.Log("validPoint: " + validPoint);

                    // Enqueue the valid point instead of the direct hit point
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        destinationQueue.Enqueue(validPoint);
                    }
                    else
                    {
                        if (!agent.isOnNavMesh)
                        {
                            // Not an agent-type problem despite the old wording - the
                            // agent simply has no NavMesh under it. Try to recover in
                            // case one was baked after this unit spawned.
                            if (!TryPlaceOnNavMesh())
                            {
                                Debug.LogWarning($"{name}: no NavMesh for agent type {agent.agentTypeID} - movement order ignored.");
                                return;
                            }
                        }
                        destinationQueue.Clear();
                        agent.ResetPath();
                        destinationQueue.Enqueue(validPoint);
                    }
                }
                else
                {
                    // Nearest possible point should be provided instead then 
                    // if that is not possible! Then trigger this 
                    if (hit.point != null) Debug.LogError("Hit point is not on NavMesh - hit.point:" + hit.point);
                    else Debug.LogError("Hit point is not on NavMesh - hit.point: Null");
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;  // Set the color to blue for the destinations

        Vector3[] destinations = destinationQueue.ToArray();

        // Draw a line from the agent to the first point in the queue
        if (destinations.Length > 0)
        {
            Gizmos.DrawLine(transform.position, destinations[0]);
        }

        // Loop through the array and draw a sphere for each point and a line connecting them
        for (int i = 0; i < destinations.Length; i++)
        {
            Gizmos.DrawSphere(destinations[i], 0.5f);  // Draws a sphere at each point in the queue

            // Draw a line to the next point if there is one
            if (i < destinations.Length - 1)
            {
                Gizmos.DrawLine(destinations[i], destinations[i + 1]);
            }
        }
    }

}
