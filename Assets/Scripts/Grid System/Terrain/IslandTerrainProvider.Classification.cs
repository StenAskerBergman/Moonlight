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
// Buildable terrace shaping. Deliberately coarse: the goal is a few broad platforms with sparse,
// large-scale grade breaks, not fine stepping. TerraceStep is the vertical rise between benches;
// the flat window is [0, TerraceFlatLower] and [TerraceFlatUpper, 1] of each band.
private const float TerraceStep = 0.26f;
// Most of each level is bench, with the rest as the slope band connecting it to the next.
//
// TerraceFlatLower also bounds |f - shaped|, which is what the blend weight's derivative
// multiplies, so lowering it additionally widens the monotonicity margin (at 0.45 the remap went
// non-monotonic, slope -0.081, partway through the blend).
// Flat fraction = TerraceFlatLower + (1 - TerraceFlatUpper) = 0.30 + 0.30 = 60%, leaving 40% as
// the grade break.
//
// The break was previously 24%, which concentrated a whole 0.26 step into so little of the band
// that the transition measured 0.21 world units wide on average - a hairline on a 60-unit
// island, dropping 0.197 in a single sample. That reads as a concentric retaining wall rather
// than a landform. Widening the ramp lowers peak gradient through the break from ~6.25 to ~3.75
// in curve units while still leaving most of each level flat.
private const float TerraceFlatLower = 0.30f;
private const float TerraceFlatUpper = 0.70f;
// Terracing engages well above the beach so the shoreline and the coastal slope below it stay a
// single clean continuous band. Starting at 0.22 stepped the coastal slope too, which read as
// tight repeated rings around the shore rather than as a few large-scale levels.
private const float TerraceStartHeight = 0.36f;
// Wide on purpose: the blend weight's derivative multiplies the gap between the terraced and
// untouched fraction, so a narrow band drives the remap non-monotonic.
// Must be comfortably narrower than the height range actually being terraced (buildable land
// spans roughly 0.40..0.95), or the terracing never reaches full strength before the land runs
// out and the benches stay vestigial. Balanced against monotonicity: this band's derivative
// multiplies TerraceFlatLower * TerraceStep, and too narrow a band drives the remap backwards.
private const float TerraceBlendBand = 0.45f;

// Spatial variation of the terrace phase.
//
// A terrace bench edge is an ISO-HEIGHT contour, and on an island an iso-height contour is a
// closed loop - so every bench edge wrapped the whole coastline as one continuous lip. Measured:
// 58% of all strong convex ridges on buildable land fell in h[0.70..0.85], peaking at 30% in
// h[0.75..0.80], which is exactly the bench at 0.78. That single elevation reads as a retaining
// wall ringing the island.
//
// Offsetting the terrace phase by low-frequency noise makes the bench elevation differ from place
// to place, so the edge meanders and breaks into separate benches instead of closing into a ring.
// The wavelength is long compared with a grade break (~18 world units against ~0.24), so the
// offset's own spatial gradient is negligible next to the terrain's and cannot steepen anything.
private const float TerracePhaseScale = 1f / 18f;

private float EvaluateTerracePhase(float localX, float localZ)
{
    float n = Mathf.PerlinNoise(
        localX * TerracePhaseScale + legacyOffsetX + 313.7f,
        localZ * TerracePhaseScale + legacyOffsetZ + 977.1f);
    return n * TerraceStep;
}

private float CalculateBaseContinuousHeight(float value)
{
    return CalculateBaseContinuousHeight(value, 0f);
}

private float CalculateBaseContinuousHeight(float value, float terracePhase)
{
    return CalculateBaseContinuousHeight(value, terracePhase, out _);
}

