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
    PlateauZones          // Tabletop / rim / escarpment / sand opening / abyss
}

public class TextureBuilder
{
    // Shoreline sand band, in world height. Sand fades out by SandLower + SandBand.
    // Kept deliberately narrow: a wide band produced a broad flat beige apron that read as a
    // "plate rim" around every island. Keep roughly in step with the Beach height cut in
    // IslandTerrainProvider.ClassifySynthesizedIsland so texture and gameplay cells agree.
    private const float SandLower = 0.02f;
    private const float SandBand = 0.14f;

    // Depth over which the submerged shelf fades from sand to silt. See the seabed branch.
    private const float SeabedSiltDepth = 1.1f;

    // Height by which the coastal rock assist has fully faded out. See the land branch.
    private const float CoastalRockAssistHeight = 0.75f;

    private readonly Cell[,] grid;
    private readonly IslandTerrainProvider terrainSource;
    private readonly int visualSamplesPerCell;
    private readonly ClimateProfile climate;
    private readonly TerrainDebugViewMode debugViewMode;

    public TextureBuilder(Cell[,] grid, ClimateProfile climate = null, TerrainDebugViewMode debugViewMode = TerrainDebugViewMode.Normal)
    {
        this.grid = grid;
        this.visualSamplesPerCell = 1;
        this.climate = climate != null ? climate : ScriptableObject.CreateInstance<ClimateProfile>();
        this.debugViewMode = debugViewMode;
    }

    public TextureBuilder(
        Cell[,] grid,
        IslandTerrainProvider terrainSource,
        int visualSamplesPerCell,
        ClimateProfile climate = null,
        TerrainDebugViewMode debugViewMode = TerrainDebugViewMode.Normal)
    {
        this.grid = grid;
        this.terrainSource = terrainSource;
        this.visualSamplesPerCell = Mathf.Max(1, visualSamplesPerCell);
        this.climate = climate != null ? climate : ScriptableObject.CreateInstance<ClimateProfile>();
        this.debugViewMode = debugViewMode;
    }

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
        if (debugViewMode != TerrainDebugViewMode.Normal)
        {
            return BuildDiagnosticTexture();
        }

