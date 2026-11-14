// tldr: Decides what the ground LOOKS like: converts height, slope, shore distance and coastal bands into the
// RGBA control texture (grass / sand / rock / seabed weights) the terrain shader blends.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TerrainDebugViewMode
{
    Normal = 0,
    PaintballAttribution, // Distinct vibrant color per subsystem & feature ID
    DeltaHeatmap,         // Net height delta from base (carved = red, raised = green)
    MountainBoostOnly,    // Isolated ridge mountain boost
    RiverCarveOnly,       // Isolated river carve depths
    MainlandReliefOnly,   // Mainland micro-relief offsets
    PlateauDeltaOnly,     // Underwater plateau shifts
    SlopeField,           // Steepness gradient
    SemanticClassification,// Discrete terrain types
    PlateauZones,         // Tabletop / rim / escarpment / sand opening / abyss
    MaterialWeights,      // Production weights: grass red, sand green, rock blue, seabed black
    SurfaceColour         // Production climate colour before tiled detail and lighting
}

public class TextureBuilder
{
    // Sand now follows the SHORELINE DISTANCE BANDS, not an absolute height window.
    //
    // It used to be `height in [0.02, 0.16]`, which had to be kept narrow because it was slicing a
    // ramp whose ground width the noise gradient chose - a wider window produced the beige "plate
    // rim" apron. Reading the same bands the geometry and the Beach classification use means the
    // painted sand is exactly as wide as the sand actually is, and it can be authored.
    //
    // Fraction of the backshore over which sand hands over to grass.
    private const float BackshoreSandFade = 0.65f;

    // Depth over which submerged ground is replaced by marine silt.
    private const float SeabedSiltDepth = 1.1f;

    // Height above the ocean surface at which coastal rock assistance fades out.
    private const float CoastalRockAssistHeight = 0.75f;

    private readonly Cell[,] grid;
    private readonly IslandTerrainProvider terrainSource;
    private readonly int visualSamplesPerCell;
    private readonly TerrainDebugViewMode debugViewMode;

    public TextureBuilder(Cell[,] grid, ClimateProfile climate = null, TerrainDebugViewMode debugViewMode = TerrainDebugViewMode.Normal)
        : this(grid, null, 1, climate, debugViewMode) { }

    public TextureBuilder(Cell[,] grid, IslandTerrainProvider terrainSource, int visualSamplesPerCell,
        ClimateProfile climate = null, TerrainDebugViewMode debugViewMode = TerrainDebugViewMode.Normal)
    {
        this.grid = grid;
        this.terrainSource = terrainSource;
        this.visualSamplesPerCell = Mathf.Max(1, visualSamplesPerCell);
        this.debugViewMode = debugViewMode;
        // Snapshot appearance on the calling thread before parallel pixel emission.
        if (climate != null)
        {
            ForestColor1 = climate.forestColor1;
            ForestColor2 = climate.forestColor2;
            GrassColor1 = climate.grassColor1;
            GrassColor2 = climate.grassColor2;
            PlateauCoarseSandColor = climate.plateauCoarseSandColor;
            PlateauFineSandColor = climate.plateauFineSandColor;
            PlateauGravelColor = climate.plateauGravelColor;
            PlateauMaterialVariation = climate.plateauMaterialVariation;
            PlateauMudColor = climate.plateauMudColor;
            PlateauReefColor = climate.plateauReefColor;
            PlateauShellSedimentColor = climate.plateauShellSedimentColor;
            PlateauSiltColor = climate.plateauSiltColor;
            RockColor1 = climate.rockColor1;
            RockColor2 = climate.rockColor2;
            SandColor1 = climate.sandColor1;
            SandColor2 = climate.sandColor2;
            SnowColor = climate.snowColor;
        }
    }

    private readonly Color ForestColor1 = new Color(0.2f, 0.35f, 0.15f);
    private readonly Color ForestColor2 = new Color(0.25f, 0.4f, 0.18f);
    private readonly Color GrassColor1 = new Color(0.3f, 0.45f, 0.15f);
    private readonly Color GrassColor2 = new Color(0.35f, 0.5f, 0.18f);
    private readonly Color PlateauCoarseSandColor = new Color(0.46f, 0.43f, 0.34f, 1f);
    private readonly Color PlateauFineSandColor = new Color(0.62f, 0.59f, 0.47f, 1f);
    private readonly Color PlateauGravelColor = new Color(0.34f, 0.38f, 0.37f, 1f);
    private readonly float PlateauMaterialVariation = 0.78f;
    private readonly Color PlateauMudColor = new Color(0.20f, 0.23f, 0.20f, 1f);
    private readonly Color PlateauReefColor = new Color(0.13f, 0.28f, 0.22f, 1f);
    private readonly Color PlateauShellSedimentColor = new Color(0.76f, 0.73f, 0.61f, 1f);
    private readonly Color PlateauSiltColor = new Color(0.11f, 0.15f, 0.17f, 1f);
    private readonly Color RockColor1 = new Color(0.45f, 0.45f, 0.45f);
    private readonly Color RockColor2 = new Color(0.35f, 0.35f, 0.35f);
    private readonly Color SandColor1 = new Color(0.75f, 0.75f, 0.7f);
    private readonly Color SandColor2 = new Color(0.7f, 0.7f, 0.65f);
    private readonly Color SnowColor = new Color(0.95f, 0.95f, 0.98f);

