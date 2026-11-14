// tldr: Turns fields into GROUND: the height of every sample (seabed, shelf, surf, beach, backshore, mainland,
// mountain) and the TerrainType that height is then classified as. This is where the coastal profile
// is actually shaped, so it is the file that decides what a beach looks like.

using UnityEngine;
using System.Collections.Generic;

public partial class IslandTerrainProvider
{

// Called with two different inputs on purpose (see SampleSynthesizedIsland and the Pass 1
// loop): baseField for reservationBaseHeight (what featureReservations.EvaluateAll sees -
// river/mountain reservation logic must keep seeing exactly what it always has, or their
// outcomes shift and carve holes elsewhere) and the low-frequency-only field for
// visualBaseHeight (what actually becomes rendered geometry). The waterUpper..beachUpper
// band is only 0.05 wide, so this S-curve divides by that narrow denominator - any high-
// frequency noise in `value` gets amplified into visible height ripples on the shoreline
// ramp. Feeding smoothed input only into visualBaseHeight kills that ripple without moving
// anything the reservation system depends on.
// Mainland micro-relief.
//
// The mainland is a CONSTANT foundation at surfaceFlatlandHeight - this is the only thing that
// varies it, and it exists purely so a large buildable region does not read as a machined table.
// It is deliberately not a terrain feature: hills, cliffs, valleys and rivers are placed
// deliberately by the reservation system and displace the foundation inside their own footprints.
//
// Two properties are load-bearing:
//   - it is SYMMETRIC about zero, so the mean mainland elevation is exactly
//     surfaceFlatlandHeight and no part of the island drifts off the datum;
//   - the wavelength is long (~21 world units) against the amplitude (a few centimetres), so the
//     gradient it contributes is about amplitude/10 per world unit - orders of magnitude below
//     maxBuildableHeightVariance, and therefore incapable of failing a cell's slope gate.
//
// This replaced a terracing pass. Terracing was a remap of HEIGHT, which only made sense while
// mainland height was a continuous ramp through the noise field: it quantised that ramp into
// benches. Against a constant foundation it does the opposite of its purpose - a per-position
// phase offset applied to a single height turns that constant back into a 0.13-unit wobble.
private const float MainlandReliefScale = 1f / 21f;

// Field width over which the relief fades in above coastalUpper. The coastal ramp arrives at
// exactly surfaceFlatlandHeight, so relief has to start at zero there or it stamps a step along
// the whole shoreline - an iso-field contour, which on an island closes into a ring.
private const float MainlandReliefFadeBand = 0.04f;

/// <summary>
/// The gentle micro-relief laid over the otherwise dead-flat mainland. Deliberately tiny: every real
/// elevation change is supposed to come from an authored ridge or valley, not from noise.
/// </summary>
private float EvaluateMainlandRelief(float localX, float localZ)
{
    float n = Mathf.PerlinNoise(
        localX * MainlandReliefScale + legacyOffsetX + 313.7f,
        localZ * MainlandReliefScale + legacyOffsetZ + 977.1f);
    return (n * 2f - 1f) * settings.mainlandRelief;
}

/// <summary>
/// The coastal band edges at one position, in world units of signed shoreline distance.
/// Geometry and classification both resolve through this, so a cell can never be painted or
/// labelled Beach somewhere the surface is not actually beach-shaped.
/// </summary>
public readonly struct CoastalBands
{
    public CoastalBands(float beachOuter, float backshoreOuter, float surfOuter, float shelfOuter,
                        float shelfDepth, float headlandMask, float crestFreeboard)
    {
        BeachOuter = beachOuter;
        BackshoreOuter = backshoreOuter;
        SurfOuter = surfOuter;
        ShelfOuter = shelfOuter;
        ShelfDepth = shelfDepth;
        HeadlandMask = headlandMask;
        CrestFreeboard = crestFreeboard;
    }

    /// <summary>Inland edge of the dry sand, positive world units from the waterline.</summary>
    public float BeachOuter { get; }
    /// <summary>Inland edge of the rise up to the flat mainland.</summary>
    public float BackshoreOuter { get; }
    /// <summary>Seaward edge of the surf zone (negative).</summary>
    public float SurfOuter { get; }
    /// <summary>Seaward edge of the shallow shelf (negative).</summary>
    public float ShelfOuter { get; }
    /// <summary>Depth of the shelf edge below the ocean surface at this position.</summary>
    public float ShelfDepth { get; }
    /// <summary>0 = open sandy shore, 1 = bare rock headland with no beach at all.</summary>
    public float HeadlandMask { get; }

    /// <summary>
    /// Height of the back of the beach above the ocean surface AT THIS POSITION. It falls with the
    /// beach's width rather than being a constant, so that as a beach narrows into a rocky headland
    /// the crest sinks to meet the waterline instead of the profile stepping down to it.
    /// </summary>
    public float CrestFreeboard { get; }

    public bool HasBeach => BeachOuter > 0.0001f;
}

/// <summary>
/// How strongly a coastal ridge reaches this position. Beaches are suppressed under it, which is
/// what produces the rocky interruptions in the references rather than one uniform sand ring.
///
/// Read from the RIDGE FIELD, which is defined in real 2D space and tapers isotropically. It is
/// deliberately NOT read from PerimeterSectorMap's mountain-coast weights: those are a function of
/// angle about the island centre only, so driving any continuous quantity from them carves
/// pie-slice wedges with radial seams. See the standing note further down this file.
/// </summary>
/// <summary>
/// How rocky this stretch of coast is, from ridge elevation at the shoreline. 1 means a rock headland
/// that suppresses the beach entirely; 0 means ordinary coast that keeps its sand.
/// </summary>
private float EvaluateHeadlandMask(float localX, float localZ)
{
    if (featureReservations == null || featureReservations.Ridges.Count == 0) return 0f;

    Vector2 point = new Vector2(localX, localZ);
    float strongest = 0f;
    for (int i = 0; i < featureReservations.Ridges.Count; i++)
    {
        float elevation = featureReservations.Ridges[i].EvaluateRawElevation(point);
        if (elevation > strongest) strongest = elevation;
    }

    float threshold = Mathf.Max(0.1f, settings.coastalProfile.headlandRidgeThreshold);
    float t = Mathf.Clamp01(strongest / threshold);
    return t * t * (3f - 2f * t);
}

/// <summary>
/// Beach width at this position: the authored width, modulated by restrained low-frequency
/// variation so the shore meanders, then suppressed by any rock reaching the coast.
/// </summary>
/// <summary>
/// Decides how WIDE each coastal band is at this specific point - beach, backshore, surf, shelf.
///
/// This is the method that determines whether beaches look authored or look like a uniform ring. Width
/// is modulated by low-frequency variation, by shoreline curvature (sand collects in bays and is
/// stripped from headlands) and by the headland mask. If beaches read as a constant-width band, the
/// cause is here or in the classifier's slope gate - not in the height profile below.
/// </summary>
public CoastalBands ResolveCoastalBands(float localX, float localZ, float headlandMask, float shoreCurvature)
{
    if (worldField != null) return worldField.Bands(chunkWorldOrigin + new Vector2(localX, localZ));
    CoastalProfileSettings coast = settings.coastalProfile;

    // Long wavelength on purpose: the variation must be slow compared with the beach's own width
    // or the shoreline ripples instead of meandering.
    float scale = 1f / Mathf.Max(8f, coast.widthVariationScale);
    float noise = Mathf.PerlinNoise(
        localX * scale + legacyOffsetX + 613.1f,
        localZ * scale + legacyOffsetZ + 271.9f);
    float widthScale = 1f + (noise * 2f - 1f) * coast.widthVariation;

    // COASTLINE SHAPE, not just noise.
    //
    // Sand collects where the coast is concave toward the sea and is stripped from where it juts
    // out. Driving width from curvature is what turns a constant-width ring into wide sandy bays
    // separated by thin or absent shore on the points between them - the difference between a
    // ring and a coastline. Noise alone cannot do this: it has no idea where the bays are.
    float bay = Mathf.Clamp((shoreCurvature - shorelineMeanCurvature) / Mathf.Max(0.01f, coast.fullBayCurvature), -1f, 1f);
    float bayScale = 1f + bay * coast.bayInfluence;

    float beachWidth = coast.beachWidth * widthScale * bayScale * (1f - headlandMask);
    if (beachWidth < 0f) beachWidth = 0f;

    // Taper to nothing, never snap to it.
    //
    // This used to be `if (beachWidth < minimumBeachWidth) beachWidth = 0`. That is a hard
    // discontinuity in a quantity the SURFACE HEIGHT is built from: the instant the width crossed
    // the threshold the backshore's lower anchor jumped from the beach crest to the waterline, a
    // step of (crest - waterline) appearing along whatever contour the threshold happened to fall
    // on. It rendered as a sharp ledge running along the shore.
    float taper = Mathf.Clamp01(beachWidth / Mathf.Max(0.01f, coast.minimumBeachWidth));
    beachWidth *= taper * taper * (3f - 2f * taper);

    // Rock meets the sea more abruptly than sand does, and its shelf continues further down -
    // the submerged rock shelves visible in the reference shots.
    float backshoreWidth = Mathf.Lerp(coast.backshoreWidth, coast.backshoreWidth * 0.45f, headlandMask);
    float surfWidth = coast.surfWidth * (1f - 0.5f * headlandMask);

    // ONE UNIFORM SHELF, under sand and under rock alike.
    //
    // Two attempts at making rock plunge from here both failed, for the same underlying reason:
    //   - releasing a headland's submerged samples to fieldSeabedHeight cut a crater, because that
    //     ramp is anchored to the abyss datum and sits several units below the shelf beside it;
    //   - narrowing and deepening the shelf under rock (width x0.3, depth +3) sheared the
    //     mountain's foot off, because CalculateStructuralMountainBoost's landMask already erases
    //     the ridge over roughly waterUpper / |grad field| ~= 2.2 world units seaward. Dropping the
    //     base 6 units across the same 2 units gives a near-vertical wall of (peak + 6).
    //
    // Both are the same error: putting a coastal shelf underneath a mountain at all. That geometry
    // belongs to the ridge, and it cannot be handed over from here while the boost is being masked
    // to nothing over two units. Left uniform until the mountain side can carry its own descent.
    float shelfDepth = coast.shelfDepth;
    float shelfWidth = coast.shelfWidth;

    // The crest sinks with the beach so the two ends of the profile always meet.
    float crestScale = Mathf.Clamp01(beachWidth / Mathf.Max(0.01f, coast.beachWidth));
    float crestFreeboard = Mathf.Lerp(coast.waterlineFreeboard, coast.beachCrestFreeboard, crestScale);

    return new CoastalBands(
        beachWidth,
        beachWidth + backshoreWidth,
        -surfWidth,
        -(surfWidth + shelfWidth),
        shelfDepth,
        headlandMask,
        crestFreeboard);
}

/// <summary>
/// Terrain height from signed shoreline distance: mainland, backshore, beach, surf, shelf, and a
/// blend back to the field-driven seabed.
///
/// The far seabed is NOT redefined here. It owns chunk-seam continuity - every chunk border and
/// the open ocean consume the same field-driven abyss datum - and the distance estimate is least
/// trustworthy furthest from the shore, so the two are blended rather than joined. The blend is a
/// lerp between two continuous functions, so it cannot introduce a step wherever they disagree.
/// </summary>
/// <summary>
/// The coastal ELEVATION profile: given a distance from the waterline and the band widths above, the
/// height of the ground there - shelf, surf, wet sand, dry beach, backshore rise, then mainland.
/// </summary>
private float EvaluateCoastalHeight(
    float shoreDistance,
    CoastalBands bands,
    float mainlandRelief,
    float fieldSeabedHeight)
{
    CoastalProfileSettings coast = settings.coastalProfile;
    float surface = settings.waterHeight;
    float mainland = settings.surfaceFlatlandHeight;
    float beachCrest = surface + bands.CrestFreeboard;
    float waterline = surface + coast.waterlineFreeboard;
    float surfFloor = surface - coast.surfDepth;
    float shelfFloor = surface - bands.ShelfDepth;

    float coastal;
    if (shoreDistance >= bands.BackshoreOuter)
    {
        // Flat mainland. Relief fades in over the first stretch so the join to the backshore,
        // which arrives at exactly the mainland datum, stays continuous.
        float fade = Mathf.Clamp01((shoreDistance - bands.BackshoreOuter) / 3f);
        fade = fade * fade * (3f - 2f * fade);
        coastal = mainland + mainlandRelief * fade;
    }
    else if (shoreDistance >= bands.BeachOuter)
    {
        float span = Mathf.Max(0.01f, bands.BackshoreOuter - bands.BeachOuter);
        float u = (shoreDistance - bands.BeachOuter) / span;
        float shaped = u * u * (3f - 2f * u);
        coastal = Mathf.Lerp(beachCrest, mainland, shaped);
    }
    else if (shoreDistance >= 0f)
    {
        // The beach itself. Deliberately close to linear: a smoothstep here flattens the sand
        // against the water and makes the waterline read as a lip rather than a slope.
        float u = bands.HasBeach ? shoreDistance / bands.BeachOuter : 1f;
        coastal = Mathf.Lerp(waterline, beachCrest, u);
    }
    else if (shoreDistance >= bands.SurfOuter)
    {
        float span = Mathf.Max(0.01f, -bands.SurfOuter);
        float u = -shoreDistance / span;
        float shaped = u * u * (3f - 2f * u);
        coastal = Mathf.Lerp(waterline, surfFloor, shaped);
    }
    else if (shoreDistance >= bands.ShelfOuter)
    {
        float span = Mathf.Max(0.01f, bands.SurfOuter - bands.ShelfOuter);
        float u = Mathf.Clamp01((bands.SurfOuter - shoreDistance) / span);
        float shaped = u * u * (3f - 2f * u);
        coastal = Mathf.Lerp(surfFloor, shelfFloor, shaped);
    }
    else
    {
        // CONTINENTAL SLOPE: shelf floor down to the abyss datum, over an authored distance.
        //
        // Previously the profile stopped at the shelf edge and handed straight to the field-driven
        // seabed, which compresses the entire ~18 unit drop into however much ground the noise
        // gradient happens to occupy - measured about 10 units, a gradient of 1.5. That wall is
        // what made the island render as a flat-topped cake and left the 0.35-unit beach as 1.3%
        // of the visible height, unable to read at any tuning.
        //
        // Exponent > 1 starts the descent gently just past the shelf and steepens with distance,
        // which is both the requested profile and what keeps the shelf edge from becoming a lip.
        float span = Mathf.Max(0.01f, coast.continentalSlopeWidth);
        float u = Mathf.Clamp01((bands.ShelfOuter - shoreDistance) / span);
        float shaped = Mathf.Pow(u, coast.continentalSlopeExponent);
        coastal = Mathf.Lerp(shelfFloor, settings.abyssHeight, shaped);
    }

    // The distance profile now owns everything down to the abyss, so the handoff to the
    // field-driven seabed happens beyond the continental slope rather than at the shelf edge.
    float slopeOuter = bands.ShelfOuter - coast.continentalSlopeWidth;
    float handoff = Mathf.Clamp01(
        (shoreDistance - (slopeOuter - coast.deepHandoffWidth))
        / Mathf.Max(0.01f, coast.deepHandoffWidth));
    handoff = handoff * handoff * (3f - 2f * handoff);

    // DO NOT hand a rocky coast back to fieldSeabedHeight to get a natural-looking plunge.
    //
    // Tried exactly that: below the waterline, lerp authority away from the coastal profile toward
    // the field ramp in proportion to headlandMask. It cut a crater out of every coastal mountain.
    // fieldSeabedHeight is anchored to the abyss datum (-20.94) across the whole field range, so
    // near the shore it evaluates about -4.1 where this profile sits at -3.2, and about -8.4 where
    // this profile sits at -5.5. Releasing a headland to it drops that ground several units below
    // the sandy coast immediately beside it, and the boundary between the two IS the hole.
    //
    // The field ramp being useless near shore is the reason this distance-based profile exists.
    // A steep rock coast comes from narrowing and deepening the shelf under it - see
    // ResolveCoastalBands - which stays inside one continuous profile and cannot tear.
    return Mathf.Lerp(fieldSeabedHeight, coastal, handoff);
}

/// <summary>
/// The field-driven seabed, from the abyss datum up to the ocean surface. Unchanged in behaviour
/// beyond being anchored to the real ocean surface instead of a hardcoded zero.
/// </summary>
/// <summary>
/// Deep seabed driven by the island field rather than by shore distance. Takes over past the shelf edge,
/// where distance-to-shore is no longer trustworthy.
/// </summary>
private float EvaluateFieldSeabedHeight(float value)
{
    float t = Mathf.Clamp01(
        (value - settings.abyssUpper) / Mathf.Max(0.01f, settings.waterUpper - settings.abyssUpper));
    float shelfCurve = Mathf.Pow(t, 1.25f);
    return Mathf.Lerp(settings.abyssHeight, settings.waterHeight, shelfCurve);
}

private float CalculateBaseContinuousHeight(float value)
{
    return CalculateBaseContinuousHeight(value, 0f);
}

private float CalculateBaseContinuousHeight(float value, float mainlandRelief)
{
    return CalculateBaseContinuousHeight(value, mainlandRelief, out _);
}

/// <param name="rawBaseHeight">
/// The foundation before micro-relief - i.e. the flat mainland datum itself on land, and the
/// plain shelf/shore curve below it. Diagnostics subtract this from the returned value to
/// isolate what the relief contributed.
/// </param>
private float CalculateBaseContinuousHeight(float value, float mainlandRelief, out float rawBaseHeight)
{
    // FIELD-ONLY fallback profile. The rendered coast no longer comes from here - it comes from
    // EvaluateCoastalHeight, driven by world-unit shoreline distance. This remains because
    // feature reservation runs BEFORE the sample cache exists (see BuildFeatureReservations, which
    // calls it to find the coastline and to height mountain ridges), so it must stay callable with
    // nothing but a field value. It is anchored to the same ocean surface so the two agree.
    float waterUpper = settings.waterUpper;
    float coastalUpper = 0.46f;
    float abyssUpper = settings.abyssUpper;
    float seaLevel = settings.waterHeight;

    if (value <= waterUpper)
    {
        float t = Mathf.Clamp01((value - abyssUpper) / Mathf.Max(0.01f, waterUpper - abyssUpper));
        float shelfCurve = Mathf.Pow(t, 1.25f);
        rawBaseHeight = Mathf.Lerp(settings.abyssHeight, seaLevel, shelfCurve);
        return rawBaseHeight;
    }
    else if (value <= coastalUpper)
    {
        // Coastal slope from Mean Sea Level (0.0m) up to Mainland baseline (+0.85m).
        //
        // This used a plain smoothstep, whose derivative is ZERO at u=0. The shelf branch above
        // arrives at the waterline with a large slope (-abyssHeight * 1.25 / (waterUpper -
        // abyssUpper), about 14 in height per field unit), so the two branches agreed on the
        // value (both 0) but not on the gradient. That left a C1 crease - a sharp fold in the
        // surface - running along the entire waterline. Values are continuous so it never showed
        // up in any contour or curvature sweep of the field, but a fold crossing the regular
        // triangulated mesh makes RecalculateNormals' per-vertex averaging flip from vertex to
        // vertex, which renders as sawtooth teeth exactly where a mountain flank meets the beach.
        //
        // Cubic Hermite with the incoming tangent matched to the shelf's exit slope and a flat
        // outgoing tangent: f(u) = m0*(u^3 - 2u^2 + u) + (3u^2 - 2u^3), which satisfies f(0)=0,
        // f(1)=1, f'(1)=0 for any m0, so the join to the inland branch is unaffected.
        float band = coastalUpper - waterUpper;
        float u = (value - waterUpper) / band;

        float freeboard = Mathf.Max(0.01f, settings.surfaceFlatlandHeight - seaLevel);
        float shelfExitSlope = (seaLevel - settings.abyssHeight) * 1.25f / Mathf.Max(0.01f, waterUpper - abyssUpper);
        float m0 = Mathf.Clamp(shelfExitSlope * band / freeboard, 0f, 2f);

        float shoreCurve = m0 * (u * u * u - 2f * u * u + u) + (3f * u * u - 2f * u * u * u);
        rawBaseHeight = Mathf.Lerp(seaLevel, settings.surfaceFlatlandHeight, shoreCurve);
        return rawBaseHeight;
    }
    else
    {
        // Inland mainland: ONE FLAT FOUNDATION at surfaceFlatlandHeight.
        //
        // This was `surfaceFlatlandHeight + (value - coastalUpper) * 1.15`, which made every
        // mainland vertex a linear function of the noise field. That is what made the island a
        // continuously varying dome rather than a construction surface: measured, "Land" cells
        // spanned -0.128..1.239 against an intended 0.85, 52% of Land mesh vertices sat more
        // than 0.15 off the datum, and 73% of Land cells failed the buildable slope gate - only
        // 1.7% of a chunk came out buildable at all.
        //
        // The field still decides WHERE land is (the branch above this one owns the shoreline).
        // It no longer decides how high that land is. Elevation changes on the mainland now come
        // only from deliberate features - ridges add mountainBoost, rivers and lakes subtract
        // riverCarve - each inside its own reserved footprint, tapering to zero at its boundary.
        // Outside every such footprint the mainland is flat by construction.
        //
        // The relief fades in from zero across MainlandReliefFadeBand so the join to the coastal
        // ramp, which arrives at exactly surfaceFlatlandHeight, stays continuous.
        float reliefBlend = Mathf.Clamp01((value - coastalUpper) / MainlandReliefFadeBand);
        reliefBlend = reliefBlend * reliefBlend * (3f - 2f * reliefBlend);
        rawBaseHeight = settings.surfaceFlatlandHeight;
        return rawBaseHeight + mainlandRelief * reliefBlend;
    }
}

// DO NOT drive terrain height off PerimeterSectorMap weights (GetMountainCoastWeight etc).
// Tried it to make mountain coasts plunge into the water instead of ending in a sand skirt:
// those weights are a function of ANGLE about the island centre only, so lerping height
// against one carves literal pie-slice wedges into the terrain - radial seams running from
// the shore toward the middle, extremely visible. Sector weights are fine for *classification*
// (they pick which coastal character a region has) but never for continuous geometry.
// If mountain coasts need to reach the water, that has to come from the ridge field itself,
// which is defined in real 2D space and tapers isotropically.

/// <summary>
/// Final height for a sample once every contribution - coastal profile, mainland, relief, ridges - has
/// been resolved.
/// </summary>
private float CalculateContinuousHeight(float value)
{
    float baseHeight = CalculateBaseContinuousHeight(value);
    if (value <= settings.surfaceFlatlandUpper)
    {
        return baseHeight;
    }

    // High elevation features (hills and mountains)
    float u = Mathf.Clamp01((value - settings.surfaceFlatlandUpper) / Mathf.Max(0.01f, 1f - settings.surfaceFlatlandUpper));
    float hillMountainHeight = Mathf.Lerp(settings.surfaceFlatlandHeight, settings.mountainPeakHeight, Mathf.Pow(u, 1.5f));
    return Mathf.Max(baseHeight, hillMountainHeight);
}

// Single-point probes share the same finished cache as gameplay and rendered geometry.
private TerrainSample SampleSynthesizedIsland(float localX, float localZ)
{
    return SampleFinishedIsland(localX, localZ);
}

/// <summary>
/// The elevation a reserved mountain ridge adds on top of the base ground at this point.
/// </summary>
private float CalculateStructuralMountainBoost(
    float smoothField,
    FeatureReservationMap.ReservationEvaluation reservation)
{
    if (reservation.MountainAllowance <= 0.001f
        || reservation.RawRidgeElevation <= 0.001f
        || smoothField <= settings.abyssUpper)
    {
        return 0f;
    }

    // Smooth continental shelf landMask over [abyssUpper..waterUpper] avoiding arbitrary offshore abyss spikes.
    // Preserves 100% full mountain boost across dry island landmass and into the shallow coastline.
    float u = Mathf.Clamp01((smoothField - settings.abyssUpper) / Mathf.Max(0.01f, settings.waterUpper - settings.abyssUpper));
    float landMask = u * u * (3f - 2f * u);
    return reservation.RawRidgeElevation * reservation.MountainAllowance * landMask;
}

/// <summary>
/// Assigns the gameplay MEANING of a sample - water, beach, land, hill, cliff, mountain, river - from its
/// height, shore distance, coastal bands and slope.
///
/// Classification is separate from height on purpose: the terrain decides its own shape, and this decides
/// what the grid is allowed to call it. A slope gate here can reject ground that the height profile built
/// correctly as a beach, which is why a beach can be present geometrically and absent semantically.
/// </summary>
private Cell.TerrainType ClassifySynthesizedIsland(
    float baseField, float height, float mountainBoost, bool isInRiverChannel,
    float shoreDistance, CoastalBands bands, float slope = 0f)
{
    float seaLevel = settings.waterHeight;

    // Every threshold below is a FREEBOARD relative to the ocean surface. They used to be absolute
    // world heights written when sea level was assumed to be zero; against the scene's -3 surface
    // that put the "just above the waterline" beach test 3 units into the air.
    // River channels keep exactly the ordering they had, so mouths and inland channels classify
    // as they did before this pass. Sol owns river geometry; the coast must not relabel it.
    if (isInRiverChannel)
    {
        if (baseField < settings.abyssUpper - settings.visualTransitionWidth) return Cell.TerrainType.Abyssal;
        if (baseField < settings.deepUpper) return Cell.TerrainType.Deep;
        if (baseField < settings.shallowUpper) return Cell.TerrainType.Shallow;
        return Cell.TerrainType.River;
    }

    if (height < seaLevel - 0.15f)
    {
        // The RESOLVED SHELF WINS over the field bands, and must be tested first.
        //
        // Tested after them (the obvious ordering, and what this did on the first pass) the field
        // bands preempt it: just offshore the island field has already fallen below abyssUpper, so
        // ground sitting 0.45 units under the surface - well inside the shelf the geometry just
        // built - came back labelled Abyssal. Quays, OffShore buildings and the depot foundation
        // all key off Shallow, so that silently withheld the entire usable harbour band.
        if (shoreDistance >= bands.ShelfOuter) return Cell.TerrainType.Shallow;

        // Beyond the shelf, the geometry is blending back to the field-driven seabed, so the
        // labels follow it there. One Deep band over the handoff keeps the shelf from meeting
        // abyss at a hard edge.
        if (shoreDistance >= bands.ShelfOuter - settings.coastalProfile.deepHandoffWidth)
        {
            return Cell.TerrainType.Deep;
        }

        if (baseField < settings.abyssUpper - settings.visualTransitionWidth) return Cell.TerrainType.Abyssal;
        if (baseField < settings.deepUpper) return Cell.TerrainType.Deep;
        if (baseField < settings.shallowUpper) return Cell.TerrainType.Shallow;
        return Cell.TerrainType.Water;
    }

    // High mountain material is height-driven. Its gentler shoulders are Hills, and
    // only genuinely steep faces are Cliffs; mountain boost by itself must not turn
    // the complete low apron into an unbuildable rock blanket.
    if (height >= settings.mountainPeakHeight - 0.4f) return Cell.TerrainType.MountainPeak;
    if (height >= settings.mountainHeight - 0.5f) return Cell.TerrainType.Mountain;

    if (height >= settings.hillHeight && mountainBoost > 0.3f && slope <= 0.55f)
    {
        return Cell.TerrainType.Hill;
    }

    bool steepMountainFace = height >= seaLevel + 0.15f
        && slope > 0.55f
        && mountainBoost > 0.12f;
    bool rockyCoastalFace = height >= seaLevel + 0.05f
        && slope > 0.32f
        && mountainBoost > 0.55f;
    if (steepMountainFace || rockyCoastalFace)
    {
        return Cell.TerrainType.Cliff;
    }

    // Beach is now a BAND, not a height cut.
    //
    // It was `height < 0.18`, which sampled a slice of the shore ramp. Because the ramp's ground
    // width is set by the noise gradient, that slice measured about 0.02 world units across and
    // produced 0 to 2 Beach cells per island - so the depot, which requires Beach, was effectively
    // unplaceable, and coastal fisheries never spawned. The band is the same one the geometry was
    // built from, so sand, shape and placement cannot disagree.
    //
    // A headland with its beach suppressed correctly falls through to Land or Cliff - bare rock
    // meeting the water, which is the interruption the references show.
    // Lower bound is slightly seaward of the nominal shoreline, not 0. The distance field's zero
    // crossing is where the FIELD meets waterUpper, while the surface crosses the water a fraction
    // further out because the beach meets the sea at waterlineFreeboard rather than exactly at it.
    // Clamping at 0 left that wet-sand sliver classified Land.
    if (bands.HasBeach && shoreDistance >= -1f && shoreDistance <= bands.BeachOuter)
    {
        return Cell.TerrainType.Beach;
    }

    return Cell.TerrainType.Land;
}

private TerrainSample ClassifyLegacyIsland(float value)
{
    float height = CalculateContinuousHeight(value);

    if (value < settings.abyssUpper - settings.visualTransitionWidth) return Sample(Cell.TerrainType.Abyssal, height, value);
    if (value < settings.deepUpper) return Sample(Cell.TerrainType.Deep, height, value);
    if (value < settings.shallowUpper) return Sample(Cell.TerrainType.Shallow, height, value);
    if (value < settings.waterUpper) return Sample(Cell.TerrainType.Water, height, value);
    if (value < settings.surfaceFlatlandUpper) return Sample(Cell.TerrainType.Land, height, value);
    if (value < settings.hillUpper) return Sample(Cell.TerrainType.Hill, height, value);
    if (value < settings.cliffUpper) return Sample(Cell.TerrainType.Cliff, height, value);
    if (value < settings.mountainUpper) return Sample(Cell.TerrainType.Mountain, height, value);
    return Sample(Cell.TerrainType.MountainPeak, height, value);
}

private float BlendVisualHeight(float lower, float upper, float threshold, float value)
{
    float width = settings.visualTransitionWidth;
    float t = Mathf.InverseLerp(threshold - width, threshold + width, value);
    return Mathf.Lerp(lower, upper, Mathf.SmoothStep(0f, 1f, t));
}
}
