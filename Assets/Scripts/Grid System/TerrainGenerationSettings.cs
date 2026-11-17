// tldr: Every authored knob for terrain generation, plus the derivation and migration rules that keep them
// self-consistent. WARNING: these objects are MUTATED during generation - Validate,
// EnforceAuthoritativeHeights and ApplyLegacyIslandTuning all write derived values back - so a chunk
// must own its instance. See TerrainWorldSettings for the shared, read-only authoring source.

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

/// <summary>
/// The shoreline contract. Every coastal band is a WIDTH IN WORLD UNITS OF GROUND measured
/// outward from the waterline, and every elevation is a FREEBOARD relative to the rendered
/// ocean surface. Nothing here is expressed in noise-field units.
///
/// That distinction is the whole point. The previous coast was a fixed 0.06-wide band of the
/// scalar field, and the field's spatial gradient around one island varies 5:1 (measured:
/// 0.062 field-units per world unit at the shallowest decile, 0.307 at the steepest), so the
/// same band covered 0.97 world units of ground in a bay and 0.20 on a headland. A beach
/// cannot have an authored width while it is defined in field space.
/// </summary>
[Serializable]
public sealed class CoastalProfileSettings
{
    private const int CurrentDataVersion = 4;

    [SerializeField, HideInInspector] private int dataVersion;

    [Header("Band widths (world units of ground, measured from the waterline)")]
    [Tooltip("Rise from the back of the beach up to the flat mainland. Widened automatically if " +
             "it is too narrow for the climb it has to make - see maxBackshoreGradient.")]
    [Range(0.5f, 12f)] public float backshoreWidth = 1.8f;
    [Tooltip("Dry sand/shingle, from the waterline inland.")]
    [Range(0.5f, 16f)] public float beachWidth = 2.2f;
    [Tooltip("Wet sand and breaking water just below the waterline.")]
    [Range(0.5f, 12f)] public float surfWidth = 2.5f;
    [Tooltip("Gently descending shallow shelf beyond the surf. This is also the band that " +
             "classifies as Shallow, and Shallow is what quays and OffShore buildings may occupy - " +
             "so its width is a gameplay reach, not only a look. At 9 it put ~45% of a 60x60 chunk " +
             "inside the harbour-buildable ring.")]
    [Range(1f, 30f)] public float shelfWidth = 5.0f;
    [Tooltip("Distance over which the shelf hands back to the field-driven deep seabed. Kept " +
             "as a blend rather than a join so the far seabed - which owns chunk-seam continuity - " +
             "is never overwritten by a distance estimate that is unreliable that far offshore.")]
    [Range(1f, 20f)] public float deepHandoffWidth = 6.0f;
    [Tooltip("Ground distance beyond the shelf edge over which the seabed descends from the shelf " +
             "floor to the abyss datum. THIS IS WHAT STOPS THE ISLAND LOOKING LIKE A CAKE: the drop " +
             "is ~18 units, and compressed into the few units the noise field happened to take, it " +
             "renders as a vertical wall that dwarfs the beach. Spread over a real slope the same " +
             "drop reads as a seabed.")]
    [Range(2f, 40f)] public float continentalSlopeWidth = 22f;
    [Tooltip("Shape of that descent. 1 = straight; above 1 starts gently and steepens with " +
             "distance offshore, which is the profile the reference material shows.")]
    [Range(1f, 3f)] public float continentalSlopeExponent = 1.65f;

    [Header("Elevations (freeboard relative to the ocean surface)")]
    [Tooltip("Dry sand at the back of the beach, above the water surface.")]
    [Range(0f, 3f)] public float beachCrestFreeboard = 0.40f;
    [Tooltip("Where sand meets sea. Small and positive so the waterline sits ON the beach.")]
    [Range(0f, 0.5f)] public float waterlineFreeboard = 0.05f;
    [Tooltip("Depth at the seaward edge of the surf zone.")]
    [Range(0.05f, 3f)] public float surfDepth = 0.45f;
    [Tooltip("Depth at the seaward edge of the shallow shelf.")]
    [Range(0.5f, 8f)] public float shelfDepth = 3.0f;

