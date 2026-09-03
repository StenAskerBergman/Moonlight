using UnityEngine;

/// <summary>
/// Categories of commands a unit can execute.
/// </summary>
public enum CommandType
{
    Move,
    Interact,
    Dock,
    Trade,
    Custom
}

/// <summary>
/// Authoritative interface for any executable unit command.
/// Provides lifecycle methods and metadata required for unattended execution and in-world visualization.
/// </summary>
public interface IUnitCommand
{
    /// <summary>Human-readable description of this command (e.g. "Move to Waypoint", "Dock at Venera Harbor").</summary>
    string Description { get; }

    /// <summary>Categorical type of the command.</summary>
    CommandType Type { get; }

    /// <summary>Target position in world space, if applicable.</summary>
    Vector3? TargetPosition { get; }

    /// <summary>Target transform or entity in world space, if applicable.</summary>
    Transform TargetTransform { get; }

    /// <summary>True when the command has fully satisfied its completion criteria.</summary>
    bool IsCompleted { get; }

    /// <summary>Initiates command execution on the target unit.</summary>
    void Execute(Unit unit);

    /// <summary>Called per frame by UnitCommandExecutor while this command is active.</summary>
    void Update();

    /// <summary>Called when the command is cancelled, superseded, or aborted.</summary>
    void Cancel();
}
