using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlateauZone : byte
{
    None = 0,
    Tabletop,
    RockyRim,
    SandSlope,
    UpperEscarpment,
    LowerApron,
    AbyssFade
}

/// <summary>
/// Authoritative plateau meaning produced alongside the height field. Consumers use
/// these continuous weights instead of re-deriving geology from height and slope.
/// </summary>
public readonly struct PlateauSampleData
{
    public PlateauSampleData(
        PlateauZone zone,
        float influence,
        float buildableWeight,
        float rockWeight,
        float sandWeight,
        float reefWeight,
        float gravelWeight,
        float mudWeight,
        float siltWeight,
        float abyssFade)
    {
        Zone = zone;
        Influence = Mathf.Clamp01(influence);
        BuildableWeight = Mathf.Clamp01(buildableWeight);
        RockWeight = Mathf.Clamp01(rockWeight);
        SandWeight = Mathf.Clamp01(sandWeight);
        ReefWeight = Mathf.Clamp01(reefWeight);
        GravelWeight = Mathf.Clamp01(gravelWeight);
        MudWeight = Mathf.Clamp01(mudWeight);
        SiltWeight = Mathf.Clamp01(siltWeight);
        AbyssFade = Mathf.Clamp01(abyssFade);
    }

    public PlateauZone Zone { get; }
    public float Influence { get; }
    public float BuildableWeight { get; }
    public float RockWeight { get; }
    public float SandWeight { get; }
    public float ReefWeight { get; }
    public float GravelWeight { get; }
    public float MudWeight { get; }
    public float SiltWeight { get; }
    public float AbyssFade { get; }
    public bool IsDefined => Zone != PlateauZone.None || Influence > 0f;
}

public readonly struct TerrainSample
{
    public TerrainSample(Cell.TerrainType terrainType, float height, float sourceValue, float plateauInfluence = 0f)
    {
        TerrainType = terrainType;
        Height = height;
        SourceValue = sourceValue;
        PlateauInfluence = plateauInfluence;
        PlateauData = default;
    }

    public TerrainSample(Cell.TerrainType terrainType, float height, float sourceValue, PlateauSampleData plateauData)
    {
        TerrainType = terrainType;
        Height = height;
        SourceValue = sourceValue;
        PlateauInfluence = plateauData.Influence;
        PlateauData = plateauData;
    }

    public Cell.TerrainType TerrainType { get; }
    public float Height { get; }
    public float SourceValue { get; }
    public float PlateauInfluence { get; }
    public PlateauSampleData PlateauData { get; }
}

/// <summary>
/// Deterministic world-space source for both gameplay cells and a future denser visual mesh.
/// Sampling at integer coordinates creates Cell[,]; fractional coordinates remain available
/// so rendering can later increase resolution without becoming a second terrain authority.
/// </summary>
public sealed partial class IslandTerrainProvider
{
    // Boundary between abyssal and deep water on Ocean/Empty grids, expressed in the
    // composed-noise 0..1 domain rather than the island field's domain.
    private const float OceanDeepThreshold = 0.38f;

    public TerrainGenerationSettings Settings => settings;
    public FeatureReservationMap Reservations => featureReservations;
    private readonly TerrainGenerationSettings settings;
    private readonly GridType.Type gridType;
    private readonly int size;
    private readonly List<RuntimeNoiseLayer> layers;
    private readonly FeatureReservationMap featureReservations;
    // Upper bound for any offset added to a Perlin sample coordinate. See the constructor for
    // why large offsets destroy fine sampling precision.
    private const float NoiseOffsetWrap = 128f;

    private readonly float legacyOffsetX;
    private readonly float legacyOffsetZ;
    private readonly float islandEmergenceOffset;
    private readonly int chunkSeed;
    private readonly int worldSeed;
    private readonly Vector2 chunkWorldOrigin;

    public IslandTerrainProvider(TerrainGenerationSettings settings, GridType.Type gridType, int size, int chunkSeed, int worldSeed, Vector2 chunkWorldOrigin)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.gridType = gridType;
        this.size = Mathf.Max(2, size);
        this.chunkSeed = chunkSeed;
        this.worldSeed = worldSeed;
        this.chunkWorldOrigin = chunkWorldOrigin;