private float CalculateBaseContinuousHeight(float value, float terracePhase, out float rawBaseHeight)
{
    float waterUpper = settings.waterUpper; // 0.40f (Coastline MSL = 0.0m)
    float coastalUpper = 0.46f;            // Smooth backshore transition up to mainland baseline (+0.85m)
    float abyssUpper = settings.abyssUpper; // 0.05f (Deep ocean floor)

    if (value <= waterUpper)
    {
        // Submerged ocean floor and continental shelf (0.0m at coastline down to abyssHeight)
        // Smooth continuous ramp without discrete terrace steps
        float t = Mathf.Clamp01((value - abyssUpper) / Mathf.Max(0.01f, waterUpper - abyssUpper));
        // Smooth concave shelf profile (t=0 -> abyssHeight, t=1 -> 0.0m Mean Sea Level)
        float shelfCurve = Mathf.Pow(t, 1.25f);
        rawBaseHeight = Mathf.Lerp(settings.abyssHeight, 0.0f, shelfCurve);
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

        float shelfExitSlope = -settings.abyssHeight * 1.25f / Mathf.Max(0.01f, waterUpper - abyssUpper);
        float m0 = Mathf.Clamp(shelfExitSlope * band / Mathf.Max(0.01f, settings.surfaceFlatlandHeight), 0f, 2f);

        float shoreCurve = m0 * (u * u * u - 2f * u * u + u) + (3f * u * u - 2f * u * u * u);
        rawBaseHeight = Mathf.Lerp(0.0f, settings.surfaceFlatlandHeight, shoreCurve);
        return ApplyBuildableTerraces(rawBaseHeight, terracePhase);
    }
    else
    {
        // Inland mainland: +0.85m baseline with organic rolling topography (rises naturally towards the interior)
        float excess = value - coastalUpper;
        rawBaseHeight = settings.surfaceFlatlandHeight + excess * 1.15f;
        return ApplyBuildableTerraces(rawBaseHeight, terracePhase);
    }
}

// Buildable elevation platforms.
//
// Measured on the generated island: of all non-mountain land, ~2.9% sat in EVERY 0.05 height
// band from 0.00 to 0.70 - a dead-flat histogram, which is the signature of one unbroken linear
// ramp. That is roughly 40% of the buildable surface spread across a single featureless
// gradient (the backshore branch maps field 0.40..0.46 onto height 0.00..0.85), which is what
// makes the island read as one continuously warped surface rather than as terrain with levels.
//
// This remaps that ramp into a few broad benches joined by short slope bands. It is a remap of
// the height CURVE only:
//   - the coastline is untouched, because the remap is identity at and below the beach band and
//     T(h) -> h as h -> 0, so the h=0 contour cannot move;
//   - mountain boost is added after this and is masked off the low-frequency field, so mountain
//     placement, shape and coastal contact are unaffected and stay organic;
//   - reservations and the rendered surface both read this same function, so river carves stay
//     registered to the ground they are cut into.
//
// Shape of one level: flat for the first TerraceFlatLower of the band, a smoothstep ramp, then
// flat again. Smoothstep has zero derivative at both ends, so consecutive levels join with
// matching (zero) gradient and the whole function is C1 - no crease at a bench edge, which is
// the failure mode that has produced every visible artifact in this pipeline so far.
private static float ApplyBuildableTerraces(float height, float terracePhase)
{
    if (height <= 0f) return height;

    // terracePhase shifts WHERE the bench boundaries fall, per position. Without it every bench
    // edge is a pure iso-height contour, which on an island closes into a ring right around the
    // coastline (and continues across mountains, since boost is added on top of this base). The
    // phase is subtracted before quantising and added back after, so the level structure is
    // preserved exactly - only its elevation is displaced.
    float shifted = height - terracePhase;

    float level = shifted / TerraceStep;
    float index = Mathf.Floor(level);
    float f = level - index;

    float u = Mathf.Clamp01((f - TerraceFlatLower) / (TerraceFlatUpper - TerraceFlatLower));
    float shaped = u * u * (3f - 2f * u);

    // Fade the terracing in above the beach so the shoreline keeps its natural continuous slope
    // and stays a separate low coastal band.
    float w = Mathf.Clamp01((height - TerraceStartHeight) / TerraceBlendBand);
    w = w * w * (3f - 2f * w);

    // Blend INSIDE the level coordinate rather than between the two output heights. Lerping the
    // outputs adds a w' * (T(h) - h) term to the derivative, and since T(h) sits up to
    // TerraceFlatLower * TerraceStep BELOW h on a bench, that term goes negative: measured a
    // slope of -0.272, i.e. the remap ran backwards and would have inverted terrain. Blending the
    // fraction keeps the result exactly (index + something in [0,1]) * step, which is identity at
    // w = 0 by construction and shrinks the offending term by a factor of TerraceStep.
    float blendedFraction = Mathf.Lerp(f, shaped, w);
    return (index + blendedFraction) * TerraceStep + terracePhase;
}

