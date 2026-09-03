using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Interface implemented by persistent autonomous systems (such as ShipTradeRouteController).
/// Enables SelectedUnitOrderVisualizer to inspect the autonomous system's sequence
/// and lets UnitCommandExecutor notify the autonomous source when a manual player command preempts it.
/// </summary>
public interface IAutonomousBehaviorSource
{
    /// <summary>Name of the autonomous behavior (e.g. "Trade Route: Venera <-> Horizon").</summary>
    string SourceName { get; }

    /// <summary>Explanation of what the unit is currently doing and why (e.g. "Loading 40x Coal at Station 1").</summary>
    string CurrentActionDescription { get; }

    /// <summary>Whether this autonomous behavior is currently active and controlling the unit.</summary>
    bool IsActive { get; }

    /// <summary>Returns the persistent world-space waypoints of this autonomous route/behavior.</summary>
    IReadOnlyList<Vector3> GetAutonomousWaypoints();

    /// <summary>Returns descriptive labels for each waypoint in sequence.</summary>
    IReadOnlyList<string> GetAutonomousWaypointLabels();

    /// <summary>Called when a player issues a manual order that preempts this autonomous behavior.</summary>
    void OnPlayerManualOverride();
}
