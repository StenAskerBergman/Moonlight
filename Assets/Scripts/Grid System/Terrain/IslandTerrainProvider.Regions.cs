// tldr: Marks part of an island as a different REGION - one continuous 0..1 weight in world space that
// the splat shader tints its layers by, so an island can read as two places without a second material,
// a second mesh, or a second climate profile.

using UnityEngine;

public sealed partial class IslandTerrainProvider
{
    // Resolution of the land-distribution histogram used to turn a coverage FRACTION into a
    // boundary position. 512 bins across an island's extent is well under a texel, so the
    // quantisation is invisible; the cost is one int array, not a second pass per bin.
    private const int RegionCalibrationBins = 512;

    private bool regionPrepared;
    private bool regionActive;
    private Vector2 regionDirection = Vector2.right;
    private float regionThreshold; // Projection along regionDirection at which the boundary sits.
    private float regionNoiseOffsetX;
    private float regionNoiseOffsetZ;

    /// <summary>Updates material-only authoring before texture workers start; terrain caches stay frozen.</summary>
    public void RefreshRegionSettings(TerrainRegionSettings region)
    {
        if (worldField != null)
        {
            ownedLandform?.RefreshRegionSettings(region);
            PrepareRegionField();
            return;
        }
        settings.region = region == null ? new TerrainRegionSettings()
            : JsonUtility.FromJson<TerrainRegionSettings>(JsonUtility.ToJson(region));
        settings.region.Validate();
        regionPrepared = false;
        PrepareRegionField();
    }

    /// <summary>
    /// Resolves the region boundary once, from the island's actual land.
    ///
    /// MUST be called before any parallel sampling that reads <see cref="ResolveRegionWeight"/>.
    /// This method mutates provider state; the read is pure. Both the mesh and the texture passes
    /// fan out over Parallel.For, so a lazy resolve inside the read would be a data race, and
    /// resolving under a lock would serialise a per-texel call. Idempotent, and cheap next to the
    /// generation pass it follows - two linear scans of the height array, one of which samples the
    /// boundary noise.
    /// </summary>
    public void PrepareRegionField()
    {
        if (worldField != null)
        {
            foreach (var source in worldField.Sources) source.PrepareRegionField();
            return;
        }
        if (regionPrepared) return;
        regionActive = false;

        TerrainRegionSettings region = settings.region;
        if (region == null || !region.enabled) { regionPrepared = true; return; }

        // The plane belongs to the LANDFORM, not to the chunk looking at it. In world view several
        // chunk views share one island. Only the canonical source prepares these fields; views
        // resolve the same winning source as geometry at every position.
        IslandTerrainProvider landform = this;
        if (landform.gridType != GridType.Type.Island) { regionPrepared = true; return; }

        TerrainSampleCache cache = landform.sampleCache;
        if (cache == null) return;
        regionPrepared = true;

        float radians = region.directionDegrees * Mathf.Deg2Rad;
        regionDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

        // Kept inside NoiseOffsetWrap for the same float-precision reason the constructor documents
        // for legacyOffsetX: a large offset quantises adjacent samples onto the same representable
        // noise coordinate.
        System.Random random = new System.Random(unchecked(landform.chunkSeed * 83492791 ^ 0x2F1B3C4D));
        regionNoiseOffsetX = Mathf.Repeat((float)random.NextDouble() * 20000f - 10000f, NoiseOffsetWrap);
        regionNoiseOffsetZ = Mathf.Repeat((float)random.NextDouble() * 20000f - 10000f, NoiseOffsetWrap);

        regionActive = CalibrateRegionBoundary(landform, cache, region.coverage);
    }

    /// <summary>
    /// Whether this island has a resolved region at all. Only meaningful after
    /// <see cref="PrepareRegionField"/>; false means every <see cref="ResolveRegionWeight"/> would
    /// return 0, so callers can skip building a mask entirely.
    /// </summary>
    public bool HasRegion
    {
        get
        {
            if (worldField == null) return regionActive;
            foreach (var source in worldField.Sources)
                if (source.regionActive) return true;
            return false;
        }
    }

