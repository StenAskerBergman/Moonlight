using UnityEngine;

/// <summary>
/// Single source of truth for the local player's colour.
///
/// Everything that says "this is yours" - the selection ring under a unit, the
/// buoys marking its ordered destinations - reads its hue from here, so changing
/// the player's colour changes all of them together. Nothing hard-codes yellow.
///
/// Resolution order is the same one GameSession established: the config the match
/// was actually started with, falling back to the default when the Match scene was
/// entered directly and no lobby ran.
/// </summary>
public static class PlayerColors
{
    /// <summary>Used when no lobby ran. The Anno-style yellow the visuals were designed against.</summary>
    public static readonly Color DefaultPlayerColor = new Color(1f, 0.87f, 0.15f, 1f);

    /// <summary>The local player's colour for the running match.</summary>
    public static Color Current =>
        GameSession.Active != null ? GameSession.Active.playerColor : DefaultPlayerColor;

    /// <summary>Uses the concrete player's colour when ownership is known.</summary>
    public static Color For(Owner owner) => owner != null ? owner.PlayerColor : Current;

    /// <summary>
    /// The same hue held at full strength - used for the thing the unit is acting on
    /// right now (the selection ring, the active destination buoy).
    /// </summary>
    public static Color Active(float alpha = 0.95f)
    {
        Color c = Current;
        c.a = alpha;
        return c;
    }

    /// <summary>
    /// The same hue, dimmed and translucent - used for destinations still waiting in
    /// the queue, so order position reads at a glance without introducing a second hue.
    /// </summary>
    public static Color Queued(float alpha = 0.6f)
    {
        Color.RGBToHSV(Current, out float h, out float s, out float v);
        Color c = Color.HSVToRGB(h, s * 0.75f, v * 0.8f);
        c.a = alpha;
        return c;
    }

    /// <summary>
    /// Tints a selection ring (and any child graphics) to the player's colour.
    /// Uses a MaterialPropertyBlock so prefabs keep sharing one material instead of
    /// leaking an instance per selected unit.
    /// </summary>
    public static void ApplySelectionTint(Transform ringRoot)
    {
        if (ringRoot == null) return;

        Color tint = Active();

        var meshRenderers = ringRoot.GetComponentsInChildren<MeshRenderer>(true);
        if (meshRenderers.Length > 0)
        {
            var mpb = new MaterialPropertyBlock();
            foreach (var renderer in meshRenderers)
            {
                renderer.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", tint);
                mpb.SetColor("_BaseColor", tint);
                renderer.SetPropertyBlock(mpb);
            }
        }

        // Some units carry the ring as a sprite rather than a quad.
        var spriteRenderers = ringRoot.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sprite in spriteRenderers)
        {
            sprite.color = tint;
        }
    }
}