    private float FractalNoise(float x, float y, float scale)
    {
        float noise = 0f;
        float frequency = scale;
        float amplitude = 1f;
        float maxValue = 0f;
        for (int i = 0; i < 3; i++)
        {
            noise += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }
        return noise / maxValue;
    }

    public Texture2D Build()
    {
        if (debugViewMode == TerrainDebugViewMode.Normal || debugViewMode == TerrainDebugViewMode.SurfaceColour)
            return BuildMaterialMap(false);
        if (debugViewMode == TerrainDebugViewMode.MaterialWeights)
        {
            Texture2D texture = BuildTerrainControlMap();
            // Diagnostic display is opaque; seabed is black because its weight occupies alpha.
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++) pixels[i].a = 1f;
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }
        return BuildDiagnosticTexture();
    }

    // Control and colour emission share one material decision. Geometry remains authoritative.
    public Texture2D BuildSurfaceTintMap()
    {
        return BuildMaterialMap(false, true);
    }

    private Texture2D BuildMaterialMap(bool control, bool surfaceMetadata = false)
    {
        TerrainSampleCache cache = terrainSource != null
            ? terrainSource.GetOrCreateSampleCache(visualSamplesPerCell) : null;
        int resolution = cache != null ? cache.Resolution : grid.GetLength(0);
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, control);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[resolution * resolution];
        System.Threading.Tasks.Parallel.For(0, resolution, y =>
        {
            for (int x = 0; x < resolution; x++)
            {
                int idx = cache != null ? cache.GetIndex(x, y) : 0;
                Vector2 point = new Vector2(x, y) * (cache != null ? cache.Step : 1f);
                if (terrainSource != null) point -= Vector2.one * terrainSource.SampleCachePadding;
                Color weights = cache != null ? EvaluateMaterialWeights(cache, idx, point)
                    : EvaluateFallbackWeights(grid[x, y].currentTerrainType);
                Color pixel = control ? weights : EvaluateSurfaceTint(cache, idx, point, weights,
                    cache != null ? cache.TerrainTypes[idx] : grid[x, y].currentTerrainType);
                if (!control && !surfaceMetadata) pixel.a = 1f;
                pixels[y * resolution + x] = pixel;
            }
        });
        texture.SetPixels(pixels);
        texture.Apply(true, false);
        return texture;
    }

    private static Color EvaluateFallbackWeights(Cell.TerrainType type)
    {
        switch (type)
        {
            case Cell.TerrainType.Beach: case Cell.TerrainType.Shore: case Cell.TerrainType.Coast:
            case Cell.TerrainType.River: case Cell.TerrainType.Lake: return new Color(0f, 1f, 0f, 0f);
            case Cell.TerrainType.Mountain: case Cell.TerrainType.MountainPeak:
            case Cell.TerrainType.Cliff: case Cell.TerrainType.Rocky: return new Color(0f, 0f, 1f, 0f);
            case Cell.TerrainType.Abyssal: case Cell.TerrainType.Deep: case Cell.TerrainType.Shallow:
            case Cell.TerrainType.Water: case Cell.TerrainType.Sea: case Cell.TerrainType.Ocean:
                return new Color(0f, 0f, 0f, 1f);
            default: return new Color(1f, 0f, 0f, 0f);
        }
    }

    private Color EvaluateMaterialWeights(TerrainSampleCache cache, int idx, Vector2 point)
    {
        TerrainGenerationSettings settings = terrainSource.SurfaceSettingsAt(point);
        float relativeHeight = cache.Heights[idx] - settings.waterHeight;
        PlateauSampleData plateau = cache.PlateauData != null ? cache.PlateauData[idx] : default;
        Color weights;
        if (plateau.IsDefined)
        {
            float sediment = Mathf.Max(plateau.SiltWeight, plateau.AbyssFade);
            float rock = plateau.RockWeight * (1f - sediment);
            float sand = plateau.SandWeight * (1f - sediment);
            float total = Mathf.Max(0.0001f, rock + sand + sediment);
            weights = new Color(0f, sand / total, rock / total, sediment / total);
            if (rock + sand + sediment < 0.0001f) weights.a = 1f;
        }
        else
        {
            IslandTerrainProvider.CoastalBands bands = terrainSource.ResolveCoastalBands(
                point.x, point.y, cache.HeadlandMasks[idx], cache.ShoreCurvatures[idx]);
            float assist = Smooth01(1f - relativeHeight / CoastalRockAssistHeight);
            float boost = cache.MountainBoosts != null ? cache.MountainBoosts[idx] : 0f;
            float rock = Smooth01(Mathf.Max(
                (boost - Mathf.Lerp(0.22f, 0.025f, assist)) / Mathf.Lerp(0.50f, 0.16f, assist),
                (cache.Slopes[idx] - 0.35f) / 0.25f,
                (relativeHeight - 1.6f) / 1.2f));
            float fadeEnd = Mathf.Lerp(bands.BeachOuter, bands.BackshoreOuter, BackshoreSandFade);
            float grass = fadeEnd > 0.0001f
                ? Smooth01((cache.ShoreDistances[idx] - bands.BeachOuter) / Mathf.Max(0.01f, fadeEnd - bands.BeachOuter)) : 1f;
            float wash = Smooth01(cache.ShoreDistances[idx] / Mathf.Max(0.01f, settings.coastalProfile.waterlineSandWidth));
            grass = Mathf.Lerp(Mathf.Min(grass, wash), 1f, bands.HeadlandMask);
            grass = Mathf.Max(grass, rock);
            float depth = Smooth01(-relativeHeight / SeabedSiltDepth);
            float ground = 1f - rock;
            // Replacement, not additive weights: deep flat seabed reaches 100% silt.
            weights = new Color(grass * ground * (1f - depth),
                (1f - grass) * ground * (1f - depth), rock, ground * depth);
        }
        if (terrainSource.TrySampleRiver(point, out RiverSurfaceSample river))
        {
            float rock = Mathf.Clamp01(river.RockWeight);
            float gravel = Mathf.Clamp01(river.GravelWeight) * (1f - rock);
            Color riverWeights = new Color(1f - rock - gravel, gravel, rock, 0f);
            weights = Color.Lerp(weights, riverWeights, Mathf.Clamp01(river.Influence));
        }
        return weights;
    }

    private Color EvaluateSurfaceTint(TerrainSampleCache cache, int idx, Vector2 point, Color weights, Cell.TerrainType type)
    {
        Vector2 world = terrainSource != null ? terrainSource.WorldOrigin + point : point;
        TerrainGenerationSettings surfaceSettings = terrainSource != null ? terrainSource.SurfaceSettingsAt(point) : null;
        int seed = surfaceSettings != null ? surfaceSettings.seed : 0;
        world += new Vector2((seed & 65535) * 0.0137f, ((seed >> 16) & 65535) * 0.0173f);
        float micro = FractalNoise(world.x * 2.5f, world.y * 2.5f, 0.08f);
        float macro = FractalNoise(world.x, world.y, 0.02f);
        Color grass = Color.Lerp(GrassColor1, GrassColor2, micro);
        if (type == Cell.TerrainType.Forest) grass = Color.Lerp(ForestColor1, ForestColor2, micro);
        Color sand = Color.Lerp(SandColor1, SandColor2, micro);
        Color rock = Color.Lerp(RockColor1, RockColor2, macro * 0.7f + micro * 0.3f);
        Color silt = Color.Lerp(sand * 0.72f, rock * 0.65f, 0.45f);
        Color surface = grass * weights.r + sand * weights.g + rock * weights.b + silt * weights.a;
        PlateauSampleData plateau = cache != null && cache.PlateauData != null ? cache.PlateauData[idx] : default;
        if (plateau.IsDefined) surface = EvaluatePlateauSurfaceColor(plateau, weights, sand, rock, micro, macro);
        float snow = 0f;
        if (cache != null && !plateau.IsDefined)
        {
            snow = Smooth01((cache.Heights[idx] - (surfaceSettings.mountainPeakHeight - 0.4f)) / 0.8f);
            surface = Color.Lerp(surface, SnowColor, snow);
        }
        else if (type == Cell.TerrainType.MountainPeak && !plateau.IsDefined)
        {
            surface = SnowColor;
            snow = 1f;
        }
        surface = ApplyRiverSurfaceColor(surface, point);
        // Alpha carries snow coverage only in the surface tint map; baked colour emission restores opacity.
        surface.a = snow;
        return surface;
    }

    private Color EvaluatePlateauSurfaceColor(
        PlateauSampleData plateau,
        Color weights,
        Color sand,
        Color rock,
        float microNoise,
        float macroNoise)
    {
        // Plateau materials are semantic, not height bands. The tabletop is deep
        // underwater by design, so the generic `height < 0` seabed fade would turn
        // the entire buildable sand surface into the water channel and erase the
        // rocky escarpment. These authored weights are produced with the geometry and
        // are therefore the only stable material interface for this landform.
        float variation = Mathf.Clamp01(PlateauMaterialVariation);
        float sandWeight = weights.g;
        float rockWeight = weights.b;
        float materialWeight = Mathf.Max(0.0001f, sandWeight + rockWeight);
        float normalizedRock = rockWeight / materialWeight;

        Color fineSand = Color.Lerp(
            sand,
            ResolveUnderwaterColor(PlateauFineSandColor, sand * 0.86f),
            0.82f);
        Color coarseSand = ResolveUnderwaterColor(
            PlateauCoarseSandColor,
            Color.Lerp(sand, rock, 0.22f));
        Color shellSediment = ResolveUnderwaterColor(
            PlateauShellSedimentColor,
            sand * 1.08f);
        Color gravel = ResolveUnderwaterColor(
            PlateauGravelColor,
            Color.Lerp(sand, rock, 0.58f));
        Color mud = ResolveUnderwaterColor(
            PlateauMudColor,
            Color.Lerp(sand * 0.42f, rock * 0.48f, 0.38f));
        Color silt = ResolveUnderwaterColor(
            PlateauSiltColor,
            Color.Lerp(sand * 0.30f, rock * 0.36f, 0.72f));
        Color reef = ResolveUnderwaterColor(
            PlateauReefColor,
            new Color(0.16f, 0.27f, 0.22f, 1f));

        float coarseMask = sandWeight
            * Smooth01(Mathf.InverseLerp(0.34f, 0.76f, macroNoise))
            * variation * 0.72f;
        float shellMask = sandWeight
            * Smooth01(Mathf.InverseLerp(0.70f, 0.92f, microNoise))
            * Smooth01(Mathf.InverseLerp(0.42f, 0.78f, macroNoise))
            * variation * 0.46f;
        float mudMask = plateau.MudWeight
            * Mathf.Lerp(0.68f, 1f, 1f - microNoise)
            * variation;

        Color sedimentColor = Color.Lerp(fineSand, coarseSand, coarseMask);
        sedimentColor = Color.Lerp(sedimentColor, shellSediment, shellMask);
        sedimentColor = Color.Lerp(sedimentColor, mud, mudMask);

        // Rock remains the dominant escarpment material. Gravel occupies the
        // continuously mixed sand/rock seam instead of appearing as a new band.
        Color formationColor = Color.Lerp(sedimentColor, rock, normalizedRock);
        float gravelMask = plateau.GravelWeight
            * Mathf.Lerp(0.72f, 1f, microNoise)
            * variation;
        formationColor = Color.Lerp(formationColor, gravel, gravelMask);

        formationColor = Color.Lerp(
            formationColor,
            Color.Lerp(formationColor, reef, 0.52f),
            plateau.ReefWeight * variation * 0.48f);

        float siltMask = Mathf.Max(plateau.SiltWeight, plateau.AbyssFade);
        Color abyssSilt = Color.Lerp(mud, silt, Mathf.Clamp01(0.35f + plateau.AbyssFade * 0.65f));
        formationColor.a = 1f;
        abyssSilt.a = 1f;
        return Color.Lerp(formationColor, abyssSilt, siltMask);
    }

    private static Color ResolveUnderwaterColor(Color authored, Color fallback)
    {
        authored.a = 1f;
        fallback.a = 1f;
        return authored.maxColorComponent > 0.001f ? authored : fallback;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static readonly Color[] RidgePalette = new Color[]
    {
        new Color(1.0f, 0.15f, 0.15f), // Ridge 0: Vivid Red
        new Color(1.0f, 0.55f, 0.0f),  // Ridge 1: Orange
        new Color(0.95f, 0.1f, 0.65f), // Ridge 2: Magenta / Hot Pink
        new Color(0.6f, 0.1f, 0.95f),  // Ridge 3: Purple
        new Color(1.0f, 0.85f, 0.1f),  // Ridge 4: Amber / Gold
        new Color(0.85f, 0.35f, 0.2f), // Ridge 5: Coral
        new Color(0.4f, 0.0f, 0.6f),   // Ridge 6: Deep Violet
        new Color(0.7f, 0.15f, 0.3f)   // Ridge 7: Crimson
    };

    private static readonly Color[] RiverPalette = new Color[]
    {
        new Color(0.0f, 0.9f, 1.0f),   // River 0: Electric Cyan
        new Color(0.1f, 0.5f, 1.0f),   // River 1: Dodger Blue
        new Color(0.0f, 1.0f, 0.7f),   // River 2: Mint / Aquamarine
        new Color(0.3f, 0.7f, 0.9f),   // River 3: Sky Blue
    };

    private static Color GetRidgePaintColor(short ridgeId, float boost)
    {
        Color baseColor = RidgePalette[Mathf.Abs(ridgeId) % RidgePalette.Length];
        float intensity = Mathf.Clamp(0.55f + (boost / 3.0f) * 0.45f, 0.55f, 1.0f);
        return baseColor * intensity;
    }

    private static Color GetRiverPaintColor(short riverId, float carve)
    {
        Color baseColor = RiverPalette[Mathf.Abs(riverId) % RiverPalette.Length];
        float intensity = Mathf.Clamp(0.6f + (carve / 0.8f) * 0.4f, 0.6f, 1.0f);
        return baseColor * intensity;
    }

    private static Color GetClassificationColor(Cell.TerrainType type)
    {
        switch (type)
        {
            case Cell.TerrainType.Abyssal: return new Color(0.02f, 0.05f, 0.15f);
            case Cell.TerrainType.Deep: return new Color(0.05f, 0.15f, 0.4f);
            case Cell.TerrainType.Shallow: return new Color(0.15f, 0.45f, 0.75f);
            case Cell.TerrainType.Water: return new Color(0.1f, 0.35f, 0.7f);
            case Cell.TerrainType.Beach:
            case Cell.TerrainType.Shore:
            case Cell.TerrainType.Coast: return new Color(0.9f, 0.85f, 0.55f);
            case Cell.TerrainType.Land:
            case Cell.TerrainType.Plain: return new Color(0.2f, 0.65f, 0.25f);
            case Cell.TerrainType.Forest: return new Color(0.1f, 0.45f, 0.15f);
            case Cell.TerrainType.Plateau: return new Color(0.85f, 0.75f, 0.3f);
            case Cell.TerrainType.Cliff: return new Color(0.55f, 0.45f, 0.35f);
            case Cell.TerrainType.Mountain: return new Color(0.45f, 0.45f, 0.45f);
            case Cell.TerrainType.MountainPeak: return new Color(0.95f, 0.95f, 1.0f);
            case Cell.TerrainType.River: return new Color(0.0f, 0.85f, 0.95f);
            case Cell.TerrainType.WaterFall: return new Color(0.82f, 0.95f, 1f);
            case Cell.TerrainType.Lake: return new Color(0.0f, 0.55f, 0.90f);
            default: return Color.gray;
        }
    }

    private static Color GetPlateauZoneColor(PlateauZone zone)
    {
        switch (zone)
        {
            case PlateauZone.Tabletop: return new Color(0.90f, 0.78f, 0.42f);
            case PlateauZone.RockyRim: return new Color(0.52f, 0.58f, 0.48f);
            case PlateauZone.SandSlope: return new Color(0.82f, 0.65f, 0.30f);
            case PlateauZone.UpperEscarpment: return new Color(0.42f, 0.46f, 0.43f);
            case PlateauZone.LowerApron: return new Color(0.24f, 0.31f, 0.34f);
            case PlateauZone.AbyssFade: return new Color(0.04f, 0.08f, 0.13f);
            default: return Color.black;
        }
    }

    private Color ApplyRiverSurfaceColor(Color underlying, Vector2 point)
    {
        if (terrainSource == null || !terrainSource.TrySampleRiver(point, out RiverSurfaceSample river)) return underlying;
        Color gravel = Color.Lerp(SandColor1, RockColor1, 0.55f);
        Color bank = Color.Lerp(GrassColor1, gravel, Mathf.Clamp01(river.GravelWeight));
        Color surface = Color.Lerp(bank, RockColor1, Mathf.Clamp01(river.RockWeight));
        if (river.IsWater) surface *= 0.78f;
        return Color.Lerp(underlying, surface, river.Influence);
    }

    private Color ApplyRiverControl(Color underlying, Vector2 point)
    {
        if (terrainSource == null || !terrainSource.TrySampleRiver(point, out RiverSurfaceSample river)) return underlying;
        float rock = Mathf.Clamp01(river.RockWeight);
        float gravel = Mathf.Clamp01(river.GravelWeight) * (1f - rock);
        return Color.Lerp(underlying, new Color(1f - rock - gravel, gravel, rock, 1f), river.Influence);
    }

    private Color EvaluateDiagnosticPixel(TerrainSampleCache cache, int idx)
    {
        float height = cache.Heights[idx];
        float slope = cache.Slopes[idx];
        float mountainBoost = cache.MountainBoosts != null ? cache.MountainBoosts[idx] : 0f;
        float riverCarve = cache.RiverCarveDepths != null ? cache.RiverCarveDepths[idx] : 0f;
        float plateauInfluence = cache.PlateauInfluences != null ? cache.PlateauInfluences[idx] : 0f;
        Cell.TerrainType terrainType = cache.TerrainTypes[idx];
        PlateauSampleData plateauData = cache.PlateauData != null
            ? cache.PlateauData[idx]
            : default;

        TerrainAttributionData attr = cache.Attribution;
        float rawBaseHeight = attr != null ? attr.RawBaseHeights[idx] : height;
        float reliefDelta = attr != null ? attr.ReliefDeltas[idx] : 0f;
        float plateauDelta = attr != null ? attr.PlateauDeltas[idx] : 0f;
        short ridgeId = attr != null ? attr.DominantRidgeIds[idx] : (short)-1;
        short riverId = attr != null ? attr.DominantRiverIds[idx] : (short)-1;

        switch (debugViewMode)
        {
            case TerrainDebugViewMode.PaintballAttribution:
            {
                // 1. Ridge Massif (Colored distinctly per ridge ID)
                if (ridgeId >= 0 && mountainBoost > 0.005f)
                {
                    return GetRidgePaintColor(ridgeId, mountainBoost);
                }
                // 2. River Carve (Colored distinctly per river ID)
                if (riverId >= 0 && riverCarve > 0.005f)
                {
                    return GetRiverPaintColor(riverId, riverCarve);
                }
                // 3. Submerged Plateau Shelf (Bright Yellow)
                if (Mathf.Abs(plateauDelta) > 0.01f || plateauInfluence > 0.01f)
                {
                    return Color.Lerp(new Color(0.9f, 0.75f, 0.1f), new Color(1f, 0.95f, 0.3f), plateauInfluence);
                }
                // 4. Mainland micro-relief offset
                if (Mathf.Abs(reliefDelta) > 0.015f)
                {
                    return reliefDelta > 0f
                        ? Color.Lerp(new Color(0.2f, 0.35f, 0.35f), new Color(0f, 0.9f, 0.8f), Mathf.Clamp01(reliefDelta / 0.15f))
                        : Color.Lerp(new Color(0.2f, 0.35f, 0.35f), new Color(0.1f, 0.3f, 0.9f), Mathf.Clamp01(-reliefDelta / 0.15f));
                }
                // 5. Base Island / Seabed Landform
                if (height < terrainSource.Settings.waterHeight)
                {
                    return Color.Lerp(new Color(0.08f, 0.12f, 0.18f), new Color(0.15f, 0.25f, 0.35f), Mathf.Clamp01((height - terrainSource.Settings.waterHeight + 4.5f) / 4.5f));
                }
                return new Color(0.25f, 0.38f, 0.28f); // Base island mainland (slate olive)
            }

            case TerrainDebugViewMode.DeltaHeatmap:
            {
                float netDelta = height - rawBaseHeight;
                if (netDelta > 0.001f)
                {
                    float t = Mathf.Clamp01(netDelta / 3.0f);
                    return Color.Lerp(new Color(0.15f, 0.15f, 0.15f), Color.green, t);
                }
                else if (netDelta < -0.001f)
                {
                    float t = Mathf.Clamp01(-netDelta / 1.5f);
                    return Color.Lerp(new Color(0.15f, 0.15f, 0.15f), Color.red, t);
                }
                return new Color(0.15f, 0.15f, 0.15f);
            }

            case TerrainDebugViewMode.MountainBoostOnly:
            {
                float t = Mathf.Clamp01(mountainBoost / 3.5f);
                return Color.Lerp(Color.black, new Color(1f, 0.25f, 0f), t);
            }

            case TerrainDebugViewMode.RiverCarveOnly:
            {
                float t = Mathf.Clamp01(riverCarve / 1.0f);
                return Color.Lerp(Color.black, new Color(0f, 0.7f, 1f), t);
            }

            case TerrainDebugViewMode.MainlandReliefOnly:
            {
                if (reliefDelta > 0.001f)
                    return Color.Lerp(Color.black, Color.cyan, Mathf.Clamp01(reliefDelta / 0.2f));
                if (reliefDelta < -0.001f)
                    return Color.Lerp(Color.black, Color.magenta, Mathf.Clamp01(-reliefDelta / 0.2f));
                return Color.black;
            }

            case TerrainDebugViewMode.PlateauDeltaOnly:
            {
                return Color.Lerp(Color.black, Color.yellow, plateauInfluence);
            }

            case TerrainDebugViewMode.SlopeField:
            {
                float t = Mathf.Clamp01(slope / 0.75f);
                return Color.Lerp(new Color(0.05f, 0.05f, 0.15f), Color.red, t);
            }

            case TerrainDebugViewMode.SemanticClassification:
            {
                return GetClassificationColor(terrainType);
            }

            case TerrainDebugViewMode.PlateauZones:
            {
                return plateauData.IsDefined
                    ? GetPlateauZoneColor(plateauData.Zone)
                    : Color.black;
            }

            default:
                return Color.magenta;
        }
    }

    public Texture2D BuildDiagnosticTexture()
    {
        int gridSize = grid.GetLength(0);
        bool useFractionalSampling = terrainSource != null;
        TerrainSampleCache cache = useFractionalSampling
            ? terrainSource.GetOrCreateSampleCache(visualSamplesPerCell, trackAttribution: true) : null;
        int textureSize = cache != null ? cache.Resolution : gridSize;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, true);

        Color[] colorMap = new Color[textureSize * textureSize];
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        if (useFractionalSampling)
        {

            System.Threading.Tasks.Parallel.For(0, textureSize, y =>
            {
                int rowOffset = y * textureSize;
                for (int x = 0; x < textureSize; x++)
                {
                    int idx = cache.GetIndex(x, y);
                    colorMap[rowOffset + x] = EvaluateDiagnosticPixel(cache, idx);
                }
            });
        }
        else
        {
            for (int y = 0; y < textureSize; y++)
            {
                int rowOffset = y * textureSize;
                for (int x = 0; x < textureSize; x++)
                {
                    Cell.TerrainType tType = grid[x, y].currentTerrainType;
                    colorMap[rowOffset + x] = GetClassificationColor(tType);
                }
            }
        }

        texture.SetPixels(colorMap);
        texture.Apply(true, false);
        return texture;
    }

    /// <summary>
    /// The production control map for Moonlight/Terrain Splat: R grass, G sand, B rock, A seabed,
    /// as CONTINUOUS weights rather than the discrete labels BuildDiagnosticSplatMask produces.
    ///
    /// This carries only WHICH material, never how it looks - the shader gets its detail from
    /// tiled albedo and normal maps sampled at their own frequency. That is the point of the split:
    /// a baked map at 6 texels per world unit cannot hold pebbles, but it can perfectly well say
    /// "sand here", and the tiled material supplies the pebbles.
    ///
    /// Weights come from the same resolved coastal bands the geometry was built from, so material
    /// and shape cannot disagree.
    /// </summary>
    public Texture2D BuildTerrainControlMap()
    {
        return BuildMaterialMap(true);
    }

    /// <summary>
    /// The region mask for Moonlight/Terrain Splat: R is how strongly the alternate region reaches
    /// each texel. The shader tints its four layers toward the alternate colours by this.
    ///
    /// A separate single-channel texture rather than a fifth control channel, because the control
    /// map's four channels are material WEIGHTS that the shader normalises to sum to one - there is
    /// no room in them for a quantity that is not a weight. Keeping it separate also means the
    /// region can be re-authored through UpdateTerrainTexture without rebuilding the mesh.
    ///
    /// Returns null when the island has no region authored, so the shader keeps sampling "black"
    /// and the terrain renders exactly as it did before.
    /// </summary>
    public Texture2D BuildRegionMask()
    {
        if (terrainSource == null || visualSamplesPerCell < 1) return null;

        // Resolves the boundary on THIS thread, before the parallel loop below reads it.
        terrainSource.PrepareRegionField();
        if (!terrainSource.HasRegion) return null;

        int gridSize = grid.GetLength(0);
        TerrainSampleCache cache = terrainSource.GetOrCreateSampleCache(visualSamplesPerCell);
        int textureSize = cache.Resolution;

        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.R8, true, true);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[textureSize * textureSize];

        System.Threading.Tasks.Parallel.For(0, textureSize, y =>
        {
            int rowOffset = y * textureSize;
            for (int x = 0; x < textureSize; x++)
            {
                float weight = terrainSource.ResolveRegionWeight(
                    x * cache.Step - terrainSource.SampleCachePadding,
                    y * cache.Step - terrainSource.SampleCachePadding);
                pixels[rowOffset + x] = new Color(weight, 0f, 0f, 1f);
            }
        });

        texture.SetPixels(pixels);
        texture.Apply(true, false);
        return texture;
    }

    public Texture2D BuildDiagnosticSplatMask()
    {
        int gridSize = grid.GetLength(0);
        bool useFractionalSampling = terrainSource != null;
        TerrainSampleCache cache = useFractionalSampling
            ? terrainSource.GetOrCreateSampleCache(visualSamplesPerCell) : null;
        int textureSize = cache != null ? cache.Resolution : gridSize;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false, true);

        Color[] colorMap = new Color[textureSize * textureSize];
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        if (useFractionalSampling)
        {
            TerrainGenerationSettings splatSettings = terrainSource.Settings;

            System.Threading.Tasks.Parallel.For(0, textureSize, y =>
            {
                int rowOffset = y * textureSize;
                for (int x = 0; x < textureSize; x++)
                {
                    int idx = cache.GetIndex(x, y);
                    float height = cache.Heights[idx];
                    float slope = cache.Slopes[idx];
                    Cell.TerrainType terrainType = cache.TerrainTypes[idx];
                    PlateauSampleData plateauData = cache.PlateauData != null
                        ? cache.PlateauData[idx]
                        : default;

                    Color finalColor;
                    if (plateauData.IsDefined)
                    {
                        float sand = plateauData.SandWeight * (1f - plateauData.AbyssFade);
                        float rock = plateauData.RockWeight * (1f - plateauData.AbyssFade);
                        finalColor = new Color(0f, sand, rock, 1f);
                    }
                    // Read the TERRAIN TYPE the generator produced, with height only as a
                    // water-relative tiebreak.
                    //
                    // These branches used absolute literals (< -0.10 water, >= 2.0 mountain,
                    // < 0.45 beach) from when sea level was assumed to be zero. Against a -3 ocean
                    // surface the mainland sits at -1.75 and the beach crest at -2.60, so every dry
                    // sample fell into the first branch and the whole island rendered as black
                    // water - which made this diagnostic actively misleading rather than merely
                    // stale, since it reported no mainland and no beach where the grid had 566 and
                    // 190 cells of each.
                    else if (terrainType == Cell.TerrainType.Abyssal
                        || terrainType == Cell.TerrainType.Deep
                        || terrainType == Cell.TerrainType.Shallow
                        || terrainType == Cell.TerrainType.Water
                        || terrainType == Cell.TerrainType.Sea
                        || terrainType == Cell.TerrainType.Ocean
                        || height < splatSettings.waterHeight - 0.10f)
                    {
                        finalColor = new Color(0f, 0f, 0f, 1f); // Water / Submerged (Black)
                    }
                    else if (terrainType == Cell.TerrainType.Mountain
                        || terrainType == Cell.TerrainType.MountainPeak
                        || terrainType == Cell.TerrainType.Cliff
                        || terrainType == Cell.TerrainType.Rocky
                        || height >= splatSettings.cliffHeight
                        || (height >= splatSettings.waterHeight + 0.15f && slope > 0.45f))
                    {
                        finalColor = new Color(0f, 0f, 1f, 1f); // Mountain / Rock (Blue)
                    }
                    else if (terrainType == Cell.TerrainType.Beach
                        || terrainType == Cell.TerrainType.Shore
                        || terrainType == Cell.TerrainType.Coast
                        || terrainType == Cell.TerrainType.River
                        || terrainType == Cell.TerrainType.Lake)
                    {
                        finalColor = new Color(0f, 1f, 0f, 1f); // Beach / Sand (Green)
                    }
                    else
                    {
                        finalColor = new Color(1f, 0f, 0f, 1f); // Mainland / Plain (Red)
                    }

                    colorMap[rowOffset + x] = ApplyRiverControl(finalColor, new Vector2(x * cache.Step, y * cache.Step) - Vector2.one * terrainSource.SampleCachePadding);
                }
            });
        }
        else
        {
            for (int y = 0; y < textureSize; y++)
            {
                int rowOffset = y * textureSize;
                for (int x = 0; x < textureSize; x++)
                {
                    Cell.TerrainType tType = grid[x, y].currentTerrainType;
                    colorMap[rowOffset + x] = GetClassificationColor(tType);
                }
            }
        }

        texture.SetPixels(colorMap);
        texture.Apply(false);
        return texture;
    }
}
