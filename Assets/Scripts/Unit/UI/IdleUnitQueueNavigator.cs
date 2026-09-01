using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Maintains a FIFO queue of selectable idle units. Tab selects the front unit
/// and returns it to the back, providing stable round-robin traversal.
/// </summary>
public sealed class IdleUnitQueueNavigator : MonoBehaviour
{
    private const float RefreshInterval = 0.25f;

    private readonly Queue<Unit> idleUnits = new Queue<Unit>();
    private readonly HashSet<Unit> queuedUnits = new HashSet<Unit>();
    private UnitSelections selections;
    private CameraRig cameraRig;
    private float nextRefreshTime;

    public int Count => idleUnits.Count;

    private void Awake()
    {
        selections = GetComponent<UnitSelections>();
    }

    private void Start()
    {
        cameraRig = FindObjectOfType<CameraRig>();
        ReconcileQueue();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextRefreshTime)
        {
            ReconcileQueue();
            nextRefreshTime = Time.unscaledTime + RefreshInterval;
        }

        if (Input.GetKeyDown(KeyCode.Tab) && !IsEditingText())
        {
            SelectNextIdleUnit();
        }
    }

    public void SelectNextIdleUnit()
    {
        ReconcileQueue();
        if (idleUnits.Count == 0) return;

        Unit next = idleUnits.Dequeue();
        queuedUnits.Remove(next);

        // A visited idle unit returns to the tail. Units that receive an order
        // are removed by the next reconciliation and rejoin at the tail when idle.
        if (IsIdle(next))
        {
            idleUnits.Enqueue(next);
            queuedUnits.Add(next);
        }

        selections.SelectOnly(next);
        if (cameraRig == null) cameraRig = FindObjectOfType<CameraRig>();
        if (cameraRig != null) cameraRig.FocusOnWorldPosition(next.transform.position);
    }

    private void ReconcileQueue()
    {
        if (selections == null || selections.unitList == null) return;

        int existingCount = idleUnits.Count;
        queuedUnits.Clear();
        for (int i = 0; i < existingCount; i++)
        {
            Unit unit = idleUnits.Dequeue();
            if (!IsIdle(unit) || !selections.unitList.Contains(unit)) continue;
            if (!queuedUnits.Add(unit)) continue;
            idleUnits.Enqueue(unit);
        }

        // unitList registration order supplies FIFO order for newly idle units.
        for (int i = 0; i < selections.unitList.Count; i++)
        {
            Unit unit = selections.unitList[i];
            if (!IsIdle(unit) || !queuedUnits.Add(unit)) continue;
            idleUnits.Enqueue(unit);
        }
    }

    private static bool IsIdle(Unit unit)
    {
        if (unit == null || !unit.isActiveAndEnabled || !unit.Selectable) return false;

        NavalUnit navalUnit = unit.GetComponent<NavalUnit>();
        if (navalUnit != null) return navalUnit.CurrentState == NavalMovementState.Idle;

        Truck truck = unit.GetComponent<Truck>();
        if (truck != null) return truck.CurrentState == Truck.TruckState.Idle;

        NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();
        return agent != null
            && agent.enabled
            && !agent.pathPending
            && (!agent.hasPath || agent.velocity.sqrMagnitude <= 0.05f);
    }

    private static bool IsEditingText()
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null) return false;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        return selected.GetComponent<InputField>() != null || selected.GetComponent<TMP_InputField>() != null;
    }
}
