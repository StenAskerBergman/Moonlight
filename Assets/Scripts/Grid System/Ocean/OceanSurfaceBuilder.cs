// tldr: Builds the visible ocean after map generation - a tessellated, wave-displaced surface
// over the scene's water cube, plus the baked shoreline field and the shader globals that let
// the ocean and the beaches agree on where every coast is and when each wave arrives.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Shore-wave and swash tuning. Heights are world units relative to the rendered ocean datum.
/// The waterline itself is kept almost still (see OceanShoreEnvelope in OceanShoreCommon.hlsl):
/// the shore wave's height AT the waterline is 0.2 * 0.625 * shoreWaveHeight, ~0.03 at the
/// default, well inside CoastalProfileSettings.waterlineFreeboard. What reads as water washing
/// up the beach is the swash, drawn on the sand by the terrain shader.
/// </summary>
[Serializable]
public sealed class OceanSurfaceSettings
{
    [Tooltip("Replace the flat water cube's rendering with the tessellated shore-aware ocean. " +
             "The cube stays in the scene as the sea-level datum and for raycasts/colliders.")]
    public bool enabled = true;

    [Header("Surface mesh")]
    [Tooltip("Vertex spacing of the displaced surface. Shore waves are ~7 units long, so this " +
             "needs to stay near 1 to carry them.")]
    [Range(0.5f, 4f)] public float vertexSpacing = 1.25f;
    [Tooltip("Size of one culling tile of the surface.")]
    [Range(20f, 200f)] public float tileSize = 60f;
    [Tooltip("Distance over which all wave motion fades out toward the surface's outer edge, so " +
             "it meets the flat horizon skirt with no crack.")]
    [Range(5f, 120f)] public float edgeFadeDistance = 40f;

    [Header("Shoreline field")]
    [Tooltip("World units per texel of the baked distance-to-coast field.")]
    [Range(0.25f, 2f)] public float fieldTexelSize = 0.5f;

    [Header("Shore waves (roll in toward every coast)")]
    [Range(0f, 0.6f)] public float shoreWaveHeight = 0.22f;
    [Range(2f, 20f)] public float shoreWaveLength = 7f;
    [Tooltip("Seconds between breakers arriving at a beach.")]
    [Range(2f, 15f)] public float shoreWavePeriod = 5.5f;
    [Tooltip("How far offshore the shore-bound wave train reaches.")]
    [Range(4f, 40f)] public float shoreWaveReach = 16f;

    [Header("Swash on the beach")]
    [Tooltip("Height above the datum the swash runs up the sand at the peak of each breaker. " +
             "Clamped to stay below the beach crest.")]
    [Range(0f, 0.5f)] public float swashRunupHeight = 0.16f;
    [Tooltip("Extra height above the run-up that stays damp and dark.")]
    [Range(0f, 0.5f)] public float wetSandHeight = 0.18f;
    [Range(0f, 1.5f)] public float swashFoamStrength = 1f;
    [Tooltip("How far inland (ground distance) the swash and wet sand may reach. Keeps river " +
             "banks and inland lakes untouched.")]
    [Range(1f, 20f)] public float swashInlandReach = 6f;

    public void Validate()
    {
        vertexSpacing = Mathf.Clamp(vertexSpacing, 0.5f, 4f);
        tileSize = Mathf.Clamp(tileSize, 20f, 200f);
        edgeFadeDistance = Mathf.Clamp(edgeFadeDistance, 5f, 120f);
        fieldTexelSize = Mathf.Clamp(fieldTexelSize, 0.25f, 2f);
        shoreWaveHeight = Mathf.Clamp(shoreWaveHeight, 0f, 0.6f);
        shoreWaveLength = Mathf.Clamp(shoreWaveLength, 2f, 20f);
        shoreWavePeriod = Mathf.Clamp(shoreWavePeriod, 2f, 15f);
        shoreWaveReach = Mathf.Clamp(shoreWaveReach, 4f, 40f);
        swashRunupHeight = Mathf.Clamp(swashRunupHeight, 0f, 0.5f);
        wetSandHeight = Mathf.Clamp(wetSandHeight, 0f, 0.5f);
        swashFoamStrength = Mathf.Clamp(swashFoamStrength, 0f, 1.5f);
        swashInlandReach = Mathf.Clamp(swashInlandReach, 1f, 20f);
    }
}

public static class OceanSurfaceBuilder
{
    public const string MaterialResourcePath = "Ocean/Shore Ocean";
    public const string SurfaceObjectName = "Shore Ocean Surface";

