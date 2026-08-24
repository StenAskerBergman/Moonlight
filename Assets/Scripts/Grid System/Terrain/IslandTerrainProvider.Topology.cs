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

    // Deterministic seed-derived offsets for the orthogonal warp dimensions
    float offsetX1 = (seed * 198491317 & 0x7FFFFFFF) % 10000f;
    float offsetZ1 = (seed * 6542989 & 0x7FFFFFFF) % 10000f;
    float offsetX2 = (seed * 87654323 & 0x7FFFFFFF) % 10000f;
    float offsetZ2 = (seed * 91827364 & 0x7FFFFFFF) % 10000f;

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

    warpX = totalWarpX;
    warpZ = totalWarpZ;
}

private float EvaluateRidgedMultifractal(float worldX, float worldZ, int seed)
{
    RidgedMultifractalSettings ridged = settings.ridgedMultifractal;
    if (!ridged.enabled || ridged.peakStrength <= 0f) return 0f;

    float frequency = 1f / ridged.scale;
    float amplitude = 1f;
    float totalValue = 0f;
    float maxAmplitude = 0f;

    float seedOffsetX = (seed * 73856093 & 0x7FFFFFFF) % 10000f;
    float seedOffsetZ = (seed * 19349663 & 0x7FFFFFFF) % 10000f;

    for (int octave = 0; octave < ridged.octaves; octave++)
    {
        float sx = (worldX + seedOffsetX + octave * 31.7f) * frequency;
        float sz = (worldZ + seedOffsetZ + octave * 17.3f) * frequency;

        // Map Perlin from [0, 1] to [-1, 1], then calculate sharp ridge n = 1 - abs(raw)
        float raw = Mathf.PerlinNoise(sx, sz) * 2f - 1f;
        float n = 1f - Mathf.Abs(raw);
        float ridge = Mathf.Pow(n, ridged.power);

        totalValue += ridge * amplitude;
        maxAmplitude += amplitude;

        frequency *= ridged.lacunarity;
        amplitude *= ridged.persistence;
    }

    if (maxAmplitude <= 0f) return 0f;
    return (totalValue / maxAmplitude) * ridged.peakStrength;
}

private float EvaluateLocalIslandField(float localX, float localZ)
{
    float noise = Mathf.PerlinNoise(
        localX * settings.legacyIslandScale + legacyOffsetX,
        localZ * settings.legacyIslandScale + legacyOffsetZ);
    float normalizedX = localX / size * 2f - 1f;
    float normalizedZ = localZ / size * 2f - 1f;
    float radius = Mathf.Sqrt(normalizedX * normalizedX + normalizedZ * normalizedZ);
    float radiusCubed = Mathf.Pow(radius, 3f);
    float inverse = 2.2f - 2.2f * radius;
    float falloff = radiusCubed / (radiusCubed + Mathf.Pow(inverse, 3f));
    return noise - falloff;
}

private float CalculateLegacyIslandField(float localX, float localZ)
{
    float worldX = chunkWorldOrigin.x + localX;
    float worldZ = chunkWorldOrigin.y + localZ;

    float sharedBase = EvaluateSharedBaseField(worldX, worldZ, worldSeed);

    // Evaluate low-frequency domain warp in world coordinates
    EvaluateDomainWarp(worldX, worldZ, worldSeed, out float warpX, out float warpZ);

    float localField = EvaluateLocalIslandField(localX + warpX, localZ + warpZ);

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
    
    Vector2 sample = new Vector2(x + settings.offset.x, z + settings.offset.y) / scale;
    sample += layer.OctaveOffsets[0]; // Seed offset for variation

    float value = EvaluateGradientTrick(sample, octaveCount, settings.persistence, settings.lacunarity);
    
    // EvaluateGradientTrick typically returns values varying roughly around -1 to 1 (or 0 to 1 depending on noise).
    // Our old SampleLayer expected 0 to 1.
    // Let's normalize it to 0-1 range roughly. Value noise without offset gives 0 to 1.
    // With gradient trick, max amplitude is roughly 1 / (1-persistence).
    float amplitudeTotal = (1f - Mathf.Pow(settings.persistence, octaveCount)) / (1f - settings.persistence);
    
    if (amplitudeTotal <= 0f) return 0.5f;
    return Mathf.Clamp01(value / amplitudeTotal);
}

private static float EvaluateGradientTrick(Vector2 sample, int octaves, float persistence, float lacunarity)
{
    float height = 0f;
    float amplitude = 1f;
    Vector2 gradient = Vector2.zero;

    
    
    for (int i = 0; i < octaves; i++)
    {
        // x = noise (0 to 1)
        // y = analytical dNoise/dX
        // z = analytical dNoise/dY
        Vector3 n = EvaluateNoiseWithDerivatives(sample);

        gradient += new Vector2(n.y, n.z);

        height += amplitude * n.x / (1f + Vector2.Dot(gradient, gradient));

        amplitude *= persistence;
        
sample *= lacunarity;
    }

    return height;
}

private static Vector3 EvaluateNoiseWithDerivatives(Vector2 p)
{
    // Integer part
    Vector2 i = new Vector2(Mathf.Floor(p.x), Mathf.Floor(p.y));
    // Fractional part
    Vector2 f = new Vector2(p.x - i.x, p.y - i.y);

    // Quintic interpolation: u = f*f*f*(f*(f*6.0-15.0)+10.0)
    // First derivative: du = 30.0*f*f*(f*(f-2.0)+1.0)
    Vector2 u = new Vector2(
        f.x * f.x * f.x * (f.x * (f.x * 6f - 15f) + 10f),
        f.y * f.y * f.y * (f.y * (f.y * 6f - 15f) + 10f)
    );

    Vector2 du = new Vector2(
        30f * f.x * f.x * (f.x * (f.x - 2f) + 1f),
        30f * f.y * f.y * (f.y * (f.y - 2f) + 1f)
    );

    // Random values at the 4 corners
    float a = Hash(i + new Vector2(0f, 0f));
    float b = Hash(i + new Vector2(1f, 0f));
    float c = Hash(i + new Vector2(0f, 1f));
    float d = Hash(i + new Vector2(1f, 1f));

    // Bilinear interpolation
    float k0 = a;
    float k1 = b - a;
    float k2 = c - a;
    float k3 = a - b - c + d;

    // Noise value
    float noise = k0 + k1 * u.x + k2 * u.y + k3 * u.x * u.y;
    
    // Analytical derivatives
    float dNoiseDx = du.x * (k1 + k3 * u.y);
    float dNoiseDy = du.y * (k2 + k3 * u.x);

    return new Vector3(noise, dNoiseDx, dNoiseDy);
}

private static float Hash(Vector2 p)
{
    float h = Vector2.Dot(p, new Vector2(127.1f, 311.7f));
    float val = Mathf.Sin(h) * 43758.5453123f;
    return val - Mathf.Floor(val);
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
