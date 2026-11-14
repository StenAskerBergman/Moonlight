using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates FoliageSpecies assets for the generated tree prefabs and points the climate profile at
/// them, migrating it off the legacy ClimateProfile.treePrefabs array.
///
/// Densities are copied straight from the profile's legacy forestDensity/plainsTreeDensity, so a
/// migrated profile scatters at the same rate it did before. Species selection then becomes a
/// weighted pick instead of a uniform one, which changes WHICH tree lands on a given cell but not
/// HOW MANY - expect the same density with a different arrangement on the next regeneration.
///
/// Menu: Tools > Moonlight > Generate Foliage Species
/// </summary>
public static class FoliageSpeciesGenerator
{
    private const string OutputFolder = "Assets/Data/Foliage";
    private const string PrefabFolder = "Assets/Prefabs/Tree Prefabs/Photoreal";
    private const string ClimateProfilePath = "Assets/Scripts/Grid System/SOB Climate Profile.asset";

    private struct Entry
    {
        public string Prefab;
        public FoliageKind Kind;
        public float ScatterWeight;
        public int WoodYield;
        public float FellSeconds;
        public float RegrowSeconds;
        /// <summary>Whether the climate profile scatters this species across the island.</summary>
        public bool Temperate;
    }

    // Saplings carry less wood and regrow faster, so a logger working a mixed stand naturally takes
    // the mature trees first once forestry starts choosing by yield.
    private static readonly Entry[] Entries =
    {
        new Entry { Prefab = "Oak Broadleaf", Kind = FoliageKind.Tree, ScatterWeight = 1.0f, WoodYield = 3, FellSeconds = 6f, RegrowSeconds = 150f, Temperate = true },
        new Entry { Prefab = "Oak Sapling",   Kind = FoliageKind.Tree, ScatterWeight = 1.2f, WoodYield = 1, FellSeconds = 3f, RegrowSeconds = 90f,  Temperate = true },
        new Entry { Prefab = "Pine Conifer",  Kind = FoliageKind.Tree, ScatterWeight = 1.0f, WoodYield = 3, FellSeconds = 6f, RegrowSeconds = 150f, Temperate = true },
        new Entry { Prefab = "Pine Sapling",  Kind = FoliageKind.Tree, ScatterWeight = 1.2f, WoodYield = 1, FellSeconds = 3f, RegrowSeconds = 90f,  Temperate = true },
        new Entry { Prefab = "Coastal Palm",  Kind = FoliageKind.Tree, ScatterWeight = 1.0f, WoodYield = 2, FellSeconds = 5f, RegrowSeconds = 120f, Temperate = false },
    };

    [MenuItem("Tools/Moonlight/Generate Foliage Species")]
    public static void GenerateAll()
    {
        var profile = AssetDatabase.LoadAssetAtPath<ClimateProfile>(ClimateProfilePath);
        if (profile == null)
        {
            Debug.LogError($"Foliage species: missing climate profile at {ClimateProfilePath}");
            return;
        }

        EnsureFolder(OutputFolder);

        var temperate = new List<FoliageSpecies>();
        foreach (Entry entry in Entries)
        {
            FoliageSpecies species = BuildSpecies(entry, profile);
            if (species == null) continue;
            if (entry.Temperate) temperate.Add(species);
        }

        var serialized = new SerializedObject(profile);
        SerializedProperty array = serialized.FindProperty("foliageSpecies");
        array.arraySize = temperate.Count;
        for (int i = 0; i < temperate.Count; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = temperate[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Generated {Entries.Length} foliage species in {OutputFolder}; " +
                  $"climate profile now scatters {temperate.Count} of them.");
    }

    private static FoliageSpecies BuildSpecies(Entry entry, ClimateProfile profile)
    {
        string prefabPath = $"{PrefabFolder}/{entry.Prefab}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Foliage species: missing prefab at {prefabPath}");
            return null;
        }

        string assetPath = $"{OutputFolder}/{entry.Prefab}.asset";
        var species = AssetDatabase.LoadAssetAtPath<FoliageSpecies>(assetPath);
        bool created = species == null;
        if (created) species = ScriptableObject.CreateInstance<FoliageSpecies>();

        species.displayName = entry.Prefab;
        species.kind = entry.Kind;
        species.prefab = prefab;
        species.scatterWeight = entry.ScatterWeight;
        species.slopeMax = 0.4f;
        species.shoreDistanceMin = 3f;
        species.scaleMin = profile.treeScaleMin;
        species.scaleMax = profile.treeScaleMax;
        species.harvestable = true;
        species.woodYield = entry.WoodYield;
        species.fellSeconds = entry.FellSeconds;
        species.regrowSeconds = entry.RegrowSeconds;
        species.playerPlantable = true;
        species.pickRadius = 0.6f;
        // Carried over verbatim from the legacy profile fields so migration does not change density.
        species.terrainAffinity = new[]
        {
            new FoliageSpecies.TerrainDensity { terrain = Cell.TerrainType.Forest, density = profile.forestDensity },
            new FoliageSpecies.TerrainDensity { terrain = Cell.TerrainType.Land, density = profile.plainsTreeDensity },
            new FoliageSpecies.TerrainDensity { terrain = Cell.TerrainType.Plain, density = profile.plainsTreeDensity },
        };

        if (created) AssetDatabase.CreateAsset(species, assetPath);
        else EditorUtility.SetDirty(species);
        return species;
    }

    /// <summary>
    /// Creates a project folder through the AssetDatabase; a bare Directory.CreateDirectory leaves
    /// the folder invisible to CreateAsset until the next import.
    /// </summary>
    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