    private static readonly int ShoreFieldId = Shader.PropertyToID("_OceanShoreField");
    private static readonly int ShoreFieldRectId = Shader.PropertyToID("_OceanShoreFieldRect");
    private static readonly int DenseRectId = Shader.PropertyToID("_OceanDenseRect");
    private static readonly int ShoreWaveId = Shader.PropertyToID("_OceanShoreWave");
    private static readonly int SwashId = Shader.PropertyToID("_OceanSwash");
    private static readonly int SurfaceParamsId = Shader.PropertyToID("_OceanSurfaceParams");
    private static readonly int SeaLevelId = Shader.PropertyToID("_OceanSeaLevel");
    private static readonly int ShoreEnabledId = Shader.PropertyToID("_OceanShoreEnabled");

    /// <summary>
    /// Builds the surface under <paramref name="parent"/> and publishes the shore globals.
    /// Returns the ocean material on success (so the horizon skirt can share it), or null when
    /// the shore ocean is disabled or unavailable - in which case the water cube keeps rendering.
    /// Every created Unity object is appended to <paramref name="ownedResources"/>.
    /// </summary>
    public static Material Build(
        Transform parent,
        Renderer waterRenderer,
        IReadOnlyList<MapGrid> chunks,
        OceanSurfaceSettings settings,
        List<Object> ownedResources)
    {
        if (settings == null || !settings.enabled || parent == null || waterRenderer == null)
        {
            Disable(waterRenderer);
            return null;
        }
        settings.Validate();

        Material material = Resources.Load<Material>(MaterialResourcePath);
        if (material == null || material.shader == null || !material.shader.isSupported)
        {
            Debug.LogWarning(
                $"OceanSurfaceBuilder: Resources/{MaterialResourcePath} is missing or its shader is not " +
                "supported here; keeping the legacy water cube.", waterRenderer);
            Disable(waterRenderer);
            return null;
        }

        // The cube is the datum: its rendered top is the sea level the terrain was built against.
        Bounds waterBounds = waterRenderer.bounds;
        float seaLevel = waterBounds.max.y;
        Rect denseRect = new Rect(waterBounds.min.x, waterBounds.min.z, waterBounds.size.x, waterBounds.size.z);

        // --- shoreline field from the generated ground ---
        var meshes = new List<(Mesh, Matrix4x4)>();
        float abyss = float.NaN;
        float beachHeadroom = float.PositiveInfinity;
        if (chunks != null)
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                MapGrid grid = chunks[i];
                if (grid == null) continue;
                MeshFilter filter = grid.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    meshes.Add((filter.sharedMesh, grid.transform.localToWorldMatrix));

                TerrainGenerationSettings terrainSettings = grid.TerrainSource != null ? grid.TerrainSource.Settings : null;
                if (terrainSettings != null)
                {
                    if (float.IsNaN(abyss)) abyss = terrainSettings.abyssHeight;
                    CoastalProfileSettings coast = terrainSettings.coastalProfile;
                    if (coast != null)
                        beachHeadroom = Mathf.Min(beachHeadroom, coast.beachCrestFreeboard - coast.waterlineFreeboard);
                }
            }
        }

        float noDataDepth = float.IsNaN(abyss) ? 50f : Mathf.Max(5f, seaLevel - abyss);
        OceanShoreFieldBaker.Result field = OceanShoreFieldBaker.Bake(
            meshes, seaLevel, noDataDepth, settings.fieldTexelSize, settings.shoreWaveReach + 10f);

        // --- the displaced surface ---
        GameObject surfaceRoot = new GameObject(SurfaceObjectName);
        surfaceRoot.layer = waterRenderer.gameObject.layer;
        surfaceRoot.transform.SetParent(parent, false);
        // Vertices are authored in world space, so the root must be the identity.
        surfaceRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        surfaceRoot.transform.localScale = Vector3.one;
        if (surfaceRoot.transform.lossyScale != Vector3.one)
        {
            Debug.LogWarning("OceanSurfaceBuilder: parent is scaled; ocean surface tiles may not line up.", parent);
        }
        BuildTiles(surfaceRoot.transform, denseRect, seaLevel, settings, material, ownedResources);

        // --- publish ---
        float runup = settings.swashRunupHeight;
        if (!float.IsPositiveInfinity(beachHeadroom))
        {
            float limit = Mathf.Max(0f, beachHeadroom * 0.6f);
            if (runup > limit)
            {
                Debug.Log($"OceanSurfaceBuilder: swash run-up {runup:F2} clamped to {limit:F2} to stay below the beach crest.");
                runup = limit;
            }
        }

        if (field.Texture != null)
        {
            ownedResources.Add(field.Texture);
            Shader.SetGlobalTexture(ShoreFieldId, field.Texture);
            Shader.SetGlobalVector(ShoreFieldRectId, new Vector4(
                field.WorldRect.xMin, field.WorldRect.yMin,
                1f / Mathf.Max(field.WorldRect.width, 1e-3f), 1f / Mathf.Max(field.WorldRect.height, 1e-3f)));
        }
        Shader.SetGlobalVector(DenseRectId, new Vector4(denseRect.xMin, denseRect.yMin, denseRect.xMax, denseRect.yMax));
        Shader.SetGlobalVector(ShoreWaveId, new Vector4(
            settings.shoreWaveHeight, settings.shoreWaveLength, settings.shoreWavePeriod, settings.shoreWaveReach));
        Shader.SetGlobalVector(SwashId, new Vector4(
            runup, settings.wetSandHeight, settings.swashFoamStrength, settings.swashInlandReach));
        Shader.SetGlobalVector(SurfaceParamsId, new Vector4(settings.edgeFadeDistance, 0f, 0f, 0f));
        Shader.SetGlobalFloat(SeaLevelId, seaLevel);
        Shader.SetGlobalFloat(ShoreEnabledId, field.Texture != null ? 1f : 0f);

        // The cube stays enabled (camera rig, underwater controller and MapManager all read its
        // renderer bounds as the sea level) but no longer draws.
        waterRenderer.forceRenderingOff = true;
        return material;
    }

    /// <summary>Returns the scene to the legacy cube ocean and switches the shore globals off.</summary>
    public static void Disable(Renderer waterRenderer)
    {
        Shader.SetGlobalFloat(ShoreEnabledId, 0f);
        if (waterRenderer != null) waterRenderer.forceRenderingOff = false;
    }

    private static void BuildTiles(
        Transform root, Rect rect, float y, OceanSurfaceSettings settings, Material material,
        List<Object> ownedResources)
    {
        int quadsX = Mathf.Max(1, Mathf.RoundToInt(rect.width / settings.vertexSpacing));
        int quadsZ = Mathf.Max(1, Mathf.RoundToInt(rect.height / settings.vertexSpacing));
        float stepX = rect.width / quadsX;
        float stepZ = rect.height / quadsZ;
        int quadsPerTileX = Mathf.Clamp(Mathf.RoundToInt(settings.tileSize / stepX), 1, 250);
        int quadsPerTileZ = Mathf.Clamp(Mathf.RoundToInt(settings.tileSize / stepZ), 1, 250);

        // Generous bounds so wave displacement never gets a tile frustum-culled while visible.
        const float displacementMargin = 2f;

        for (int tz = 0; tz * quadsPerTileZ < quadsZ; tz++)
        {
            for (int tx = 0; tx * quadsPerTileX < quadsX; tx++)
            {
                int ix0 = tx * quadsPerTileX, ix1 = Mathf.Min(quadsX, ix0 + quadsPerTileX);
                int iz0 = tz * quadsPerTileZ, iz1 = Mathf.Min(quadsZ, iz0 + quadsPerTileZ);
                int nx = ix1 - ix0 + 1;
                int nz = iz1 - iz0 + 1;

                var vertices = new Vector3[nx * nz];
                var normals = new Vector3[nx * nz];
                for (int z = 0; z < nz; z++)
                {
                    int gz = iz0 + z;
                    // Shared edges compute their coordinate from the same GLOBAL index, so
                    // neighbouring tiles hold bit-identical edge vertices: no cracks, no sparkles.
                    float wz = gz == quadsZ ? rect.yMax : rect.yMin + gz * stepZ;
                    for (int x = 0; x < nx; x++)
                    {
                        int gx = ix0 + x;
                        float wx = gx == quadsX ? rect.xMax : rect.xMin + gx * stepX;
                        vertices[z * nx + x] = new Vector3(wx, y, wz);
                        normals[z * nx + x] = Vector3.up;
                    }
                }

                var triangles = new int[(nx - 1) * (nz - 1) * 6];
                int t = 0;
                for (int z = 0; z < nz - 1; z++)
                {
                    for (int x = 0; x < nx - 1; x++)
                    {
                        int i0 = z * nx + x;
                        int i1 = i0 + 1;
                        int i2 = i0 + nx;
                        int i3 = i2 + 1;
                        triangles[t++] = i0; triangles[t++] = i2; triangles[t++] = i1;
                        triangles[t++] = i1; triangles[t++] = i2; triangles[t++] = i3;
                    }
                }

                Mesh mesh = new Mesh { name = $"Shore Ocean Tile {tx},{tz}", hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
                if (vertices.Length > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.triangles = triangles;
                mesh.RecalculateBounds();
                Bounds b = mesh.bounds;
                b.Expand(new Vector3(displacementMargin * 2f, displacementMargin * 2f, displacementMargin * 2f));
                mesh.bounds = b;
                ownedResources.Add(mesh);

                GameObject tile = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                tile.layer = root.gameObject.layer;
                tile.transform.SetParent(root, false);
                tile.GetComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = tile.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }
        }
    }
}