        int gridSize = grid.GetLength(0);
        bool useFractionalSampling = terrainSource != null && visualSamplesPerCell > 1;
        int textureSize = useFractionalSampling ? gridSize * visualSamplesPerCell : gridSize;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, true);

        Color[] colorMap = new Color[textureSize * textureSize];
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        if (useFractionalSampling)
        {
            TerrainSampleCache cache = terrainSource.GetOrCreateSampleCache(visualSamplesPerCell, false);
            TerrainGenerationSettings settings = terrainSource.Settings;

            System.Threading.Tasks.Parallel.For(0, textureSize, y =>
            {
                int rowOffset = y * textureSize;
                for (int x = 0; x < textureSize; x++)
                {
                    int idx = cache.GetIndex(x, y);
                    float height = cache.Heights[idx];
                    float slope = cache.Slopes[idx];
                    float mountainBoost = cache.MountainBoosts != null ? cache.MountainBoosts[idx] : 0f;
                    Cell.TerrainType terrainType = cache.TerrainTypes[idx];
                    PlateauSampleData plateauData = cache.PlateauData != null
                        ? cache.PlateauData[idx]
                        : default;

                    // Fine micro-detail noise for natural organic shading
                    float microNoise = FractalNoise(x * 2.5f, y * 2.5f, 0.08f);
                    float macroNoise = FractalNoise(x, y, 0.02f);

                    // Material colors from ClimateProfile
                    Color grass = Color.Lerp(climate.grassColor1, climate.grassColor2, microNoise);
                    Color sand = Color.Lerp(climate.sandColor1, climate.sandColor2, microNoise);
                    Color rock = Color.Lerp(climate.rockColor1, climate.rockColor2, macroNoise * 0.7f + microNoise * 0.3f);
                    Color snow = climate.snowColor;
                    Color shallowSea = climate.shallowWaterColor;
                    Color deepSea = climate.deepWaterColor;
                    Color riverWater = climate.riverColor;

                    Color finalColor;

                    if (plateauData.IsDefined)
                    {
                        finalColor = EvaluatePlateauSurfaceColor(
                            plateauData,
                            sand,
                            rock,
                            microNoise,
                            macroNoise);
                    }
                    else if ((terrainType == Cell.TerrainType.River || terrainType == Cell.TerrainType.Lake) && height <= 0.15f)
                    {
                        finalColor = Color.Lerp(sand, rock, 0.35f); // Riverbed sand/gravel
                    }
                    else
                    {
                        // Surface treatment, evaluated for EVERY height - above and below water alike.
                        //
                        // This used to be split into an `else if (height < 0)` seabed branch and an
                        // `else` land branch, each computing its own rock weighting. The two did not
                        // agree at their shared boundary: measured across 0.01 units of height at a
                        // coastal mountain flank, the rock fraction stepped 1.000 (land) -> 0.600
                        // (seabed) instantly, because the seabed branch capped rock at
                        // mountainFactor * 0.6 while the land branch could reach a full 1.0.
                        //
                        // That 40% colour step followed the h=0 contour across the 960x960 texel
                        // grid, and a hard edge crossing a pixel grid at a shallow angle stair-steps
                        // by one texel - the serrated band along the foot of coastal mountains. It
                        // survived every heightfield fix because the geometry was never at fault:
                        // height, boost and rockWeight contours all traced with zero direction
                        // reversals, and replacing the texture with a flat colour removed the teeth
                        // entirely on identical geometry.
                        //
                        // Now there is a single surface colour with no branch in it, and depth only
                        // fades that surface toward marine silt. depthT is 0 at h=0, so the result is
                        // continuous across the waterline by construction rather than by tuning.
                        //
                        // Rock weight is resolved BEFORE the ground layer, because the ground layer
                        // needs to know about it (see below).
                        //
                        // 1. Mountain Rock / Cliff blending based on mountain boost, slope, and elevation
                        // On mountain coasts, rock plunges directly into the water without an artificial sand apron.
                        //
                        // Deliberately NOT flooring rockWeight off terrainType (tried: Mathf.Max(rockWeight,
                        // 0.85f) whenever terrainType is Mountain/MountainPeak/Cliff). terrainType is a
                        // per-vertex discrete classification - a neighbor just outside that classification
                        // can have a genuinely low continuous mountainFactor/slopeFactor, so a hard floor
                        // on one side and none on the other creates a huge rockWeight jump right at the
                        // classification boundary: hard-edged dark rock blobs sitting on flat sand. Trusting
                        // the continuous fields alone keeps rockWeight smooth everywhere; they're already
                        // tuned to track the same thresholds ClassifySynthesizedIsland uses for Cliff.
                        // A ridge's outer apron decays to zero boost, so wherever a coastline happens
                        // to fall inside that apron the boost sits under the 0.43 needed for full
                        // rock and the last stretch down to the water renders as sand - a beige band
                        // wedged between the mountain and the sea. Measured at 25% of shore points
                        // that have mountain mass within ~1 world unit.
                        //
                        // So the boost needed to read as rock is relaxed as terrain approaches the
                        // waterline: a shore lying beneath a mountain is a rocky shore. This is
                        // continuous in BOTH height and boost, and it scales what is already a
                        // mountain-only signal - terrain with no boost (an ordinary beach) is
                        // untouched no matter how close to the water it is.
                        float coastalRockAssist = 1f - Mathf.Clamp01(height / CoastalRockAssistHeight);
                        coastalRockAssist = coastalRockAssist * coastalRockAssist * (3f - 2f * coastalRockAssist);
                        // Away from the shoreline, low ridge boost is a grassy
                        // foothill rather than rock. The coast assist still lets a
                        // true mountain face meet the sea without a sand collar.
                        float boostFloor = Mathf.Lerp(0.22f, 0.025f, coastalRockAssist);
                        float boostRange = Mathf.Lerp(0.50f, 0.16f, coastalRockAssist);

                        float mountainFactor = Mathf.Clamp01((mountainBoost - boostFloor) / boostRange);
                        float slopeFactor = Mathf.Clamp01((slope - 0.35f) / 0.25f);
                        float heightFactor = Mathf.Clamp01((height - 1.6f) / 1.2f);

                        float rockWeight = Mathf.Max(mountainFactor, slopeFactor, heightFactor);
                        rockWeight = rockWeight * rockWeight * (3f - 2f * rockWeight);

                        // 2. Shoreline Beach vs Inland Grass Plain.
                        //
                        // Sand is keyed on absolute height, so a mountain flank descending to the
                        // waterline passes straight through the sand band. Because sand is the base
                        // layer that rock is blended OVER, a partial rockWeight there (~0.6-0.7 on a
                        // steep flank) mixed 30-40% sand into the rock and painted a beige collar
                        // right across the foot of the massif, cutting the mountain texture off.
                        //
                        // Suppressing sand by rockWeight fixes that: where the surface reads as rock,
                        // the layer underneath it is grass rather than sand, so any partial rock blend
                        // resolves toward rock-on-grass instead of rock-on-beach. It stays fully
                        // continuous - no threshold on terrainType - so it cannot produce the hard
                        // rock/sand borders an earlier discrete floor did.
                        float beachToGrass = Mathf.Clamp01((height - SandLower) / SandBand);
                        beachToGrass = beachToGrass * beachToGrass * (3f - 2f * beachToGrass);
                        beachToGrass = Mathf.Max(beachToGrass, rockWeight);
                        Color groundColor = Color.Lerp(sand, grass, beachToGrass);

                        Color surfaceColor = Color.Lerp(groundColor, rock, rockWeight);

                        // 3. High altitude snow peak
                        if (height >= 3.8f)
                        {
                            float snowWeight = Mathf.Clamp01((height - 3.8f) / 0.8f);
                            surfaceColor = Color.Lerp(surfaceColor, snow, snowWeight);
                        }

                        // 4. Submerged: fade the SAME surface toward deep marine silt with depth.
                        //
                        // SeabedSiltDepth controls how wide the beach LOOKS. The dry sand strip above
                        // the waterline is only a fraction of a world unit, but the shallow shelf is
                        // visible straight through the water, so a slow ramp (this was 4.0) kept the
                        // shelf bright sand far out to sea and read as one continuous apron - the
                        // "plate rim" every island appeared to sit on.
                        //
                        // Because this fades the surface colour rather than replacing it, a mountain
                        // flank keeps its rock as it enters the water and simply darkens with depth,
                        // and there is no value to disagree about at h=0.
                        if (height < 0f)
                        {
                            float depthT = Mathf.Clamp01((-height) / SeabedSiltDepth);
                            Color deepSeabedSilt = Color.Lerp(sand * 0.72f, rock * 0.65f, 0.45f);
                            surfaceColor = Color.Lerp(surfaceColor, deepSeabedSilt, depthT);
                        }

                        finalColor = surfaceColor;
                    }

                    colorMap[rowOffset + x] = finalColor;
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
                    Color finalColor;
                    if (tType == Cell.TerrainType.Land || tType == Cell.TerrainType.Plain)
                        finalColor = climate.grassColor1;
                    else if (tType == Cell.TerrainType.Forest)
                        finalColor = climate.forestColor1;
                    else if (tType == Cell.TerrainType.Beach || tType == Cell.TerrainType.Shore || tType == Cell.TerrainType.Coast)
                        finalColor = climate.sandColor1;
                    else if (tType == Cell.TerrainType.Mountain || tType == Cell.TerrainType.Cliff)
                        finalColor = climate.rockColor1;
                    else if (tType == Cell.TerrainType.MountainPeak)
                        finalColor = climate.snowColor;
                    else if (tType == Cell.TerrainType.River || tType == Cell.TerrainType.Lake)
                        finalColor = Color.Lerp(climate.sandColor1, climate.rockColor1, 0.3f);
                    else
                        finalColor = Color.Lerp(climate.sandColor1 * 0.75f, climate.rockColor2 * 0.6f, 0.5f); // Natural dark seabed

                    colorMap[rowOffset + x] = finalColor;
                }
            }
        }

        texture.SetPixels(colorMap);
        texture.Apply(true);
        return texture;
    }

    private Color EvaluatePlateauSurfaceColor(
        PlateauSampleData plateau,
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
        float variation = Mathf.Clamp01(climate.plateauMaterialVariation);
        float sandWeight = plateau.SandWeight * (1f - plateau.AbyssFade);
        float rockWeight = plateau.RockWeight * (1f - plateau.AbyssFade * 0.45f);
        float materialWeight = Mathf.Max(0.0001f, sandWeight + rockWeight);
        float normalizedRock = rockWeight / materialWeight;

        Color fineSand = Color.Lerp(
            sand,
            ResolveUnderwaterColor(climate.plateauFineSandColor, sand * 0.86f),
            0.82f);
        Color coarseSand = ResolveUnderwaterColor(
            climate.plateauCoarseSandColor,
            Color.Lerp(sand, rock, 0.22f));
        Color shellSediment = ResolveUnderwaterColor(
            climate.plateauShellSedimentColor,
            sand * 1.08f);
        Color gravel = ResolveUnderwaterColor(
            climate.plateauGravelColor,
            Color.Lerp(sand, rock, 0.58f));
        Color mud = ResolveUnderwaterColor(
            climate.plateauMudColor,
            Color.Lerp(sand * 0.42f, rock * 0.48f, 0.38f));
        Color silt = ResolveUnderwaterColor(
            climate.plateauSiltColor,
            Color.Lerp(sand * 0.30f, rock * 0.36f, 0.72f));
        Color reef = ResolveUnderwaterColor(
            climate.plateauReefColor,
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
                if (height < 0f)
                {
                    return Color.Lerp(new Color(0.08f, 0.12f, 0.18f), new Color(0.15f, 0.25f, 0.35f), Mathf.Clamp01((height + 4.5f) / 4.5f));
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
        bool useFractionalSampling = terrainSource != null && visualSamplesPerCell > 1;
        int textureSize = useFractionalSampling ? gridSize * visualSamplesPerCell : gridSize;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, true);

        Color[] colorMap = new Color[textureSize * textureSize];
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        if (useFractionalSampling)
        {
            TerrainSampleCache cache = terrainSource.GetOrCreateSampleCache(visualSamplesPerCell, trackAttribution: true);

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
        texture.Apply(true);
        return texture;
    }

    public Texture2D BuildDiagnosticSplatMask()
    {
        int gridSize = grid.GetLength(0);
        bool useFractionalSampling = terrainSource != null && visualSamplesPerCell > 1;
        int textureSize = useFractionalSampling ? gridSize * visualSamplesPerCell : gridSize;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);

        Color[] colorMap = new Color[textureSize * textureSize];
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        if (useFractionalSampling)
        {
            TerrainSampleCache cache = terrainSource.GetOrCreateSampleCache(visualSamplesPerCell);

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
                    else if (height < -0.10f || ((terrainType == Cell.TerrainType.River || terrainType == Cell.TerrainType.Lake) && height <= 0.05f))
                    {
                        finalColor = new Color(0f, 0f, 0f, 1f); // Water / Submerged (Black)
                    }
                    else if (terrainType == Cell.TerrainType.Mountain
                        || terrainType == Cell.TerrainType.MountainPeak
                        || terrainType == Cell.TerrainType.Cliff
                        || height >= 2.0f
                        || (height >= 0.15f && slope > 0.45f))
                    {
                        finalColor = new Color(0f, 0f, 1f, 1f); // Mountain / Rock (Blue)
                    }
                    else if (terrainType == Cell.TerrainType.Beach
                        || terrainType == Cell.TerrainType.Shore
                        || terrainType == Cell.TerrainType.River
                        || terrainType == Cell.TerrainType.Lake
                        || height < 0.45f)
                    {
                        finalColor = new Color(0f, 1f, 0f, 1f); // Beach / Sand (Green)
                    }
                    else
                    {
                        finalColor = new Color(1f, 0f, 0f, 1f); // Mainland / Plain (Red)
                    }

                    colorMap[rowOffset + x] = finalColor;
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
