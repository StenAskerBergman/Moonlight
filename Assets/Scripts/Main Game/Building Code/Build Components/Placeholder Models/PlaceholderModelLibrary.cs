using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lookup table of placeholder looks. One asset can drive every building in the game:
/// buildings without a matching profile fall back to <see cref="defaultProfile"/>, so a
/// brand new building always renders as *something* without any art being authored.
/// </summary>
[CreateAssetMenu(fileName = "Placeholder Model Library", menuName = "Data/Building/Placeholder Model Library")]
public class PlaceholderModelLibrary : ScriptableObject
{
    [Tooltip("Used whenever no profile below matches the building.")]
    public PlaceholderModelProfile defaultProfile = new PlaceholderModelProfile();

    [Tooltip("Matched against the building's type, then its tags, then its name.")]
    public List<PlaceholderModelProfile> profiles = new List<PlaceholderModelProfile>();

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

    /// <summary>
    /// Seeds a reasonable starting set covering the building families that exist today.
    /// Also callable from the inspector context menu to refill an authored asset.
    /// </summary>
    [ContextMenu("Populate Built-In Profiles")]
    public void PopulateBuiltInProfiles()
    {
        profiles = new List<PlaceholderModelProfile>
        {
            NewProfile("residential", PlaceholderShape.Shed, new Color(0.78f, 0.70f, 0.55f), new Color(0.55f, 0.33f, 0.27f), 1.4f),
            NewProfile("resident",    PlaceholderShape.Shed, new Color(0.78f, 0.70f, 0.55f), new Color(0.55f, 0.33f, 0.27f), 1.4f),
            NewProfile("house",       PlaceholderShape.Shed, new Color(0.78f, 0.70f, 0.55f), new Color(0.55f, 0.33f, 0.27f), 1.4f),
            NewProfile("storage",     PlaceholderShape.Box,  new Color(0.60f, 0.55f, 0.42f), new Color(0.40f, 0.37f, 0.30f), 1.2f),
            NewProfile("depot",       PlaceholderShape.Box,  new Color(0.60f, 0.55f, 0.42f), new Color(0.40f, 0.37f, 0.30f), 1.2f),
            NewProfile("production",  PlaceholderShape.Silo, new Color(0.55f, 0.58f, 0.62f), new Color(0.36f, 0.38f, 0.42f), 2.0f),
            NewProfile("extractor",   PlaceholderShape.Rig,  new Color(0.52f, 0.46f, 0.40f), new Color(0.35f, 0.31f, 0.28f), 2.2f),
            NewProfile("mine",        PlaceholderShape.Rig,  new Color(0.45f, 0.42f, 0.40f), new Color(0.30f, 0.28f, 0.27f), 2.4f),
            NewProfile("military",    PlaceholderShape.Tower,new Color(0.42f, 0.47f, 0.56f), new Color(0.28f, 0.32f, 0.40f), 3.0f),
            NewProfile("defence",     PlaceholderShape.Tower,new Color(0.42f, 0.47f, 0.56f), new Color(0.28f, 0.32f, 0.40f), 3.0f),
            NewProfile("civic",       PlaceholderShape.Tower,new Color(0.70f, 0.72f, 0.76f), new Color(0.45f, 0.48f, 0.55f), 2.6f),
            NewProfile("city center", PlaceholderShape.Tower,new Color(0.70f, 0.72f, 0.76f), new Color(0.45f, 0.48f, 0.55f), 2.6f),
            NewProfile("road",        PlaceholderShape.Platform, new Color(0.35f, 0.35f, 0.36f), new Color(0.30f, 0.30f, 0.31f), 0.12f),
            NewProfile("wall",        PlaceholderShape.Box,  new Color(0.50f, 0.50f, 0.52f), new Color(0.38f, 0.38f, 0.40f), 1.8f)
        };
    }

    private static PlaceholderModelProfile NewProfile(string key, PlaceholderShape shape, Color color, Color accent, float height)
    {
        return new PlaceholderModelProfile
        {
            key = key,
            shape = shape,
            color = color,
            accentColor = accent,
            height = height
        };
    }

    /// <summary>
    /// Picks the profile for a building. Type wins over tags, tags win over name; anything
    /// unmatched gets the default profile, so this never returns null.
    /// </summary>
    public PlaceholderModelProfile Resolve(BuildingData data, string fallbackName = null)
    {
        if (data != null)
        {
            PlaceholderModelProfile match = Match(data.buildingType);
            if (match != null) return match;

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

        PlaceholderModelProfile byName = Match(fallbackName);
        if (byName != null) return byName;

        return defaultProfile ?? new PlaceholderModelProfile();
    }

    private PlaceholderModelProfile Match(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || profiles == null) return null;

        string needle = value.Trim().ToLowerInvariant();

        // Exact key first so "mine" does not lose to a broader substring entry.
        foreach (PlaceholderModelProfile profile in profiles)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.key)) continue;
            if (profile.key.Trim().ToLowerInvariant() == needle) return profile;
        }

        foreach (PlaceholderModelProfile profile in profiles)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.key)) continue;
            if (needle.Contains(profile.key.Trim().ToLowerInvariant())) return profile;
        }

        return null;
    }
}
