using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lookup table of placeholder looks. One asset can drive every building in the game:
/// buildings without a matching profile fall back to <see cref="defaultProfile"/>, so a
/// brand new building always renders as *something* without any art being authored.
///
/// Each profile is a building ROLE (residential, extraction, processing, ...) with its own
/// colour and silhouette, so a player can tell what stands on a plot before real models
/// exist. The default profile is a deliberately loud pink: a pink block means "this
/// building matched no role - add a key or alias for it".
/// </summary>
[CreateAssetMenu(fileName = "Placeholder Model Library", menuName = "Data/Building/Placeholder Model Library")]
public class PlaceholderModelLibrary : ScriptableObject
{
    [Tooltip("Used whenever no profile below matches the building.")]
    public PlaceholderModelProfile defaultProfile = NewDefaultProfile();

    [Tooltip("Matched against the building's tags, then its name, then its type. Within one pass, " +
             "exact matches win, then the FIRST profile in this list whose key or alias the value " +
             "contains - so keep specific profiles (fields, aquaculture) above broad ones (farms).")]
    public List<PlaceholderModelProfile> profiles = new List<PlaceholderModelProfile>();

    [Header("Labels")]
    [Tooltip("Master switch for the floating name labels over placed placeholder buildings.")]
    public bool showLabels = true;

    private static PlaceholderModelLibrary _runtimeDefault;

    /// <summary>
    /// A library that exists even when no asset has been authored yet, so the placeholder
    /// system never depends on someone remembering to create and wire one up.
    /// </summary>
    public static PlaceholderModelLibrary RuntimeDefault
    {
        get
        {
            if (_runtimeDefault != null) return _runtimeDefault;

            _runtimeDefault = Resources.Load<PlaceholderModelLibrary>("Placeholder Model Library");
            if (_runtimeDefault != null) return _runtimeDefault;

            _runtimeDefault = CreateInstance<PlaceholderModelLibrary>();
            _runtimeDefault.name = "Placeholder Model Library (Built-in)";
            _runtimeDefault.hideFlags = HideFlags.HideAndDontSave;
            _runtimeDefault.PopulateBuiltInProfiles();
            return _runtimeDefault;
        }
    }

    private static PlaceholderModelProfile NewDefaultProfile()
    {
        return new PlaceholderModelProfile
        {
            roleLabel = "Unclassified",
            shape = PlaceholderShape.Box,
            color = new Color(0.88f, 0.36f, 0.78f),
            accentColor = new Color(0.45f, 0.16f, 0.40f),
            height = 1.5f
        };
    }

