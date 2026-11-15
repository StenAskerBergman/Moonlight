using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// World-space billboard indicator that observes simulation shutdown reasons and displays
/// high-contrast status icons (No Road, Missing Input, Storage Full, Missing Workers, Unsupported, Paused).
/// Generates crisp procedural diamond icons when optional Sprite deliverables are not assigned.
/// </summary>
public class BuildingStatusBadge : MonoBehaviour
{
    [Header("Simulation Reference")]
    [SerializeField] private BuildingSimulation simulation;

    [Header("Badge Visuals")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private Vector3 overheadOffset = new Vector3(0f, 3f, 0f);

    [Header("Icon Deliverables (Optional)")]
    [SerializeField] private Sprite noRoadIcon;
    [SerializeField] private Sprite missingInputIcon;
    [SerializeField] private Sprite storageFullIcon;
    [SerializeField] private Sprite missingWorkersIcon;
    [SerializeField] private Sprite unsupportedAnchorIcon;
    [SerializeField] private Sprite pausedIcon;
    [SerializeField] private Sprite damagedIcon;

    private static readonly Dictionary<BuildingEnums.BuildingShutdownReason, Sprite> ProceduralCache =
        new Dictionary<BuildingEnums.BuildingShutdownReason, Sprite>();

    private Camera _mainCamera;

    private void Awake()
    {
        if (simulation == null)
        {
            simulation = GetComponentInParent<BuildingSimulation>();
        }

        if (iconRenderer == null)
        {
            iconRenderer = GetComponent<SpriteRenderer>();
            if (iconRenderer == null)
            {
                GameObject iconObj = new GameObject("BadgeIcon");
                iconObj.transform.SetParent(transform, false);
                iconObj.transform.localPosition = ComputeOverheadPosition();
                iconRenderer = iconObj.AddComponent<SpriteRenderer>();
                iconRenderer.sortingOrder = 50;
            }
        }

        _mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (simulation == null)
        {
            simulation = GetComponentInParent<BuildingSimulation>();
        }

        if (simulation == null) return;

        simulation.OnShutdownReasonChanged += HandleShutdownReason;
        HandleShutdownReason(simulation.CurrentShutdownReason);
    }

    private void OnDisable()
    {
        if (simulation == null) return;

        simulation.OnShutdownReasonChanged -= HandleShutdownReason;
    }

    private void LateUpdate()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera != null && iconRenderer != null && iconRenderer.enabled)
        {
            Transform badgeTransform = iconRenderer.transform;
            badgeTransform.rotation = _mainCamera.transform.rotation;

            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 3.5f) * 0.08f;
            badgeTransform.localScale = Vector3.one * (0.9f * pulse);
        }
    }

    private void HandleShutdownReason(BuildingEnums.BuildingShutdownReason reason)
    {
        if (iconRenderer == null) return;

        if (reason == BuildingEnums.BuildingShutdownReason.None ||
            reason == BuildingEnums.BuildingShutdownReason.UnderConstruction)
        {
            iconRenderer.enabled = false;
            return;
        }

        Sprite targetSprite = GetIconForReason(reason) ?? GetOrCreateProceduralIcon(reason);
        if (targetSprite != null)
        {
            iconRenderer.sprite = targetSprite;
            iconRenderer.color = Color.white;
            iconRenderer.enabled = true;
        }
        else
        {
            iconRenderer.enabled = false;
        }
    }

    private Sprite GetIconForReason(BuildingEnums.BuildingShutdownReason reason)
    {
        switch (reason)
        {
            case BuildingEnums.BuildingShutdownReason.NoRoadAccess:
                return noRoadIcon;
            case BuildingEnums.BuildingShutdownReason.MissingInput:
                return missingInputIcon;
            case BuildingEnums.BuildingShutdownReason.StorageFull:
                return storageFullIcon;
            case BuildingEnums.BuildingShutdownReason.MissingWorkers:
                return missingWorkersIcon;
            case BuildingEnums.BuildingShutdownReason.UnsupportedAnchor:
                return unsupportedAnchorIcon;
            case BuildingEnums.BuildingShutdownReason.PausedByPlayer:
                return pausedIcon;
            case BuildingEnums.BuildingShutdownReason.Damaged:
                return damagedIcon;
            default:
                return null;
        }
    }

    private Vector3 ComputeOverheadPosition()
    {
        float localTop = overheadOffset.y;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        float maxLocalY = 0f;
        bool found = false;

        foreach (Renderer rend in renderers)
        {
            if (rend == null || rend is SpriteRenderer || rend is LineRenderer || rend is ParticleSystemRenderer) continue;
            float y = transform.InverseTransformPoint(rend.bounds.max).y;
            if (!found || y > maxLocalY)
            {
                maxLocalY = y;
                found = true;
            }
        }

        if (found)
        {
            localTop = Mathf.Max(overheadOffset.y, maxLocalY + 0.85f);
        }

        return new Vector3(overheadOffset.x, localTop, overheadOffset.z);
    }

    private static Sprite GetOrCreateProceduralIcon(BuildingEnums.BuildingShutdownReason reason)
    {
        if (ProceduralCache.TryGetValue(reason, out Sprite cached) && cached != null)
        {
            return cached;
        }

        const int Size = 48;
        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color fill = GetReasonFillColor(reason);
        Color border = new Color(0.1f, 0.12f, 0.16f, 0.96f);
        Color glyph = Color.white;
        Color[] pixels = new Color[Size * Size];

        float half = (Size - 1) * 0.5f;
        float radius = half - 2f;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float nx = (x - half) / radius;
                float ny = (y - half) / radius;
                float diamond = Mathf.Abs(nx) + Mathf.Abs(ny);

                if (diamond > 1.0f)
                {
                    pixels[y * Size + x] = Color.clear;
                    continue;
                }

                Color c = diamond > 0.84f ? border : fill;
                if (IsGlyphPixel(reason, nx, ny))
                {
                    c = glyph;
                }

                // Soft AA outer edge
                float edgeAlpha = Mathf.Clamp01((1.0f - diamond) * 14f);
                c.a *= edgeAlpha;
                pixels[y * Size + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 42f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        ProceduralCache[reason] = sprite;
        return sprite;
    }

    private static Color GetReasonFillColor(BuildingEnums.BuildingShutdownReason reason)
    {
        switch (reason)
        {
            case BuildingEnums.BuildingShutdownReason.NoRoadAccess:
                return new Color(0.96f, 0.64f, 0.10f, 0.96f);
            case BuildingEnums.BuildingShutdownReason.MissingInput:
                return new Color(0.95f, 0.44f, 0.14f, 0.96f);
            case BuildingEnums.BuildingShutdownReason.StorageFull:
                return new Color(0.88f, 0.72f, 0.12f, 0.96f);
            case BuildingEnums.BuildingShutdownReason.MissingWorkers:
                return new Color(0.88f, 0.24f, 0.24f, 0.96f);
            case BuildingEnums.BuildingShutdownReason.UnsupportedAnchor:
                return new Color(0.85f, 0.20f, 0.55f, 0.96f);
            case BuildingEnums.BuildingShutdownReason.PausedByPlayer:
                return new Color(0.38f, 0.48f, 0.62f, 0.95f);
            case BuildingEnums.BuildingShutdownReason.Damaged:
                return new Color(0.92f, 0.16f, 0.16f, 0.96f);
            default:
                return Color.white;
        }
    }

    private static bool IsGlyphPixel(BuildingEnums.BuildingShutdownReason reason, float nx, float ny)
    {
        switch (reason)
        {
            case BuildingEnums.BuildingShutdownReason.NoRoadAccess:
                // Broken road bars with diagonal gap
                bool roadBars = (Mathf.Abs(nx) < 0.22f && Mathf.Abs(ny) < 0.46f && Mathf.Abs(nx) > 0.06f);
                bool centerDash = Mathf.Abs(nx) <= 0.04f && Mathf.Abs(ny) > 0.16f && Mathf.Abs(ny) < 0.38f;
                bool slash = Mathf.Abs(nx - ny) < 0.09f && Mathf.Abs(nx) < 0.36f;
                return (roadBars || centerDash) && !slash || (slash && Mathf.Abs(nx) < 0.32f && Mathf.Abs(nx - ny) < 0.055f);

            case BuildingEnums.BuildingShutdownReason.PausedByPlayer:
                return Mathf.Abs(ny) < 0.34f && Mathf.Abs(Mathf.Abs(nx) - 0.14f) < 0.07f;

            case BuildingEnums.BuildingShutdownReason.StorageFull:
                bool outer = Mathf.Abs(nx) < 0.34f && Mathf.Abs(ny) < 0.28f;
                bool inner = Mathf.Abs(nx) < 0.22f && Mathf.Abs(ny) < 0.16f;
                return outer && (!inner || Mathf.Abs(ny) < 0.04f);

            case BuildingEnums.BuildingShutdownReason.Damaged:
                return (Mathf.Abs(nx) < 0.10f && Mathf.Abs(ny) < 0.36f)
                    || (Mathf.Abs(ny) < 0.10f && Mathf.Abs(nx) < 0.36f);

            case BuildingEnums.BuildingShutdownReason.UnsupportedAnchor:
                return Mathf.Abs(Mathf.Abs(nx) - Mathf.Abs(ny)) < 0.09f && Mathf.Abs(nx) < 0.32f;

            case BuildingEnums.BuildingShutdownReason.MissingWorkers:
            case BuildingEnums.BuildingShutdownReason.MissingInput:
            default:
                // Exclamation mark
                bool bar = Mathf.Abs(nx) < 0.08f && ny > -0.06f && ny < 0.38f;
                bool dot = nx * nx + (ny + 0.26f) * (ny + 0.26f) < 0.09f * 0.09f;
                return bar || dot;
        }
    }
}
