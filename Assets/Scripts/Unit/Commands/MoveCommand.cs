using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Executable move command that drives a unit's NavMeshAgent to a designated world position.
/// Reports completion when the unit arrives within tolerance.
/// </summary>
public class MoveCommand : IUnitCommand
{
    private readonly Vector3 destination;
    private readonly float arrivalTolerance;
    private readonly Action onCompleted;
    private readonly string customDescription;

    private Unit unit;
    private NavMeshAgent agent;
    private UnitMovement movement;
    private bool isCompleted = false;

    public string Description => !string.IsNullOrEmpty(customDescription)
        ? customDescription
        : $"Move ({destination.x:F0}, {destination.z:F0})";

    public CommandType Type => CommandType.Move;
    public Vector3? TargetPosition => destination;
    public Transform TargetTransform => null;
    public bool IsCompleted => isCompleted;

    public MoveCommand(Vector3 destination, float arrivalTolerance = 2.0f, Action onCompleted = null, string description = null)
    {
        this.destination = destination;
        this.arrivalTolerance = arrivalTolerance;
        this.onCompleted = onCompleted;
        this.customDescription = description;
    }

    public void Execute(Unit targetUnit)
    {
        unit = targetUnit;
        if (unit == null)
        {
            isCompleted = true;
            return;
        }

        agent = unit.GetComponent<NavMeshAgent>();
        movement = unit.GetComponent<UnitMovement>();

        if (movement != null && !movement.enabled)
        {
            movement.enabled = true;
        }

        if (agent != null)
        {
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                movement?.TryPlaceOnNavMesh();
            }

            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(destination);
            }
        }

        isCompleted = false;
    }

    public void Update()
    {
        if (isCompleted || unit == null) return;

        // Ensure agent is navigating
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (!agent.pathPending && agent.remainingDistance <= Mathf.Max(arrivalTolerance, agent.stoppingDistance + 0.5f))
            {
                Finish();
                return;
            }
        }

        // Spatial fallback
        if (Vector3.Distance(unit.transform.position, destination) <= arrivalTolerance)
        {
            Finish();
        }
    }

    private void Finish()
    {
        if (!isCompleted)
        {
            isCompleted = true;
            onCompleted?.Invoke();
        }
    }

    public void Cancel()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        isCompleted = true;
    }
}