// DO NOT drive terrain height off PerimeterSectorMap weights (GetMountainCoastWeight etc).
// Tried it to make mountain coasts plunge into the water instead of ending in a sand skirt:
// those weights are a function of ANGLE about the island centre only, so lerping height
// against one carves literal pie-slice wedges into the terrain - radial seams running from
// the shore toward the middle, extremely visible. Sector weights are fine for *classification*
// (they pick which coastal character a region has) but never for continuous geometry.
// If mountain coasts need to reach the water, that has to come from the ridge field itself,
// which is defined in real 2D space and tapers isotropically.

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

private TerrainSample SampleSynthesizedIsland(float localX, float localZ)
{
    float baseField = CalculateLegacyIslandField(localX, localZ);
    float smoothField = CalculateLegacyIslandField(localX, localZ, true);
    float mountainCoastWeight = (featureReservations != null && featureReservations.Sectors != null)
        ? featureReservations.Sectors.GetMountainCoastWeight(localX, localZ)
        : 0f;
    float terracePhase = EvaluateTerracePhase(localX, localZ);
    float reservationBaseHeight = CalculateBaseContinuousHeight(baseField, terracePhase);
    float visualBaseHeight = CalculateBaseContinuousHeight(smoothField, terracePhase);

    float mountainBoost = 0f;
    float riverCarve = 0f;
    bool isInRiverChannel = false;

    if (featureReservations != null)
    {
        var res = featureReservations.EvaluateAll(localX, localZ, reservationBaseHeight, settings.waterHeight);
        mountainBoost = CalculateStructuralMountainBoost(smoothField, res);
        riverCarve = res.RiverCarveDepth * EvaluateRiverCarveGate(mountainBoost);
        isInRiverChannel = res.IsInRiverChannel;
    }

    float height = visualBaseHeight + mountainBoost - riverCarve;

    // Semantic Classification
    Cell.TerrainType terrainType = ClassifySynthesizedIsland(
        baseField, height, mountainBoost, isInRiverChannel, mountainCoastWeight);

    return new TerrainSample(terrainType, height, baseField);
}

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

private Cell.TerrainType ClassifySynthesizedIsland(
    float baseField, float height, float mountainBoost, bool isInRiverChannel, float mountainCoastWeight = 0f, float slope = 0f)
{
    // Water and River channels
    if (isInRiverChannel || height < -0.15f)
    {
        if (baseField < settings.abyssUpper - settings.visualTransitionWidth) return Cell.TerrainType.Abyssal;
        if (baseField < settings.deepUpper) return Cell.TerrainType.Deep;
        if (baseField < settings.shallowUpper) return Cell.TerrainType.Shallow;
        return isInRiverChannel ? Cell.TerrainType.River : Cell.TerrainType.Water;
    }

    // Mountain / Cliff classification: strictly elevated terrain, steep slopes, or mountain coast sector plunging to sea
    if (height >= settings.mountainPeakHeight - 0.4f) return Cell.TerrainType.MountainPeak;
    if (height >= settings.mountainHeight - 0.5f) return Cell.TerrainType.Mountain;
    if (height >= 1.6f || (height >= 0.15f && slope > 0.45f) || (height >= 0.05f && mountainBoost > 0.20f))
    {
        return Cell.TerrainType.Cliff;
    }

    // Natural shoreline beach: a narrow strip just above the waterline. The height cut is kept
    // roughly in step with TextureBuilder's SandLower + SandBand so gameplay Beach cells and the
    // sand the player can actually see agree; widening this again reintroduces the broad flat
    // beige apron that made every island look like it sat on a plate rim.
    if (mountainCoastWeight <= 0.45f && mountainBoost <= 0.20f && slope <= 0.45f && height < 0.18f)
    {
        return Cell.TerrainType.Beach;
    }

    if (height >= settings.hillHeight && mountainBoost > 0.3f)
    {
        return Cell.TerrainType.Hill;
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
