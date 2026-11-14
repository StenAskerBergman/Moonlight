using System;
using UnityEngine;

/// <summary>
/// Per-chunk deviations from the shared <see cref="TerrainWorldSettings"/> contract.
///
/// Deliberately small. The world asset owns everything that must agree across chunk borders -
/// the ocean datum, the abyss depth, the coastal profile, the land ladder - because a chunk that
/// disagrees with its neighbour about sea level tears the seam between them. What belongs here is
/// only what one island may legitimately differ on: how it is seeded, how finely it is sampled,
/// and which landscape features it is allowed to grow.
///
/// Each knob is opt-in through its own flag rather than a sentinel value, because zero is a
/// legitimate authored value for several of these (mainland relief of 0 means a perfectly flat
/// foundation, which is a real choice and not "unset").
/// </summary>
[Serializable]
public sealed class IslandGenerationOverrides
{
    [Header("Determinism")]
    [Tooltip("Replace the world seed for this chunk. The island's id is still mixed in, so two " +
             "chunks sharing an override seed still generate differently.")]
    public bool overrideSeed;
    public int seed = 1337;

    [Header("Sampling")]
    [Tooltip("Visual samples per cell for this chunk only. Cost scales with the SQUARE of this, " +
             "so it is the first thing to lower on a chunk that does not need the detail.")]
    public bool overrideVisualSamples;
    [Range(1, 16)] public int visualSamplesPerCell = 6;

    [Header("Landscape features")]
    [Tooltip("Allow coastal mountain ridges on this island.")]
    public bool overrideMountains;
    public bool mountainsEnabled = true;

    [Tooltip("Allow river corridors on this island. River planning only finds sources on elevated " +
             "ground, so disabling mountains also removes this chunk's rivers regardless.")]
    public bool overrideRivers;
    public bool riversEnabled = true;

    [Tooltip("Peak-to-datum amplitude of the mainland's micro-relief. 0 is a legitimate value " +
             "and gives a perfectly flat buildable foundation.")]
    public bool overrideMainlandRelief;
    [Range(0f, 0.25f)] public float mainlandRelief = 0.05f;

    [Header("Regional material variation")]
    [Tooltip("Give this island a region that renders in the alternate material tints. This belongs " +
             "per-island rather than in the world contract: a region is a property of one place, and " +
             "an authored coverage and direction shared by every island would put the same wedge on " +
             "all of them. Nothing here crosses a chunk border, so it cannot tear a seam.")]
    public bool overrideRegion;
    public TerrainRegionSettings region = new TerrainRegionSettings();

    public bool HasAny =>
        overrideSeed || overrideVisualSamples || overrideMountains || overrideRivers
        || overrideMainlandRelief || overrideRegion;

    /// <summary>
    /// Applies this chunk's deviations to its own resolved copy of the world contract. Never call
    /// this with the shared template - see <see cref="TerrainWorldSettings.CreateResolved"/>.
    /// </summary>
    public void Apply(TerrainGenerationSettings resolved)
    {
        if (resolved == null) return;

        if (overrideSeed) resolved.seed = seed;
        if (overrideVisualSamples) resolved.visualSamplesPerCell = Mathf.Clamp(visualSamplesPerCell, 1, 16);
        if (overrideMainlandRelief) resolved.mainlandRelief = Mathf.Clamp(mainlandRelief, 0f, 0.25f);

        if (overrideMountains)
        {
            resolved.coastalMountains ??= new CoastalMountainSettings();
            resolved.coastalMountains.enabled = mountainsEnabled;
        }

        if (overrideRivers)
        {
            resolved.riverCorridors ??= new RiverCorridorSettings();
            resolved.riverCorridors.enabled = riversEnabled;
        }

        if (overrideRegion)
        {
            // Copied by value, not assigned by reference: the override object is authored on the
            // chunk and outlives generation, while `resolved` is a throwaway copy that generation
            // mutates. Sharing the instance would let a derived value written during generation
            // land back on the authored asset - the leak CreateResolved exists to prevent.
            region ??= new TerrainRegionSettings();
            resolved.region = JsonUtility.FromJson<TerrainRegionSettings>(JsonUtility.ToJson(region))
                ?? new TerrainRegionSettings();
        }
    }

    public string Describe()
    {
        if (!HasAny) return "none";

        var parts = new System.Collections.Generic.List<string>();
        if (overrideSeed) parts.Add($"seed={seed}");
        if (overrideVisualSamples) parts.Add($"samples={visualSamplesPerCell}");
        if (overrideMountains) parts.Add($"mountains={mountainsEnabled}");
        if (overrideRivers) parts.Add($"rivers={riversEnabled}");
        if (overrideMainlandRelief) parts.Add($"relief={mainlandRelief}");
        if (overrideRegion) parts.Add(region != null && region.enabled
            ? $"region={region.coverage:P0}@{region.directionDegrees:F0}deg"
            : "region=off");
        return string.Join(", ", parts);
    }
}
