using UnityEngine;
using System.Collections.Generic;

public partial class IslandTerrainProvider
{

private float CalculateBaseContinuousHeight(float value)
{
    float waterUpper = settings.waterUpper; // 0.40f (Coastline MSL = 0.0m)
    float beachUpper = settings.beachUpper; // 0.45f (Shoreline to Mainland transition)
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
    else if (value <= beachUpper)
    {
        // Shoreline rise from Mean Sea Level (0.0m) to Mainland baseline (+0.85m)
        float u = (value - waterUpper) / Mathf.Max(0.01f, beachUpper - waterUpper);
        float shoreCurve = u * u * (3f - 2f * u); // Smooth cubic S-curve
        return Mathf.Lerp(0.0f, settings.surfaceFlatlandHeight, shoreCurve);
    }
    else
    {
        // Inland mainland: +0.85m baseline with organic rolling topography (rises naturally towards the interior)
        float excess = value - beachUpper;
        return settings.surfaceFlatlandHeight + excess * 0.85f;
    }
}

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
    float baseHeight = CalculateBaseContinuousHeight(baseField);

    float mountainBoost = 0f;
    float riverCarve = 0f;
    bool isInRiverChannel = false;

    if (featureReservations != null)
    {
        var res = featureReservations.EvaluateAll(localX, localZ, baseHeight, settings.waterHeight);
        float smoothField = CalculateLegacyIslandField(localX, localZ, true);
        mountainBoost = CalculateStructuralMountainBoost(smoothField, res);
        riverCarve = res.RiverCarveDepth;
        isInRiverChannel = res.IsInRiverChannel;
    }

    float height = baseHeight + mountainBoost - riverCarve;
    float mountainCoastWeight = (featureReservations != null && featureReservations.Sectors != null)
        ? featureReservations.Sectors.GetMountainCoastWeight(localX, localZ)
        : 0f;

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

    // Smooth continental shelf landMask over [abyssUpper..beachUpper] avoiding sharp cliff cuts.
    //
    // Gated on the low-frequency-only field (see CalculateLegacyIslandField's
    // lowFrequencyOnly flag), not the full multi-octave baseField: the high-frequency
    // octaves flip landMask on/off between adjacent vertices right at the coastline
    // threshold, which showed up as a row of saw-tooth spikes along the mountain base.
    float u = Mathf.Clamp01((smoothField - settings.abyssUpper) / Mathf.Max(0.01f, settings.beachUpper - settings.abyssUpper));
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
    if (height >= 1.6f || (height >= 0.15f && slope > 0.45f) || (height >= 0.05f && mountainCoastWeight > 0.45f && mountainBoost > 0.20f))
    {
        return Cell.TerrainType.Cliff;
    }

    // Natural shoreline beach: coastal perimeter rising from waterline (0.0m) up to flatland plain (0.85m)
    if (mountainCoastWeight <= 0.45f && height < 0.48f)
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
