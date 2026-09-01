using System;
using UnityEngine;

/// <summary>
/// Shapes the placeholder builder can assemble out of Unity primitives.
/// Deliberately blocky - these stand in for art that does not exist yet.
/// </summary>
public enum PlaceholderShape
{
    Box,        // Plain slab filling the footprint.
    Shed,       // Slab plus a narrower "roof" block on top.
    Tower,      // Tall, inset box.
    Platform,   // Thin pad, barely off the ground (roads, pads, yards).
    Silo,       // Cylinder inscribed in the footprint.
    Rig         // Four corner legs carrying a deck.
}

/// <summary>
/// One placeholder look. Matched against a building by <see cref="key"/>, which is
/// compared (case-insensitively) against the building's type, tags and name.
/// </summary>
[Serializable]
public class PlaceholderModelProfile
{
    [Tooltip("Building type, tag or name this profile stands in for. Leave empty on the default profile.")]
    public string key;

    public PlaceholderShape shape = PlaceholderShape.Box;

    [Tooltip("Body colour. Alpha is used, so translucent placeholders are possible.")]
    public Color color = new Color(0.62f, 0.63f, 0.66f, 1f);

    [Tooltip("Colour of the roof / deck / accent block, where the shape has one.")]
    public Color accentColor = new Color(0.42f, 0.44f, 0.48f, 1f);

    [Tooltip("Height in world units of the main body.")]
    public float height = 1.5f;

    [Tooltip("How much of the footprint the body covers. 1 = flush with the grid cells.")]
    [Range(0.1f, 1f)] public float footprintFill = 0.9f;

    public PlaceholderModelProfile Clone()
    {
        return new PlaceholderModelProfile
        {
            key = key,
            shape = shape,
            color = color,
            accentColor = accentColor,
            height = height,
            footprintFill = footprintFill
        };
    }
}
