using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-island population count broken down by faction and demographic. This is the
/// authority the goods panel reads to decide whether a <see cref="PopulationUnlock"/>
/// is satisfied.
///
/// Residences report themselves in via <see cref="SetContribution"/> keyed on their own
/// object, so a residence upgrading, being demolished or changing occupancy just
/// re-reports; no incremental add/subtract bookkeeping to get out of sync. BuildingPop
/// is still the raw per-building count and is not wired here yet.
/// </summary>
[DisallowMultipleComponent]
public sealed class IslandPopulation : MonoBehaviour
{
    private readonly struct Key : IEquatable<Key>
    {
        public readonly Enums.Faction Faction;
        public readonly PopulationClass Class;

        public Key(Enums.Faction faction, PopulationClass populationClass)
        {
            Faction = faction;
            Class = populationClass;
        }

        public bool Equals(Key other) => Faction == other.Faction && Class == other.Class;
        public override bool Equals(object obj) => obj is Key other && Equals(other);
        public override int GetHashCode() => ((int)Faction * 397) ^ (int)Class;
    }

    [Serializable]
    private struct SeedEntry
    {
        public Enums.Faction faction;
        public PopulationClass populationClass;
        [Min(0)] public int population;
    }

    [Header("Identity")]
    [Tooltip("Faction settled on this island. Decides which demographics the warehouse panel's tier tabs offer (Tycoon/Eco share four classes, Tech has its own three).")]
    [SerializeField] private Enums.Faction faction = Enums.Faction.Tyc;

    public Enums.Faction Faction => faction;

    [Header("Starting Population")]
    [Tooltip("Seed counts applied on Awake. Useful for testing the tier tabs before residences report in.")]
    [SerializeField] private List<SeedEntry> startingPopulation = new List<SeedEntry>();

    private readonly Dictionary<Key, int> totals = new Dictionary<Key, int>();
    private readonly Dictionary<UnityEngine.Object, (Key key, int amount)> contributions =
        new Dictionary<UnityEngine.Object, (Key, int)>();

    /// <summary>Raised whenever any count changes, so open UI can refresh its locked states.</summary>
    public event Action PopulationChanged;

    private void Awake()
    {
        foreach (SeedEntry entry in startingPopulation)
        {
            if (entry.population <= 0) continue;
            Key key = new Key(entry.faction, entry.populationClass);
            totals[key] = GetPopulation(entry.faction, entry.populationClass) + entry.population;
        }
    }

    public int GetPopulation(Enums.Faction faction, PopulationClass populationClass) =>
        totals.TryGetValue(new Key(faction, populationClass), out int value) ? value : 0;

    public int GetTotalPopulation(Enums.Faction faction)
    {
        int sum = 0;
        foreach (PopulationClass populationClass in PopulationClasses.ForFaction(faction))
        {
            sum += GetPopulation(faction, populationClass);
        }
        return sum;
    }

    /// <summary>
    /// Declare how much population <paramref name="source"/> currently contributes.
    /// Re-reporting replaces the previous figure for that source; pass 0 (or call
    /// <see cref="RemoveContribution"/>) when it stops contributing.
    /// </summary>
    public void SetContribution(UnityEngine.Object source, Enums.Faction faction, PopulationClass populationClass, int amount)
    {
        if (source == null) return;

        amount = Mathf.Max(0, amount);
        Key key = new Key(faction, populationClass);

        if (contributions.TryGetValue(source, out var previous))
        {
            if (previous.key.Equals(key) && previous.amount == amount) return;
            ApplyDelta(previous.key, -previous.amount);
        }
        else if (amount == 0)
        {
            return;
        }

        if (amount > 0)
        {
            contributions[source] = (key, amount);
            ApplyDelta(key, amount);
        }
        else
        {
            contributions.Remove(source);
        }

        PopulationChanged?.Invoke();
    }

    public void RemoveContribution(UnityEngine.Object source)
    {
        if (source == null || !contributions.TryGetValue(source, out var previous)) return;

        contributions.Remove(source);
        ApplyDelta(previous.key, -previous.amount);
        PopulationChanged?.Invoke();
    }

    private void ApplyDelta(Key key, int delta)
    {
        int updated = (totals.TryGetValue(key, out int current) ? current : 0) + delta;

        if (updated > 0) totals[key] = updated;
        else totals.Remove(key);
    }

    /// <summary>Whether an unlock is met on this island right now.</summary>
    public bool IsUnlocked(PopulationUnlock unlock) =>
        unlock.IsSatisfiedBy(GetPopulation(unlock.faction, unlock.populationClass));
}