    [Tooltip("Steepest the backshore may be. The beach classifier rejects anything above 0.45 " +
             "slope, and a Sobel gradient bleeds a steep backshore back across the sand next to " +
             "it, so a backshore steeper than this disqualifies its own beach.")]
    [Range(0.2f, 1.5f)] public float maxBackshoreGradient = 0.55f;

    [Tooltip("Peak VERTEX displacement of the ocean surface, in world units. Set this ONLY if the " +
             "ocean shader actually moves geometry - a beach shorter than the swash would be fully " +
             "flooded at crest and fully dry at trough. Ocean Shader Graph does NOT displace: its " +
             "Position, Normal and Tangent vertex blocks have no incoming connections, and its " +
             "'Water Lerp' (0.45) and 'Wave' (0.1) properties are unconnected. The surface is flat, " +
             "so this is 0.")]
    [Range(0f, 2f)] public float waveAmplitude = 0f;
    [Tooltip("Steepest the dry beach may be. Real sand sits well under this; it also has to stay " +
             "below the 0.45 slope the Beach classifier rejects at.")]
    [Range(0.05f, 0.6f)] public float maxBeachGradient = 0.30f;

    [Header("Deliberate width variation")]
    [Tooltip("0 = every beach the same width. 1 = width swings fully between zero and double.")]
    [Range(0f, 1f)] public float widthVariation = 0.55f;
    [Tooltip("World units per cycle of the low-frequency width variation. Long compared with the " +
             "beach itself, so the shore meanders instead of rippling.")]
    [Range(8f, 80f)] public float widthVariationScale = 26f;

    [Tooltip("How strongly coastline shape drives beach width. Sand collects in bays and is " +
             "stripped from headlands; without this the beach is a constant-width ring modulated " +
             "only by arbitrary noise, which is exactly what a ring looks like.")]
    [Range(0f, 1f)] public float bayInfluence = 0.75f;
    [Tooltip("Shoreline curvature, in 1/world-units, treated as a full bay. An island of radius 16 " +
             "has a curvature near 0.06, so a value around 0.10 means only genuine inlets widen.")]
    [Range(0.01f, 0.5f)] public float fullBayCurvature = 0.10f;
    [Tooltip("World-unit radius of the stencil used to measure that curvature. Large enough to " +
             "read the shape of the coastline rather than of the triangulation.")]
    [Range(0.5f, 8f)] public float curvatureStencilRadius = 2.5f;

    [Tooltip("Grass is not allowed within this distance of the waterline. Where the beach tapers " +
             "to nothing the sand fade collapses and grass would otherwise run right down to the " +
             "water, which reads as green speckles through the sand. Rock still overrides it, so a " +
             "rocky headland stays rock.")]
    [Range(0f, 4f)] public float waterlineSandWidth = 0.9f;

    [Header("Rocky headlands")]
    [Tooltip("Ridge elevation at the coast that fully suppresses the beach, leaving bare rock. " +
             "Driven by the ridge field, which is defined in real 2D space - NOT by angular " +
             "sector weights, which carve radial pie-slice wedges.")]
    [Range(0.1f, 6f)] public float headlandRidgeThreshold = 1.80f;
    [Tooltip("How far below the waterline a rocky headland keeps its rock shelf before the " +
             "ordinary shelf profile resumes.")]
    [Range(0f, 6f)] public float headlandShelfExtension = 3.0f;
    [Tooltip("Shelf width multiplier under a full headland. Combined with headlandShelfExtension " +
             "this is what makes rock plunge and sand shelve: 0.3 turns a 5-wide/3-deep sandy shelf " +
             "into a 1.5-wide/6-deep rock face.")]
    [Range(0.05f, 1f)] public float headlandShelfNarrowing = 0.30f;
    [Tooltip("Lowest width a beach may narrow to before it is treated as absent.")]
    [Range(0f, 2f)] public float minimumBeachWidth = 0.10f;

