using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds photorealistic tree prefabs from the SpeedTree desktop models and points the
/// climate profile at them, replacing the hand-built primitive trees.
///
/// The prefabs are shaped for <see cref="IslandFoliagePlacer"/>, which renders foliage itself
/// through Graphics.DrawMeshInstanced: it reads LOD0 for near trees and the last LOD for distant
/// ones, and only understands MeshRenderer/MeshFilter pairs. The imported SpeedTree LOD3 is a
/// BillboardRenderer with no MeshFilter, so it is dropped here - otherwise distant trees would
/// silently render nothing.
///
/// Menu: Tools > Moonlight > Generate Photorealistic Trees
/// </summary>
public static class PhotorealTreeGenerator
{
    private const string OutputFolder = "Assets/Prefabs/Tree Prefabs/Photoreal";
    private const string LegacyFolder = "Assets/Prefabs/Tree Prefabs/Legacy";
    private const string ClimateProfilePath = "Assets/Scripts/Grid System/SOB Climate Profile.asset";

    private static readonly string[] RetiredPrefabs =
    {
        "Assets/Prefabs/Tree Prefabs/Oak Tree 1.prefab",
        "Assets/Prefabs/Tree Prefabs/Oak Tree 2.prefab",
        "Assets/Prefabs/Tree Prefabs/DarkOak Tree 1.prefab",
    };

    private struct Variant
    {
        public string Name;
        public string Model;
        /// <summary>Crown height in world units once the root scale is applied.</summary>
        public float Height;
        /// <summary>Whether the climate profile scatters this species across the island.</summary>
        public bool Temperate;
    }

    // Heights track the primitive trees they replace (2.5 - 4.0 units) so building footprints and
    // camera framing stay readable; the placer varies each instance by treeScaleMin/treeScaleMax on top.
    // The sapling entries reuse their species' meshes and materials, so the mixed-age canopy they give
    // costs no extra draw batches - the placer keys its batches on mesh + material, not on prefab.
    private static readonly Variant[] Variants =
    {
        new Variant { Name = "Oak Broadleaf", Model = "Broadleaf/Broadleaf_Desktop.spm", Height = 4.2f, Temperate = true },
        new Variant { Name = "Oak Sapling", Model = "Broadleaf/Broadleaf_Desktop.spm", Height = 2.6f, Temperate = true },
        new Variant { Name = "Pine Conifer", Model = "Conifer/Conifer_Desktop.spm", Height = 5.2f, Temperate = true },
        new Variant { Name = "Pine Sapling", Model = "Conifer/Conifer_Desktop.spm", Height = 3.0f, Temperate = true },
        new Variant { Name = "Coastal Palm", Model = "Palm/Palm_Desktop.spm", Height = 3.8f, Temperate = false },
    };

    private const string ModelRoot = "Assets/Standard Assets/Environment/SpeedTree/";

    [MenuItem("Tools/Moonlight/Generate Photorealistic Trees")]
    public static void GenerateAll()
    {
        EnsureFolder(OutputFolder);

        var temperate = new List<GameObject>();
        foreach (Variant variant in Variants)
        {
            GameObject prefab = BuildVariant(variant);
            if (prefab == null) continue;
            if (variant.Temperate) temperate.Add(prefab);
        }

        RetireLegacyPrefabs();
        AssignToClimateProfile(temperate);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Generated {Variants.Length} photorealistic tree prefabs in {OutputFolder}; " +
                  $"climate profile now scatters {temperate.Count} of them.");
    }

    private static GameObject BuildVariant(Variant variant)
    {
        string modelPath = ModelRoot + variant.Model;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
        {
            Debug.LogError($"Photoreal trees: missing SpeedTree model at {modelPath}");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = variant.Name;

        try
        {
            if (!StripBillboardAndRebuildLods(instance, out float sourceHeight))
            {
                Debug.LogError($"Photoreal trees: {variant.Name} has no mesh LODs to build from.");
                return null;
            }

            // SpeedTree pivots sit at the trunk base, so a uniform root scale is all that is needed
            // to bring a 20-30 unit forest tree down to island scale.
            float scale = sourceHeight > 0.0001f ? variant.Height / sourceHeight : 1f;
            instance.transform.localScale = Vector3.one * scale;

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            // The Tree component only drives Unity's terrain tree system, which this project does not use.
            foreach (Tree tree in instance.GetComponentsInChildren<Tree>(true))
                Object.DestroyImmediate(tree);

            string path = $"{OutputFolder}/{variant.Name}.prefab";
            return PrefabUtility.SaveAsPrefabAsset(instance, path);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    /// <summary>
    /// Drops the billboard LOD, rebuilds the LODGroup from the remaining mesh LODs and reports the
    /// unscaled height of LOD0.
    /// </summary>
    private static bool StripBillboardAndRebuildLods(GameObject instance, out float sourceHeight)
    {
        sourceHeight = 0f;
        var group = instance.GetComponentInChildren<LODGroup>();
        if (group == null) return false;

        var kept = new List<LOD>();
        foreach (LOD lod in group.GetLODs())
        {
            var meshRenderers = new List<Renderer>();
            foreach (Renderer renderer in lod.renderers)
            {
                if (renderer == null) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) meshRenderers.Add(renderer);
                else Object.DestroyImmediate(renderer.gameObject);
            }
            if (meshRenderers.Count == 0) continue;
            if (kept.Count == 0)
                sourceHeight = meshRenderers[0].GetComponent<MeshFilter>().sharedMesh.bounds.size.y;
            kept.Add(new LOD(lod.screenRelativeTransitionHeight, meshRenderers.ToArray()));
        }
        if (kept.Count == 0) return false;

        // The billboard used to carry the smallest transition height; hand it to the last mesh LOD so
        // the group still covers the full screen-size range for anything that does use LODGroup culling.
        LOD last = kept[kept.Count - 1];
        kept[kept.Count - 1] = new LOD(0.01f, last.renderers);
        group.SetLODs(kept.ToArray());
        group.RecalculateBounds();
        return sourceHeight > 0f;
    }

    /// <summary>
    /// Creates a project folder through the AssetDatabase so the move/save calls below can see it;
    /// a bare Directory.CreateDirectory leaves the folder invisible until the next import.
    /// </summary>
    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static void RetireLegacyPrefabs()
    {
        foreach (string path in RetiredPrefabs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            EnsureFolder(LegacyFolder);
            string destination = $"{LegacyFolder}/{Path.GetFileName(path)}";
            string error = AssetDatabase.MoveAsset(path, destination);
            if (!string.IsNullOrEmpty(error)) Debug.LogWarning($"Photoreal trees: could not retire {path} - {error}");
        }
    }

    private static void AssignToClimateProfile(List<GameObject> prefabs)
    {
        var profile = AssetDatabase.LoadAssetAtPath<ClimateProfile>(ClimateProfilePath);
        if (profile == null)
        {
            Debug.LogError($"Photoreal trees: missing climate profile at {ClimateProfilePath}");
            return;
        }

        var serialized = new SerializedObject(profile);
        SerializedProperty array = serialized.FindProperty("treePrefabs");
        array.arraySize = prefabs.Count;
        for (int i = 0; i < prefabs.Count; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }
}
