using UnityEngine;

/// <summary>
/// Maps a road tile's local vertices onto the terrain surface underneath it.
///
/// A tile object sits at its cell's single height, so its geometry is flat while the
/// ground beneath it is not: on any slope the tile floats downhill and sinks uphill.
/// This supplies the vertical correction per vertex, turning a flat tile into one that
/// hugs the ground the way the reference roads do.
///
/// Bridges deliberately do not use this - a deck spans water at its own height and has
/// to stay straight.
/// </summary>
public readonly struct RoadSurfaceDrape
{
    private readonly TerrainHeightSampler _sampler;
    private readonly float _originX;
    private readonly float _originZ;
    private readonly float _originHeight;
    private readonly bool _enabled;

    /// <param name="sampler">Height field for the island this tile belongs to.</param>
    /// <param name="tileLocalCenter">The tile's own origin in grid-local space, which its
    /// vertices are positioned relative to.</param>
    public RoadSurfaceDrape(TerrainHeightSampler sampler, Vector3 tileLocalCenter)
    {
        _sampler = sampler;
        _originX = tileLocalCenter.x;
        _originZ = tileLocalCenter.z;
        _originHeight = tileLocalCenter.y;
        _enabled = sampler.IsValid;
    }

    public bool IsActive => _enabled;

    /// <summary>
    /// How far to move a vertex at tile-local (x, z) vertically so it lands on the
    /// terrain rather than on the tile's flat origin plane. Zero when inactive, which
    /// leaves the tile exactly as authored.
    /// </summary>
    public float OffsetAt(float localX, float localZ)
    {
        if (!_enabled) return 0f;
        return _sampler.Sample(_originX + localX, _originZ + localZ) - _originHeight;
    }
}
