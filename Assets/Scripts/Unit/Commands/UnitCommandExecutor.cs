using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative command execution engine for a Unit.
/// Manages ActiveCommand and an unattended sequential CommandQueue.
/// Emits events when commands are queued, completed, or cleared, enabling read-only visualization.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Unit))]
public class UnitCommandExecutor : MonoBehaviour
{
    private Unit unit;
    private IUnitCommand activeCommand;
    private readonly List<IUnitCommand> commandQueue = new List<IUnitCommand>();
    private IAutonomousBehaviorSource autonomousSource;

    public IUnitCommand ActiveCommand => activeCommand;
    public IReadOnlyList<IUnitCommand> CommandQueue => commandQueue;
    public IAutonomousBehaviorSource AutonomousSource => autonomousSource;

    public bool HasActiveOrders => activeCommand != null || commandQueue.Count > 0;
    public bool IsExecutingAutonomous => autonomousSource != null && autonomousSource.IsActive && !HasActiveOrders;

    /// <summary>Fired whenever ActiveCommand or CommandQueue changes (issued, advanced, finished, or cleared).</summary>
    public event Action OnCommandsChanged;

    private void Awake()
    {
        unit = GetComponent<Unit>();
    }

    private void Update()
    {
        // Execute active command
        if (activeCommand != null)
        {
            activeCommand.Update();

            if (activeCommand.IsCompleted)
            {
                AdvanceToNextCommand();
            }
        }
    }

    /// <summary>
    /// Issues a command to this unit.
    /// If queue is false, replaces all current and pending commands immediately.
    /// If queue is true, appends the command to the sequence.
    /// </summary>
    public void IssueCommand(IUnitCommand command, bool queue = false, bool isPlayerOrder = true)
    {
        if (command == null) return;

        // Player manual override breaks autonomous routines
        if (isPlayerOrder && autonomousSource != null && autonomousSource.IsActive)
        {
            autonomousSource.OnPlayerManualOverride();
        }

        if (!queue)
        {
            // Cancel current and wipe queue
            if (activeCommand != null)
            {
                activeCommand.Cancel();
                activeCommand = null;
            }
            commandQueue.Clear();

            activeCommand = command;
            activeCommand.Execute(unit);
        }
        else
        {
            // Append to queue, or start immediately if idle
            if (activeCommand == null)
            {
                activeCommand = command;
                activeCommand.Execute(unit);
            }
            else
            {
                commandQueue.Add(command);
            }
        }

        OnCommandsChanged?.Invoke();
    }

    /// <summary>
    /// Clears both active and queued commands immediately.
    /// </summary>
    public void ClearCommands()
    {
        if (activeCommand != null)
        {
            activeCommand.Cancel();
            activeCommand = null;
        }
        commandQueue.Clear();
        OnCommandsChanged?.Invoke();
    }

    private void AdvanceToNextCommand()
    {
        activeCommand = null;

        if (commandQueue.Count > 0)
        {
            var next = commandQueue[0];
            commandQueue.RemoveAt(0);
            activeCommand = next;
            activeCommand.Execute(unit);
        }

        OnCommandsChanged?.Invoke();
    }

    #region Autonomous Source Registration

    public void RegisterAutonomousSource(IAutonomousBehaviorSource source)
    {
        autonomousSource = source;
        OnCommandsChanged?.Invoke();
    }

    public void UnregisterAutonomousSource(IAutonomousBehaviorSource source)
    {
        if (autonomousSource == source)
        {
            autonomousSource = null;
            OnCommandsChanged?.Invoke();
        }
    }

    #endregion

    private void OnDestroy()
    {
        ClearCommands();
    }
}
