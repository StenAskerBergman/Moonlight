using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws a highlight over a building without touching what the building normally looks
/// like.
///
/// The building's own renderers and materials are never written to. Instead each source
/// renderer gets a sibling overlay renderer sharing the same mesh and transform, drawn
/// in the Transparent queue on top of it. Turning the highlight off just disables those
/// overlay objects, so nothing has to be restored and a building can never be left
/// wearing a highlight material after the relationship that caused it ends.
///
/// This is deliberately not a ScriptableRendererFeature. The project has been burned by
/// custom blit passes in this URP version before, and per-object overlay geometry needs
/// no changes to the renderer asset at all.
/// </summary>
[DisallowMultipleComponent]
public class BuildingHighlighter : MonoBehaviour
{
    [Tooltip("Optional explicit materials. Left empty, they are built from the Moonlight/Highlights shaders.")]
    [SerializeField] private Material selectedMaterialOverride;
    [SerializeField] private Material influenceMaterialOverride;

    [Header("Pulse")]
    [Tooltip("Breathe the blue selection highlight in and out. Green influence stays steady, since many buildings show it at once.")]
    [SerializeField] private bool pulseSelection = true;

    [Min(0.01f)]
    [Tooltip("Full dim-to-bright-to-dim cycles per second.")]
    [SerializeField] private float pulseSpeed = 1.2f;

    [Range(0f, 1f)]
    [Tooltip("Rim alpha at the dim end of the cycle, as a fraction of the material's own _RimAlpha.")]
    [SerializeField] private float pulseMinIntensity = 0.45f;

    [Range(0f, 1f)]
    [Tooltip("Extra outline width at the bright end, as a fraction of the material's own _OutlineWidth.")]
    [SerializeField] private float pulseWidthAmount = 0.25f;

    private static readonly int RimAlphaId = Shader.PropertyToID("_RimAlpha");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    private readonly List<Renderer> overlayRenderers = new List<Renderer>();
    private bool built;

    private MaterialPropertyBlock propertyBlock;
    private float baseRimAlpha = 0.65f;
    private float baseOutlineWidth = 0.08f;

    public BuildingHighlight State { get; private set; } = BuildingHighlight.None;

    #region Public API

    /// <summary>
    /// Sets - or clears - this building's highlight. Safe to call every frame; it exits
    /// immediately when nothing has changed.
    /// </summary>
    public void SetHighlight(BuildingHighlight highlight)
    {
        if (State == highlight && built) return;

        State = highlight;

        if (highlight == BuildingHighlight.None)
        {
            SetOverlaysActive(false);
            return;
        }

        EnsureOverlays();

        Material material = ResolveMaterial(highlight);
        if (material == null)
        {
            // Without a material there is nothing to draw, and leaving the overlay
            // enabled would render magenta over the building.
            SetOverlaysActive(false);
            return;
        }

        CachePulseBaseValues(material);
        ApplyMaterial(material);
        SetOverlaysActive(true);

        // Start bright so a fresh selection reads immediately instead of fading in from
        // the dim end of the cycle.
        if (highlight == BuildingHighlight.Selected) ApplyPulse(1f);
    }

    public void ClearHighlight() => SetHighlight(BuildingHighlight.None);

    /// <summary>
    /// Rebuilds the overlay geometry. Call after swapping the building's meshes, e.g.
    /// when construction finishes and the scaffold model is replaced by the real one.
    /// </summary>
    public void RefreshOverlays()
    {
        DestroyOverlays();
        built = false;

        if (State != BuildingHighlight.None)
        {
            BuildingHighlight current = State;
            State = BuildingHighlight.None;
            SetHighlight(current);
        }
    }

    #endregion

    private void OnDisable()
    {
        SetOverlaysActive(false);
    }