    /// <summary>
    /// Seeds the role set covering the building families in the catalogue today, including
    /// the underwater layer. Also callable from the inspector context menu to refill an
    /// authored asset.
    ///
    /// ORDER MATTERS: substring matching takes the first hit, so "Algae Farm Field" must meet
    /// Field before Aquaculture before Farm, "Sugar Beet Plantation" must meet Farm before
    /// Processing's "plant", and "Slaughterhouse" must meet Processing before Residential.
    /// </summary>
    [ContextMenu("Populate Built-In Profiles")]
    public void PopulateBuiltInProfiles()
    {
        defaultProfile = NewDefaultProfile();

        profiles = new List<PlaceholderModelProfile>
        {
            // Placed by the hundred - no labels, low and flat.
            Role("Field", PlaceholderShape.Field, Rgb(0.55f, 0.74f, 0.30f), Rgb(0.42f, 0.30f, 0.18f), 0.22f,
                 false, "field", "paddy"),
            Role("Road", PlaceholderShape.Platform, Rgb(0.35f, 0.35f, 0.36f), Rgb(0.28f, 0.28f, 0.29f), 0.08f,
                 false, "road"),

            // Below the surface. Checked early so "Deep Sea Oil Driller" reads as underwater.
            Role("Underwater", PlaceholderShape.Dome, Rgb(0.30f, 0.82f, 0.90f), Rgb(0.10f, 0.30f, 0.45f), 1.4f,
                 true, "deepsea", "deep sea", "underwater", "seabed", "subsea", "submarine", "plateau"),

            Role("Harbor", PlaceholderShape.Box, Rgb(0.62f, 0.45f, 0.28f), Rgb(0.36f, 0.25f, 0.15f), 1.3f,
                 true, "warehouse", "harbor", "depot", "storage", "shipyard", "dock", "port"),

            Role("Aquaculture", PlaceholderShape.Platform, Rgb(0.24f, 0.62f, 0.78f), Rgb(0.12f, 0.33f, 0.50f), 0.35f,
                 true, "fish", "lobster", "aqua", "algae", "crab", "kelp"),

            Role("Farm", PlaceholderShape.Shed, Rgb(0.46f, 0.70f, 0.30f), Rgb(0.42f, 0.30f, 0.18f), 1.1f,
                 true, "farm", "plantation", "cultivation", "vineyard", "orchard", "pigsty", "ranch", "grove"),

            Role("Power", PlaceholderShape.Silo, Rgb(0.96f, 0.80f, 0.20f), Rgb(0.32f, 0.32f, 0.34f), 2.4f,
                 true, "power", "energy", "reactor", "generator", "turbine", "solar"),

            Role("Military", PlaceholderShape.Tower, Rgb(0.26f, 0.31f, 0.40f), Rgb(0.72f, 0.16f, 0.16f), 2.2f,
                 true, "military", "defence", "defense", "arsenal", "munition", "explosive", "missile", "turret", "wall"),

            Role("Environment", PlaceholderShape.Silo, Rgb(0.38f, 0.78f, 0.60f), Rgb(0.14f, 0.44f, 0.34f), 1.6f,
                 true, "co2", "deacidif", "waste", "ozone", "filter", "recycl", "purif"),

            Role("Extraction", PlaceholderShape.Rig, Rgb(0.52f, 0.45f, 0.38f), Rgb(0.88f, 0.46f, 0.14f), 2.2f,
                 true, "mine", "quarry", "excavat", "extract", "drill", "borer", "harvest", "crusher", "well"),

            Role("Civic", PlaceholderShape.Tower, Rgb(0.92f, 0.93f, 0.96f), Rgb(0.24f, 0.40f, 0.82f), 2.6f,
                 true, "civic", "city center", "center", "centre", "casino", "health", "ministry", "corporate",
                 "office", "school", "university", "hospital", "police", "fire"),

            Role("Monument", PlaceholderShape.Tower, Rgb(0.92f, 0.74f, 0.26f), Rgb(0.55f, 0.40f, 0.14f), 3.5f,
                 true, "monument", "statue", "wonder"),

            Role("Processing", PlaceholderShape.Factory, Rgb(0.58f, 0.61f, 0.66f), Rgb(0.30f, 0.32f, 0.37f), 1.8f,
                 true, "production", "factory", "smelt", "refinery", "plant", "workshop", "works", "cellar",
                 "distill", "lab", "slaughter", "compactor", "mill", "brewery", "manufactur", "industrial"),

            // Last on purpose: "house" would otherwise claim "Slaughterhouse".
            Role("Residential", PlaceholderShape.Shed, Rgb(0.88f, 0.76f, 0.54f), Rgb(0.72f, 0.32f, 0.22f), 1.4f,
                 true, "residential", "resident", "house", "apartment", "mansion", "barracks", "dwelling", "habitat"),
        };
    }

    private static Color Rgb(float r, float g, float b) => new Color(r, g, b, 1f);

    private static PlaceholderModelProfile Role(string role, PlaceholderShape shape, Color color, Color accent,
                                                float height, bool showLabel, params string[] keys)
    {
        string[] aliases = new string[Mathf.Max(0, keys.Length - 1)];
        System.Array.Copy(keys, 1, aliases, 0, aliases.Length);

        return new PlaceholderModelProfile
        {
            key = keys.Length > 0 ? keys[0] : role.ToLowerInvariant(),
            aliases = aliases,
            roleLabel = role,
            shape = shape,
            color = color,
            accentColor = accent,
            height = height,
            showLabel = showLabel
        };
    }

    /// <summary>
    /// Picks the profile for a building. Tags win over name, name wins over type: the
    /// catalogue's type is a broad category ("Production" covers mines, fisheries and
    /// refineries alike), so it is only the fallback. Never returns null.
    /// </summary>
    public PlaceholderModelProfile Resolve(BuildingData data, string fallbackName = null)
    {
        PlaceholderModelProfile match;

        if (data != null)
        {
            if (data.buildingTags != null)
            {
                foreach (string tag in data.buildingTags)
                {
                    match = Match(tag);
                    if (match != null) return match;
                }
            }

            match = Match(data.buildingName);
            if (match != null) return match;
        }

        match = Match(fallbackName);
        if (match != null) return match;

        if (data != null)
        {
            match = Match(data.buildingType);
            if (match != null) return match;
        }

        return defaultProfile ?? NewDefaultProfile();
    }

    private PlaceholderModelProfile Match(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || profiles == null) return null;

        string needle = value.Trim().ToLowerInvariant();

        // Exact key first so "mine" does not lose to a broader substring entry.
        foreach (PlaceholderModelProfile profile in profiles)
        {
            if (profile == null) continue;
            foreach (string key in KeysOf(profile))
            {
                if (key == needle) return profile;
            }
        }

        foreach (PlaceholderModelProfile profile in profiles)
        {
            if (profile == null) continue;
            foreach (string key in KeysOf(profile))
            {
                if (needle.Contains(key)) return profile;
            }
        }

        return null;
    }

    private static IEnumerable<string> KeysOf(PlaceholderModelProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.key)) yield return profile.key.Trim().ToLowerInvariant();
        if (profile.aliases == null) yield break;

        foreach (string alias in profile.aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias)) yield return alias.Trim().ToLowerInvariant();
        }
    }
}
