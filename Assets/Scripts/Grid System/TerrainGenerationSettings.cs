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
    private const int CurrentDataVersion = 2;

    [SerializeField, HideInInspector] private int dataVersion;

    public bool enabled = true;
    [Range(0, 8)] public int minRidges = 3;
    [Range(0, 8)] public int maxRidges = 5;
    [Min(3f)] public float minRidgeLength = 7f;
    [Min(3f)] public float maxRidgeLength = 15f;
    [Min(2f)] public float minRidgeWidth = 3.5f;
    [Min(2f)] public float maxRidgeWidth = 6.5f;
    [Range(1f, 8f)] public float ridgePeakHeight = 4.0f;
    [Range(0.5f, 3f)] public float cliffSharpness = 1.55f;

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
        if (dataVersion < 1)
        {
            // The original defaults described continent-scale 22-48m ridges on a
            // 60m island. Candidate generation had to ignore those values and still
            // produced long one-sided ribbons. Migrate only that legacy envelope to
            // compact coastal massifs which can be repeated around the shore.
            if (minRidgeLength >= 21.9f && maxRidgeLength >= 47.9f)
            {
                minRidges = Mathf.Max(4, minRidges);
                maxRidges = Mathf.Max(6, maxRidges);
                minRidgeLength = 8f;
                maxRidgeLength = 18f;
                minRidgeWidth = 5f;
                maxRidgeWidth = 10f;
                ridgePeakHeight = Mathf.Max(4f, ridgePeakHeight);
                cliffSharpness = Mathf.Max(1.55f, cliffSharpness);
            }

            dataVersion = 1;
        }

        if (dataVersion < 2)
        {
            // Version 1 made the individual ridges shorter than the legacy
            // continent-scale ribbons, but their 1.8x generated apron still let a
            // 10m authored width occupy 36m across. Keep several distinct mountain
            // groups while reserving most of the island interior for buildable land.
            minRidges = 3;
            maxRidges = 5;
            minRidgeLength = 7f;
            maxRidgeLength = 15f;
            minRidgeWidth = 3.5f;
            maxRidgeWidth = 6.5f;
            dataVersion = 2;
        }

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
    [Range(0f, 1f), Tooltip("Chance that a mountain river begins at an alpine lake; otherwise it begins at a waterfall spring.")]
    public float lakeSourceChance = 0.45f;
    [Range(1.5f, 6f)] public float minLakeRadius = 2.2f;
    [Range(1.5f, 8f)] public float maxLakeRadius = 4.2f;
    [Range(0.1f, 1.5f)] public float lakeDepth = 0.65f;
    [Range(8f, 80f), Tooltip("Minimum source-to-sea distance. Keeps generated rivers from becoming short coastal ditches.")]
    public float minimumRiverLength = 18f;

    public void Validate()
    {
        minRivers = Mathf.Max(0, minRivers);
        maxRivers = Mathf.Max(minRivers, maxRivers);
        channelRadius = Mathf.Max(0.5f, channelRadius);
        clearanceRadius = Mathf.Max(channelRadius + 1f, clearanceRadius);
        valleyDepth = Mathf.Max(0.5f, valleyDepth);
        lakeSourceChance = Mathf.Clamp01(lakeSourceChance);
        minLakeRadius = Mathf.Max(1.5f, minLakeRadius);
        maxLakeRadius = Mathf.Max(minLakeRadius, maxLakeRadius);
        lakeDepth = Mathf.Max(0.1f, lakeDepth);
        minimumRiverLength = Mathf.Max(8f, minimumRiverLength);
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
    private const int CurrentDataVersion = 8;

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

    [Header("Submergence")]
    [Tooltip("Minimum vertical distance from the authoritative water surface to the plateau tabletop.")]
    [Min(2f)] public float tabletopDepthBelowWater = 6.0f;
    [Tooltip("Minimum water above the highest generated rim formation.")]
    [Min(0.5f)] public float minimumFormationClearance = 2.5f;

    public float RequiredTabletopDepthBelowWater
    {
        get
        {
            // The spire volume begins slightly above its embedded base and then adds
            // its full height. Include relief so the guarantee covers the actual mesh,
            // not only the scalar heightfield.
            float tallestSpire = occasionalSpireHeight * 1.12f + tabletopRelief;
            float tallestCluster = perimeterClusterHeight * 1.18f + tabletopRelief;
            return Mathf.Max(
                tabletopDepthBelowWater,
                Mathf.Max(tallestSpire, tallestCluster) + minimumFormationClearance);
        }
    }

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
            tabletopDepthBelowWater = tabletopDepthBelowWater < 2f ? 6f : tabletopDepthBelowWater;
            // Version 6 briefly shipped with 0.75m as the generated default. That
            // technically submerged a spire but left it plainly visible through the
            // surface; migrate that exact former default to the deep-sea clearance.
            minimumFormationClearance = minimumFormationClearance <= 0.7501f
                ? 2.5f
                : minimumFormationClearance;
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
        tabletopDepthBelowWater = Mathf.Max(2f, tabletopDepthBelowWater);
        minimumFormationClearance = Mathf.Max(0.5f, minimumFormationClearance);
    }

    public StandalonePlateauSettings Clone()
    {
        return (StandalonePlateauSettings)MemberwiseClone();
    }
}

