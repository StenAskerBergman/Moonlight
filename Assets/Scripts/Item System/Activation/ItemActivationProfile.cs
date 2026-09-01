using System;
using UnityEngine;

/// <summary>Which bank of item slots an activation lives in.</summary>
public enum ItemSocketScope
{
    /// <summary>Slots on the Island. An activated item here affects every building on that island.</summary>
    Island = 0,

    /// <summary>Slots on a single building. An activated item here affects only that building.</summary>
    Local = 1,
}

/// <summary>Which scopes will accept a given item.</summary>
[Flags]
public enum ItemSocketScopeMask
{
    None = 0,
    Island = 1 << 0,
    Local = 1 << 1,
    Either = Island | Local,
}

/// <summary>
/// How an item behaves once it is slotted and switched on.
///
/// Key characters are items: they slot and activate through exactly the same path as a
/// seed or a warehouse upgrade, and differ only in that they never stack and are always
/// <see cref="ItemActivationKind.Movable"/>.
/// </summary>
public enum ItemActivationKind
{
    /// <summary>Can be switched off and pulled back out at any time.</summary>
    Movable = 0,

    /// <summary>Spends charges while active and is destroyed when the last one is gone.</summary>
    Consumable = 1,

    /// <summary>A person rather than an object. Slots and activates like an item; never stacks.</summary>
    KeyCharacter = 2,
}

/// <summary>
/// Per-item activation rules, authored on the <see cref="ItemData"/> asset.
///
/// A class rather than a struct so Unity runs the field initialisers below when it
/// deserialises an ItemData that predates this block — a zeroed struct would default to
/// <see cref="ItemSocketScopeMask.None"/> and silently make every existing item
/// un-activatable.
/// </summary>
[Serializable]
public sealed class ItemActivationProfile
{
    [Tooltip("Movable = can be switched off and taken back out. Consumable = spends charges while active, then is destroyed. Key Character = a person; slots like an item but never stacks.")]
    public ItemActivationKind kind = ItemActivationKind.Movable;

    [Tooltip("Which slot banks accept this item. Island slots affect the whole island; Local slots affect only the building holding them.")]
    public ItemSocketScopeMask allowedScopes = ItemSocketScopeMask.Either;

    [Tooltip("How long the activation lasts, in seconds. 0 = runs until it is switched off or consumed.")]
    [Min(0f)] public float durationSeconds = 0f;

    [Tooltip("Uses a consumable spends before it is destroyed. Ignored unless Kind is Consumable.")]
    [Min(0)] public int charges = 1;

    [Tooltip("Seconds of activation that one charge covers. 0 = a charge is spent on activation rather than over time.")]
    [Min(0f)] public float secondsPerCharge = 0f;

    public bool IsConsumable => kind == ItemActivationKind.Consumable;
    public bool IsKeyCharacter => kind == ItemActivationKind.KeyCharacter;
    public bool HasDuration => durationSeconds > 0f;

    public bool Allows(ItemSocketScope scope)
    {
        ItemSocketScopeMask bit = scope == ItemSocketScope.Island
            ? ItemSocketScopeMask.Island
            : ItemSocketScopeMask.Local;

        return (allowedScopes & bit) != 0;
    }
}
