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
public sealed class TerrainGenerationSettings
{
    [Header("Determinism")]
    public int seed = 1337;

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
    // VESTIGIAL for island classification: the semantic ladder now uses cliffUpper and
    // mountainUpper instead. legacyMountainThreshold is still written every generation
    // by MapGrid.ApplyLegacyIslandTuning from the prefab's mountainThreshold, so it is
    // kept to avoid breaking that call, but nothing reads it any more.
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
    // Above-water bands. These are not touched by ApplyLegacyIslandTuning, so they are
    // the semantic ladder's own tuning. Retuned downward because the legacy island
    // field rarely exceeds ~0.86: at the previous values Mountain and MountainPeak sat
    // beyond the field's practical range and never occurred.
    [Range(0f, 1f)] public float beachUpper = 0.46f;
    [Range(0f, 1f)] public float surfaceFlatlandUpper = 0.66f;
    [Range(0f, 1f)] public float hillUpper = 0.74f;
    [Range(0f, 1f)] public float cliffUpper = 0.80f;
    [Range(0f, 1f)] public float mountainUpper = 0.86f;

    [Header("Deliberate underwater plateau regions")]
    public UnderwaterPlateauGenerationSettings underwaterPlateaus = new UnderwaterPlateauGenerationSettings();

    [Header("Semantic heights")]
    public float abyssHeight = -5f;
    public float deepHeight = -3f;
    public float naturalPlateauHeight = -3f;
    public float underwaterPlateauHeight = -2.5f;
    public float shallowHeight = -2f;
    public float waterHeight = -1f;
    public float beachHeight = -0.5f;
    public float surfaceFlatlandHeight = 0f;
    public float hillHeight = 0.65f;
    public float cliffHeight = 1.35f;
    // Must stay above cliffHeight. These were both 1.0, below cliffHeight's 1.35, which
    // made the height curve descend across the mountain bands and left cliff and peak
    // geometry unreachable.
    public float mountainHeight = 2f;
    public float mountainPeakHeight = 2.6f;

    [Header("Gameplay suitability")]
    [Min(0f)] public float maxBuildableHeightVariance = 0.2f;

    [Header("Visual sampling")]
    [Range(1, 32)] public int visualSamplesPerCell = 16;
    [Range(0.001f, 0.1f)] public float visualTransitionWidth = 0.035f;

    public void Validate()
    {
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
        beachUpper = Mathf.Max(waterUpper + MinimumBandSeparation, beachUpper);
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
        legacyIslandScale = scale;
        abyssUpper = abyssThreshold;
        deepUpper = deepThreshold;
        underwaterPlateauUpper = plateauThreshold;
        shallowUpper = shallowThreshold;
        waterUpper = waterThreshold;
        legacyMountainThreshold = mountainThreshold;
    }
}
