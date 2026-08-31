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
}
