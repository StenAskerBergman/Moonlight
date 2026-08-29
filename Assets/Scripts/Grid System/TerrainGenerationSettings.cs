using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class TerrainNoiseLayerSettings
{
    public string name = "Terrain Layer";
    public bool enabled = true;
    [Min(0.001f)] public float scale = 48f;
    [Range(1, 8)] public int octaves = 3;
    [Range(0f, 1f)] public float persistence = 0.5f;
    [Min(1f)] public float lacunarity = 2f;
    [Min(0f)] public float weight = 1f;
    public Vector2 offset;
    public TerrainNoiseMode mode = TerrainNoiseMode.Fractal;
}

public enum TerrainNoiseMode
{
    Fractal,
    Ridged
}

public enum PlateauShapeMode
{
    Auto,
    Rounded,
    Elongated,
    Crescent,
    TwinLobed
}

[Serializable]
public sealed class CoastalMountainSettings
{
    public bool enabled = true;
    [Range(0, 8)] public int minRidges = 3;
    [Range(0, 8)] public int maxRidges = 5;
    [Min(3f)] public float minRidgeLength = 22f;
    [Min(3f)] public float maxRidgeLength = 48f;
    [Min(2f)] public float minRidgeWidth = 8f;
    [Min(2f)] public float maxRidgeWidth = 16f;
    [Range(1f, 8f)] public float ridgePeakHeight = 3.2f;
    [Range(0.5f, 3f)] public float cliffSharpness = 1.4f;

    [Header("Structural validation")]
    [Range(0.1f, 1f)] public float minimumPeakRatio = 0.55f;
    [Range(0.1f, 1f)] public float minimumWidthRatio = 0.55f;
    [Range(0.01f, 2f)] public float minimumUsefulSlope = 0.08f;
    [Range(0.1f, 4f)] public float maximumSlope = 1.25f;
    [Range(0.1f, 1f)] public float minimumSurvivingMassRatio = 0.45f;
    [Range(0.5f, 1f)] public float minimumConnectedMassRatio = 0.90f;
    [Min(4)] public int minimumMountainSamples = 24;

    public void Validate()
    {
        minRidges = Mathf.Max(0, minRidges);
        maxRidges = Mathf.Max(minRidges, maxRidges);
        minRidgeLength = Mathf.Max(3f, minRidgeLength);
        maxRidgeLength = Mathf.Max(minRidgeLength, maxRidgeLength);
        minRidgeWidth = Mathf.Max(2f, minRidgeWidth);
        maxRidgeWidth = Mathf.Max(minRidgeWidth, maxRidgeWidth);
        minimumPeakRatio = Mathf.Clamp(minimumPeakRatio, 0.1f, 1f);
        minimumWidthRatio = Mathf.Clamp(minimumWidthRatio, 0.1f, 1f);
        minimumUsefulSlope = Mathf.Max(0.01f, minimumUsefulSlope);
        maximumSlope = Mathf.Max(minimumUsefulSlope, maximumSlope);
        minimumSurvivingMassRatio = Mathf.Clamp(minimumSurvivingMassRatio, 0.1f, 1f);
        minimumConnectedMassRatio = Mathf.Clamp(minimumConnectedMassRatio, 0.5f, 1f);
        minimumMountainSamples = Mathf.Max(4, minimumMountainSamples);
    }
}

[Serializable]
public sealed class RiverCorridorSettings
{
    public bool enabled = true;
    [Range(0, 4)] public int minRivers = 1;
    [Range(0, 4)] public int maxRivers = 2;
    [Range(0.5f, 4f)] public float channelRadius = 1.4f;
    [Range(2f, 16f)] public float clearanceRadius = 6.0f;
    [Range(0.5f, 4f)] public float valleyDepth = 1.2f;

    public void Validate()
    {
        minRivers = Mathf.Max(0, minRivers);
        maxRivers = Mathf.Max(minRivers, maxRivers);
        channelRadius = Mathf.Max(0.5f, channelRadius);
        clearanceRadius = Mathf.Max(channelRadius + 1f, clearanceRadius);
        valleyDepth = Mathf.Max(0.5f, valleyDepth);
    }
}

[Serializable]
public sealed class DomainWarpSettings
{
    public bool enabled = true;
    [Min(1f)] public float scale = 36f;
    [Range(0f, 40f)] public float amplitude = 22f;
    [Range(1, 4)] public int octaves = 3;
    [Range(0.1f, 1f)] public float persistence = 0.5f;
    [Min(1f)] public float lacunarity = 2.0f;

