using System;
using UnityEngine;

/// <summary>
/// Shapes the placeholder builder can assemble out of Unity primitives.
/// Deliberately blocky - these stand in for art that does not exist yet.
///
/// New values are appended so existing serialized ordinals keep their meaning.
/// </summary>
public enum PlaceholderShape
{
    Box,        // Plain slab filling the footprint.
    Shed,       // Slab plus a narrower "roof" block on top.
    Tower,      // Tall, inset box.
    Platform,   // Thin pad, barely off the ground (roads, pads, yards).
    Silo,       // Cylinder inscribed in the footprint.
    Rig,        // Four corner legs carrying a deck.
    Factory,    // Hall with a chimney stack in one corner.
    Field,      // Low bed with crop rows.
    Dome        // Half sphere - pressurised underwater habitat.
}

/// <summary>
/// One placeholder look. Matched against a building by <see cref="key"/> and
/// <see cref="aliases"/>, compared (case-insensitively) against the building's tags,
/// name and type.
/// </summary>
[Serializable]
public class PlaceholderModelProfile
{
    [Tooltip("Building tag, name or type this profile stands in for. Leave empty on the default profile.")]
    public string key;

    [Tooltip("Further words that select this profile, e.g. 'quarry' and 'borer' for an extraction profile. " +
             "A name only has to contain one of them.")]
    public string[] aliases = Array.Empty<string>();

    [Tooltip("Short role caption shown under the building name, e.g. 'Extraction'.")]
    public string roleLabel;

    public PlaceholderShape shape = PlaceholderShape.Box;

    [Tooltip("Body colour. Alpha is used, so translucent placeholders are possible.")]
    public Color color = new Color(0.62f, 0.63f, 0.66f, 1f);

    [Tooltip("Colour of the roof / deck / accent block, where the shape has one. The grid plot uses it too.")]
    public Color accentColor = new Color(0.42f, 0.44f, 0.48f, 1f);

    [Tooltip("Height in world units of the main body.")]
    public float height = 1.5f;

    [Tooltip("How much of the footprint the body covers. 1 = flush with the grid cells.")]
    [Range(0.1f, 1f)] public float footprintFill = 0.9f;

    [Tooltip("Float a name label over placed buildings. Off for things placed by the hundred (fields, roads).")]
    public bool showLabel = true;

    public PlaceholderModelProfile Clone()
    {
        return new PlaceholderModelProfile
        {
            key = key,
            aliases = aliases != null ? (string[])aliases.Clone() : Array.Empty<string>(),
            roleLabel = roleLabel,
            shape = shape,
            color = color,
            accentColor = accentColor,
            height = height,
            footprintFill = footprintFill,
            showLabel = showLabel
        };
    }
}
