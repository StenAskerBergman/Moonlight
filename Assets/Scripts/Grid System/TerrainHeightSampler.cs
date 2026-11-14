using UnityEngine;

/// <summary>
/// Reads the sampled terrain heightfield an island's mesh was generated from, at
/// continuous grid-local coordinates.
///
/// Cell.height is one quantised value for a whole cell, so anything that has to sit
/// flush on the ground - a road surface, an overlay, a foundation - steps and floats
/// when it reads that instead of this. The field is resolved once when the sampler is
/// created, so sampling per vertex stays cheap.
/// </summary>
public readonly struct TerrainHeightSampler
{
    private readonly TerrainSampleCache _field;
    private readonly int _samplesPerCell;
    private readonly float _fallbackHeight;

    public TerrainHeightSampler(TerrainSampleCache field, float fallbackHeight)
    {
        _field = field;
        _samplesPerCell = field != null ? Mathf.Max(1, field.VisualSamplesPerCell) : 1;
        _fallbackHeight = fallbackHeight;
    }

    /// <summary>
    /// False when the island was not built through IslandTerrainProvider, in which case
    /// every sample returns the fallback height and callers should stay flat.
    /// </summary>
    public bool IsValid => _field != null;

    /// <summary>
    /// Terrain height at grid-local (x, z), bilinearly interpolated between samples so
    /// the result is continuous and matches the terrain mesh at every sample point.
    /// </summary>
    public float Sample(float localX, float localZ)
    {
        if (_field == null) return _fallbackHeight;

        float sampleX = localX * _samplesPerCell;
        float sampleZ = localZ * _samplesPerCell;

        int x0 = Mathf.FloorToInt(sampleX);
        int z0 = Mathf.FloorToInt(sampleZ);
        float tx = sampleX - x0;
        float tz = sampleZ - z0;

        // GetHeight clamps, so sampling past the island edge flattens out rather than
        // throwing - which is what a road running off the last cell wants anyway.
        float south = Mathf.Lerp(_field.GetHeight(x0, z0), _field.GetHeight(x0 + 1, z0), tx);
        float north = Mathf.Lerp(_field.GetHeight(x0, z0 + 1), _field.GetHeight(x0 + 1, z0 + 1), tx);
        return Mathf.Lerp(south, north, tz);
    }
}
