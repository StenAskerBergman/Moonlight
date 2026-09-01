using UnityEngine;

[CreateAssetMenu(fileName = "New Climate Profile", menuName = "Terrain/Climate Profile")]
public class ClimateProfile : ScriptableObject
{
    [Header("Grass / Land")]
    public Color grassColor1 = new Color(0.3f, 0.45f, 0.15f);
    public Color grassColor2 = new Color(0.35f, 0.5f, 0.18f);

    [Header("Forest / Dirt")]
    public Color forestColor1 = new Color(0.2f, 0.35f, 0.15f);
    public Color forestColor2 = new Color(0.25f, 0.4f, 0.18f);

    [Header("Beach / Gravel")]
    public Color sandColor1 = new Color(0.75f, 0.75f, 0.7f); // Paler, greyish gravel sand
    public Color sandColor2 = new Color(0.7f, 0.7f, 0.65f);

    [Header("Mountain / Rock")]
    public Color rockColor1 = new Color(0.45f, 0.45f, 0.45f); // Slate grey
    public Color rockColor2 = new Color(0.35f, 0.35f, 0.35f);
    public Color snowColor = new Color(0.95f, 0.95f, 0.98f); // High altitude snow

    [Header("Water")]
    public Color riverColor = new Color(0.22f, 0.52f, 0.90f, 1f);
    public Color shallowWaterColor = new Color(0.38f, 0.74f, 0.95f, 1f);
    public Color deepWaterColor = new Color(0.06f, 0.14f, 0.45f, 1f);

    [Header("Underwater Plateau Surface")]
    [Tooltip("Fine sediment covering the buildable plateau tabletop.")]
    public Color plateauFineSandColor = new Color(0.62f, 0.59f, 0.47f, 1f);
    [Tooltip("Darker, larger-grained sand used in broad tabletop patches and sand descents.")]
    public Color plateauCoarseSandColor = new Color(0.46f, 0.43f, 0.34f, 1f);
    [Tooltip("Pale shell and limestone fragments scattered through clean sand.")]
    public Color plateauShellSedimentColor = new Color(0.76f, 0.73f, 0.61f, 1f);
    [Tooltip("Loose gravel concentrated where sand meets the rocky rim.")]
    public Color plateauGravelColor = new Color(0.34f, 0.38f, 0.37f, 1f);
    [Tooltip("Soft organic mud collected in protected tabletop pockets.")]
    public Color plateauMudColor = new Color(0.20f, 0.23f, 0.20f, 1f);
    [Tooltip("Deep marine silt covering the lower apron and abyss transition.")]
    public Color plateauSiltColor = new Color(0.11f, 0.15f, 0.17f, 1f);
    [Tooltip("Algae and reef growth tint applied around exposed rock.")]
    public Color plateauReefColor = new Color(0.13f, 0.28f, 0.22f, 1f);
    [Tooltip("How strongly secondary sand, shell, gravel, mud, and reef patches break up the base surface.")]
    [Range(0f, 1f)] public float plateauMaterialVariation = 0.78f;

    [Header("Foliage Scattering")]
    public GameObject[] treePrefabs;
    [Range(0f, 1f)] public float forestDensity = 0.85f;
    [Range(0f, 1f)] public float plainsTreeDensity = 0.08f;
    public float treeScaleMin = 0.8f;
    public float treeScaleMax = 1.3f;

    [Header("Underwater Plateau Life")]
    [Tooltip("Decorates every standalone underwater plateau during terrain generation.")]
    public bool populateUnderwaterPlateaus = true;
    [Tooltip("Optional rooted seabed prefabs such as kelp, coral, or sea grass. Procedural kelp is used when empty.")]
    public GameObject[] underwaterFoliagePrefabs;
    [Tooltip("Optional decorative swimming prefabs. Procedural fish are used when empty.")]
    public GameObject[] underwaterWildlifePrefabs;
    [Range(0f, 1f)] public float underwaterFoliageDensity = 0.16f;
    [Range(0f, 1f)] public float lushBillboardPlantRatio = 0.45f;
    [Min(0)] public int minimumFoliagePerPlateau = 12;
    [Min(0)] public int wildlifePerPlateau = 5;
    public Vector2 underwaterFoliageScale = new Vector2(0.7f, 1.35f);
    public Vector2 underwaterWildlifeScale = new Vector2(0.55f, 1.1f);
    [Min(0f)] public float wildlifeHeightAboveSeabed = 1.2f;
    [Header("Splatmap Generation (Textures)")]
    [Tooltip("Offset from surfaceFlatlandHeight. Negative pulls grass up the slope.")]
    public float splatGrassThresholdOffset = -0.05f;
    [Tooltip("Offset from waterHeight. Negative pushes sand deeper underwater.")]
    public float splatSandThresholdOffset = -0.2f;
    [Tooltip("Frequency of the jagged coastline noise.")]
    [Range(0.01f, 1f)] public float splatNoiseFrequency = 0.1f;
    [Tooltip("Amplitude of the sand boundary noise.")]
    [Range(0f, 1f)] public float splatSandNoiseAmplitude = 0.2f;
    [Tooltip("Amplitude of the grass boundary noise (keep small to avoid puddles).")]
    [Range(0f, 1f)] public float splatGrassNoiseRestriction = 0.04f;
}
