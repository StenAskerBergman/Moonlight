using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds blocky stand-in geometry for buildings that have no art yet.
/// Everything is assembled from Unity primitives at runtime, so no meshes,
/// prefabs or materials need to be authored for a new building to be visible.
///
/// Every stand-in has three readable layers:
///   1. Plot  - a plate covering exactly the footprint's grid cells, with a skirt reaching
///              down so uneven terrain never shows daylight under it. Its colour is the
///              role's accent, so the grid size AND the role read from far out.
///   2. Body  - a role silhouette (shed, rig, factory, dome, ...) inset by the profile fill.
///   3. Label - building name and role on a camera-facing plate. Play mode only, and only
///              when the caller asks for it (blueprints never do: the preview re-tints every
///              MeshRenderer, which would turn the text into a solid quad).
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

    // Plot plate dimensions, in world units (one grid cell = one unit).
    private const float PlotThickness = 0.06f;
    private const float PlotSkirtDepth = 0.6f;
    private const float PlotCellInset = 0.02f;   // leaves a hairline between neighbouring plots

    // Label dimensions.
    private const float LabelGap = 0.7f;
    private const float LabelMinWidth = 2.2f;
    private const float LabelMaxWidth = 6f;
    private const float LabelAspect = 0.34f;
    private static readonly Color LabelPlateColor = new Color(0.07f, 0.08f, 0.10f, 1f);

    private static readonly Dictionary<Color, Material> MaterialCache = new Dictionary<Color, Material>();
    private static Shader _placeholderShader;

    /// <summary>
    /// Rebuilds the placeholder under <paramref name="root"/>, replacing any previous one.
    /// </summary>
    /// <param name="footprint">Footprint in world units (X and Z). Values below 0.1 are clamped.</param>
    /// <param name="labelText">
    /// Building name for the floating label, or null for no label. Ignored outside play mode so
    /// editor tools baking stand-ins into prefabs never serialise label geometry.
    /// </param>
    public static GameObject Build(
        Transform root,
        PlaceholderModelProfile profile,
        Vector2 footprint,
        FootprintAlignment alignment = FootprintAlignment.Centered,
        bool addCollider = true,
        string labelText = null)
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

        // 1. Plot: the exact grid cells this building claims.
        AddPart(placeholder.transform, PrimitiveType.Cube, "Plot",
            new Vector3(0f, (PlotThickness - PlotSkirtDepth) * 0.5f, 0f),
            new Vector3(sizeX - PlotCellInset, PlotThickness + PlotSkirtDepth, sizeZ - PlotCellInset), accent);

        // 2. Body, standing on the plot.
        Transform bodyRoot = new GameObject("Body").transform;
        bodyRoot.gameObject.layer = placeholder.layer;
        bodyRoot.SetParent(placeholder.transform, false);
        bodyRoot.localPosition = new Vector3(0f, PlotThickness, 0f);

        float top = BuildBody(bodyRoot, profile.shape, sizeX, sizeZ, fill, height, body, accent);

        if (addCollider)
        {
            BoxCollider collider = placeholder.AddComponent<BoxCollider>();
            collider.size = new Vector3(sizeX, height, sizeZ);
            collider.center = new Vector3(0f, height * 0.5f, 0f);
        }

        // 3. Label.
        if (Application.isPlaying && !string.IsNullOrWhiteSpace(labelText))
        {
            BuildLabel(placeholder.transform, labelText, profile,
                Mathf.Max(sizeX, sizeZ), PlotThickness + top + LabelGap);
        }

        return placeholder;
    }

    /// <summary>Builds the role silhouette and returns the height of its highest point.</summary>
    private static float BuildBody(Transform parent, PlaceholderShape shape, float sizeX, float sizeZ,
                                   float fill, float height, Material body, Material accent)
    {
        float innerX = sizeX * fill;
        float innerZ = sizeZ * fill;
        float minInner = Mathf.Min(innerX, innerZ);

        switch (shape)
        {
            case PlaceholderShape.Shed:
                AddPart(parent, PrimitiveType.Cube, "Body",
                    new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(innerX, height, innerZ), body);
                AddPart(parent, PrimitiveType.Cube, "Roof",
                    new Vector3(0f, height + height * 0.2f, 0f),
                    new Vector3(innerX * 1.05f, height * 0.4f, innerZ * 0.55f), accent);
                return height * 1.4f;

            case PlaceholderShape.Tower:
                AddPart(parent, PrimitiveType.Cube, "Base",
                    new Vector3(0f, height * 0.1f, 0f),
                    new Vector3(innerX, height * 0.2f, innerZ), accent);
                AddPart(parent, PrimitiveType.Cube, "Shaft",
                    new Vector3(0f, height * 0.5f + height * 0.1f, 0f),
                    new Vector3(innerX * 0.7f, height, innerZ * 0.7f), body);
                return height * 1.1f;

            case PlaceholderShape.Platform:
                AddPart(parent, PrimitiveType.Cube, "Pad",
                    new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(innerX, height, innerZ), body);
                return height;

            case PlaceholderShape.Silo:
                AddPart(parent, PrimitiveType.Cube, "Base",
                    new Vector3(0f, height * 0.05f, 0f),
                    new Vector3(innerX, height * 0.1f, innerZ), accent);
                // Unity's cylinder primitive is 2 units tall, hence the halved Y scale.
                AddPart(parent, PrimitiveType.Cylinder, "Drum",
                    new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(minInner, height * 0.5f, minInner), body);
                return height;

            case PlaceholderShape.Rig:
            {
                float legSize = Mathf.Min(sizeX, sizeZ) * 0.18f;
                float legInsetX = (innerX - legSize) * 0.5f;
                float legInsetZ = (innerZ - legSize) * 0.5f;
                float deckHeight = height * 0.25f;
                float legHeight = height - deckHeight;

                for (int i = 0; i < 4; i++)
                {
                    float x = (i == 0 || i == 3) ? -legInsetX : legInsetX;
                    float z = (i < 2) ? -legInsetZ : legInsetZ;
                    AddPart(parent, PrimitiveType.Cube, $"Leg {i}",
                        new Vector3(x, legHeight * 0.5f, z),
                        new Vector3(legSize, legHeight, legSize), accent);
                }

                AddPart(parent, PrimitiveType.Cube, "Deck",
                    new Vector3(0f, legHeight + deckHeight * 0.5f, 0f),
                    new Vector3(innerX, deckHeight, innerZ), body);
                return height;
            }

            case PlaceholderShape.Factory:
            {
                float hallHeight = height * 0.75f;
                AddPart(parent, PrimitiveType.Cube, "Hall",
                    new Vector3(0f, hallHeight * 0.5f, 0f),
                    new Vector3(innerX, hallHeight, innerZ), body);
                AddPart(parent, PrimitiveType.Cube, "Roof Band",
                    new Vector3(0f, hallHeight + height * 0.04f, 0f),
                    new Vector3(innerX * 1.02f, height * 0.08f, innerZ * 1.02f), accent);

                // Chimney in the +X/+Z corner - the one feature that says "factory" at range.
                float stackDiameter = Mathf.Max(0.2f, minInner * 0.16f);
                float stackHeight = height * 1.6f;
                AddPart(parent, PrimitiveType.Cylinder, "Chimney",
                    new Vector3(innerX * 0.5f - stackDiameter, stackHeight * 0.5f, innerZ * 0.5f - stackDiameter),
                    new Vector3(stackDiameter, stackHeight * 0.5f, stackDiameter), accent);
                return stackHeight;
            }

            case PlaceholderShape.Field:
            {
                AddPart(parent, PrimitiveType.Cube, "Bed",
                    new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(innerX, height, innerZ), accent);

                // Crop rows along X; at least three so even a 1x1 field reads as planted.
                int rows = Mathf.Max(3, Mathf.RoundToInt(innerZ / 0.45f));
                float pitch = innerZ / rows;
                float rowHeight = height * 0.7f;
                for (int i = 0; i < rows; i++)
                {
                    float z = -innerZ * 0.5f + pitch * (i + 0.5f);
                    AddPart(parent, PrimitiveType.Cube, $"Row {i}",
                        new Vector3(0f, height + rowHeight * 0.5f, z),
                        new Vector3(innerX * 0.92f, rowHeight, pitch * 0.5f), body);
                }
                return height + rowHeight;
            }

            case PlaceholderShape.Dome:
            {
                AddPart(parent, PrimitiveType.Cube, "Ring",
                    new Vector3(0f, height * 0.06f, 0f),
                    new Vector3(innerX, height * 0.12f, innerZ), accent);
                // Sphere primitive has diameter 1; centred on the plot so only the top half shows.
                AddPart(parent, PrimitiveType.Sphere, "Dome",
                    Vector3.zero,
                    new Vector3(innerX * 0.95f, height * 2f, innerZ * 0.95f), body);
                return height;
            }

            default: // Box
                AddPart(parent, PrimitiveType.Cube, "Body",
                    new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(innerX, height, innerZ), body);
                return height;
        }
    }

    /// <summary>
    /// Name plate floating over the building: dark backing, a stripe in the role colour, the
    /// building name and the role caption. Kept upright towards the camera by
    /// <see cref="PlaceholderLabelBillboard"/>.
    /// </summary>
    private static void BuildLabel(Transform parent, string labelText, PlaceholderModelProfile profile,
                                   float footprintSpan, float elevation)
    {
        float width = Mathf.Clamp(footprintSpan * 0.9f, LabelMinWidth, LabelMaxWidth);
        float plateHeight = width * LabelAspect;
        float stripeHeight = plateHeight * 0.12f;

        GameObject label = new GameObject("Label");
        label.layer = parent.gameObject.layer;
        label.transform.SetParent(parent, false);
        label.transform.localPosition = new Vector3(0f, elevation + plateHeight * 0.5f, 0f);
        label.AddComponent<PlaceholderLabelBillboard>();

        // Text faces local -Z, so the plate sits just behind it at +Z.
        DisableShadows(AddPart(label.transform, PrimitiveType.Cube, "Plate",
            new Vector3(0f, 0f, 0.03f),
            new Vector3(width, plateHeight, 0.02f), GetMaterial(LabelPlateColor)));
        DisableShadows(AddPart(label.transform, PrimitiveType.Cube, "Role Stripe",
            new Vector3(0f, -plateHeight * 0.5f + stripeHeight * 0.5f, 0.015f),
            new Vector3(width, stripeHeight, 0.01f), GetMaterial(profile.color)));

        GameObject textObject = new GameObject("Text");
        textObject.layer = label.layer;
        textObject.transform.SetParent(label.transform, false);
        textObject.transform.localPosition = new Vector3(0f, stripeHeight * 0.5f, -0.01f);

        TextMeshPro text = textObject.AddComponent<TextMeshPro>();
        text.rectTransform.sizeDelta = new Vector2(width * 0.92f, (plateHeight - stripeHeight) * 0.9f);
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.5f;
        text.fontSizeMax = 6f;
        text.color = Color.white;
        text.richText = true;
        text.text = string.IsNullOrWhiteSpace(profile.roleLabel)
            ? labelText
            : $"{labelText}\n<size=60%><color=#B8BCC6>{profile.roleLabel.ToUpperInvariant()}</color></size>";

        MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
        if (textRenderer != null)
        {
            textRenderer.shadowCastingMode = ShadowCastingMode.Off;
            textRenderer.receiveShadows = false;
        }
    }

    private static void DisableShadows(GameObject part)
    {
        MeshRenderer renderer = part != null ? part.GetComponent<MeshRenderer>() : null;
        if (renderer == null) return;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
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
