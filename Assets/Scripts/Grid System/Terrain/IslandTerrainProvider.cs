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
    private readonly TerrainGenerationSettings settings;
    private readonly GridType.Type gridType;
    private readonly int size;
    private readonly List<RuntimeNoiseLayer> layers;
    private readonly List<UnderwaterPlateauRegion> plateauRegions;
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
        this.settings.Validate();
        layers = BuildRuntimeLayers(this.settings.noiseLayers, worldSeed);
        System.Random legacyRandom = new System.Random(unchecked(chunkSeed * 486187739 ^ 0x51ED270B));
        legacyOffsetX = RandomRange(legacyRandom, -10000f, 10000f);
        legacyOffsetZ = RandomRange(legacyRandom, -10000f, 10000f);
        plateauRegions = new List<UnderwaterPlateauRegion>();
    }

    public TerrainSample[,] GenerateGameplaySamples()
    {
        TerrainSample[,] samples = new TerrainSample[size, size];

        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                // Cell [x,z] physically occupies local [x, x+1) x [z, z+1), so its
                // representative sample is taken at the centre. The array index and
                // the spatial sample point are deliberately not the same number.
                samples[x, z] = Sample(x + 0.5f, z + 0.5f);
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
                // 1. Base terrain (without coast)
                TerrainSample baseSample = SampleLegacyIsland(localX, localZ);
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
        return Sample(localX, localZ);
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

