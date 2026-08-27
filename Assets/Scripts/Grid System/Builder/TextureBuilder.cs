using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    public TextureBuilder(Cell[,] grid, ClimateProfile climate = null)
    {
        this.grid = grid;
        this.visualSamplesPerCell = 1;
        this.climate = climate != null ? climate : ScriptableObject.CreateInstance<ClimateProfile>();
    }

    public TextureBuilder(
        Cell[,] grid,
        IslandTerrainProvider terrainSource,
        int visualSamplesPerCell,
        ClimateProfile climate = null)
    {
        this.grid = grid;
        this.terrainSource = terrainSource;
        this.visualSamplesPerCell = Mathf.Max(1, visualSamplesPerCell);
        this.climate = climate != null ? climate : ScriptableObject.CreateInstance<ClimateProfile>();
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
        int gridSize = grid.GetLength(0);
        bool useFractionalSampling = terrainSource != null && visualSamplesPerCell > 1;
        int textureSize = useFractionalSampling ? gridSize * visualSamplesPerCell : gridSize;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, true);

        Color[] colorMap = new Color[textureSize * textureSize];
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        if (useFractionalSampling)
        {
            TerrainSampleCache cache = terrainSource.GetOrCreateSampleCache(visualSamplesPerCell);
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

                    if (terrainType == Cell.TerrainType.River && height <= 0.15f)
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
                        float boostFloor = Mathf.Lerp(0.08f, 0.015f, coastalRockAssist);
                        float boostRange = Mathf.Lerp(0.35f, 0.13f, coastalRockAssist);

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
                    else if (tType == Cell.TerrainType.River)
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

                    Color finalColor;
                    if (height < -0.10f || (terrainType == Cell.TerrainType.River && height <= 0.05f))
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
                    Color finalColor;
                    Cell.TerrainType tType = grid[x, y].currentTerrainType;
                    if (tType == Cell.TerrainType.Land || tType == Cell.TerrainType.Plain || tType == Cell.TerrainType.Hill || tType == Cell.TerrainType.Forest)
                        finalColor = new Color(1f, 0f, 0f, 1f);
                    else if (tType == Cell.TerrainType.Beach || tType == Cell.TerrainType.Shore || tType == Cell.TerrainType.Coast || tType == Cell.TerrainType.Desert)
                        finalColor = new Color(0f, 1f, 0f, 1f);
                    else if (tType == Cell.TerrainType.Mountain || tType == Cell.TerrainType.Cliff || tType == Cell.TerrainType.Rocky)
                        finalColor = new Color(0f, 0f, 1f, 1f);
                    else
                        finalColor = new Color(0f, 0f, 0f, 1f);

                    colorMap[rowOffset + x] = finalColor;
                }
            }
        }

        texture.SetPixels(colorMap);
        texture.Apply(false);
        return texture;
    }
}