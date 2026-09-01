using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds blocky stand-in geometry for buildings that have no art yet.
/// Everything is assembled from Unity primitives at runtime, so no meshes,
/// prefabs or materials need to be authored for a new building to be visible.
/// </summary>
public static class PlaceholderModelFactory
{
    /// <summary>
    /// Name of the generated root. Distinct from any hand-authored "Model - Placeholder"
    /// child so regenerating never deletes geometry somebody built by hand.
    /// </summary>
    public const string PlaceholderRootName = "Model - Generated Placeholder";

    /// <summary>Where the footprint sits relative to the building's pivot.</summary>
    public enum FootprintAlignment
    {
        Centered,  // Pivot in the middle of the footprint (matches the existing prefabs).
        MinCorner  // Pivot at the -X/-Z corner (matches how grid cells are reserved).
    }

    private static readonly Dictionary<Color, Material> MaterialCache = new Dictionary<Color, Material>();
    private static Shader _placeholderShader;

    /// <summary>
    /// Rebuilds the placeholder under <paramref name="root"/>, replacing any previous one.
    /// </summary>
    /// <param name="footprint">Footprint in world units (X and Z). Values below 0.1 are clamped.</param>
    public static GameObject Build(
        Transform root,
        PlaceholderModelProfile profile,
        Vector2 footprint,
        FootprintAlignment alignment = FootprintAlignment.Centered,
        bool addCollider = true)
    {
        if (root == null) return null;

        profile = profile ?? new PlaceholderModelProfile();
        Clear(root);

        float sizeX = Mathf.Max(0.1f, footprint.x);
        float sizeZ = Mathf.Max(0.1f, footprint.y);
        float fill = Mathf.Clamp(profile.footprintFill, 0.1f, 1f);
        float height = Mathf.Max(0.05f, profile.height);

        GameObject placeholder = new GameObject(PlaceholderRootName);
        placeholder.layer = root.gameObject.layer;
        placeholder.transform.SetParent(root, false);
        placeholder.transform.localRotation = Quaternion.identity;
        placeholder.transform.localScale = Vector3.one;
        placeholder.transform.localPosition = alignment == FootprintAlignment.MinCorner
            ? new Vector3(sizeX * 0.5f, 0f, sizeZ * 0.5f)
            : Vector3.zero;

        Material body = GetMaterial(profile.color);
        Material accent = GetMaterial(profile.accentColor);

        switch (profile.shape)
        {
            case PlaceholderShape.Shed:
                AddPart(placeholder.transform, PrimitiveType.Cube, "Body",
                    new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(sizeX * fill, height, sizeZ * fill), body);
                AddPart(placeholder.transform, PrimitiveType.Cube, "Roof",
                    new Vector3(0f, height + height * 0.2f, 0f),
                    new Vector3(sizeX * fill * 1.05f, height * 0.4f, sizeZ * fill * 0.55f), accent);
                break;

            case PlaceholderShape.Tower:
                AddPart(placeholder.transform, PrimitiveType.Cube, "Base",
                    new Vector3(0f, height * 0.1f, 0f),
                    new Vector3(sizeX * fill, height * 0.2f, sizeZ * fill), accent);
                AddPart(placeholder.transform, PrimitiveType.Cube, "Shaft",
                    new Vector3(0f, height * 0.5f + height * 0.1f, 0f),
                    new Vector3(sizeX * fill * 0.7f, height, sizeZ * fill * 0.7f), body);
                break;

            case PlaceholderShape.Platform:
                AddPart(placeholder.transform, PrimitiveType.Cube, "Pad",
                    new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(sizeX * fill, height, sizeZ * fill), body);
                break;

            case PlaceholderShape.Silo:
                AddPart(placeholder.transform, PrimitiveType.Cube, "Base",
                    new Vector3(0f, height * 0.05f, 0f),
                    new Vector3(sizeX * fill, height * 0.1f, sizeZ * fill), accent);
                // Unity's cylinder primitive is 2 units tall, hence the halved Y scale.
                AddPart(placeholder.transform, PrimitiveType.Cylinder, "Drum",
                    new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(Mathf.Min(sizeX, sizeZ) * fill, height * 0.5f, Mathf.Min(sizeX, sizeZ) * fill), body);
                break;

            case PlaceholderShape.Rig:
            {
                float legSize = Mathf.Min(sizeX, sizeZ) * 0.18f;
                float legInsetX = (sizeX * fill - legSize) * 0.5f;
                float legInsetZ = (sizeZ * fill - legSize) * 0.5f;
                float deckHeight = height * 0.25f;
                float legHeight = height - deckHeight;

                for (int i = 0; i < 4; i++)
                {
                    float x = (i == 0 || i == 3) ? -legInsetX : legInsetX;
                    float z = (i < 2) ? -legInsetZ : legInsetZ;
                    AddPart(placeholder.transform, PrimitiveType.Cube, $"Leg {i}",
                        new Vector3(x, legHeight * 0.5f, z),
                        new Vector3(legSize, legHeight, legSize), accent);
                }

                AddPart(placeholder.transform, PrimitiveType.Cube, "Deck",
                    new Vector3(0f, legHeight + deckHeight * 0.5f, 0f),
                    new Vector3(sizeX * fill, deckHeight, sizeZ * fill), body);
                break;
            }

            default: // Box
                AddPart(placeholder.transform, PrimitiveType.Cube, "Body",
                    new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(sizeX * fill, height, sizeZ * fill), body);
                break;
        }

        if (addCollider)
        {
            BoxCollider collider = placeholder.AddComponent<BoxCollider>();
            collider.size = new Vector3(sizeX, height, sizeZ);
            collider.center = new Vector3(0f, height * 0.5f, 0f);
        }

        return placeholder;
    }

