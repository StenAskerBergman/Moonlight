using UnityEngine;
using System.Collections.Generic;

public partial class IslandTerrainProvider
{


private float SampleComposedNoise(float x, float z)
{
    float weightedValue = 0f;
    float totalWeight = 0f;

    for (int i = 0; i < layers.Count; i++)
    {
        RuntimeNoiseLayer layer = layers[i];
        if (!layer.Settings.enabled || layer.Settings.weight <= 0f) continue;

        weightedValue += SampleLayer(layer, x, z) * layer.Settings.weight;
        totalWeight += layer.Settings.weight;
    }

    return totalWeight > 0f ? Mathf.Clamp01(weightedValue / totalWeight) : 0.5f;
}

private float EvaluateSharedBaseField(float worldX, float worldZ, int worldSeed)
{
    float globalNoise = SampleComposedNoise(worldX, worldZ);
    return (globalNoise * 0.2f) - 0.15f;
}

private void EvaluateDomainWarp(float worldX, float worldZ, int seed, out float warpX, out float warpZ)
{
    DomainWarpSettings warp = settings.domainWarp;
    if (!warp.enabled || warp.amplitude <= 0f)
    {
        warpX = 0f;
        warpZ = 0f;
        return;
    }

    float frequency = 1f / warp.scale;
    float currentAmplitude = warp.amplitude;
    float totalWarpX = 0f;
    float totalWarpZ = 0f;
    float maxAmp = 0f;

    // Deterministic seed-derived offsets for the orthogonal warp dimensions.
    // Wrapped small for the same float32 precision reason as legacyOffsetX/Z: these are added
    // straight onto the Perlin sample coordinate, and at ~10000 the ULP is comparable to the
    // per-sample increment, which quantises the warp into a period-2 stair-step and feeds a
    // Nyquist ripple into every field derived from it.
    float offsetX1 = (seed * 198491317 & 0x7FFFFFFF) % NoiseOffsetWrap;
    float offsetZ1 = (seed * 6542989 & 0x7FFFFFFF) % NoiseOffsetWrap;
    float offsetX2 = (seed * 87654323 & 0x7FFFFFFF) % NoiseOffsetWrap;
    float offsetZ2 = (seed * 91827364 & 0x7FFFFFFF) % NoiseOffsetWrap;

    for (int octave = 0; octave < warp.octaves; octave++)
    {
        float sampleX1 = (worldX + offsetX1 + octave * 53.1f) * frequency;
        float sampleZ1 = (worldZ + offsetZ1 + octave * 37.7f) * frequency;
        float sampleX2 = (worldX + offsetX2 + octave * 71.3f) * frequency;
        float sampleZ2 = (worldZ + offsetZ2 + octave * 91.9f) * frequency;

        float nx = Mathf.PerlinNoise(sampleX1, sampleZ1) * 2f - 1f;
        float nz = Mathf.PerlinNoise(sampleX2, sampleZ2) * 2f - 1f;

        totalWarpX += nx * currentAmplitude;
        totalWarpZ += nz * currentAmplitude;
        maxAmp += currentAmplitude;

        frequency *= warp.lacunarity;
        currentAmplitude *= warp.persistence;
    }

    float effectiveAmp = Mathf.Clamp(warp.amplitude, 0f, 7.5f);
    warpX = maxAmp > 0f ? (totalWarpX / maxAmp) * effectiveAmp : 0f;
    warpZ = maxAmp > 0f ? (totalWarpZ / maxAmp) * effectiveAmp : 0f;
}

private float EvaluateLocalIslandField(float localX, float localZ, float warpX, float warpZ, bool lowFrequencyOnly = false)
{
    float scale = settings.legacyIslandScale;

    // Evaluate 3-octave fractal composite in domain-warped coordinates
    float wx = localX + warpX;
    float wz = localZ + warpZ;

    float n1 = Mathf.PerlinNoise(wx * scale + legacyOffsetX, wz * scale + legacyOffsetZ);
    float fractalNoise;
    if (lowFrequencyOnly)
    {
        // Skips the n2/n3 high-frequency octaves. Used to gate mountain boost:
        // sampling those octaves right at the coastline threshold made the boost
        // mask flicker on/off between adjacent mesh vertices, producing a row of
        // saw-tooth spikes at the mountain/land boundary.
        fractalNoise = n1;
    }
    else
    {
        float n2 = Mathf.PerlinNoise(wx * scale * 2.3f + legacyOffsetX + 127.3f, wz * scale * 2.3f + legacyOffsetZ + 89.1f);
        float n3 = Mathf.PerlinNoise(wx * scale * 4.8f + legacyOffsetX + 311.7f, wz * scale * 4.8f + legacyOffsetZ + 241.9f);
        fractalNoise = n1 * 0.58f + n2 * 0.30f + n3 * 0.12f;
    }

    // Evaluate normalized coordinates in domain-warped space so the perimeter contour is organically sculpted
    float warpedNormX = (wx / size) * 2f - 1f;
    float warpedNormZ = (wz / size) * 2f - 1f;
    float warpedRadius = Mathf.Sqrt(warpedNormX * warpedNormX + warpedNormZ * warpedNormZ);

    // Natural sigmoid falloff in warped space creating the organic multi-lobed / kidney silhouette
    float radiusCubed = warpedRadius * warpedRadius * warpedRadius;
    float inv = Mathf.Max(0.001f, 2.05f - 2.05f * warpedRadius);
    float falloff = radiusCubed / (radiusCubed + inv * inv * inv);

    // Smooth outer suppression near chunk edges to ensure clean ocean framing.
    //
    // Shaped with smoothstep rather than Pow(x, 2). Both start with zero gradient at x=0, but x^2
    // arrives at x=1 with gradient 2, and Clamp01 then flattens it to 0 - a derivative
    // discontinuity right where the clamp saturates, at warpedRadius 0.93. Because that is a
    // constant radius in warped space it closes into a RING around the island, and because it
    // lives in the base field - before mountain boost is added on top - the ring rides up and over
    // the mountains too, which is exactly how it reads: one continuous unnatural crease
    // encircling the whole island and crossing every landform in its path.
    //
    // smoothstep is zero-gradient at both ends, so the suppression now starts and finishes without
    // stamping a crease at either limit.
    float outerT = Mathf.Clamp01((warpedRadius - 0.65f) / 0.28f);
    float outerDrop = outerT * outerT * (3f - 2f * outerT) * 0.35f;

    // Base field sculpted directly by the domain-warped fractal noise
    float field = fractalNoise - falloff * 0.82f - outerDrop;
    return field;
}

private float CalculateLegacyIslandField(float localX, float localZ, bool lowFrequencyOnly = false)
{
    float worldX = chunkWorldOrigin.x + localX;
    float worldZ = chunkWorldOrigin.y + localZ;

    float sharedBase = EvaluateSharedBaseField(worldX, worldZ, worldSeed);

    // Evaluate low-frequency domain warp in world coordinates
    EvaluateDomainWarp(worldX, worldZ, worldSeed, out float warpX, out float warpZ);

    float localField = EvaluateLocalIslandField(localX, localZ, warpX, warpZ, lowFrequencyOnly);

    float W = 8f;
    float dx = Mathf.Min(localX, size - localX);
    float dz = Mathf.Min(localZ, size - localZ);
    float tx = Mathf.Clamp01(dx / W);
    float tz = Mathf.Clamp01(dz / W);
    
    float weightX = tx * tx * tx * (tx * (tx * 6f - 15f) + 10f);
    float weightZ = tz * tz * tz * (tz * (tz * 6f - 15f) + 10f);
    float weight = weightX * weightZ;

    return Mathf.Lerp(sharedBase, localField, weight);
}


private float CalculateIslandField(float x, float z, float noise)
{
    float mask = SampleIslandMask(x, z, noise);
    return Mathf.Clamp01(
        noise * settings.noiseContribution
        + mask * settings.islandMaskContribution
        - settings.fieldBias);
}


private float CalculatePlateauField(float x, float z, float noise)
{
    float mask = SampleIslandMask(x, z, noise);
    return Mathf.Clamp01(noise * 0.25f + mask * 0.75f);
}


private float SampleIslandMask(float x, float z, float noise)
{
    float denominator = Mathf.Max(1f, size - 1f);
    float normalizedX = x / denominator * 2f - 1f;
    float normalizedZ = z / denominator * 2f - 1f;
    float distance = Mathf.Sqrt(normalizedX * normalizedX + normalizedZ * normalizedZ);

    // Noise only warps the boundary. It cannot erase the intentionally broad
    // interior bands that provide useful construction space.
    float warpedDistance = distance + (noise - 0.5f) * settings.coastWarp;
    float t = Mathf.InverseLerp(settings.falloffStart, settings.falloffEnd, warpedDistance);
    return 1f - Mathf.SmoothStep(0f, 1f, t);
}


private static float SampleLayer(RuntimeNoiseLayer layer, float x, float z)
{
    TerrainNoiseLayerSettings settings = layer.Settings;
    float scale = Mathf.Max(0.001f, settings.scale);
    int octaveCount = Mathf.Max(1, settings.octaves);

    float total = 0f;
    float frequency = 1f / scale;
    float amplitude = 1f;
    float maxAmplitude = 0f;

    for (int i = 0; i < octaveCount; i++)
    {
        Vector2 offset = layer.OctaveOffsets[i];
        float sampleX = (x + settings.offset.x) * frequency + offset.x;
        float sampleZ = (z + settings.offset.y) * frequency + offset.y;

        float perlinValue = Mathf.PerlinNoise(sampleX, sampleZ);
        total += perlinValue * amplitude;
        maxAmplitude += amplitude;

        amplitude *= settings.persistence;
        frequency *= settings.lacunarity;
    }

    return maxAmplitude > 0f ? Mathf.Clamp01(total / maxAmplitude) : 0.5f;
}

private static List<RuntimeNoiseLayer> BuildRuntimeLayers(List<TerrainNoiseLayerSettings> configuredLayers, int seed)
{
    List<TerrainNoiseLayerSettings> source = configuredLayers;
    if (source == null || source.Count == 0)
    {
        source = new TerrainGenerationSettings().noiseLayers;
    }

    List<RuntimeNoiseLayer> runtimeLayers = new List<RuntimeNoiseLayer>(source.Count);
    for (int layerIndex = 0; layerIndex < source.Count; layerIndex++)
    {
        TerrainNoiseLayerSettings layer = source[layerIndex] ?? new TerrainNoiseLayerSettings();
        int octaveCount = Mathf.Max(1, layer.octaves);
        Vector2[] octaveOffsets = new Vector2[octaveCount];
        System.Random random = new System.Random(unchecked(seed * 397 ^ layerIndex * 7919));

        for (int octave = 0; octave < octaveCount; octave++)
        {
            octaveOffsets[octave] = new Vector2(
                random.Next(-100000, 100000),
                random.Next(-100000, 100000));
        }

        runtimeLayers.Add(new RuntimeNoiseLayer(layer, octaveOffsets));
    }

    return runtimeLayers;
}
}
