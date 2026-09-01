using System.Collections.Generic;

/// <summary>
/// Demographic a good, item or building is gated behind. Deliberately NOT a generic
/// "tier" number: an unlock in this game is the triple (Faction, PopulationClass,
/// required population count), so Tycoon Engineers and Eco Engineers are separate
/// progressions that happen to share a name, and Tech has its own three classes.
///
/// See <see cref="PopulationUnlock"/> for the per-item requirement and
/// <see cref="PopulationClasses"/> for faction ownership and unlock bands.
/// </summary>
public enum PopulationClass
{
    None = 0,

    // Shared by Tycoon and Eco. Progression: Workers -> Employees -> Engineers -> Executives
    Workers = 1,
    Employees = 2,
    Engineers = 3,
    Executives = 4,

    // Tech (Enums.Faction.Sci). Progression: LabAssistants -> Researchers -> Geniuses
    LabAssistants = 10,
    Researchers = 11,
    Geniuses = 12,
}

/// <summary>
/// Static table describing which faction owns each <see cref="PopulationClass"/>,
/// the order classes appear in, and the population bands at which that class unlocks
/// new content. The bands are the numbers the tier tabs and unlock checks read;
/// they live here rather than on each item so a band change is one edit.
/// </summary>
public static class PopulationClasses
{
    private sealed class ClassInfo
    {
        public Enums.Faction[] Factions;
        public int Order;
        public string DisplayName;
        public int[] UnlockBands;
    }

    private static readonly Enums.Faction[] TycoonAndEco = { Enums.Faction.Tyc, Enums.Faction.Eco };
    private static readonly Enums.Faction[] TechOnly = { Enums.Faction.Sci };

    private static readonly Dictionary<PopulationClass, ClassInfo> Table =
        new Dictionary<PopulationClass, ClassInfo>
        {
            [PopulationClass.Workers] = new ClassInfo
            {
                Factions = TycoonAndEco,
                Order = 0,
                DisplayName = "Workers",
                UnlockBands = new[] { 1, 60, 144 },
            },
            [PopulationClass.Employees] = new ClassInfo
            {
                Factions = TycoonAndEco,
                Order = 1,
                DisplayName = "Employees",
                UnlockBands = new[] { 1, 360, 600, 750 },
            },
            [PopulationClass.Engineers] = new ClassInfo
            {
                Factions = TycoonAndEco,
                Order = 2,
                DisplayName = "Engineers",
                UnlockBands = new[] { 1, 250, 950, 1200 },
            },
            [PopulationClass.Executives] = new ClassInfo
            {
                Factions = TycoonAndEco,
                Order = 3,
                DisplayName = "Executives",
                UnlockBands = new[] { 1, 600, 1200, 1400 },
            },
            [PopulationClass.LabAssistants] = new ClassInfo
            {
                Factions = TechOnly,
                Order = 0,
                DisplayName = "Lab Assistants",
                UnlockBands = new[] { 1, 50, 100, 150 },
            },
            [PopulationClass.Researchers] = new ClassInfo
            {
                Factions = TechOnly,
                Order = 1,
                DisplayName = "Researchers",
                UnlockBands = new[] { 1, 600, 750, 1200 },
            },
            [PopulationClass.Geniuses] = new ClassInfo
            {
                Factions = TechOnly,
                Order = 2,
                DisplayName = "Geniuses",
                UnlockBands = new[] { 1, 600, 1250 },
            },
        };

    private static readonly PopulationClass[] TycoonEcoOrder =
    {
        PopulationClass.Workers,
        PopulationClass.Employees,
        PopulationClass.Engineers,
        PopulationClass.Executives,
    };

    private static readonly PopulationClass[] TechOrder =
    {
        PopulationClass.LabAssistants,
        PopulationClass.Researchers,
        PopulationClass.Geniuses,
    };

    private static readonly PopulationClass[] Empty = new PopulationClass[0];

    /// <summary>
    /// Classes belonging to a faction, in progression order. This is the order the
    /// numbered tier tabs are laid out in.
    /// </summary>
    public static IReadOnlyList<PopulationClass> ForFaction(Enums.Faction faction)
    {
        switch (faction)
        {
            case Enums.Faction.Tyc:
            case Enums.Faction.Eco:
                return TycoonEcoOrder;
            case Enums.Faction.Sci:
                return TechOrder;
            default:
                return Empty;
        }
    }

    public static bool BelongsTo(PopulationClass populationClass, Enums.Faction faction)
    {
        if (!Table.TryGetValue(populationClass, out ClassInfo info)) return false;

        foreach (Enums.Faction owner in info.Factions)
        {
            if (owner == faction) return true;
        }
        return false;
    }

    public static string DisplayName(PopulationClass populationClass) =>
        Table.TryGetValue(populationClass, out ClassInfo info) ? info.DisplayName : populationClass.ToString();

    /// <summary>Progression index within its faction; -1 for <see cref="PopulationClass.None"/>.</summary>
    public static int Order(PopulationClass populationClass) =>
        Table.TryGetValue(populationClass, out ClassInfo info) ? info.Order : -1;

    /// <summary>
    /// Population counts at which this class unlocks new content, ascending. Item
    /// requirements are expected to sit on one of these bands, though nothing forces it.
    /// </summary>
    public static IReadOnlyList<int> UnlockBands(PopulationClass populationClass) =>
        Table.TryGetValue(populationClass, out ClassInfo info) ? info.UnlockBands : System.Array.Empty<int>();
}
