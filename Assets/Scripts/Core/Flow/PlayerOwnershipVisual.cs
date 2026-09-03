using System;
using UnityEngine;

/// <summary>
/// Gives player-owned world objects a persistent team-colour accent. Renderers whose
/// object or material names identify them as flags/banners/team accents get the full
/// colour; the remaining model receives a restrained wash so ships, buildings and
/// modular structures still read as one player's property without losing their art.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerOwnershipVisual : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float bodyTintStrength = 0.28f;
    [SerializeField, Range(0f, 1f)] private float accentTintStrength = 1f;

    private Owner owner;
    private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

    public static PlayerOwnershipVisual Ensure(GameObject target, Owner owner = null)
    {
        if (target == null) return null;

        PlayerOwnershipVisual visual = target.GetComponent<PlayerOwnershipVisual>();
        if (visual == null) visual = target.AddComponent<PlayerOwnershipVisual>();
        visual.SetOwner(owner);
        return visual;
    }

    public void SetOwner(Owner value)
    {
        owner = value;
        Refresh();
    }

    public void Refresh()
    {
        Color teamColor = PlayerColors.For(owner);

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is LineRenderer || renderer is SpriteRenderer || IsSelectionVisual(renderer.transform)) continue;

            bool accent = IsOwnershipAccent(renderer);
            float strength = accent ? accentTintStrength : bodyTintStrength;
            Color source = GetSourceColor(renderer);
            Color tint = Color.Lerp(source, teamColor, strength);
            tint.a = source.a;

            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", tint);
            properties.SetColor("_Color", tint);
            renderer.SetPropertyBlock(properties);
        }

        foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>(true))
        {
            float strength = IsOwnershipName(sprite.name) ? accentTintStrength : bodyTintStrength;
            Color tint = Color.Lerp(sprite.color, teamColor, strength);
            tint.a = sprite.color.a;
            sprite.color = tint;
        }
    }

    private static Color GetSourceColor(Renderer renderer)
    {
        Material material = renderer.sharedMaterial;
        if (material == null) return Color.white;
        if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color")) return material.GetColor("_Color");
        return Color.white;
    }

    private static bool IsOwnershipAccent(Renderer renderer)
    {
        if (IsOwnershipName(renderer.name)) return true;

        Material[] materials = renderer.sharedMaterials;
        foreach (Material material in materials)
        {
            if (material != null && IsOwnershipName(material.name)) return true;
        }

        return false;
    }

    private static bool IsOwnershipName(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return value.IndexOf("flag", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("banner", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("owner", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("team", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("accent", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsSelectionVisual(Transform candidate)
    {
        return candidate != null
            && candidate.name.IndexOf("selection", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