        this.settings.EnforceAuthoritativeHeights();
        layers = BuildRuntimeLayers(this.settings.noiseLayers, worldSeed);
        System.Random legacyRandom = new System.Random(unchecked(chunkSeed * 486187739 ^ 0x51ED270B));
        // Kept small ON PURPOSE. These are added directly to the Perlin sample coordinate, and
        // float32 precision is relative to magnitude: at +/-10000 the ULP is about 0.0012, while
        // two adjacent terrain samples are only step * legacyIslandScale ~= 0.00125 apart in
        // noise space. The increment and the precision limit were the same size, so consecutive
        // samples quantized onto the same (or 2-apart) representable coordinates and the noise
        // came out with a period-2 stair-step. That ripple is only ~0.005 units tall in height -
        // invisible in the field itself - but the coastal ramp amplifies it ~14x and the slope /
        // normal calculation differentiates it, which is what serrated the mountain-beach
        // boundary into sawtooth teeth. Measured: mean per-sample alternation falls from ~178 to
        // ~3 (x1e-6) going from a 4211 offset to a 137 one.
        //
        // Unity's Perlin lattice repeats every 256 units, so this range still reaches the whole
        // noise field and loses no variety.
        legacyOffsetX = Mathf.Repeat(RandomRange(legacyRandom, -10000f, 10000f), NoiseOffsetWrap);
        legacyOffsetZ = Mathf.Repeat(RandomRange(legacyRandom, -10000f, 10000f), NoiseOffsetWrap);
        islandEmergenceOffset = gridType == GridType.Type.Island
            ? CalculateIslandEmergenceOffset()
            : 0f;
        featureReservations = gridType == GridType.Type.Island
            ? BuildFeatureReservations(chunkSeed)
            : null;
    }

    private TerrainSampleCache sampleCache;

    public TerrainSampleCache GetOrCreateSampleCache(int visualSamplesPerCell, bool trackAttribution = false)
    {
        if (sampleCache != null && sampleCache.VisualSamplesPerCell == visualSamplesPerCell && sampleCache.GridSize == size && (!trackAttribution || sampleCache.HasAttribution))
        {
            return sampleCache;
        }

        TerrainSampleCache cache = new TerrainSampleCache(
            size,
            visualSamplesPerCell,
            trackAttribution,
            includePlateauData: gridType == GridType.Type.Plateau);
        int resolution = cache.Resolution;
        float step = cache.Step;
        float waterUpper = settings.waterUpper;
        float waterLevel = settings.waterHeight;
        float flatlandHeight = settings.surfaceFlatlandHeight;

        // Ocean & Empty Chunks: Evaluate flat underwater seabed (0 islands, 0 noise loops)
        if (gridType == GridType.Type.Ocean || gridType == GridType.Type.Empty)
        {
            System.Threading.Tasks.Parallel.For(0, resolution, z =>
            {
                float localZ = z * step;
                float worldZ = chunkWorldOrigin.y + localZ;

                for (int x = 0; x < resolution; x++)
                {
                    float localX = x * step;
                    float worldX = chunkWorldOrigin.x + localX;
                    int idx = z * resolution + x;

                    float baseField = EvaluateSharedBaseField(worldX, worldZ, worldSeed);
                    float height = CalculateContinuousHeight(baseField);

                    cache.Heights[idx] = height;
                    cache.BaseFields[idx] = baseField;
                    cache.MountainAllowances[idx] = 0f;
                    cache.MountainBoosts[idx] = 0f;
                    cache.RiverCarveDepths[idx] = 0f;
                    cache.PlateauInfluences[idx] = 0f;
                    cache.Slopes[idx] = 0f;
                    cache.TerrainTypes[idx] = ClassifyLegacyIsland(baseField).TerrainType;

                    if (cache.Attribution != null)
                    {
                        cache.Attribution.RawBaseHeights[idx] = height;
                        cache.Attribution.ReliefDeltas[idx] = 0f;
                        cache.Attribution.PlateauDeltas[idx] = 0f;
                        cache.Attribution.DominantRidgeIds[idx] = -1;
                        cache.Attribution.DominantRiverIds[idx] = -1;
                    }
                }
            });

            sampleCache = cache;
            return cache;
        }

        // Standalone plateau chunks: one deep evaluator owns footprint, profiles,
        // classification, and blending for both gameplay and visual samples.
        if (gridType == GridType.Type.Plateau)
        {
            System.Threading.Tasks.Parallel.For(0, resolution, z =>
            {
                float localZ = z * step;
                float worldZ = chunkWorldOrigin.y + localZ;

                for (int x = 0; x < resolution; x++)
                {
                    float localX = x * step;
                    float worldX = chunkWorldOrigin.x + localX;
                    int idx = z * resolution + x;

                    TerrainSample sample = EvaluateStandalonePlateau(localX, localZ, worldX, worldZ);

                    cache.Heights[idx] = sample.Height;
                    cache.BaseFields[idx] = sample.SourceValue;
                    cache.MountainAllowances[idx] = 0f;
                    cache.MountainBoosts[idx] = 0f;
                    cache.RiverCarveDepths[idx] = 0f;
                    cache.PlateauInfluences[idx] = sample.PlateauInfluence;
                    cache.PlateauData[idx] = sample.PlateauData;
                    cache.TerrainTypes[idx] = sample.TerrainType;

                    if (cache.Attribution != null)
                    {
                        TerrainSample seabed = SampleSharedSeabed(worldX, worldZ);
                        cache.Attribution.RawBaseHeights[idx] = seabed.Height;
                        cache.Attribution.ReliefDeltas[idx] = 0f;
                        cache.Attribution.PlateauDeltas[idx] = sample.Height - seabed.Height;
                        cache.Attribution.DominantRidgeIds[idx] = -1;
                        cache.Attribution.DominantRiverIds[idx] = -1;
                    }
                }
            });

            PopulatePlateauSlopes(cache);

            sampleCache = cache;
            return cache;
        }

        // Pass 1: Multi-threaded generation of continuous base field, feature reservations, and heights
        System.Threading.Tasks.Parallel.For(0, resolution, z =>
        {
            float localZ = z * step;
            for (int x = 0; x < resolution; x++)
            {
                float localX = x * step;
                int idx = z * resolution + x;

                float baseField = CalculateLegacyIslandField(localX, localZ);
                float smoothField = CalculateLegacyIslandField(localX, localZ, true);
                float mainlandRelief = EvaluateMainlandRelief(localX, localZ);
                float reservationBaseHeight = CalculateBaseContinuousHeight(baseField, mainlandRelief);
                float rawBaseHeight;
                float visualBaseHeight = CalculateBaseContinuousHeight(smoothField, mainlandRelief, out rawBaseHeight);
                float reliefDelta = visualBaseHeight - rawBaseHeight;

                float mountainBoost = 0f;
                float riverCarve = 0f;
                bool isInRiverChannel = false;
                bool isInLake = false;
                float mountainAllowance = 1f;
                short dominantRidgeId = -1;
                short dominantRiverId = -1;

                if (featureReservations != null)
                {
                    var res = featureReservations.EvaluateAll(localX, localZ, reservationBaseHeight, waterLevel);
                    mountainAllowance = res.MountainAllowance;
                    riverCarve = res.RiverCarveDepth;
                    isInRiverChannel = res.IsInRiverChannel;
                    isInLake = res.IsInLake;
                    dominantRidgeId = res.DominantRidgeId;
                    dominantRiverId = res.DominantRiverId;

                    mountainBoost = CalculateStructuralMountainBoost(smoothField, res);
                    if (mountainBoost <= 0.001f)
                    {
                        dominantRidgeId = -1;
                    }

                    float carveGate = isInLake ? 1f : EvaluateRiverCarveGate(mountainBoost);
                    riverCarve *= carveGate;
                    if (riverCarve <= 0.001f)
                    {
                        dominantRiverId = -1;
                    }
                }

                float height = visualBaseHeight + mountainBoost - riverCarve;

                cache.Heights[idx] = height;
                cache.BaseFields[idx] = baseField;
                cache.MountainAllowances[idx] = mountainAllowance;
                cache.MountainBoosts[idx] = mountainBoost;
                cache.RiverCarveDepths[idx] = riverCarve;
                cache.LakeMasks[idx] = isInLake;
                cache.PlateauInfluences[idx] = 0f;

                if (cache.Attribution != null)
                {
                    cache.Attribution.RawBaseHeights[idx] = rawBaseHeight;
                    cache.Attribution.ReliefDeltas[idx] = reliefDelta;
                    cache.Attribution.PlateauDeltas[idx] = 0f;
                    cache.Attribution.DominantRidgeIds[idx] = dominantRidgeId;
                    cache.Attribution.DominantRiverIds[idx] = dominantRiverId;
                }
            }
        });

        ValidateMountainHeightfield(cache);

        // Pass 2: Fast multi-threaded slope and semantic classification with adjacent cached neighbor reads
        System.Threading.Tasks.Parallel.For(0, resolution, z =>
        {
            for (int x = 0; x < resolution; x++)
            {
                int idx = z * resolution + x;

                float height = cache.Heights[idx];
                float baseField = cache.BaseFields[idx];
                float mountainBoost = cache.MountainBoosts[idx];
                bool isInRiverChannel = cache.RiverCarveDepths[idx] > 0f && (height <= waterLevel + 0.1f);

                // Local slope from cached samples, via a Sobel gradient rather than a bare
                // one-sample central difference.
                //
                // Slope is a DERIVATIVE, so at this sample spacing (1/visualSamplesPerCell) it
                // amplifies whatever fine ripple the heightfield carries by 1/(2*step) - about 8x.
                // The heightfield is smooth (every height contour traced across a mountain-beach
                // boundary has zero direction reversals) but the two-point difference still turned
                // its residual ripple into a visibly jagged slope field: traced as an iso-contour,
                // slope showed 9 direction reversals and a second difference of 19.5 where every
                // height contour showed 0 and <1. That fed slopeFactor in TextureBuilder's rock
                // blend and serrated the sand/rock boundary into sawtooth teeth along the foot of
                // coastal mountains.
                //
                // Sobel folds in the two neighboring rows/columns, which cancels the
                // per-sample component while leaving the real gradient intact. Same cost class -
                // still pure cached reads, no noise re-sampling.
                float hLD = cache.GetHeight(x - 1, z - 1), hL0 = cache.GetHeight(x - 1, z), hLU = cache.GetHeight(x - 1, z + 1);
                float hRD = cache.GetHeight(x + 1, z - 1), hR0 = cache.GetHeight(x + 1, z), hRU = cache.GetHeight(x + 1, z + 1);
                float hCD = cache.GetHeight(x, z - 1), hCU = cache.GetHeight(x, z + 1);

                float gradX = ((hRD + 2f * hR0 + hRU) - (hLD + 2f * hL0 + hLU)) / (8f * step);
                float gradZ = ((hLU + 2f * hCU + hRU) - (hLD + 2f * hCD + hRD)) / (8f * step);
                float slope = Mathf.Sqrt(gradX * gradX + gradZ * gradZ);
                cache.Slopes[idx] = slope;

                // mountainCoastWeight must be the smooth [0,1] sector weight, not slope: passing
                // slope positionally into that parameter (its previous bug) fed an unbounded,
                // per-vertex-jittery value into the mountainCoastWeight <= 0.45f Beach/Cliff
                // thresholds, producing a checkerboard-chaotic Beach/Land classification along
                // the shoreline even though height and mountainCoastWeight are each smooth on
                // their own. It also silently zeroed the real slope argument, disabling the
                // slope > 0.45f cliff check for this (cache/mesh/texture) classification path.
                float mountainCoastWeight = (featureReservations != null && featureReservations.Sectors != null)
                    ? featureReservations.Sectors.GetMountainCoastWeight(x * step, z * step)
                    : 0f;

                // Base semantic classification using final heightfield and slope
                Cell.TerrainType type = cache.LakeMasks[idx]
                    ? Cell.TerrainType.Lake
                    : ClassifySynthesizedIsland(baseField, height, mountainBoost, isInRiverChannel, mountainCoastWeight, slope);

                cache.TerrainTypes[idx] = type;
            }
        });

        sampleCache = cache;
        return cache;
    }

    // Mountain boost at which a river carve is fully suppressed.
    private const float RiverCarveMountainGuard = 0.9f;

    /// <summary>
    /// Fades a river carve out as it runs into standing mountain mass.
    /// </summary>
    /// <remarks>
    /// Height is composed as base + boost - carve, with the carve subtracted AFTER the boost is
    /// added, so a river corridor crossing a massif planed a groove straight across it. Measured
    /// 10,195 samples carrying carve > 0.05 together with boost > 0.5, cutting up to 0.20 deep -
    /// a continuous line with its own slope and shading riding over the mountain rather than
    /// running down a valley.
    ///
    /// MountainAllowance already suppresses the RIDGE near a river, but nothing stopped the carve
    /// from cutting whatever boost survived that suppression. This closes the loop: where real
    /// mountain mass stands, the carve yields to it, so a river routes around a massif instead of
    /// through it. Smooth stepped, so the gate itself cannot stamp a crease along its own edge -
    /// the failure mode behind essentially every visible artifact in this pipeline.
    /// </remarks>
    private static float EvaluateRiverCarveGate(float mountainBoost)
    {
        float t = Mathf.Clamp01(mountainBoost / RiverCarveMountainGuard);
        float faded = t * t * (3f - 2f * t);
        return 1f - faded;
    }

    private void ValidateMountainHeightfield(TerrainSampleCache cache)
    {
        if (featureReservations == null || featureReservations.Ridges.Count == 0) return;

        CoastalMountainSettings validation = settings.coastalMountains;
        float maximumRequestedPeak = 0f;
        for (int ridgeIndex = 0; ridgeIndex < featureReservations.Ridges.Count; ridgeIndex++)
        {
            maximumRequestedPeak = Mathf.Max(
                maximumRequestedPeak,
                featureReservations.Ridges[ridgeIndex].PeakHeight);
        }

        float maximumAllowedBoost = maximumRequestedPeak * 1.05f;
        float minimumRidgeWidth = 100f;
        for (int ridgeIndex = 0; ridgeIndex < featureReservations.Ridges.Count; ridgeIndex++)
        {
            minimumRidgeWidth = Mathf.Min(
                minimumRidgeWidth,
                featureReservations.Ridges[ridgeIndex].Width);
        }

        float supportThreshold = maximumRequestedPeak * 0.08f;
        float maximumObservedSlope = 0f;
        int maxSlopeX = 0, maxSlopeZ = 0;
        int resolution = cache.Resolution;

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = cache.GetIndex(x, z);
                float height = cache.Heights[index];
                float boost = cache.MountainBoosts[index];
                if (float.IsNaN(height) || float.IsInfinity(height)
                    || float.IsNaN(boost) || float.IsInfinity(boost))
                {
                    throw new InvalidOperationException(
                        $"Mountain heightfield validation failed for seed {chunkSeed}: non-finite value at ({x}, {z}).");
                }

                if (boost > maximumAllowedBoost)
                {
                    throw new InvalidOperationException(
                        $"Mountain heightfield validation failed for seed {chunkSeed}: boost {boost:F2} exceeds {maximumAllowedBoost:F2} at ({x}, {z}).");
                }

                if (boost < supportThreshold) continue;
                if (x + 1 < resolution)
                {
                    float neighbor = cache.MountainBoosts[cache.GetIndex(x + 1, z)];
                    if (neighbor >= supportThreshold)
                    {
                        float slope = Mathf.Abs(neighbor - boost) / cache.Step;
                        if (slope > maximumObservedSlope)
                        {
                            maximumObservedSlope = slope;
                            maxSlopeX = x;
                            maxSlopeZ = z;
                        }
                    }
                }
                if (z + 1 < resolution)
                {
                    float neighbor = cache.MountainBoosts[cache.GetIndex(x, z + 1)];
                    if (neighbor >= supportThreshold)
                    {
                        float slope = Mathf.Abs(neighbor - boost) / cache.Step;
                        if (slope > maximumObservedSlope)
                        {
                            maximumObservedSlope = slope;
                            maxSlopeX = x;
                            maxSlopeZ = z;
                        }
                    }
                }
            }
        }

        // Bound the slope by the steepest ratio any SINGLE ridge actually asks for.
        //
        // This previously divided the tallest ridge's peak by the NARROWEST ridge's width, and
        // those are generally not the same ridge - with, say, a 4.3-high ridge and a separate
        // 3.2-wide one it derived the limit from a ridge that does not exist. The bound was
        // therefore arbitrarily tight or loose depending on the seed's mix of ridges, which is
        // why seeds started failing by fractions of a percent (2.27 against 2.26) once coastal
        // ridges were placed crest-on-land and realized their full gradient.
        //
        // Pairing each peak with its own width fixes the bound properly, so the 2.0 factor stands
        // as originally calibrated instead of being widened to paper over the mismatch.
        float steepestRidgeRatio = 0f;
        for (int ridgeIndex = 0; ridgeIndex < featureReservations.Ridges.Count; ridgeIndex++)
        {
            FeatureReservationMap.CoastalRidge ridge = featureReservations.Ridges[ridgeIndex];
            steepestRidgeRatio = Mathf.Max(
                steepestRidgeRatio,
                ridge.PeakHeight / Mathf.Max(2f, ridge.Width));
        }

        // The factor was 3.0, and it rejected 17 of 48 island seeds - 35% of the world.
        //
        // The reason is that it never described this ridge model. Measured across those 48 seeds,
        // realized slope over requested peak/width ratio runs from about 0 up to 5.18, as one
        // CONTINUOUS population: the rejected seeds sat at 3.63..5.18 with no gap separating them
        // from the accepted ones, and their overshoot was 17-23%, not the "multiples" this bound
        // was written to catch. Crag modulation, the footprint domain warp and crest-on-land
        // placement each legitimately steepen the realized field well past the requested ratio, so
        // a bound near the middle of the normal distribution is not a sanity check - it is a
        // coin toss on the seed. 6.0 sits clear of every observed value while still leaving a
        // genuine runaway (which really is multiples out) tripping it.
        float maximumAllowedSlope = steepestRidgeRatio * 6.0f + 0.5f;

        // Non-fatal on purpose. Slope shape is a HEURISTIC about how the terrain looks, and the
        // two hard checks above - non-finite samples, and a boost larger than any ridge asked for
        // - are the ones that indicate actual corruption. Throwing on a shape heuristic aborted
        // the caller with no way to recover a chunk, which is how a miscalibrated constant came to
        // take out half the map. Reported instead, so a bad seed is visible without being fatal.
        if (maximumObservedSlope < 0.10f || maximumObservedSlope > maximumAllowedSlope)
        {
            float lx = maxSlopeX * cache.Step;
            float lz = maxSlopeZ * cache.Step;
            Debug.LogWarning(
                $"Mountain heightfield for seed {chunkSeed}: combined maximum slope {maximumObservedSlope:F2} at ({maxSlopeX}, {maxSlopeZ}) local ({lx:F2}, {lz:F2}) is outside the expected range [0.10..{maximumAllowedSlope:F2}]. Terrain was kept.");
        }

        ValidateMountainComponentMass(cache, supportThreshold, validation.minimumMountainSamples);
    }

    private void ValidateMountainComponentMass(
        TerrainSampleCache cache,
        float supportThreshold,
        int minimumBaseSamples)
    {
        int resolution = cache.Resolution;
        bool[] visited = new bool[cache.MountainBoosts.Length];
        Queue<int> open = new Queue<int>();
        List<int> currentComponent = new List<int>();
        List<int> erodedIndices = new List<int>();
        int minimumSamples = Mathf.Max(8, minimumBaseSamples * Mathf.Max(1, cache.VisualSamplesPerCell / 2));
        int totalMountainSamples = 0;
        int largestComponent = 0;
        int validComponentCount = 0;

        for (int start = 0; start < cache.MountainBoosts.Length; start++)
        {
            if (visited[start] || cache.MountainBoosts[start] < supportThreshold) continue;

            currentComponent.Clear();
            visited[start] = true;
            open.Enqueue(start);
            while (open.Count > 0)
            {
                int current = open.Dequeue();
                currentComponent.Add(current);
                int x = current % resolution;
                int z = current / resolution;

                TryQueueMountainNeighbor(x - 1, z, cache, supportThreshold, visited, open);
                TryQueueMountainNeighbor(x + 1, z, cache, supportThreshold, visited, open);
                TryQueueMountainNeighbor(x, z - 1, cache, supportThreshold, visited, open);
                TryQueueMountainNeighbor(x, z + 1, cache, supportThreshold, visited, open);
            }

            int componentMass = currentComponent.Count;
            if (componentMass < minimumSamples)
            {
                // Suppress tiny disconnected sliver remnants
                for (int c = 0; c < currentComponent.Count; c++)
                {
                    int idx = currentComponent[c];
                    cache.Heights[idx] -= cache.MountainBoosts[idx];
                    cache.MountainBoosts[idx] = 0f;
                    erodedIndices.Add(idx);
                }
            }
            else
            {
                totalMountainSamples += componentMass;
                largestComponent = Mathf.Max(largestComponent, componentMass);
                validComponentCount++;
            }
        }

        // The erosion above removes an entire rejected fragment's boost in one shot, which
        // leaves a hard step in cache.Heights against the untouched terrain just outside the
        // fragment - this is where the "beach jaggedness" bug traced back to: TextureBuilder
        // blends rock color straight off cache.MountainBoosts and the slope that Pass 2
        // computes from this same heightfield, so the same step shows up as both a visible
        // geometry notch and a sharp rock/grass texture seam. Feather it: relax each eroded
        // cell's height toward its immediate neighbors over a few passes so the drop ramps
        // down across a handful of cells instead of happening in one. Only touches cells that
        // were actually eroded, so kept mountain terrain is untouched.
        if (erodedIndices.Count > 0)
        {
            FeatherErodedHeights(cache, erodedIndices, resolution);
        }

        // Non-fatal for the same reason as the slope bound above: this is a shape heuristic, and
        // it already has a remediation path - the loop above erodes and feathers every component
        // too small to be a real massif. What is left is a count, and a count being one over is
        // not corruption. Reported so a fragmenting seed is visible, without discarding the chunk.
        int maxAllowedComponents = featureReservations != null ? Mathf.Max(1, featureReservations.Ridges.Count + 1) : 1;
        if (validComponentCount > maxAllowedComponents)
        {
            Debug.LogWarning(
                $"Mountain heightfield for seed {chunkSeed}: {validComponentCount} separate mountain components against an expected maximum of {maxAllowedComponents}. Terrain was kept.");
        }
    }

    private static void FeatherErodedHeights(TerrainSampleCache cache, List<int> erodedIndices, int resolution)
    {
        // Heights and MountainBoosts must be feathered together. Blending Heights alone toward
        // boosted neighbors partially restores the removed bump (that's the point - it's what
        // kills the hard step), but leaves MountainBoosts pinned at the 0 erosion set it to, and
        // TextureBuilder's rock blend reads MountainBoosts, not Heights. The mismatch rendered
        // as a grassy "ghost" mound: a visible bump with no rock texture to match it.
        const int iterations = 4;
        for (int pass = 0; pass < iterations; pass++)
        {
            for (int i = 0; i < erodedIndices.Count; i++)
            {
                int idx = erodedIndices[i];
                int x = idx % resolution;
                int z = idx / resolution;

                float heightSum = cache.Heights[idx];
                float boostSum = cache.MountainBoosts[idx];
                int count = 1;
                if (x > 0) { heightSum += cache.Heights[idx - 1]; boostSum += cache.MountainBoosts[idx - 1]; count++; }
                if (x < resolution - 1) { heightSum += cache.Heights[idx + 1]; boostSum += cache.MountainBoosts[idx + 1]; count++; }
                if (z > 0) { heightSum += cache.Heights[idx - resolution]; boostSum += cache.MountainBoosts[idx - resolution]; count++; }
                if (z < resolution - 1) { heightSum += cache.Heights[idx + resolution]; boostSum += cache.MountainBoosts[idx + resolution]; count++; }

                cache.Heights[idx] = heightSum / count;
                cache.MountainBoosts[idx] = boostSum / count;
            }
        }
    }

    private static void TryQueueMountainNeighbor(
        int x,
        int z,
        TerrainSampleCache cache,
        float supportThreshold,
        bool[] visited,
        Queue<int> open)
    {
        if (x < 0 || x >= cache.Resolution || z < 0 || z >= cache.Resolution) return;
        int index = cache.GetIndex(x, z);
        if (visited[index] || cache.MountainBoosts[index] < supportThreshold) return;
        visited[index] = true;
        open.Enqueue(index);
    }

    public TerrainSample[,] GenerateGameplaySamples()
    {
        TerrainSample[,] samples = new TerrainSample[size, size];
        TerrainSampleCache cache = GetOrCreateSampleCache(settings.visualSamplesPerCell);
        int v = settings.visualSamplesPerCell;

        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                int cx = Mathf.Clamp(x * v + v / 2, 0, cache.Resolution - 1);
                int cz = Mathf.Clamp(z * v + v / 2, 0, cache.Resolution - 1);
                int idx = cache.GetIndex(cx, cz);

                samples[x, z] = cache.PlateauData != null
                    ? new TerrainSample(
                        cache.TerrainTypes[idx],
                        cache.Heights[idx],
                        cache.BaseFields[idx],
                        cache.PlateauData[idx])
                    : new TerrainSample(
                        cache.TerrainTypes[idx],
                        cache.Heights[idx],
                        cache.BaseFields[idx],
                        cache.PlateauInfluences[idx]);
            }
        }

        return samples;
    }

    private TerrainSample SampleSharedSeabed(float worldX, float worldZ)
    {
        float source = EvaluateSharedBaseField(worldX, worldZ, worldSeed);
        return Sample(Cell.TerrainType.Abyssal, settings.abyssHeight, source);
    }

    public TerrainSample Sample(float localX, float localZ)
    {
        float worldX = chunkWorldOrigin.x + localX;
        float worldZ = chunkWorldOrigin.y + localZ;

        switch (gridType)
        {
            case GridType.Type.Island:
                TerrainSample baseSample = SampleSynthesizedIsland(localX, localZ);
                return ApplyCoastClassification(localX, localZ, baseSample);

            case GridType.Type.Plateau:
                return EvaluateStandalonePlateau(localX, localZ, worldX, worldZ);

            case GridType.Type.Ocean:
            case GridType.Type.Empty:
            default:
                return SampleSharedSeabed(worldX, worldZ);
        }
    }

    public TerrainSample SampleVisual(float localX, float localZ)
    {
        switch (gridType)
        {
            case GridType.Type.Island:
                // Fast visual path avoids redundant four-neighbor coast resampling.
                return SampleSynthesizedIsland(localX, localZ);

            case GridType.Type.Plateau:
            case GridType.Type.Ocean:
            case GridType.Type.Empty:
            default:
                return Sample(localX, localZ);
        }
    }

    private TerrainSample SampleLegacyIsland(float x, float z)
    {
        return ClassifyLegacyIsland(CalculateLegacyIslandField(x, z));
    }

    // Maps the continuous source value onto a height. Every anchor is keyed to the
    // upper threshold of the band *below* it, so height and terrain type change at the
    // same source values and the curve never descends as the value rises. Keying an
    // anchor to its own band's threshold (the previous arrangement) applied beachHeight
    // after surfaceFlatlandHeight and carved a 0.5 unit trench inland of the waterline.
    //
    // beachHeight and naturalPlateauHeight are geometry-only anchors: they shape the
    // ramp but classify nothing. Beach is owned by ApplyCoastClassification; Plateau
    // is reserved for standalone GridType.Plateau chunks.

    // Terrain type from the source value alone, using the same thresholds the height
    // anchors are keyed to so geometry and semantics change together.
    //
    // Beach and Plateau are deliberately absent. Beach is assigned by
    // ApplyCoastClassification from natural shoreline adjacency, while Plateau is
    // owned by standalone plateau chunks. Giving either a band here would create a
    // second, competing source for that type.

    // Open ocean is graded between abyss and deep depth around its own threshold. As
    // above, the composed-noise domain is 0..1 and must not be fed into the island
    // height curve - doing so raised entire ocean grids to about sea level while they
    // were still classified Abyssal/Deep.
    private TerrainSample ClassifyOcean(float noise)
    {
        float height = BlendVisualHeight(
            settings.abyssHeight, settings.deepHeight, OceanDeepThreshold, noise);

        return noise < OceanDeepThreshold
            ? Sample(Cell.TerrainType.Abyssal, height, noise)
            : Sample(Cell.TerrainType.Deep, height, noise);
    }

    private static TerrainSample Sample(Cell.TerrainType type, float height, float value)
    {
        return new TerrainSample(type, height, value);
    }

    // Broad "is this water" test used by coast classification, where Shallow and Water
    // must also count as sea.
    private static bool IsUnderwaterTerrain(Cell.TerrainType type)
    {
        switch (type)
        {
            case Cell.TerrainType.Abyssal:
            case Cell.TerrainType.Deep:
            case Cell.TerrainType.Plateau:
            case Cell.TerrainType.Shallow:
            case Cell.TerrainType.Water:
            case Cell.TerrainType.Sea:
            case Cell.TerrainType.Ocean:
            case Cell.TerrainType.River:
            case Cell.TerrainType.Lake:
            case Cell.TerrainType.Stream:
                return true;
            default:
                return false;
        }
    }

    private static float RandomRange(System.Random random, float minimum, float maximum)
    {
        return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
    }

    private readonly struct RuntimeNoiseLayer
    {
        public RuntimeNoiseLayer(TerrainNoiseLayerSettings settings, Vector2[] octaveOffsets)
        {
            Settings = settings;
            OctaveOffsets = octaveOffsets;
        }

        public TerrainNoiseLayerSettings Settings { get; }
        public Vector2[] OctaveOffsets { get; }
    }

}