    public void Validate()
    {
        scale = Mathf.Max(1f, scale);
        amplitude = Mathf.Max(0f, amplitude);
        octaves = Mathf.Clamp(octaves, 1, 4);
    }
}

[Serializable]
public sealed class StandalonePlateauSettings
{
    private const int CurrentDataVersion = 5;

    [SerializeField, HideInInspector] private int dataVersion;

    [Header("Connected tabletop footprint")]
    public PlateauShapeMode shapeMode = PlateauShapeMode.Auto;
    [Range(0.30f, 0.68f)] public float tabletopRadius = 0.56f;
    [Range(1f, 1.75f)] public float elongation = 1.32f;
    [Range(0f, 0.45f)] public float curvature = 0.18f;
    [Range(0f, 0.22f)] public float silhouetteLobing = 0.12f;
    [Range(0f, 0.14f)] public float silhouetteNoise = 0.07f;
    [Range(0f, 0.16f)] public float tabletopRelief = 0.06f;
    [Range(2f, 4f)] public float edgeSquareness = 2.65f;
    [Range(0, 4)] public int boundaryNotchCount = 2;
    [Range(0f, 0.16f)] public float boundaryNotchDepth = 0.075f;

    [Header("Rock perimeter profile")]
    [Min(0.5f)] public float rockyRimWidth = 3.0f;
    [Min(0.5f)] public float upperEscarpmentWidth = 1.8f;
    [Min(1f)] public float lowerApronWidth = 5.5f;
    [Min(2f)] public float cliffDropDepth = 8.0f;
    [Min(0f)] public float lowerApronDrop = 2.0f;
    [Range(0f, 5f)] public float rimOutcropHeight = 1.35f;
    [Range(0f, 8f)] public float occasionalSpireHeight = 4.8f;
    [Range(0, 8)] public int perimeterSpireCount = 3;
    [Range(1f, 6f)] public float spireBaseWidth = 2.6f;
    [Range(0f, 2f)] public float rimErosionHeight = 0.45f;
    [Range(0f, 2.5f)] public float cliffFractureStrength = 1.1f;
    [Range(0f, 0.6f)] public float profileAsymmetry = 0.32f;

    [Header("Perimeter rock massing")]
    [Range(0, 12)] public int perimeterClusterCount = 5;
    [Range(0f, 8f)] public float perimeterClusterHeight = 3.8f;
    [Range(2f, 10f)] public float perimeterClusterWidth = 5.8f;
    [Range(1f, 6f)] public float perimeterClusterDepth = 3.4f;

    [Header("Volumetric rock geometry")]
    public bool generateVolumetricRockGeometry = true;
    [Range(48, 192)] public int escarpmentContourSegments = 112;
    [Range(3, 10)] public int escarpmentStrata = 6;
    [Range(2, 8)] public int rocksPerCluster = 5;

    [Header("Sediment openings")]
    [Range(0, 4)] public int sandOpeningCount = 0;
    [Min(2f)] public float sandOpeningTopWidth = 4.5f;
    [Range(1.25f, 3f)] public float sandOpeningWidthMultiplier = 2.0f;
    [Range(1f, 2.5f)] public float sandDescentLengthMultiplier = 1.55f;

    [Header("Chunk framing")]
    [Min(1f)] public float outerSeamWidth = 3.0f;

