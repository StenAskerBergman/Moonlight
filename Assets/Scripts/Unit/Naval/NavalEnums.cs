using System;

/// <summary>
/// Naval classification category for units.
/// </summary>
public enum NavalClass
{
    TradeShip,
    Warship,
    SupportShip,
    Submarine
}

/// <summary>
/// Bitmask flags for combat targeting capability.
/// Configured per vessel through data; checked at runtime without string or vessel-name matching.
/// </summary>
[Flags]
public enum CombatTargetCapabilities
{
    None = 0,
    Surface = 1 << 0,
    Air = 1 << 1,
    Submarine = 1 << 2,
    All = Surface | Air | Submarine
}

/// <summary>
/// Current observable movement and gameplay state of a naval vessel.
/// </summary>
public enum NavalMovementState
{
    Surface,
    Submerged,
    Moving,
    Idle,
    Attacking,
    Disabled,
    Repairing,
    Docked
}