    /// <summary>Removes a previously generated placeholder from <paramref name="root"/>.</summary>
    public static void Clear(Transform root)
    {
        if (root == null) return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child.name != PlaceholderRootName) continue;

            if (Application.isPlaying) Object.Destroy(child.gameObject);
            else Object.DestroyImmediate(child.gameObject);
        }
    }

    /// <summary>
    /// True when the object has no visible geometry of its own - i.e. it still needs a placeholder.
    /// An existing generated placeholder does not count as real art.
    /// </summary>
    public static bool NeedsPlaceholder(GameObject root)
    {
        if (root == null) return false;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer) continue;
            if (IsInsidePlaceholder(renderer.transform)) continue;
            return false;
        }

        return true;
    }

    private static bool IsInsidePlaceholder(Transform transform)
    {
        for (Transform t = transform; t != null; t = t.parent)
        {
            if (t.name == PlaceholderRootName) return true;
        }
        return false;
    }

    private static GameObject AddPart(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.layer = parent.gameObject.layer;

        // Primitives ship with a collider; the placeholder root carries a single box instead
        // so the stand-in never changes the building's collision shape part by part.
        Collider primitiveCollider = part.GetComponent<Collider>();
        if (primitiveCollider != null)
        {
            if (Application.isPlaying) Object.Destroy(primitiveCollider);
            else Object.DestroyImmediate(primitiveCollider);
        }

        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        MeshRenderer renderer = part.GetComponent<MeshRenderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }

        return part;
    }

    /// <summary>Shared material per colour, so a hundred placeholders are not a hundred materials.</summary>
    private static Material GetMaterial(Color color)
    {
        if (MaterialCache.TryGetValue(color, out Material cached) && cached != null)
        {
            return cached;
        }

        if (_placeholderShader == null)
        {
            // URP first; the Standard fallback keeps this usable if the pipeline ever changes.
            _placeholderShader = Shader.Find("Universal Render Pipeline/Lit");
            if (_placeholderShader == null) _placeholderShader = Shader.Find("Standard");
        }

        if (_placeholderShader == null)
        {
            AssetFallback.LogMissingDeliverable("Shader", "Universal Render Pipeline/Lit");
            return null;
        }

        Material material = new Material(_placeholderShader)
        {
            name = $"Placeholder ({ColorUtility.ToHtmlStringRGB(color)})",
            hideFlags = HideFlags.HideAndDontSave
        };

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);

        MaterialCache[color] = material;
        return material;
    }
}