    public float BeachOuterEdge => beachWidth;
    public float BackshoreOuterEdge => beachWidth + backshoreWidth;
    public float SurfOuterEdge => -surfWidth;
    public float ShelfOuterEdge => -(surfWidth + shelfWidth);

    public void Validate()
    {
        if (dataVersion < CurrentDataVersion)
        {
            // This block did not exist before the coastal pass, so Unity wrote its first-generation
            // field initializers straight into every prefab the moment it deserialized them. Later
            // edits to those initializers can no longer reach the serialized data - the same trap
            // CoastalMountainSettings documents above. Nothing here has been hand-authored yet, so
            // the first-generation envelope is simply replaced.
            //
            // What changed: a 9-unit shelf on top of a 3-unit surf put twelve world units of
            // Shallow around every island, which is 45% of a 60x60 chunk. Shallow is the band quays
            // and OffShore buildings may occupy, so that was a harbour-buildable ring larger than
            // the island itself.
            if (shelfWidth >= 8.9f && surfWidth >= 2.9f)
            {
                surfWidth = 2.5f;
                shelfWidth = 5.0f;
                shelfDepth = 3.0f;
                deepHandoffWidth = 6.0f;
            }

            dataVersion = 1;
        }

        if (dataVersion < 2)
        {
            // Version 1's bands were sized for an Anno-scale island and applied to a 32-unit one.
            //
            // Measured on Island 47: shore-distance radius 16 units, apron = beach 3.5 + backshore
            // 2.5 = 6 units. An annulus 6 wide on a disc of radius 16 is 61% of its AREA, and that
            // is what came out - 42% of dry land in the beach band, 19% in the backshore, only 39%
            // at full mainland height. The island rendered as a small plateau inside a wide pale
            // ramp barely clear of the water, i.e. it looked submerged.
            //
            // A 3-unit apron on the same radius is 34% of the area, leaving two thirds as mainland.
            beachWidth = 1.8f;
            backshoreWidth = 1.2f;

            // 55% of shoreline was coming out as bare rock because the suppression threshold sat
            // below what coastal ridges routinely reach, and minimumBeachWidth then snapped the
            // survivors to zero - giving a bimodal "full beach or none" shore instead of a taper.
            headlandRidgeThreshold = 1.80f;
            minimumBeachWidth = 0.10f;

            dataVersion = 2;
        }

        if (dataVersion < 3)
        {
            // v2's beach spanned 0.27 world units from waterline to crest while the ocean shader
            // sweeps +/-0.45 - the swash was 3.3x the entire height of the beach, so every frame it
            // was either completely submerged or completely dry. No width or material could make
            // that read as a shoreline. The crest is derived from the wave amplitude now, and the
            // width follows from the crest at a beach-like gradient.
            waterlineFreeboard = 0.05f;
            beachCrestFreeboard = 0.90f;
            beachWidth = 3.0f;
            dataVersion = 3;
        }

        if (dataVersion < 4)
        {
            // v3 sized the beach to survive a +/-0.45 swash. Checking the graph rather than
            // assuming: Ocean Shader Graph writes nothing to VertexDescription.Position/Normal/
            // Tangent, and both 'Water Lerp' (0.45) and 'Wave' (0.1) are unconnected properties.
            // The ocean surface does not move at all, so the beach never needed that height - and
            // paying for it in width pushed the coastal apron to 52% of the island.
            waveAmplitude = 0f;
            beachCrestFreeboard = 0.40f;
            beachWidth = 2.2f;
            dataVersion = 4;
        }

        maxBackshoreGradient = Mathf.Clamp(maxBackshoreGradient, 0.2f, 1.5f);
        backshoreWidth = Mathf.Clamp(backshoreWidth, 0.5f, 12f);
        beachWidth = Mathf.Clamp(beachWidth, 0.5f, 16f);
        surfWidth = Mathf.Clamp(surfWidth, 0.5f, 12f);
        shelfWidth = Mathf.Clamp(shelfWidth, 1f, 30f);
        deepHandoffWidth = Mathf.Clamp(deepHandoffWidth, 1f, 20f);
        continentalSlopeWidth = Mathf.Clamp(continentalSlopeWidth, 2f, 40f);
        continentalSlopeExponent = Mathf.Clamp(continentalSlopeExponent, 1f, 3f);
        waterlineFreeboard = Mathf.Clamp(waterlineFreeboard, 0f, 0.5f);
        beachCrestFreeboard = Mathf.Max(waterlineFreeboard + 0.02f, beachCrestFreeboard);
        surfDepth = Mathf.Clamp(surfDepth, 0.05f, 3f);
        shelfDepth = Mathf.Max(surfDepth + 0.1f, shelfDepth);
        widthVariation = Mathf.Clamp01(widthVariation);
        widthVariationScale = Mathf.Clamp(widthVariationScale, 8f, 80f);
        bayInfluence = Mathf.Clamp01(bayInfluence);
        waterlineSandWidth = Mathf.Clamp(waterlineSandWidth, 0f, 4f);
        fullBayCurvature = Mathf.Clamp(fullBayCurvature, 0.01f, 0.5f);
        curvatureStencilRadius = Mathf.Clamp(curvatureStencilRadius, 0.5f, 8f);
        headlandRidgeThreshold = Mathf.Max(0.1f, headlandRidgeThreshold);
        headlandShelfExtension = Mathf.Max(0f, headlandShelfExtension);
        headlandShelfNarrowing = Mathf.Clamp(headlandShelfNarrowing, 0.05f, 1f);
        minimumBeachWidth = Mathf.Clamp(minimumBeachWidth, 0f, 2f);
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
    private const int CurrentDataVersion = 9;

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
    [Min(0.5f)] public float upperEscarpmentWidth = 2.8f;
    [Min(1f)] public float lowerApronWidth = 7.0f;
    [Min(2f)] public float cliffDropDepth = 13.0f;
    [Min(0f)] public float lowerApronDrop = 4.0f;
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
    [Min(2f)] public float tabletopDepthBelowWater = 27.0f;
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
        if (dataVersion < 8)
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
                ? 2.8f
                : Mathf.Min(upperEscarpmentWidth, 2.8f);
            lowerApronWidth = lowerApronWidth <= 0f
                ? 7.0f
                : Mathf.Min(lowerApronWidth, 7.0f);
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
            tabletopDepthBelowWater = tabletopDepthBelowWater < 2f ? 27.0f : tabletopDepthBelowWater;
            // Version 6 briefly shipped with 0.75m as the generated default. That
            // technically submerged a spire but left it plainly visible through the
            // surface; migrate that exact former default to the deep-sea clearance.
            minimumFormationClearance = minimumFormationClearance <= 0.7501f
                ? 2.5f
                : minimumFormationClearance;
        }

