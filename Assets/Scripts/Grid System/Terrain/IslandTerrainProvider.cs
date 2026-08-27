using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct TerrainSample
{
    public TerrainSample(Cell.TerrainType terrainType, float height, float sourceValue, float plateauInfluence = 0f)
    {
        TerrainType = terrainType;
        Height = height;
        SourceValue = sourceValue;
        PlateauInfluence = plateauInfluence;
    }

    public Cell.TerrainType TerrainType { get; }
    public float Height { get; }
    public float SourceValue { get; }
    public float PlateauInfluence { get; }
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

    // Influence at which a cell stops being rim and becomes plateau core. Acceptance
    // and application must use the identical value or they disagree about which cells
    // the region intends to paint.
    private const float FullPlateauInfluence = 0.9999f;

    public TerrainGenerationSettings Settings => settings;
    public FeatureReservationMap Reservations => featureReservations;
    private readonly TerrainGenerationSettings settings;
    private readonly GridType.Type gridType;
    private readonly int size;
    private readonly List<RuntimeNoiseLayer> layers;
    private readonly List<UnderwaterPlateauRegion> plateauRegions;
    private readonly FeatureReservationMap featureReservations;
    // Upper bound for any offset added to a Perlin sample coordinate. See the constructor for
    // why large offsets destroy fine sampling precision.
    private const float NoiseOffsetWrap = 128f;

    private readonly float legacyOffsetX;
    private readonly float legacyOffsetZ;
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

        // Authoritative physical elevation hierarchy:
        this.settings.surfaceFlatlandHeight = 0.85f;
        this.settings.beachHeight = 0.25f;
        this.settings.waterHeight = -0.6f;
        this.settings.shallowHeight = -1.5f;
        this.settings.naturalPlateauHeight = -2.5f;
        this.settings.deepHeight = -3.2f;
        this.settings.abyssHeight = -4.5f;
        this.settings.cliffHeight = 2.4f;
        this.settings.mountainHeight = 3.2f;
        this.settings.mountainPeakHeight = 4.2f;
        this.settings.underwaterPlateauHeight = -2.2f;

        this.settings.Validate();
        layers = BuildRuntimeLayers(this.settings.noiseLayers, worldSeed);
        System.Random legacyRandom = new System.Random(unchecked(chunkSeed * 486187739 ^ 0x51ED270B));
        // Kept small ON PURPOSE. These are added directly to the Perlin sample coordinate, and
        // float32 precision is relative to magnitude: at +/-10000 the ULP is about 0.0012, while
        // two adjacent terrain samples are only step * legacyIslandScale ~= 0.00125 apart in
        // noise space. The increment and the precision limit were the same size, so consecutive
        // samples quantised onto the same (or 2-apart) representable coordinates and the noise
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
        plateauRegions = gridType == GridType.Type.Island
            ? BuildUnderwaterPlateauRegions(chunkSeed)
            : new List<UnderwaterPlateauRegion>();
        featureReservations = gridType == GridType.Type.Island
            ? BuildFeatureReservations(chunkSeed)
            : null;
    }

    private TerrainSampleCache sampleCache;

    public TerrainSampleCache GetOrCreateSampleCache(int visualSamplesPerCell)
    {
        if (sampleCache != null && sampleCache.VisualSamplesPerCell == visualSamplesPerCell && sampleCache.GridSize == size)
        {
            return sampleCache;
        }

        TerrainSampleCache cache = new TerrainSampleCache(size, visualSamplesPerCell);
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
                }
            });

            sampleCache = cache;
            return cache;
        }

        // Underwater Plateau Chunks: Evaluate shallow plateau shelf
        if (gridType == GridType.Type.Plateau)
        {
            float W = 8f;
            System.Threading.Tasks.Parallel.For(0, resolution, z =>
            {
                float localZ = z * step;
                float worldZ = chunkWorldOrigin.y + localZ;

                for (int x = 0; x < resolution; x++)
                {
                    float localX = x * step;
                    float worldX = chunkWorldOrigin.x + localX;
                    int idx = z * resolution + x;

                    TerrainSample canonicalBase = SampleSharedSeabed(worldX, worldZ);
                    float plateauNoise = SampleComposedNoise(worldX, worldZ);
                    float localMask = SampleIslandMask(localX, localZ, plateauNoise);

                    float dx = Mathf.Min(localX, size - localX);
                    float dz = Mathf.Min(localZ, size - localZ);
                    float tx = Mathf.Clamp01(dx / W);
                    float tz = Mathf.Clamp01(dz / W);
                    float edgeWeight = (tx * tx * tx * (tx * (tx * 6f - 15f) + 10f)) * (tz * tz * tz * (tz * (tz * 6f - 15f) + 10f));

                    float plateauInfluence = localMask * edgeWeight;
                    float height = Mathf.Lerp(canonicalBase.Height, settings.underwaterPlateauHeight, plateauInfluence);
                    Cell.TerrainType terrainType = plateauInfluence >= FullPlateauInfluence
                        ? Cell.TerrainType.Plateau
                        : canonicalBase.TerrainType;

                    cache.Heights[idx] = height;
                    cache.BaseFields[idx] = canonicalBase.SourceValue;
                    cache.MountainAllowances[idx] = 0f;
                    cache.MountainBoosts[idx] = 0f;
                    cache.RiverCarveDepths[idx] = 0f;
                    cache.PlateauInfluences[idx] = plateauInfluence;
                    cache.Slopes[idx] = 0f;
                    cache.TerrainTypes[idx] = terrainType;
                }
            });

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
                float reservationBaseHeight = CalculateBaseContinuousHeight(baseField);
                float visualBaseHeight = CalculateBaseContinuousHeight(smoothField);

                float mountainBoost = 0f;
                float riverCarve = 0f;
                bool isInRiverChannel = false;
                float mountainAllowance = 1f;

                if (featureReservations != null)
                {
                    var res = featureReservations.EvaluateAll(localX, localZ, reservationBaseHeight, waterLevel);
                    mountainAllowance = res.MountainAllowance;
                    riverCarve = res.RiverCarveDepth;
                    isInRiverChannel = res.IsInRiverChannel;

                    mountainBoost = CalculateStructuralMountainBoost(smoothField, res);
                }

                float height = visualBaseHeight + mountainBoost - riverCarve;

                TerrainSample sample = new TerrainSample(Cell.TerrainType.Land, height, baseField);
                if (gridType == GridType.Type.Island)
                {
                    sample = ApplyUnderwaterPlateauRegions(localX, localZ, sample);
                }

                cache.Heights[idx] = sample.Height;
                cache.BaseFields[idx] = baseField;
                cache.MountainAllowances[idx] = mountainAllowance;
                cache.MountainBoosts[idx] = mountainBoost;
                cache.RiverCarveDepths[idx] = riverCarve;
                cache.PlateauInfluences[idx] = sample.PlateauInfluence;
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
                // Sobel folds in the two neighbouring rows/columns, which cancels the
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
                Cell.TerrainType type = ClassifySynthesizedIsland(baseField, height, mountainBoost, isInRiverChannel, mountainCoastWeight, slope);

                cache.TerrainTypes[idx] = type;
            }
        });

        sampleCache = cache;
        return cache;
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
        // ridges were placed crest-on-land and realised their full gradient.
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

        float maximumAllowedSlope = steepestRidgeRatio * 2.0f + 0.5f;
        if (maximumObservedSlope < 0.10f || maximumObservedSlope > maximumAllowedSlope)
        {
            float lx = maxSlopeX * cache.Step;
            float lz = maxSlopeZ * cache.Step;
            throw new InvalidOperationException(
                $"Mountain heightfield validation failed for seed {chunkSeed}: combined maximum slope {maximumObservedSlope:F2} at ({maxSlopeX}, {maxSlopeZ}) local ({lx:F2}, {lz:F2}) is outside sanity range [0.10..{maximumAllowedSlope:F2}].");
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

        int maxAllowedComponents = featureReservations != null ? Mathf.Max(1, featureReservations.Ridges.Count + 1) : 1;
        if (validComponentCount > maxAllowedComponents)
        {
            throw new InvalidOperationException(
                $"Mountain heightfield validation failed for seed {chunkSeed}: excessive fragmented mountain components ({validComponentCount} components, max allowed: {maxAllowedComponents}).");
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

                samples[x, z] = new TerrainSample(
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
        return ClassifyLegacyIsland(source);
    }

    public TerrainSample Sample(float localX, float localZ)
    {
        float worldX = chunkWorldOrigin.x + localX;
        float worldZ = chunkWorldOrigin.y + localZ;

        switch (gridType)
        {
            case GridType.Type.Island:
                // 1. Synthesized base terrain with feature reservations
                TerrainSample baseSample = SampleSynthesizedIsland(localX, localZ);
                // 2. Plateau height adjustments
                TerrainSample adjustedSample = ApplyUnderwaterPlateauRegions(localX, localZ, baseSample);
                // 3. Coast classification
                return ApplyCoastClassification(localX, localZ, adjustedSample);

            case GridType.Type.Plateau:
                TerrainSample canonicalBase = SampleSharedSeabed(worldX, worldZ);
                float plateauNoise = SampleComposedNoise(worldX, worldZ);
                float localMask = SampleIslandMask(localX, localZ, plateauNoise);
                
                float W = 8f;
                float dx = Mathf.Min(localX, size - localX);
                float dz = Mathf.Min(localZ, size - localZ);
                float tx = Mathf.Clamp01(dx / W);
                float tz = Mathf.Clamp01(dz / W);
                float edgeWeight = (tx * tx * tx * (tx * (tx * 6f - 15f) + 10f)) * (tz * tz * tz * (tz * (tz * 6f - 15f) + 10f));

                float plateauInfluence = localMask * edgeWeight;
                float height = Mathf.Lerp(canonicalBase.Height, settings.underwaterPlateauHeight, plateauInfluence);
                Cell.TerrainType terrainType = plateauInfluence >= FullPlateauInfluence
                    ? Cell.TerrainType.Plateau
                    : canonicalBase.TerrainType;

                return new TerrainSample(terrainType, height, canonicalBase.SourceValue, plateauInfluence);

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
                // Fast visual path: computes continuous synthesized height and plateau influence
                // without redundant 4-neighbor coast resampling
                TerrainSample baseSample = SampleSynthesizedIsland(localX, localZ);
                return ApplyUnderwaterPlateauRegions(localX, localZ, baseSample);

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
    // ramp but classify nothing. Beach is owned by ApplyCoastClassification and Plateau
    // by ApplyUnderwaterPlateauRegions.

    // Terrain type from the source value alone, using the same thresholds the height
    // anchors are keyed to so geometry and semantics change together.
    //
    // Beach and Plateau are deliberately absent. Beach is assigned by
    // ApplyCoastClassification from natural shoreline adjacency, and Plateau by
    // ApplyUnderwaterPlateauRegions. Giving either a band here would create a second,
    // competing source for that type.

    // CalculateIslandHeightSource

    // value here is a Clamp01 mask from CalculatePlateauField, not the legacy island
    // field, so CalculateContinuousHeight must not be used: that curve is tuned for the
    // island domain and maps 0.4+ onto shoreline and land heights, which lifted whole
    // standalone plateau grids up to roughly sea level.

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

    /// <summary>Why a plateau candidate was turned down, for generation diagnostics.</summary>
    private enum PlateauRejection
    {
        None = 0,
        InteriorTooNarrow,
        TooCloseToExisting,
        ShelfRelationship,
        CoreOutOfBounds,
        CoreOnIllegalSubstrate,
        CoreTooSmall,
        Count
    }

    /// <summary>
    /// Every cell the region intends to paint must be substrate the application pass
    /// will actually paint.
    ///
    /// Acceptance and application have to agree on one definition of legal ground.
    /// ApplyUnderwaterPlateauRegions only paints IsDeepOffshoreTerrain, so a core cell
    /// sitting on Shallow, Water, Beach or Land is silently skipped at paint time and
    /// leaves a hole. The previous test could not see that: it took 49 points across
    /// the bounding box and measured them against the broader IsUnderwaterTerrain, so
    /// Shallow and Water counted as valid and regions were routinely accepted with well
    /// under half their core on legal ground - which is what reduced plateaus to slivers.
    ///
    /// This walks the whole core at cell resolution, at the same cell-centre
    /// coordinates GenerateGameplaySamples uses, and fails the candidate on the first
    /// illegal cell. An accepted region is therefore painted in full, with no clipping.
    /// </summary>

    private TerrainSample SampleBaseIsland(float x, float z)
    {
        return SampleLegacyIsland(x, z);
    }

    // HasSufficientInBoundsInterior was removed here: it allowed 15% of a core to fall
    // outside the grid, which is the same silent-clipping failure as illegal substrate.
    // HasLegalPlateauSubstrate now rejects on the first out-of-bounds core cell, which
    // is strictly stronger, so keeping the old sparse 11x11 test would only have been a
    // second, weaker opinion about the same question.

    // Deliberate plateaus are a deep-ocean construction feature, so they only operate
    // on open water. Keeping them off Shallow/Water leaves the coastal shelf to the
    // natural shoreline and guarantees they cannot reach Beach or Land at all.
    private static bool IsDeepOffshoreTerrain(Cell.TerrainType type)
    {
        return type == Cell.TerrainType.Deep || type == Cell.TerrainType.Abyssal;
    }

    // Broad "is this water" test, used by coast classification where Shallow and Water
    // must also count as sea. Deliberate plateau placement uses IsDeepOffshoreTerrain.
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

    private readonly struct PlateauLobe
    {
        private readonly float cosine;
        private readonly float sine;

        public PlateauLobe(
            Vector2 center,
            float radiusX,
            float radiusZ,
            float rotation)
        {
            Center = center;
            RadiusX = radiusX;
            RadiusZ = radiusZ;
            cosine = Mathf.Cos(rotation);
            sine = Mathf.Sin(rotation);
        }

        public Vector2 Center { get; }
        public float RadiusX { get; }
        public float RadiusZ { get; }
        public float BoundingRadius => Mathf.Max(RadiusX, RadiusZ);

        public float SignedField(Vector2 point)
        {
            Vector2 delta = point - Center;
            float localX = delta.x * cosine + delta.y * sine;
            float localZ = -delta.x * sine + delta.y * cosine;
            return 1f - new Vector2(localX / RadiusX, localZ / RadiusZ).magnitude;
        }

        public Vector2 NormalizedToWorld(Vector2 normalized)
        {
            float localX = normalized.x * RadiusX;
            float localZ = normalized.y * RadiusZ;
            return Center + new Vector2(
                localX * cosine - localZ * sine,
                localX * sine + localZ * cosine);
        }
    }

    private readonly struct UnderwaterPlateauRegion
    {
        private readonly PlateauLobe[] positiveLobes;
        private readonly PlateauLobe[] cutLobes;
        private readonly float edgeIrregularity;
        private readonly float edgeDistortionScale;
        private readonly Vector2 distortionOffset;
        private readonly float cutStrength;
        private readonly float boundingRadius;

        public UnderwaterPlateauRegion(
            PlateauLobe[] positiveLobes,
            PlateauLobe[] cutLobes,
            float height,
            float transitionWidth,
            float edgeIrregularity,
            float edgeDistortionScale,
            Vector2 distortionOffset,
            float cutStrength)
        {
            this.positiveLobes = positiveLobes;
            this.cutLobes = cutLobes;
            Height = height;
            TransitionWidth = transitionWidth;
            this.edgeIrregularity = edgeIrregularity;
            this.edgeDistortionScale = edgeDistortionScale;
            this.distortionOffset = distortionOffset;
            this.cutStrength = cutStrength;

            PlateauLobe primary = positiveLobes[0];
            Center = primary.Center;
            RadiusX = primary.RadiusX;
            RadiusZ = primary.RadiusZ;
            float maximumExtent = primary.BoundingRadius;
            for (int i = 1; i < positiveLobes.Length; i++)
            {
                maximumExtent = Mathf.Max(
                    maximumExtent,
                    Vector2.Distance(Center, positiveLobes[i].Center) + positiveLobes[i].BoundingRadius);
            }
            boundingRadius = maximumExtent + Mathf.Min(RadiusX, RadiusZ) * edgeIrregularity;
        }

        public Vector2 Center { get; }
        public float RadiusX { get; }
        public float RadiusZ { get; }
        public float Height { get; }
        public float TransitionWidth { get; }
        public float BoundingRadius => boundingRadius;

        public float CalculateInfluence(float worldX, float worldZ)
        {
            Vector2 point = new Vector2(worldX, worldZ);
            if (Vector2.Distance(point, Center) > BoundingRadius) return 0f;

            float warpMagnitude = Mathf.Min(RadiusX, RadiusZ) * edgeIrregularity;
            Vector2 warpedPoint = point + new Vector2(
                Mathf.PerlinNoise(
                    (worldX + distortionOffset.x) / edgeDistortionScale,
                    (worldZ + distortionOffset.y) / edgeDistortionScale) * 2f - 1f,
                Mathf.PerlinNoise(
                    (worldX + distortionOffset.y + 173.31f) / edgeDistortionScale,
                    (worldZ + distortionOffset.x - 91.73f) / edgeDistortionScale) * 2f - 1f)
                * warpMagnitude;

            float field = float.MinValue;
            for (int i = 0; i < positiveLobes.Length; i++)
            {
                field = Mathf.Max(field, positiveLobes[i].SignedField(warpedPoint));
            }

            float cutField = 0f;
            for (int i = 0; i < cutLobes.Length; i++)
            {
                cutField = Mathf.Max(cutField, Mathf.Max(0f, cutLobes[i].SignedField(warpedPoint)));
            }
            field -= cutField * cutStrength;

            if (field <= 0f) return 0f;
            if (field >= TransitionWidth) return 1f;
            return Mathf.SmoothStep(0f, 1f, field / TransitionWidth);
        }

        public Vector2 NormalizedToWorld(Vector2 normalized)
        {
            return positiveLobes[0].NormalizedToWorld(normalized);
        }
    }
}

