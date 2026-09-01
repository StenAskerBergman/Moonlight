using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the flat, unlit, alpha blended materials used by placement overlays - the
/// vessel's influence circle, the shroud outside it, and the blueprint facing arrow.
///
/// These are all depth independent on purpose. They describe a rule rather than a piece
/// of the world, so they have to stay readable where terrain rises above the waterline
/// instead of being buried inside the island mesh.
/// </summary>
public static class OverlayMaterial
{
    public static Material Create(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Material material = new Material(shader);

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_ZTest")) material.SetFloat("_ZTest", (float)CompareFunction.Always);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)RenderQueue.Overlay;

        SetColor(material, color);
        return material;
    }

    public static void SetColor(Material material, Color color)
    {
        if (material == null) return;

        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
    }
}
