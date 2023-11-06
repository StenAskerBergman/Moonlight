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
}

public class UnitMovement : MonoBehaviour
{
    // Responsibilities: Unit Moving

    public Camera cam;               // Player Ray Camera
    public NavMeshAgent agent;       // Agent Pre Settings
    public LayerMask ground;         // Medium for Travel
    public float StopFactor = 0.5f;  // Agent Stop Factor 

    // Queue for Movement Orders
    private Queue<Vector3> destinationQueue = new Queue<Vector3>();

    void Start()
    {
        cam = Camera.main;
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        ProcessMovementOrders();
        HandleMovementOrder();
    }

    private void ProcessMovementOrders()
    {
        
        
        // If we're not at a destination, and there's another in the queue, set the next destination
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + StopFactor && destinationQueue.Count > 0)
        {
            Vector3 nextDestination = destinationQueue.Dequeue();
            agent.SetDestination(nextDestination);
        }
    }

    private void HandleMovementOrder()
    {
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, ground))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    destinationQueue.Enqueue(hit.point);
                }
                else
                {
                    destinationQueue.Clear();
                    agent.ResetPath();
                    destinationQueue.Enqueue(hit.point);
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
