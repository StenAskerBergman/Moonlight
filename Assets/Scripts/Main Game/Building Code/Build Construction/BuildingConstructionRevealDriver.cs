using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives Anno-style ground-up holographic/solid construction reveal and temporary corner
/// scaffold pylons for a building in ConstructionSite, restoring original shared materials cleanly
/// once finished.
/// </summary>
[DisallowMultipleComponent]
public class BuildingConstructionRevealDriver : MonoBehaviour
{
    private const string RevealShaderName = "Moonlight/Placement/BuildingConstructionReveal";

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BuildProgressId = Shader.PropertyToID("_BuildProgress");
    private static readonly int BoundsMinYId = Shader.PropertyToID("_BoundsMinY");
    private static readonly int BoundsMaxYId = Shader.PropertyToID("_BoundsMaxY");

    private static Material sharedRevealMaterial;
    private static Material sharedScaffoldMaterial;

    private struct RendererSnapshot
    {
        public Renderer Renderer;
        public Material[] OriginalSharedMaterials;
        public Color[] OriginalColors;
        public Texture[] OriginalTextures;
    }

    private readonly List<RendererSnapshot> snapshots = new List<RendererSnapshot>();
    private MaterialPropertyBlock propertyBlock;
    private GameObject scaffoldRoot;
    private float minY;
    private float maxY;
    private bool revealActive;

    public void BeginReveal()
    {
        if (revealActive)
        {
            SetProgress(0f);
            return;
        }

        Material revealMat = GetSharedRevealMaterial();
        if (revealMat == null) return;

        snapshots.Clear();
        bool hasBounds = false;
        Bounds combinedBounds = default;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer rend in renderers)
        {
            if (IsExcludedRenderer(rend)) continue;

            Material[] origMats = rend.sharedMaterials;
            if (origMats == null || origMats.Length == 0) continue;

            Color[] origColors = new Color[origMats.Length];
            Texture[] origTextures = new Texture[origMats.Length];
            Material[] replacementMats = new Material[origMats.Length];

            for (int i = 0; i < origMats.Length; i++)
            {
                Material src = origMats[i];
                origColors[i] = ExtractColor(src);
                origTextures[i] = ExtractTexture(src);
                replacementMats[i] = revealMat;
            }

            snapshots.Add(new RendererSnapshot
            {
                Renderer = rend,
                OriginalSharedMaterials = origMats,
                OriginalColors = origColors,
                OriginalTextures = origTextures
            });

            if (!hasBounds)
            {
                combinedBounds = rend.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(rend.bounds);
            }

            rend.sharedMaterials = replacementMats;
        }

        if (!hasBounds)
        {
            snapshots.Clear();
            return;
        }

        minY = combinedBounds.min.y;
        maxY = Mathf.Max(minY + 0.5f, combinedBounds.max.y);

        SpawnCornerScaffoldPylons(combinedBounds);