    public void Validate()
    {
        if (dataVersion < CurrentDataVersion)
        {
            // Existing scene entries were created before the plateau catalogue grew
            // authored rim formations. Unity deserializes newly-added numeric fields
            // as zero, bypassing their field initializers, which silently disabled the
            // requested outcrops and spires. Repair that first-generation data once.
            elongation = elongation < 1f ? 1.32f : elongation;
            curvature = curvature <= 0f ? 0.18f : curvature;
            tabletopRelief = tabletopRelief <= 0f ? 0.06f : tabletopRelief;
            rockyRimWidth = rockyRimWidth <= 0f ? 3f : Mathf.Min(rockyRimWidth, 3f);
            upperEscarpmentWidth = upperEscarpmentWidth <= 0f
                ? 1.8f
                : Mathf.Min(upperEscarpmentWidth, 1.8f);
            lowerApronWidth = lowerApronWidth <= 0f
                ? 5.5f
                : Mathf.Min(lowerApronWidth, 5.5f);
            rimOutcropHeight = rimOutcropHeight <= 0f ? 1.35f : rimOutcropHeight;
            occasionalSpireHeight = occasionalSpireHeight <= 0f ? 4.8f : occasionalSpireHeight;
            perimeterSpireCount = perimeterSpireCount <= 0 ? 3 : perimeterSpireCount;
            spireBaseWidth = Mathf.Max(3.8f, spireBaseWidth);
            edgeSquareness = edgeSquareness < 2f ? 2.65f : edgeSquareness;
            boundaryNotchCount = boundaryNotchCount <= 0 ? 2 : boundaryNotchCount;
            boundaryNotchDepth = boundaryNotchDepth <= 0f ? 0.075f : boundaryNotchDepth;
            perimeterClusterCount = 5;
            perimeterClusterHeight = perimeterClusterHeight <= 0f ? 3.8f : perimeterClusterHeight;
            perimeterClusterWidth = perimeterClusterWidth < 2f ? 5.8f : perimeterClusterWidth;
            perimeterClusterDepth = perimeterClusterDepth < 1f ? 3.4f : perimeterClusterDepth;
            generateVolumetricRockGeometry = true;
            escarpmentContourSegments = escarpmentContourSegments < 48 ? 112 : escarpmentContourSegments;
            escarpmentStrata = escarpmentStrata < 3 ? 6 : escarpmentStrata;
            rocksPerCluster = rocksPerCluster < 2 ? 5 : rocksPerCluster;

            // Broad radial ramps read as generation artefacts in the orthographic
            // review. Sediment breaches remain available as an authored variation,
            // but the catalogue default is the continuous rocky escarpment shown by
            // the target references.
            sandOpeningCount = 0;
            sandOpeningTopWidth = sandOpeningTopWidth <= 0f
                ? 4.5f
                : Mathf.Min(sandOpeningTopWidth, 4.5f);
            dataVersion = CurrentDataVersion;
        }

        if (!Enum.IsDefined(typeof(PlateauShapeMode), shapeMode)) shapeMode = PlateauShapeMode.Auto;
        tabletopRadius = Mathf.Clamp(tabletopRadius, 0.30f, 0.68f);
        elongation = Mathf.Clamp(elongation, 1f, 1.75f);
        curvature = Mathf.Clamp(curvature, 0f, 0.45f);
        silhouetteLobing = Mathf.Clamp(silhouetteLobing, 0f, 0.22f);
        silhouetteNoise = Mathf.Clamp(silhouetteNoise, 0f, 0.14f);
        tabletopRelief = Mathf.Clamp(tabletopRelief, 0f, 0.16f);
        edgeSquareness = Mathf.Clamp(edgeSquareness, 2f, 4f);
        boundaryNotchCount = Mathf.Clamp(boundaryNotchCount, 0, 4);
        boundaryNotchDepth = Mathf.Clamp(boundaryNotchDepth, 0f, 0.16f);
        rockyRimWidth = Mathf.Max(0.5f, rockyRimWidth);
        upperEscarpmentWidth = Mathf.Max(0.5f, upperEscarpmentWidth);
        lowerApronWidth = Mathf.Max(1f, lowerApronWidth);
        cliffDropDepth = Mathf.Max(2f, cliffDropDepth);
        lowerApronDrop = Mathf.Max(0f, lowerApronDrop);
        rimOutcropHeight = Mathf.Max(0f, rimOutcropHeight);
        occasionalSpireHeight = Mathf.Max(0f, occasionalSpireHeight);
        perimeterSpireCount = Mathf.Clamp(perimeterSpireCount, 0, 8);
        spireBaseWidth = Mathf.Clamp(spireBaseWidth, 1f, 6f);
        rimErosionHeight = Mathf.Max(0f, rimErosionHeight);
        cliffFractureStrength = Mathf.Max(0f, cliffFractureStrength);
        profileAsymmetry = Mathf.Clamp(profileAsymmetry, 0f, 0.6f);
        perimeterClusterCount = Mathf.Clamp(perimeterClusterCount, 0, 12);
        perimeterClusterHeight = Mathf.Clamp(perimeterClusterHeight, 0f, 8f);
        perimeterClusterWidth = Mathf.Clamp(perimeterClusterWidth, 2f, 10f);
        perimeterClusterDepth = Mathf.Clamp(perimeterClusterDepth, 1f, 6f);
        escarpmentContourSegments = Mathf.Clamp(escarpmentContourSegments, 48, 192);
        escarpmentStrata = Mathf.Clamp(escarpmentStrata, 3, 10);
        rocksPerCluster = Mathf.Clamp(rocksPerCluster, 2, 8);
        sandOpeningCount = Mathf.Clamp(sandOpeningCount, 0, 4);
        sandOpeningTopWidth = Mathf.Max(2f, sandOpeningTopWidth);
        sandOpeningWidthMultiplier = Mathf.Clamp(sandOpeningWidthMultiplier, 1.25f, 3f);
        sandDescentLengthMultiplier = Mathf.Clamp(sandDescentLengthMultiplier, 1f, 2.5f);
        outerSeamWidth = Mathf.Max(1f, outerSeamWidth);
    }