        if (dataVersion < 9)
        {
            // Version 8 placed plateaus only ~6m below the water surface, giving the
            // camera too little vertical room to match surface island planning scale.
            // Upgrade the former shallow defaults to match deep submarine height (-30m).
            if (tabletopDepthBelowWater < 20f) tabletopDepthBelowWater = 27.0f;
            if (cliffDropDepth <= 8.5f) cliffDropDepth = 13.0f;
            if (lowerApronDrop <= 2.5f) lowerApronDrop = 4.0f;
            if (upperEscarpmentWidth <= 1.85f) upperEscarpmentWidth = 2.8f;
            if (lowerApronWidth <= 5.6f) lowerApronWidth = 7.0f;
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
public sealed class TerrainRegionSettings
{
    [Tooltip("Off by default: an island with no authored region renders exactly as it did before.")]
    public bool enabled = false;

    [Tooltip("Fraction of the island's LAND AREA the region covers. Calibrated against the actual " +
             "heightfield rather than against a disc, so it means the same thing whatever shape the " +
             "island came out - a crescent and a blob both give this fraction.")]
    [Range(0.02f, 0.5f)] public float coverage = 0.3f;

    [Tooltip("Direction the region lies in, in degrees about the island. 0 is +X, 90 is +Z.")]
    [Range(0f, 360f)] public float directionDegrees = 45f;

    [Tooltip("World units over which the region hands over to the rest of the island. Never set this " +
             "to zero: a hard boundary crossing the texel grid at a shallow angle stair-steps, which " +
             "is the serration failure documented in TextureBuilder.")]
    [Range(0.5f, 30f)] public float transitionWidth = 3.5f;

    [Tooltip("World units the boundary wanders either side of the straight line. Zero gives a ruled " +
             "edge, which reads as a cut rather than as terrain.")]
    [Range(0f, 30f)] public float boundaryWander = 6f;

    [Tooltip("Wavelength of that wander. Keep it long against the transition width or the boundary " +
             "ripples instead of meandering.")]
    [Range(4f, 120f)] public float boundaryWanderScale = 32f;

    public void Validate()
    {
        coverage = Mathf.Clamp(coverage, 0.02f, 0.5f);
        transitionWidth = Mathf.Max(0.5f, transitionWidth);
        boundaryWander = Mathf.Max(0f, boundaryWander);
        boundaryWanderScale = Mathf.Max(4f, boundaryWanderScale);
    }
}

[Serializable]
public sealed class TerrainGenerationSettings
{
    [Header("World landform layout")]
    [Range(0f, 0.5f)] public float landformPositionJitter = 0.475f;
    [Range(1f, 2f)] public float dominantLandformScale = 1.65f;
    public Vector2 secondaryLandformScale = new Vector2(0.9f, 1.25f);

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

    [Header("Coastal profile")]
    public CoastalProfileSettings coastalProfile = new CoastalProfileSettings();

    [Header("Regional material variation")]
    [Tooltip("Marks part of the island as a different region. The terrain splat shader tints its " +
             "layers toward the alternate colours wherever this reaches, so one island can read as " +
             "two places without a second material or a second mesh.")]
    public TerrainRegionSettings region = new TerrainRegionSettings();

    // Land elevation ladder, expressed as FREEBOARD ABOVE THE OCEAN SURFACE.
    //
    // These used to be absolute world heights (mainland 0.85, beach 0.25) while the underwater
    // ladder was already water-relative (plateau = surface - tabletopDepth). With the scene's
    // ocean plane at y = -3 that mixture put the mainland 3.85 units above the sea: the islands
    // were mesas, and "beach" sand sat 3.25 units above the waterline where no shoreline
    // treatment could ever be visible.
    //
    // Making the land ladder water-relative too gives the world ONE datum. The island's shape and
    // every relationship inside it are unchanged - it simply now meets the sea. abyssHeight and
    // underwaterPlateauHeight are untouched, since they were already derived from this surface.
    [Header("Seabed datum")]
    // Depth of the shared abyss below the ocean surface. 0 derives it from the standalone plateau's
    // cliff geometry, which is how it used to work and why the island's seabed depth was authored
    // by the deep-sea-plateau settings. Set it explicitly to own it here.
    //
    // It stays SHARED across island, plateau and ocean chunks on purpose: every chunk border and
    // the open ocean consume the same value, so a per-chunk-type abyss would tear the seams. A
    // plateau still has to fit its tabletop and escarpment above it, so this is clamped to whatever
    // the plateau needs.
    [Min(0f)] public float abyssDepthBelowWater = 0f;

    [Header("Land elevation ladder (freeboard above the ocean surface)")]
    [SerializeField, HideInInspector] private int landLadderVersion;
    public float mainlandFreeboard = 1.25f;
    public float hillFreeboard = 1.6f;
    public float cliffFreeboard = 2.4f;
    public float mountainFreeboard = 3.2f;
    public float mountainPeakFreeboard = 4.2f;

    [Header("Semantic heights (derived - do not author directly)")]
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

        coastalProfile ??= new CoastalProfileSettings();
        coastalProfile.Validate();

        region ??= new TerrainRegionSettings();
        region.Validate();

        // Newly-added serialized floats deserialize as zero in existing prefabs. Zero freeboard
        // would drop the whole island to sea level, so repair the ladder rather than clamp it.
        MigrateLandLadder();

        if (mainlandFreeboard <= 0f) mainlandFreeboard = 1.3f;
        if (hillFreeboard <= 0f) hillFreeboard = 1.6f;
        if (cliffFreeboard <= 0f) cliffFreeboard = 2.4f;
        if (mountainFreeboard <= 0f) mountainFreeboard = 3.2f;
        if (mountainPeakFreeboard <= 0f) mountainPeakFreeboard = 4.2f;
        hillFreeboard = Mathf.Max(mainlandFreeboard, hillFreeboard);
        cliffFreeboard = Mathf.Max(hillFreeboard, cliffFreeboard);
        mountainFreeboard = Mathf.Max(cliffFreeboard, mountainFreeboard);
        mountainPeakFreeboard = Mathf.Max(mountainFreeboard, mountainPeakFreeboard);

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

    /// <summary>
    /// One-shot repair of the land elevation ladder for data written by an earlier pass.
    /// </summary>
    private void MigrateLandLadder()
    {
        if (landLadderVersion >= 4) return;

        // The first coastal pass reinterpreted the old ABSOLUTE mainland height of 0.85 as a
        // freeboard. Right axis, wrong number: 0.85 above a water plane drawn with foam and a
        // depth tint does not read as land. Measured alongside it, the coastal apron (beach 3.5 +
        // backshore 2.5 = 6 units) covered 61% of a 16-unit-radius island's area, so the island
        // came out as a small plateau inside a wide pale ramp barely clear of the sea.
        // The underwater ladder is untouched - it was already relative to this surface.
        if (mainlandFreeboard > 0f && mainlandFreeboard <= 0.86f) mainlandFreeboard = 1.3f;
        if (mainlandFreeboard >= 1.69f && mainlandFreeboard <= 1.71f) mainlandFreeboard = 1.3f;
        if (mainlandFreeboard >= 1.29f && mainlandFreeboard <= 1.31f) mainlandFreeboard = 1.8f;
        if (mainlandFreeboard >= 1.79f && mainlandFreeboard <= 1.81f) mainlandFreeboard = 1.25f;
        landLadderVersion = 4;
    }

    public void EnforceAuthoritativeHeights()
    {
        coastalProfile ??= new CoastalProfileSettings();
        coastalProfile.Validate();

        region ??= new TerrainRegionSettings();
        region.Validate();

        // Must run BEFORE the ladder below reads mainlandFreeboard. Running it only in Validate()
        // - which is called at the END of this method - migrated the value but derived
        // surfaceFlatlandHeight from the stale one, so the change took an extra regeneration to
        // appear and looked like the migration had not fired at all.
        MigrateLandLadder();

        // ONE sea level for the whole world.
        //
        // waterHeight was previously a separate "semantic band anchor" pinned at -0.6 while the
        // rendered ocean plane sat at -3. Two disagreeing sea levels is what allowed the coast to
        // be authored against a waterline three units from where the water actually is. It is now
        // the ocean surface itself.
        //
        // RIVER INTEGRATION (Sol): this moves what EvaluateAll and RiverCorridor see as sea level
        // from -0.6 to the real surface. A river mouth targeting `waterHeight + 0.03` now lands on
        // the ocean instead of ~2.4 units above it. Flagged rather than adjusted here - the river
        // long-profile is out of this pass's scope.
        float surface = authoritativeWaterSurfaceHeight;
        waterHeight = surface;

        // THE BEACH MUST CONTAIN THE SWASH.
        //
        // Everything below follows from the ocean shader's wave amplitude. The crest has to sit
        // clear of the wave peak or the sand is under water at every crest; the beach then has to
        // be wide enough to reach that crest at a beach-like gradient; and the mainland has to sit
        // above the crest. Authored values are respected whenever they already satisfy this.
        float wave = Mathf.Max(0f, coastalProfile.waveAmplitude);
        float requiredCrest = wave * 2f;
        if (coastalProfile.beachCrestFreeboard < requiredCrest)
        {
            coastalProfile.beachCrestFreeboard = requiredCrest;
        }

        float beachRise = coastalProfile.beachCrestFreeboard - coastalProfile.waterlineFreeboard;
        float requiredBeachWidth = beachRise / Mathf.Max(0.05f, coastalProfile.maxBeachGradient);
        if (coastalProfile.beachWidth < requiredBeachWidth)
        {
            coastalProfile.beachWidth = requiredBeachWidth;
        }

        float requiredFreeboard = coastalProfile.beachCrestFreeboard + 0.5f;
        if (mainlandFreeboard < requiredFreeboard) mainlandFreeboard = requiredFreeboard;

        surfaceFlatlandHeight = surface + mainlandFreeboard;

        // The backshore has to climb from the beach crest to the mainland. Authored too narrow for
        // that climb, it becomes a bank steep enough that the Sobel slope bleeds across the sand
        // below it and the beach classifier rejects its own beach - measured 59% of beach-band
        // samples failing slope > 0.45 at a backshore gradient of 1.17. Widen rather than let the
        // two settings contradict each other.
        float backshoreRise = mainlandFreeboard - coastalProfile.beachCrestFreeboard;
        if (backshoreRise > 0f)
        {
            float required = backshoreRise / Mathf.Max(0.2f, coastalProfile.maxBackshoreGradient);
            if (coastalProfile.backshoreWidth < required) coastalProfile.backshoreWidth = required;
        }
        beachHeight = surface + coastalProfile.beachCrestFreeboard;
        hillHeight = surface + hillFreeboard;
        cliffHeight = surface + cliffFreeboard;
        mountainHeight = surface + mountainFreeboard;
        mountainPeakHeight = surface + mountainPeakFreeboard;

        // Submerged band anchors follow the same surface, so the ladder stays monotone instead of
        // placing "shallow water" terrain above the sea (shallowHeight was -1.5 against a -3
        // surface, i.e. 1.5 units into the air).
        shallowHeight = surface - coastalProfile.shelfDepth;
        naturalPlateauHeight = shallowHeight - 1.5f;
        deepHeight = naturalPlateauHeight - 1.5f;

        standalonePlateau ??= new StandalonePlateauSettings();
        standalonePlateau.Validate();

        // One vertical contract owns both landform types. The tabletop is placed far
        // enough below the authoritative water surface to submerge its tallest rock,
        // then the shared abyss datum is placed below the complete plateau profile.
        // Island borders, plateau surrounds, and open ocean all consume abyssHeight,
        // so no terrain chunk can carry a shallower private version of the abyss.
        underwaterPlateauHeight = authoritativeWaterSurfaceHeight
            - standalonePlateau.RequiredTabletopDepthBelowWater;

        // The abyss is the world's floor, shared by every chunk type. It may now be authored
        // directly instead of falling out of the plateau's cliff geometry; zero keeps the old
        // derivation. Either way it must sit below the deepest thing a plateau builds, or a
        // plateau's escarpment would run through the floor.
        float plateauFloor = underwaterPlateauHeight
            - standalonePlateau.cliffDropDepth
            - standalonePlateau.lowerApronDrop;
        abyssHeight = abyssDepthBelowWater > 0f
            ? Mathf.Min(authoritativeWaterSurfaceHeight - abyssDepthBelowWater, plateauFloor)
            : plateauFloor;
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
