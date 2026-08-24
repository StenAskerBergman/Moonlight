using UnityEngine;
using System.Collections.Generic;

public partial class IslandTerrainProvider
{

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