    public StandalonePlateauSettings Clone()
    {
        return (StandalonePlateauSettings)MemberwiseClone();
    }
}

[Serializable]
public sealed class TerrainGenerationSettings
{
    [Header("Determinism")]
    public int seed = 1337;

    [Header("Geological Deformation")]
    public DomainWarpSettings domainWarp = new DomainWarpSettings();

    [Header("Standalone underwater plateau")]
    public StandalonePlateauSettings standalonePlateau = new StandalonePlateauSettings();

    [Header("Composed procedural fields")]
    public List<TerrainNoiseLayerSettings> noiseLayers = new List<TerrainNoiseLayerSettings>
    {
        new TerrainNoiseLayerSettings
        {
            name = "Island mass",
            scale = 52f,
            octaves = 3,
            persistence = 0.52f,
            lacunarity = 2f,
            weight = 0.72f,
        },
        new TerrainNoiseLayerSettings
        {
            name = "Regional variation",
            scale = 19f,
            octaves = 2,
            persistence = 0.45f,
            lacunarity = 2.2f,
            weight = 0.28f,
            mode = TerrainNoiseMode.Ridged,
        },
    };

    [Header("Island shape")]
    [Min(0.0001f)] public float legacyIslandScale = 0.025f;
    public float legacyMountainThreshold = 0.8f;
    public float legacyMountainPeakThreshold = 1.1f;
    [Range(0f, 1f)] public float noiseContribution = 0.55f;
    [Range(0f, 1f)] public float islandMaskContribution = 0.55f;
    [Range(0f, 0.5f)] public float fieldBias = 0.1f;
    [Range(0f, 1f)] public float falloffStart = 0.36f;
    [Range(0.01f, 1.5f)] public float falloffEnd = 1.05f;
    [Range(0f, 0.4f)] public float coastWarp = 0.16f;

    [Header("Semantic bands")]
    [Range(-1f, 1f)] public float abyssUpper = 0f;
    [Range(-1f, 1f)] public float deepUpper = 0.2f;
    [Range(-1f, 1f)] public float underwaterPlateauUpper = 0.2f;
    [Range(-1f, 1f)] public float shallowUpper = 0.3f;
    [Range(-1f, 1f)] public float waterUpper = 0.4f;
    [Range(0f, 1f)] public float beachUpper = 0.415f;
    [Range(0f, 1f)] public float surfaceFlatlandUpper = 0.70f;
    [Range(0f, 1f)] public float hillUpper = 0.76f;
    [Range(0f, 1f)] public float cliffUpper = 0.82f;
    [Range(0f, 1f)] public float mountainUpper = 0.88f;

    [Header("Feature Reservation & Space Allocation")]
    public CoastalMountainSettings coastalMountains = new CoastalMountainSettings();
    public RiverCorridorSettings riverCorridors = new RiverCorridorSettings();

    [Header("Semantic heights")]
    public float abyssHeight = -5f;
    public float deepHeight = -3.5f;
    public float naturalPlateauHeight = -2.5f;
    public float underwaterPlateauHeight = -1.8f;
    public float shallowHeight = -1.5f;
    public float waterHeight = -0.6f;
    public float beachHeight = 0.25f;
    public float surfaceFlatlandHeight = 0.85f;
    public float hillHeight = 1.6f;
    public float cliffHeight = 2.4f;
    public float mountainHeight = 3.2f;
    public float mountainPeakHeight = 4.2f;

    [Header("Gameplay suitability")]
    [Min(0f)] public float maxBuildableHeightVariance = 0.2f;

    [Header("Visual sampling")]
    [Range(1, 16)] public int visualSamplesPerCell = 8;
    [Range(0.001f, 0.1f)] public float visualTransitionWidth = 0.035f;

