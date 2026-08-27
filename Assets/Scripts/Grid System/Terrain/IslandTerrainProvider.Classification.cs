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
private float CalculateBaseContinuousHeight(float value)
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
        return Mathf.Lerp(settings.abyssHeight, 0.0f, shelfCurve);
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
        return Mathf.Lerp(0.0f, settings.surfaceFlatlandHeight, shoreCurve);
    }
    else
    {
        // Inland mainland: +0.85m baseline with organic rolling topography (rises naturally towards the interior)
        float excess = value - coastalUpper;
        return settings.surfaceFlatlandHeight + excess * 1.15f;
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
    float reservationBaseHeight = CalculateBaseContinuousHeight(baseField);
    float visualBaseHeight = CalculateBaseContinuousHeight(smoothField);

    float mountainBoost = 0f;
    float riverCarve = 0f;
    bool isInRiverChannel = false;

    if (featureReservations != null)
    {
        var res = featureReservations.EvaluateAll(localX, localZ, reservationBaseHeight, settings.waterHeight);
        mountainBoost = CalculateStructuralMountainBoost(smoothField, res);
        riverCarve = res.RiverCarveDepth;
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

private TerrainSample ClassifyStandalonePlateau(float value)
{
    if (value < settings.abyssUpper)
        return Sample(Cell.TerrainType.Abyssal, settings.abyssHeight, value);

    if (value < settings.deepUpper)
        return Sample(
            Cell.TerrainType.Deep,
            BlendVisualHeight(settings.abyssHeight, settings.deepHeight, settings.abyssUpper, value),
            value);

    return new TerrainSample(
        Cell.TerrainType.Plateau,
        BlendVisualHeight(settings.deepHeight, settings.underwaterPlateauHeight, settings.deepUpper, value),
        value,
        1f);
}

private float BlendVisualHeight(float lower, float upper, float threshold, float value)
{
    float width = settings.visualTransitionWidth;
    float t = Mathf.InverseLerp(threshold - width, threshold + width, value);
    return Mathf.Lerp(lower, upper, Mathf.SmoothStep(0f, 1f, t));
}
}
