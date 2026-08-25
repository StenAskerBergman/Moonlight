using UnityEngine;
using System.Collections.Generic;

public partial class IslandTerrainProvider
{

private float CalculateBaseContinuousHeight(float value)
{
    float height = settings.abyssHeight;
    height = BlendVisualHeight(height, settings.deepHeight, settings.abyssUpper, value);
    height = BlendVisualHeight(height, settings.naturalPlateauHeight, settings.deepUpper, value);
    height = BlendVisualHeight(height, settings.shallowHeight, settings.underwaterPlateauUpper, value);
    height = BlendVisualHeight(height, settings.waterHeight, settings.shallowUpper, value);
    height = BlendVisualHeight(height, settings.beachHeight, settings.waterUpper, value);
    height = BlendVisualHeight(height, settings.surfaceFlatlandHeight, settings.beachUpper, value);
    return height;
}

private float CalculateContinuousHeight(float value)
{
    float height = settings.abyssHeight;
    height = BlendVisualHeight(height, settings.deepHeight, settings.abyssUpper, value);
    height = BlendVisualHeight(height, settings.naturalPlateauHeight, settings.deepUpper, value);
    height = BlendVisualHeight(height, settings.shallowHeight, settings.underwaterPlateauUpper, value);
    height = BlendVisualHeight(height, settings.waterHeight, settings.shallowUpper, value);
    height = BlendVisualHeight(height, settings.beachHeight, settings.waterUpper, value);
    height = BlendVisualHeight(height, settings.surfaceFlatlandHeight, settings.beachUpper, value);
    height = BlendVisualHeight(height, settings.hillHeight, settings.surfaceFlatlandUpper, value);
    height = BlendVisualHeight(height, settings.cliffHeight, settings.hillUpper, value);
    height = BlendVisualHeight(height, settings.mountainHeight, settings.cliffUpper, value);
    height = BlendVisualHeight(height, settings.mountainPeakHeight, settings.mountainUpper, value);
    return height;
}

private TerrainSample SampleSynthesizedIsland(float localX, float localZ)
{
    float baseField = CalculateLegacyIslandField(localX, localZ);
    float baseHeight = CalculateBaseContinuousHeight(baseField);

    // 1. Mountain Ridge elevation (negotiated & masked by river corridors and harbors)
    float mountainBoost = featureReservations != null
        ? featureReservations.GetSynthesizedMountainHeight(localX, localZ)
        : 0f;

    // Masked Ridged Multifractal Detail (strictly obeys mountain allowance)
    float mountainAllowance = featureReservations != null
        ? featureReservations.GetMountainAllowance(localX, localZ)
        : 1f;

    if (mountainAllowance > 0.001f && mountainBoost > 0.05f)
    {
        float worldX = chunkWorldOrigin.x + localX;
        float worldZ = chunkWorldOrigin.y + localZ;
        float ridgedDetail = EvaluateRidgedMultifractal(worldX, worldZ, worldSeed);
        mountainBoost += ridgedDetail * mountainAllowance;
    }

    // 2. River Carving (negotiated depth in channel & valley)
    bool isInRiverChannel = false;
    float riverCarve = 0f;
    if (featureReservations != null)
    {
        riverCarve = featureReservations.GetRiverCarveDepth(
            localX, localZ, baseHeight + mountainBoost, settings.waterHeight, out isInRiverChannel);
    }

    // 3. Harbor Leveling
    float harborTargetHeight = settings.surfaceFlatlandHeight;
    float harborInf = featureReservations != null
        ? featureReservations.GetHarborFlattenInfluence(localX, localZ, out harborTargetHeight)
        : 0f;

    float height = baseHeight + mountainBoost - riverCarve;
    if (harborInf > 0f && height > settings.waterHeight)
    {
        height = Mathf.Lerp(height, harborTargetHeight, harborInf);
    }

    // 4. Semantic Classification
    Cell.TerrainType terrainType = ClassifySynthesizedIsland(
        baseField, height, mountainBoost, isInRiverChannel, harborInf);

    return new TerrainSample(terrainType, height, baseField);
}

private Cell.TerrainType ClassifySynthesizedIsland(
    float baseField, float height, float mountainBoost, bool isInRiverChannel, float harborInf)
{
    // Water classification is governed by base field and river channel
    if (isInRiverChannel && baseField >= settings.waterUpper)
    {
        return Cell.TerrainType.River;
    }

    if (baseField < settings.abyssUpper - settings.visualTransitionWidth) return Cell.TerrainType.Abyssal;
    if (baseField < settings.deepUpper) return Cell.TerrainType.Deep;
    if (baseField < settings.shallowUpper) return Cell.TerrainType.Shallow;
    if (baseField < settings.waterUpper) return Cell.TerrainType.Water;

    // Land / Mountain / Cliff classification
    if (mountainBoost > 0.35f || (height >= settings.cliffHeight && harborInf < 0.2f))
    {
        if (height >= settings.mountainPeakHeight - 0.2f) return Cell.TerrainType.MountainPeak;
        if (height >= settings.mountainHeight - 0.3f) return Cell.TerrainType.Mountain;
        return Cell.TerrainType.Cliff;
    }

    if (height >= settings.hillHeight && harborInf < 0.4f)
    {
        return Cell.TerrainType.Hill;
    }

    if (harborInf > 0.3f || baseField < settings.beachUpper)
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