        revealActive = true;
        SetProgress(0f);
    }

    public void SetProgress(float progress)
    {
        if (!revealActive) return;

        float clamped = Mathf.Clamp01(progress);
        propertyBlock ??= new MaterialPropertyBlock();

        for (int s = 0; s < snapshots.Count; s++)
        {
            RendererSnapshot snap = snapshots[s];
            if (snap.Renderer == null) continue;

            int count = snap.OriginalColors.Length;
            for (int i = 0; i < count; i++)
            {
                propertyBlock.Clear();
                propertyBlock.SetColor(BaseColorId, snap.OriginalColors[i]);
                if (snap.OriginalTextures[i] != null)
                {
                    propertyBlock.SetTexture(BaseMapId, snap.OriginalTextures[i]);
                }
                propertyBlock.SetFloat(BuildProgressId, clamped);
                propertyBlock.SetFloat(BoundsMinYId, minY);
                propertyBlock.SetFloat(BoundsMaxYId, maxY);
                snap.Renderer.SetPropertyBlock(propertyBlock, i);
            }
        }

        if (scaffoldRoot != null)
        {
            // Gently retract scaffold poles during the final 15% of construction.
            float scaleY = clamped < 0.85f ? 1f : Mathf.Clamp01(1f - (clamped - 0.85f) / 0.15f);
            scaffoldRoot.transform.localScale = new Vector3(1f, Mathf.Max(0.01f, scaleY), 1f);
        }
    }

    public void EndReveal()
    {
        if (scaffoldRoot != null)
        {
            Destroy(scaffoldRoot);
            scaffoldRoot = null;
        }

        if (!revealActive) return;

        for (int s = 0; s < snapshots.Count; s++)
        {
            RendererSnapshot snap = snapshots[s];
            if (snap.Renderer == null) continue;

            int count = snap.OriginalSharedMaterials.Length;
            for (int i = 0; i < count; i++)
            {
                snap.Renderer.SetPropertyBlock(null, i);
            }
            snap.Renderer.SetPropertyBlock(null);
            snap.Renderer.sharedMaterials = snap.OriginalSharedMaterials;
        }

        snapshots.Clear();
        revealActive = false;
    }

    private void OnDisable()
    {
        EndReveal();
    }

    private void OnDestroy()
    {
        EndReveal();
    }

    private void SpawnCornerScaffoldPylons(Bounds worldBounds)
    {
        if (scaffoldRoot != null) Destroy(scaffoldRoot);

        scaffoldRoot = new GameObject("ConstructionScaffoldFrame");
        scaffoldRoot.transform.SetParent(transform, true);
        scaffoldRoot.transform.position = new Vector3(worldBounds.center.x, worldBounds.min.y, worldBounds.center.z);
        scaffoldRoot.transform.rotation = Quaternion.identity;

        float halfX = Mathf.Max(0.35f, worldBounds.extents.x + 0.08f);
        float halfZ = Mathf.Max(0.35f, worldBounds.extents.z + 0.08f);
        float height = Mathf.Clamp(worldBounds.size.y * 0.9f, 0.8f, 3.6f);

        Material mat = GetSharedScaffoldMaterial();

        Vector3[] offsets =
        {
            new Vector3(-halfX, height * 0.5f, -halfZ),
            new Vector3( halfX, height * 0.5f, -halfZ),
            new Vector3(-halfX, height * 0.5f,  halfZ),
            new Vector3( halfX, height * 0.5f,  halfZ),
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = $"ScaffoldPylon_{i}";
            post.transform.SetParent(scaffoldRoot.transform, false);
            post.transform.localPosition = offsets[i];
            post.transform.localScale = new Vector3(0.06f, height, 0.06f);

            Collider col = post.GetComponent<Collider>();
            if (col != null) Destroy(col);

            MeshRenderer mr = post.GetComponent<MeshRenderer>();
            if (mr != null && mat != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }
    }

    private static bool IsExcludedRenderer(Renderer rend)
    {
        if (rend == null) return true;
        if (rend is LineRenderer || rend is SpriteRenderer || rend is ParticleSystemRenderer) return true;

        Transform current = rend.transform;
        while (current != null)
        {
            if (current.GetComponent<BuildingSelectionRing>() != null
                || current.GetComponent<BuildingStatusBadge>() != null
                || current.GetComponent<PlaceholderLabelBillboard>() != null)
            {
                return true;
            }

            string n = current.name;
            if (n.IndexOf("Scaffold", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Overlay", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("SelectionRing", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Badge", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static Color ExtractColor(Material mat)
    {
        if (mat == null) return Color.white;
        if (mat.HasProperty(BaseColorId)) return mat.GetColor(BaseColorId);
        if (mat.HasProperty("_Color")) return mat.GetColor("_Color");
        return Color.white;
    }

    private static Texture ExtractTexture(Material mat)
    {
        if (mat == null) return null;
        if (mat.HasProperty(BaseMapId)) return mat.GetTexture(BaseMapId);
        if (mat.HasProperty("_MainTex")) return mat.GetTexture("_MainTex");
        return null;
    }

    private static Material GetSharedRevealMaterial()
    {
        if (sharedRevealMaterial != null) return sharedRevealMaterial;

        Shader shader = Shader.Find(RevealShaderName);
        if (shader == null) return null;

        sharedRevealMaterial = new Material(shader)
        {
            name = "SharedConstructionReveal",
            hideFlags = HideFlags.HideAndDontSave
        };
        return sharedRevealMaterial;
    }

    private static Material GetSharedScaffoldMaterial()
    {
        if (sharedScaffoldMaterial != null) return sharedScaffoldMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;

        sharedScaffoldMaterial = new Material(shader)
        {
            name = "SharedScaffoldPylon",
            hideFlags = HideFlags.HideAndDontSave
        };
        Color steel = new Color(0.82f, 0.62f, 0.16f, 1f);
        if (sharedScaffoldMaterial.HasProperty(BaseColorId)) sharedScaffoldMaterial.SetColor(BaseColorId, steel);
        if (sharedScaffoldMaterial.HasProperty("_Color")) sharedScaffoldMaterial.SetColor("_Color", steel);
        return sharedScaffoldMaterial;
    }
}
