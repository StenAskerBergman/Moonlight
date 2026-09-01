using UnityEngine;

/// <summary>
/// Submersible naval vessel capable of navigating deep seabed layers via DiveInteraction.
/// </summary>
[RequireComponent(typeof(DiveInteraction))]
public class Submarine : NavalUnit
{
    public override NavalMovementState CurrentState
    {
        get
        {
            if (IsEmpDisabled) return NavalMovementState.Disabled;

            if (Dive != null && Dive.IsSubmerged)
            {
                return NavalMovementState.Submerged;
            }

            if (agent != null && agent.enabled && agent.hasPath && agent.velocity.sqrMagnitude > 0.05f)
            {
                return NavalMovementState.Moving;
            }

            return NavalMovementState.Idle;
        }
    }
}