    private void Update()
    {
        if (!pulseSelection || State != BuildingHighlight.Selected) return;

        // Unscaled time so the highlight keeps breathing while the game is paused - the
        // build and selection UI stay usable then.
        float pulse01 = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * pulseSpeed * 2f * Mathf.PI);
        ApplyPulse(pulse01);
    }

    /// <summary>
    /// Drives the pulse through a MaterialPropertyBlock rather than the material itself.
    /// The highlight materials are shared across every building (see
    /// <see cref="BuildingHighlightMaterials"/>), so writing to one would pulse all of
    /// them in lockstep and defeat the point of sharing.
    /// </summary>
    private void ApplyPulse(float pulse01)
    {
        if (overlayRenderers.Count == 0) return;

        propertyBlock ??= new MaterialPropertyBlock();

        float rimAlpha = baseRimAlpha * Mathf.Lerp(pulseMinIntensity, 1f, pulse01);
        float outlineWidth = baseOutlineWidth * (1f + pulseWidthAmount * pulse01);

        foreach (Renderer overlay in overlayRenderers)
        {
            if (overlay == null) continue;

            overlay.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(RimAlphaId, rimAlpha);
            propertyBlock.SetFloat(OutlineWidthId, outlineWidth);
            overlay.SetPropertyBlock(propertyBlock);
        }
    }

    // The pulse scales the material's authored values rather than replacing them, so
    // restyling the material still drives how strong the highlight is.
    private void CachePulseBaseValues(Material material)
    {
        if (material == null) return;

        if (material.HasProperty(RimAlphaId)) baseRimAlpha = material.GetFloat(RimAlphaId);
        if (material.HasProperty(OutlineWidthId)) baseOutlineWidth = material.GetFloat(OutlineWidthId);
    }

    private void OnDestroy()
    {
        DestroyOverlays();
    }

    #region Overlay construction

    private void EnsureOverlays()
    {
        if (built) return;
        built = true;

        overlayRenderers.Clear();

        // GetComponentsInChildren picks up the overlays themselves on a rebuild, so they
        // are destroyed first and the source list is snapshotted before anything is added.
        var sources = new List<Renderer>();
        GetComponentsInChildren(true, sources);

        foreach (Renderer source in sources)
        {
            if (IsOverlay(source)) continue;

            Renderer overlay = CreateOverlayFor(source);
            if (overlay != null) overlayRenderers.Add(overlay);
        }
    }

    private Renderer CreateOverlayFor(Renderer source)
    {
        // The overlay is parented to the source renderer with an identity local
        // transform, so it tracks any animation or scaling of that part for free.
        GameObject overlayObject = new GameObject($"{source.name} Highlight Overlay");
        overlayObject.transform.SetParent(source.transform, false);
        overlayObject.layer = source.gameObject.layer;

        Renderer overlay = null;
        int subMeshCount = 1;

        if (source is SkinnedMeshRenderer skinned)
        {
            SkinnedMeshRenderer copy = overlayObject.AddComponent<SkinnedMeshRenderer>();
            copy.sharedMesh = skinned.sharedMesh;
            copy.bones = skinned.bones;
            copy.rootBone = skinned.rootBone;
            copy.localBounds = skinned.localBounds;
            copy.updateWhenOffscreen = skinned.updateWhenOffscreen;

            subMeshCount = skinned.sharedMesh != null ? skinned.sharedMesh.subMeshCount : 1;
            overlay = copy;
        }
        else if (source is MeshRenderer)
        {
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                Destroy(overlayObject);
                return null;
            }

            overlayObject.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            subMeshCount = sourceFilter.sharedMesh.subMeshCount;
            overlay = overlayObject.AddComponent<MeshRenderer>();
        }
        else
        {
            // Particles, trails, line renderers - nothing meaningful to outline.
            Destroy(overlayObject);
            return null;
        }

        overlay.shadowCastingMode = ShadowCastingMode.Off;
        overlay.receiveShadows = false;
        overlay.lightProbeUsage = LightProbeUsage.Off;
        overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
        overlay.allowOcclusionWhenDynamic = false;

        // One slot per submesh, or Unity draws only the first.
        overlay.sharedMaterials = new Material[Mathf.Max(1, subMeshCount)];
        overlay.enabled = false;
        overlayObject.SetActive(false);

        return overlay;
    }

    private static bool IsOverlay(Renderer renderer)
    {
        return renderer != null && renderer.name.EndsWith("Highlight Overlay");
    }

    private void DestroyOverlays()
    {
        foreach (Renderer overlay in overlayRenderers)
        {
            if (overlay != null) Destroy(overlay.gameObject);
        }
        overlayRenderers.Clear();
    }

    private void ApplyMaterial(Material material)
    {
        foreach (Renderer overlay in overlayRenderers)
        {
            if (overlay == null) continue;

            Material[] slots = overlay.sharedMaterials;
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            overlay.sharedMaterials = slots;
        }
    }

    private void SetOverlaysActive(bool active)
    {
        foreach (Renderer overlay in overlayRenderers)
        {
            if (overlay == null) continue;

            overlay.enabled = active;
            overlay.gameObject.SetActive(active);
        }
    }

    private Material ResolveMaterial(BuildingHighlight highlight)
    {
        switch (highlight)
        {
            case BuildingHighlight.Selected:
                return selectedMaterialOverride != null
                    ? selectedMaterialOverride
                    : BuildingHighlightMaterials.Selected;

            case BuildingHighlight.Influence:
                return influenceMaterialOverride != null
                    ? influenceMaterialOverride
                    : BuildingHighlightMaterials.Influence;

            default:
                return null;
        }
    }

    #endregion
}

/// <summary>
/// The two shared highlight materials, created once for the whole game.
///
/// They are shared rather than per building on purpose: an instance per building would
/// break SRP batching and leak a material every time a building is destroyed.
///
/// The shaders are found by name, so they must be reachable in a player build - add both
/// Moonlight/Highlights shaders to Project Settings > Graphics > Always Included Shaders,
/// or assign the material overrides on the prefab instead.
/// </summary>
public static class BuildingHighlightMaterials
{
    public const string SelectedShaderName = "Moonlight/Highlights/BuildingSelectedBlue";
    public const string InfluenceShaderName = "Moonlight/Highlights/BuildingInfluenceGreen";

    private static Material selected;
    private static Material influence;

    public static Material Selected => selected != null ? selected : (selected = Create(SelectedShaderName));
    public static Material Influence => influence != null ? influence : (influence = Create(InfluenceShaderName));

    private static Material Create(string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning($"BuildingHighlightMaterials: shader '{shaderName}' was not found. " +
                             "Add it to Always Included Shaders, or assign a material override on BuildingHighlighter.");
            return null;
        }

        return new Material(shader) { name = shaderName, hideFlags = HideFlags.DontSave };
    }
}
