// tldr: Value types shared by the foliage model - what one plant is, and why it changed.
using UnityEngine;

/// <summary>
/// What a species is, for rules that treat trees and ground clutter differently.
/// </summary>
public enum FoliageKind
{
    Tree,
    Bush,
    DeadTree,
    Stump,
    Log,
    Rock,
}

/// <summary>
/// Life stage of one instance.
///
/// Felling transitions Living -> Stump on the SAME instance rather than deleting and re-adding, so
/// the stump inherits the trunk's exact position and regrowth is simply the reverse transition.
/// </summary>
public enum FoliageState
{
    Sapling,
    Living,
    Dead,
    Stump,
    Log,
}

/// <summary>Whether the generator authored this instance or the player planted it.</summary>
public enum FoliageOrigin
{
    Generated,
    Player,
}

/// <summary>
/// Why an instance left the store. Every removal path reports one, so yield, audio and effects can
/// react to the cause without the code doing the removing knowing they exist.
/// </summary>
public enum FoliageRemovalCause
{
    Construction,
    PlayerClear,
    Harvest,
    Decay,
    Regeneration,
}

/// <summary>
/// Constraints a query must satisfy. <see cref="None"/> means "no constraint", so the default
/// argument returns everything.
/// </summary>
[System.Flags]
public enum FoliageFilter
{
    None = 0,
    /// <summary>Only fully grown plants - excludes saplings, stumps and logs.</summary>
    LivingOnly = 1 << 0,
    /// <summary>Only plants still standing on a trunk - excludes stumps and logs.</summary>
    StandingOnly = 1 << 1,
    /// <summary>Only species a forestry building is allowed to fell.</summary>
    HarvestableOnly = 1 << 2,
    /// <summary>Only plants the player put there.</summary>
    PlayerPlacedOnly = 1 << 3,
}

/// <summary>
/// One plant. Position is stored rather than derived from the cell, because a player-planted tree
/// does not sit at a generator-chosen offset and a felled tree's stump must stay exactly where the
/// trunk was.
/// </summary>
public struct FoliageInstance
{
    public int Id;
    public Vector2Int Cell;
    public int SpeciesId;
    public FoliageState State;
    public FoliageOrigin Origin;
    /// <summary>MapGrid-local, the same space as <see cref="global::Cell.localCenter"/>.</summary>
    public Vector3 LocalPosition;
    public float Yaw;
    public float Scale;
}
