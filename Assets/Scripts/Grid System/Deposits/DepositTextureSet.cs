using UnityEngine;

/// <summary>
/// Keeps the authored material maps for a resource-deposit prefab together, including
/// source maps that the active render-pipeline shader may not consume directly.
/// </summary>
public sealed class DepositTextureSet : MonoBehaviour
{
    [Header("Surface")]
    [SerializeField] private Material material;
    [SerializeField] private Texture2D albedo;
    [SerializeField] private Texture2D alternateAlbedo;
    [SerializeField] private Texture2D normal;

    [Header("Depth")]
    [SerializeField] private Texture2D height;
    [SerializeField] private Texture2D bump;
    [SerializeField] private Texture2D parallax;

    public Material Material => material;
    public Texture2D Albedo => albedo;
    public Texture2D AlternateAlbedo => alternateAlbedo;
    public Texture2D Normal => normal;
    public Texture2D Height => height;
    public Texture2D Bump => bump;
    public Texture2D Parallax => parallax;

    private void Awake()
    {
        ApplyTextures();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyTextures();
    }
#endif

    public void ApplyTextures()
    {
        if (material == null)
            return;

        if (albedo != null)
            material.SetTexture("_BaseMap", albedo);

        Texture2D normalTexture = normal != null ? normal : bump;

        if (normalTexture != null)
            material.SetTexture("_BumpMap", normalTexture);

        if (height != null)
            material.SetTexture("_HeightMap", height);

        if (parallax != null)
            material.SetTexture("_ParallaxMap", parallax);
    }
}