    /// <summary>
    /// How strongly the alternate region reaches this position: 0 is ordinary island, 1 is fully
    /// inside the region. Continuous everywhere. Returns 0 until <see cref="PrepareRegionField"/>
    /// has run, and for every island that has no region authored.
    /// </summary>
    public float ResolveRegionWeight(float localX, float localZ)
    {
        Vector2 world = chunkWorldOrigin + new Vector2(localX, localZ);
        if (worldField != null)
        {
            var source = worldField.Find(world, out _);
            if (source == null) return 0f;
            Vector2 sourceLocal = world - source.chunkWorldOrigin;
            return source.ResolveRegionWeight(sourceLocal.x, sourceLocal.y);
        }
        if (!regionActive) return 0f;

        TerrainRegionSettings region = settings.region;

        float signed = ProjectOntoRegionAxis(world, region) - regionThreshold;

        // Smoothstep across the authored width, never a threshold. A hard value step crossing the
        // texel grid at a shallow angle stair-steps by one texel and reads as a torn edge - the
        // serration failure documented at length in TextureBuilder.Build.
        float t = Mathf.Clamp01(0.5f + signed / (2f * Mathf.Max(0.01f, region.transitionWidth)));
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Position along the region axis, with the boundary's wander folded in.
    ///
    /// The wander DISPLACES the boundary rather than blending across it: adding the noise to the
    /// projection moves where the edge falls and leaves the authored transition width intact, where
    /// blending two separately-evaluated weights would smear the edge out to the wander amplitude -
    /// and the wander is deliberately much wider than the transition.
    ///
    /// Calibration and evaluation both go through here, so the coverage fraction is measured
    /// against the boundary that will actually be drawn. Calibrating on the straight line instead
    /// undershot by about a fifth: an island narrows toward its edge, so wander pushing the
    /// boundary outward sheds more land than the same wander pushing inward gains.
    /// </summary>
    private float ProjectOntoRegionAxis(Vector2 world, TerrainRegionSettings region)
    {
        float projection = world.x * regionDirection.x + world.y * regionDirection.y;
        if (region.boundaryWander <= 0f) return projection;

        float scale = 1f / Mathf.Max(4f, region.boundaryWanderScale);
        float noise = Mathf.PerlinNoise(
            world.x * scale + regionNoiseOffsetX,
            world.y * scale + regionNoiseOffsetZ);
        return projection + (noise * 2f - 1f) * region.boundaryWander;
    }

    /// <summary>
    /// Turns the authored coverage FRACTION into a boundary position, by reading how this island's
    /// land is actually distributed along the region direction.
    ///
    /// Deliberately not the circular-segment formula. The island is not a disc - falloff, coast warp
    /// and the ridge field routinely produce lobed and crescent shapes - so a disc-derived offset
    /// would mean a different fraction on every seed, which is exactly the "authored knob that does
    /// not hold its meaning" problem the coastal bands were rewritten to avoid.
    /// </summary>
    private bool CalibrateRegionBoundary(IslandTerrainProvider landform, TerrainSampleCache cache, float coverage)
    {
        TerrainRegionSettings region = settings.region;
        int resolution = cache.Resolution;
        float step = cache.Step;
        float padding = landform.samplingPadding;
        float water = landform.settings.waterHeight;
        Vector2 origin = landform.chunkWorldOrigin;
        float[] heights = cache.Heights;

        // Pass 1: extent of the LAND along the region axis. Submerged samples are excluded so the
        // fraction is a fraction of the island, not of the chunk's seabed - a shelf that happens to
        // spread further one way would otherwise drag the boundary with it.
        //
        // Only the straight projection is needed here; the range is then widened by the wander
        // amplitude, which bounds the displaced values without a second noise pass.
        float minProjection = float.MaxValue;
        float maxProjection = float.MinValue;
        long landCount = 0;
        for (int z = 0; z < resolution; z++)
        {
            float worldZ = origin.y + z * step - padding;
            int rowOffset = z * resolution;
            for (int x = 0; x < resolution; x++)
            {
                if (heights[rowOffset + x] <= water) continue;
                float worldX = origin.x + x * step - padding;
                float projection = worldX * regionDirection.x + worldZ * regionDirection.y;
                if (projection < minProjection) minProjection = projection;
                if (projection > maxProjection) maxProjection = projection;
                landCount++;
            }
        }

        if (landCount == 0) return false;
        minProjection -= region.boundaryWander;
        maxProjection += region.boundaryWander;
        float span = maxProjection - minProjection;
        if (span < 0.0001f) return false;

        // Pass 2: the distribution of the land along the boundary axis as it will actually be
        // drawn, wander included.
        int[] histogram = new int[RegionCalibrationBins];
        float binScale = (RegionCalibrationBins - 1) / span;
        for (int z = 0; z < resolution; z++)
        {
            float worldZ = origin.y + z * step - padding;
            int rowOffset = z * resolution;
            for (int x = 0; x < resolution; x++)
            {
                if (heights[rowOffset + x] <= water) continue;
                float projection = ProjectOntoRegionAxis(
                    new Vector2(origin.x + x * step - padding, worldZ), region);
                histogram[Mathf.Clamp((int)((projection - minProjection) * binScale), 0, RegionCalibrationBins - 1)]++;
            }
        }

        // Walk in from the far end until the requested fraction of the land is behind the boundary.
        // The transition width straddles this line, so the delivered coverage is still approximate
        // by design - it is a centre, not a hard cut.
        long target = (long)(landCount * Mathf.Clamp(coverage, 0.001f, 0.999f));
        long accumulated = 0;
        int bin = RegionCalibrationBins - 1;
        while (bin > 0 && accumulated < target)
        {
            accumulated += histogram[bin];
            bin--;
        }

        regionThreshold = minProjection + bin / binScale;
        return true;
    }
}
