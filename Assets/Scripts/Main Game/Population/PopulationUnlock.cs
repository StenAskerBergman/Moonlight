using System;
using UnityEngine;

/// <summary>
/// The gate on a good, item or building: it becomes available once the given faction's
/// given demographic reaches <see cref="requiredPopulation"/>.
///
/// This replaces the idea of a flat "tier" field. A generic tier can't express that
/// Tycoon Engineers at 950 and Eco Engineers at 950 are different progressions, nor
/// that Tech's classes have their own bands entirely.
/// </summary>
[Serializable]
public struct PopulationUnlock
{
    [Tooltip("Faction whose population is counted. None = available to every faction with no population gate.")]
    public Enums.Faction faction;

    [Tooltip("Demographic that must reach the required population.")]
    public PopulationClass populationClass;

    [Tooltip("Population of that demographic required. Should normally sit on one of the class's unlock bands (see PopulationClasses.UnlockBands).")]
    [Min(0)]
    public int requiredPopulation;

    public PopulationUnlock(Enums.Faction faction, PopulationClass populationClass, int requiredPopulation)
    {
        this.faction = faction;
        this.populationClass = populationClass;
        this.requiredPopulation = requiredPopulation;
    }

    /// <summary>
    /// True when this carries no gate at all, i.e. the thing is always available.
    /// Ungated content is shown on every tier tab rather than being hidden.
    /// </summary>
    public bool IsUngated =>
        faction == Enums.Faction.None ||
        populationClass == PopulationClass.None ||
        requiredPopulation <= 0;

    /// <summary>Whether the requirement is actually met given a current population count.</summary>
    public bool IsSatisfiedBy(int currentPopulation) =>
        IsUngated || currentPopulation >= requiredPopulation;

    public override string ToString() =>
        IsUngated
            ? "Always available"
            : $"{faction}, {PopulationClasses.DisplayName(populationClass)}, {requiredPopulation}";
}