    public void Validate()
    {
        domainWarp ??= new DomainWarpSettings();
        domainWarp.Validate();

        standalonePlateau ??= new StandalonePlateauSettings();
        standalonePlateau.Validate();

        coastalMountains ??= new CoastalMountainSettings();
        coastalMountains.Validate();
        riverCorridors ??= new RiverCorridorSettings();
        riverCorridors.Validate();

        underwaterPlateauHeight = Mathf.Min(-0.01f, underwaterPlateauHeight);
        falloffEnd = Mathf.Max(falloffStart + 0.01f, falloffEnd);

        legacyIslandScale = Mathf.Max(0.0001f, legacyIslandScale);
        visualSamplesPerCell = Mathf.Clamp(visualSamplesPerCell, 1, 16);
        legacyMountainPeakThreshold = Mathf.Max(legacyMountainThreshold, legacyMountainPeakThreshold);

        // Thresholds must be strictly increasing, not merely non-decreasing. Equal
        // adjacent thresholds make a band unreachable and fire two height anchors in
        // the same window. MapGrid.ApplyLegacyIslandTuning copies deepUpper and
        // underwaterPlateauUpper straight from the prefab, where both are 0.2, so this
        // runs after that copy and is the only place the separation is guaranteed.
        deepUpper = Mathf.Max(abyssUpper + MinimumBandSeparation, deepUpper);
        underwaterPlateauUpper = Mathf.Max(deepUpper + MinimumBandSeparation, underwaterPlateauUpper);
        shallowUpper = Mathf.Max(underwaterPlateauUpper + MinimumBandSeparation, shallowUpper);
        waterUpper = Mathf.Max(shallowUpper + MinimumBandSeparation, waterUpper);
        beachUpper = Mathf.Clamp(beachUpper, waterUpper + MinimumBandSeparation, waterUpper + 0.02f);
        surfaceFlatlandUpper = Mathf.Max(beachUpper + MinimumBandSeparation, surfaceFlatlandUpper);
        hillUpper = Mathf.Max(surfaceFlatlandUpper + MinimumBandSeparation, hillUpper);
        cliffUpper = Mathf.Max(hillUpper + MinimumBandSeparation, cliffUpper);
        mountainUpper = Mathf.Max(cliffUpper + MinimumBandSeparation, mountainUpper);

        // Heights must never descend as the source value rises, or the terrain dips
        // where it should climb. Clamping here keeps that true whatever the Inspector
        // holds, so the trench that beachHeight previously carved cannot come back.
        deepHeight = Mathf.Max(abyssHeight, deepHeight);
        naturalPlateauHeight = Mathf.Max(deepHeight, naturalPlateauHeight);
        shallowHeight = Mathf.Max(naturalPlateauHeight, shallowHeight);
        waterHeight = Mathf.Max(shallowHeight, waterHeight);
        beachHeight = Mathf.Max(waterHeight, beachHeight);
        surfaceFlatlandHeight = Mathf.Max(beachHeight, surfaceFlatlandHeight);
        hillHeight = Mathf.Max(surfaceFlatlandHeight, hillHeight);
        cliffHeight = Mathf.Max(hillHeight, cliffHeight);
        mountainHeight = Mathf.Max(cliffHeight, mountainHeight);
        mountainPeakHeight = Mathf.Max(mountainHeight, mountainPeakHeight);
    }

    // Smallest gap allowed between adjacent band thresholds.
    private const float MinimumBandSeparation = 0.01f;

    public void ApplyLegacyIslandTuning(
        float scale,
        float abyssThreshold,
        float deepThreshold,
        float plateauThreshold,
        float shallowThreshold,
        float waterThreshold,
        float mountainThreshold)
    {
        legacyIslandScale = Mathf.Clamp(scale > 0.03f ? scale * 0.18f : scale, 0.012f, 0.022f);
        abyssUpper = abyssThreshold;
        deepUpper = deepThreshold;
        underwaterPlateauUpper = plateauThreshold;
        shallowUpper = shallowThreshold;
        waterUpper = waterThreshold;
        beachUpper = waterUpper + 0.015f;
        surfaceFlatlandUpper = beachUpper + 0.28f;
        legacyMountainThreshold = mountainThreshold;

        // Authoritative physical elevation ladder:
        EnforceAuthoritativeHeights();
    }

    public void EnforceAuthoritativeHeights()
    {
        surfaceFlatlandHeight = 0.85f;
        beachHeight = 0.25f;
        waterHeight = -0.6f;
        shallowHeight = -1.5f;
        naturalPlateauHeight = -2.5f;
        deepHeight = -3.2f;
        abyssHeight = -4.5f;
        hillHeight = 1.6f;
        cliffHeight = 2.4f;
        mountainHeight = 3.2f;
        mountainPeakHeight = 4.2f;
        underwaterPlateauHeight = -2.2f;
        Validate();
    }
}
