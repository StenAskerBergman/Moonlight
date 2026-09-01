using System.Collections.Generic;

/// <summary>
/// One button in the tier strip along the bottom of the warehouse panel.
///
/// A tier tab is not one-to-one with a <see cref="PopulationClass"/>. The strip shows
/// the island faction's four demographics as tabs 1-4, then a single Tech tab (the atom)
/// covering all three Tech classes at once — Tech population lives alongside Tycoon or
/// Eco on the same island rather than replacing it, so its goods need their own tab
/// instead of a slot in the main progression.
/// </summary>
public sealed class WarehouseTierTab
{
    /// <summary>Short label on the button: "1".."4" for demographics, "" for the Tech atom.</summary>
    public string Label { get; }

    /// <summary>Full name for tooltips: "Employees", "Tech", etc.</summary>
    public string DisplayName { get; }

    public Enums.Faction Faction { get; }

    /// <summary>Classes this tab covers. One for a demographic tab, three for Tech.</summary>
    public IReadOnlyList<PopulationClass> Classes { get; }

    /// <summary>True for the Tech tab, which the builder renders with the atom glyph.</summary>
    public bool IsTech { get; }

    private WarehouseTierTab(
        string label,
        string displayName,
        Enums.Faction faction,
        IReadOnlyList<PopulationClass> classes,
        bool isTech)
    {
        Label = label;
        DisplayName = displayName;
        Faction = faction;
        Classes = classes;
        IsTech = isTech;
    }

    /// <summary>The primary class this tab reports population for (the first it covers).</summary>
    public PopulationClass PrimaryClass => Classes.Count > 0 ? Classes[0] : PopulationClass.None;

    public bool Covers(PopulationClass populationClass)
    {
        foreach (PopulationClass candidate in Classes)
        {
            if (candidate == populationClass) return true;
        }
        return false;
    }

    /// <summary>Whether an unlock belongs on this tab. Ungated content lists on every tab.</summary>
    public bool Lists(PopulationUnlock unlock)
    {
        if (unlock.IsUngated) return true;
        return unlock.faction == Faction && Covers(unlock.populationClass);
    }

    /// <summary>
    /// Build the strip for an island: the island faction's demographics numbered 1..n,
    /// followed by the Tech tab. A Tech-only island gets just the Tech tab.
    /// </summary>
    public static List<WarehouseTierTab> BuildStrip(Enums.Faction islandFaction)
    {
        List<WarehouseTierTab> tabs = new List<WarehouseTierTab>();

        if (islandFaction == Enums.Faction.Tyc || islandFaction == Enums.Faction.Eco)
        {
            IReadOnlyList<PopulationClass> classes = PopulationClasses.ForFaction(islandFaction);

            for (int i = 0; i < classes.Count; i++)
            {
                tabs.Add(new WarehouseTierTab(
                    (i + 1).ToString(),
                    PopulationClasses.DisplayName(classes[i]),
                    islandFaction,
                    new[] { classes[i] },
                    isTech: false));
            }
        }

        tabs.Add(new WarehouseTierTab(
            string.Empty,
            "Tech",
            Enums.Faction.Sci,
            PopulationClasses.ForFaction(Enums.Faction.Sci),
            isTech: true));

        return tabs;
    }
}
