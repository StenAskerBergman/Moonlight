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

public enum PlateauShelfRelationship
{
    AdjacentOrDetached,
    ShelfAdjacent,
    DetachedFromShelf
}

[Serializable]
public sealed class UnderwaterPlateauGenerationSettings
{
    [Min(0)] public int minimumCount = 1;
    [Min(0)] public int maximumCount = 2;

    [Header("Footprint")]
    // Sized to what the strict 100%-legal-core rule can actually satisfy. At the old
    // 7-12 a region needed 100-200 contiguous Deep/Abyssal cells, but the placement
    // annulus is only about 45% Deep/Abyssal and fragmented, so 15 of 16 islands got
    // no plateau at all. 4-7 clears on every island.
    [Min(1f)] public float minimumRadius = 4f;
    [Min(1f)] public float maximumRadius = 7f;
    [Range(0.25f, 1f)] public float minimumAspectRatio = 0.6f;
    [Range(0.25f, 1f)] public float maximumAspectRatio = 0.9f;
    [Min(1f)] public float minimumInteriorRadius = 2.5f;
    [Range(1, 4)] public int minimumPositiveLobes = 2;
    [Range(1, 4)] public int maximumPositiveLobes = 3;
    [Range(0f, 0.8f)] public float secondaryLobeOffset = 0.4f;
    [Range(0.35f, 1f)] public float minimumSecondaryLobeScale = 0.55f;
    [Range(0.35f, 1f)] public float maximumSecondaryLobeScale = 0.8f;
    [Range(0, 2)] public int maximumCutLobes = 1;
    [Range(0f, 1f)] public float cutLobeChance = 0.6f;
    [Range(0.15f, 0.8f)] public float minimumCutLobeScale = 0.25f;
    [Range(0.15f, 0.8f)] public float maximumCutLobeScale = 0.5f;
    [Range(0f, 1.5f)] public float cutStrength = 1f;

    [Header("Placement")]
    // Pushed outward off the coastal Shallow/Water ring into open Deep/Abyssal. This
    // matters as much as the smaller radius: at 0.58-0.76 a region straddles the shelf
    // and picks up illegal cells no matter how small it is.
    [Range(0f, 1f)] public float minimumPlacementDistance = 0.70f;
    [Range(0f, 1f)] public float maximumPlacementDistance = 0.92f;
    [Min(0f)] public float minimumRegionSeparation = 3f;
    // SUPERSEDED. Acceptance now requires 100% of the core to sit on Deep/Abyssal, so
    // there is no surface-overlap budget left to spend. Kept only so existing serialized
    // data does not break; nothing reads it.
    [Range(0f, 0.5f)] public float maximumSurfaceOverlap = 0.08f;
    [Tooltip("Smallest core, in cells, worth accepting. Without this a region whose core " +
             "is legal but tiny still passes and renders as a sliver.")]
    [Min(1)] public int minimumCoreCells = 24;
    public PlateauShelfRelationship shelfRelationship = PlateauShelfRelationship.AdjacentOrDetached;
    [Range(1, 128)] public int candidateAttemptsPerRegion = 48;
    [Tooltip("Log accepted/rejected counts and rejection reasons per island.")]
    public bool logPlateauGeneration = false;

    [Header("Transition and outline")]
    [Range(0.05f, 0.45f)] public float transitionWidth = 0.22f;
    [Range(0f, 0.35f)] public float edgeIrregularity = 0.12f;
    [Min(1f)] public float edgeDistortionScale = 14f;

    public void Validate()
    {
        minimumCount = Mathf.Max(0, minimumCount);
        maximumCount = Mathf.Max(minimumCount, maximumCount);
        maximumRadius = Mathf.Max(minimumRadius, maximumRadius);
        maximumAspectRatio = Mathf.Max(minimumAspectRatio, maximumAspectRatio);
        maximumPositiveLobes = Mathf.Max(minimumPositiveLobes, maximumPositiveLobes);
        maximumSecondaryLobeScale = Mathf.Max(minimumSecondaryLobeScale, maximumSecondaryLobeScale);
        maximumCutLobeScale = Mathf.Max(minimumCutLobeScale, maximumCutLobeScale);
        maximumPlacementDistance = Mathf.Max(minimumPlacementDistance, maximumPlacementDistance);
        minimumInteriorRadius = Mathf.Max(1f, minimumInteriorRadius);
        candidateAttemptsPerRegion = Mathf.Max(1, candidateAttemptsPerRegion);
    }
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
public sealed class TerrainGenerationSettings
{
    [Header("Determinism")]
    public int seed = 1337;

    [Header("Geological Deformation")]
    public DomainWarpSettings domainWarp = new DomainWarpSettings();

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

    [Header("Deliberate underwater plateau regions")]
    public UnderwaterPlateauGenerationSettings underwaterPlateaus = new UnderwaterPlateauGenerationSettings();

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
    [Range(1, 32)] public int visualSamplesPerCell = 16;
    [Range(0.001f, 0.1f)] public float visualTransitionWidth = 0.035f;

    public void Validate()
    {
        domainWarp ??= new DomainWarpSettings();
        domainWarp.Validate();

        coastalMountains ??= new CoastalMountainSettings();
        coastalMountains.Validate();
        riverCorridors ??= new RiverCorridorSettings();
        riverCorridors.Validate();

        underwaterPlateaus ??= new UnderwaterPlateauGenerationSettings();
        underwaterPlateaus.Validate();
        underwaterPlateauHeight = Mathf.Min(-0.01f, underwaterPlateauHeight);
        falloffEnd = Mathf.Max(falloffStart + 0.01f, falloffEnd);

        legacyIslandScale = Mathf.Max(0.0001f, legacyIslandScale);
        visualSamplesPerCell = Mathf.Max(1, visualSamplesPerCell);
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
