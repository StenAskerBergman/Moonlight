using UnityEngine;

/// <summary>
/// Ground ring drawn under a selected building, matching the circle units get.
///
/// Units show theirs by enabling a hand-authored first child (see Unit.OnSelect), which
/// only works because every unit prefab was built with that child. Building prefabs have
/// no such convention, so this generates its own LineRenderer ring and sizes it from the
/// building's renderer bounds - a 2x2 depot and a 4x4 factory each get a ring that fits.
/// </summary>
[DisallowMultipleComponent]
public sealed class BuildingSelectionRing : MonoBehaviour
{
    [SerializeField, Min(8)] private int segments = 64;
    [SerializeField, Min(0f)] private float width = 0.12f;

    [Tooltip("Extra radius past the building's footprint, in world units.")]
    [SerializeField] private float padding = 0.35f;

    [Tooltip("Height above the building's base the ring sits at, to avoid z-fighting with terrain.")]
    [SerializeField] private float groundOffset = 0.05f;

    [Tooltip("Ring colour while the building is marked for demolition.")]
    [SerializeField] private Color markedColor = new Color(1f, 0.25f, 0.2f, 1f);

    private LineRenderer ring;
    private bool built;
    private bool selected;
    private bool marked;

    public void SetVisible(bool visible)
    {
        selected = visible;
        Apply();
    }

    /// <summary>
    /// Marked buildings show a red ring whether or not they are selected, so a whole
    /// demolition batch stays visible while the player keeps clicking.
    /// </summary>
    public void SetMarked(bool value)
    {
        marked = value;
        Apply();
    }

    private void Apply()
    {
        bool visible = selected || marked;
        if (visible) Build();
        if (ring == null) return;

        ring.enabled = visible;

        Color active = marked ? markedColor : PlayerColors.Active(1f);
        ring.startColor = active;
        ring.endColor = active;
    }

    /// <summary>Re-measure after the building's meshes change (construction finishing, upgrades).</summary>
    public void Refresh()
    {
        if (!built) return;
        LayoutRing();
    }

    private void Build()
    {
        if (built) return;
        built = true;

        GameObject ringObject = new GameObject("Selection Ring");
        ringObject.transform.SetParent(transform, false);

        ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.widthMultiplier = width;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.alignment = LineAlignment.TransformZ;
        ring.textureMode = LineTextureMode.Stretch;

        // Sprites/Default is already an always-included shader in this project, so the
        // ring survives a player build without extra graphics settings.
        ring.material = new Material(Shader.Find("Sprites/Default"));
        ring.startColor = PlayerColors.Active(1f);
        ring.endColor = PlayerColors.Active(1f);

        LayoutRing();
    }

    private void LayoutRing()
    {
        if (ring == null) return;

        float radius = MeasureRadius();

        // Laid out flat on the XZ plane in local space, so the ring tracks the building's
        // position and scale but not its Y rotation - a rotated building keeps a circle.
        ring.transform.localRotation = Quaternion.identity;
        ring.positionCount = segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, groundOffset, Mathf.Sin(angle) * radius));
        }
    }

    private float MeasureRadius()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool any = false;

        foreach (Renderer renderer in renderers)
        {
            // Skip the ring's own renderer, and the highlight overlays, or the ring would
            // grow a little every time it was measured.
            if (renderer is LineRenderer) continue;
            if (renderer.GetComponentInParent<BuildingSelectionRing>() != this) continue;

            if (!any) { bounds = renderer.bounds; any = true; }
            else bounds.Encapsulate(renderer.bounds);
        }

        if (!any) return 1f + padding;

        // World-space extents converted back to local, so a scaled building still fits.
        float extent = Mathf.Max(bounds.extents.x, bounds.extents.z);
        Vector3 scale = transform.lossyScale;
        float localScale = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));

        return (extent / localScale) + padding;
    }
}
