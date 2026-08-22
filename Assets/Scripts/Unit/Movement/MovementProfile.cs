using UnityEngine;

/// <summary>
/// Abstract base for unit movement configurations. Concrete subclasses carry
/// domain-specific data (NavMesh agent settings, flight altitude, etc.) and
/// know how to apply themselves to a freshly instantiated unit.
///
/// Follows the polymorphic ScriptableObject pattern established by
/// BuildingRequirement (Assets/Scripts/Main Game/Building Code/Build Construction/).
/// </summary>
public abstract class MovementProfile : ScriptableObject
{
    /// <summary>The movement domain this profile governs.</summary>
    public abstract MoveType Domain { get; }

    /// <summary>
    /// Applies this profile's movement configuration to the given unit GameObject.
    /// Called by UnitFactory after instantiation, before the object is activated.
    /// </summary>
    public abstract void Apply(GameObject unit);

    /// <summary>
    /// Editor-time validation. Returns false and populates <paramref name="error"/>
    /// if the profile is misconfigured or incompatible with the given prefab.
    /// </summary>
    public abstract bool Validate(GameObject prefab, out string error);
}
