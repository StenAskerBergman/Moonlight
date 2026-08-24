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

    [Header("Foliage Scattering")]
    public GameObject[] treePrefabs;
    [Range(0f, 1f)] public float forestDensity = 0.85f;
    [Range(0f, 1f)] public float plainsTreeDensity = 0.08f;
    public float treeScaleMin = 0.8f;
    public float treeScaleMax = 1.3f;
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