[Serializable]
public sealed class TerrainGenerationSettings
{
    private const int CurrentMainlandShapeDataVersion = 1;

    [SerializeField, HideInInspector] private int mainlandShapeDataVersion = CurrentMainlandShapeDataVersion;

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

    [Header("Mainland shape contract")]
    [Tooltip("Normalized radius which must remain continuously above the shoreline for every island seed.")]
    [Range(0.20f, 0.45f)] public float guaranteedMainlandRadius = 0.38f;
    [Tooltip("Fraction of the wider mainland survey disc raised above the shoreline. Prevents crescent and ribbon islands.")]
    [Range(0.45f, 0.90f)] public float targetMainlandCoverage = 0.84f;
    [Tooltip("Normalized radius used for the target mainland coverage survey.")]
    [Range(0.40f, 0.65f)] public float mainlandSurveyRadius = 0.62f;

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

    // The visible ocean is owned by MapManager.PatternData, while waterHeight is a
    // semantic terrain-band anchor. Keep the physical surface separately so plateau
    // depth follows the selected world without moving the island classification bands.
    [SerializeField, HideInInspector] private float authoritativeWaterSurfaceHeight = -0.6f;

    public float AuthoritativeWaterSurfaceHeight => authoritativeWaterSurfaceHeight;

    [Header("Mainland foundation")]
    [Tooltip("Peak-to-datum amplitude of the mainland's micro-relief, in world units. The mainland " +
             "is otherwise dead flat at Surface Flatland Height; every real elevation change comes " +
             "from a deliberate ridge, valley or river. Set to 0 for a perfectly flat foundation. " +
             "Keep it well under Max Buildable Height Variance or it starts failing cells' slope gate.")]
    [Range(0f, 0.25f)] public float mainlandRelief = 0.05f;

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

        // Existing scenes carry the smaller first-generation mainland contract.
        // Migrate only that known envelope (or missing serialized fields) so an
        // intentionally customized island remains authored rather than overwritten.
        if (mainlandShapeDataVersion < CurrentMainlandShapeDataVersion)
        {
            bool missingContract = guaranteedMainlandRadius <= 0f
                || targetMainlandCoverage <= 0f
                || mainlandSurveyRadius <= 0f;
            bool firstGenerationContract = Mathf.Approximately(guaranteedMainlandRadius, 0.32f)
                && Mathf.Approximately(targetMainlandCoverage, 0.72f)
                && Mathf.Approximately(mainlandSurveyRadius, 0.56f);

            if (missingContract || firstGenerationContract)
            {
                guaranteedMainlandRadius = 0.38f;
                targetMainlandCoverage = 0.84f;
                mainlandSurveyRadius = 0.62f;
            }

            mainlandShapeDataVersion = CurrentMainlandShapeDataVersion;
        }

        if (guaranteedMainlandRadius <= 0f) guaranteedMainlandRadius = 0.38f;
        if (targetMainlandCoverage <= 0f) targetMainlandCoverage = 0.84f;
        if (mainlandSurveyRadius <= 0f) mainlandSurveyRadius = 0.62f;
        guaranteedMainlandRadius = Mathf.Clamp(guaranteedMainlandRadius, 0.20f, 0.45f);
        targetMainlandCoverage = Mathf.Clamp(targetMainlandCoverage, 0.45f, 0.90f);
        mainlandSurveyRadius = Mathf.Clamp(
            mainlandSurveyRadius,
            Mathf.Max(0.40f, guaranteedMainlandRadius + 0.05f),
            0.65f);

        // Deliberately NOT repaired from 0 the way the mainland-shape fields above are. Zero is a
        // legitimate authored value here - it means a perfectly flat foundation - so a scene or
        // prefab that predates this field comes up dead flat rather than silently acquiring relief
        // nobody asked for. Raise it in the Inspector to put micro-relief back.
        mainlandRelief = Mathf.Clamp(mainlandRelief, 0f, 0.25f);

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
        hillHeight = 1.6f;
        cliffHeight = 2.4f;
        mountainHeight = 3.2f;
        mountainPeakHeight = 4.2f;
        standalonePlateau ??= new StandalonePlateauSettings();
        standalonePlateau.Validate();

        // One vertical contract owns both landform types. The tabletop is placed far
        // enough below the authoritative water surface to submerge its tallest rock,
        // then the shared abyss datum is placed below the complete plateau profile.
        // Island borders, plateau surrounds, and open ocean all consume abyssHeight,
        // so no terrain chunk can carry a shallower private version of the abyss.
        underwaterPlateauHeight = authoritativeWaterSurfaceHeight
            - standalonePlateau.RequiredTabletopDepthBelowWater;
        abyssHeight = underwaterPlateauHeight
            - standalonePlateau.cliffDropDepth
            - standalonePlateau.lowerApronDrop;
        Validate();
    }

    public void SetAuthoritativeWaterSurfaceHeight(float surfaceHeight)
    {
        if (float.IsNaN(surfaceHeight) || float.IsInfinity(surfaceHeight))
        {
            return;
        }

        authoritativeWaterSurfaceHeight = surfaceHeight;
        EnforceAuthoritativeHeights();
    }
}
