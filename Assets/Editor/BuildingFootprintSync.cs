using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Makes every building prefab in the project agree with itself about how many grid cells it
/// covers.
///
/// A building's footprint is stated in three places that have to match: the BuildingData asset
/// the placement rules read, the BuildingProperties on the prefab, and the stand-in geometry
/// and collider under it. <see cref="TycoonBuildingCatalogGenerator"/> keeps those three in
/// step for the Tycoon catalogue because it writes all three. Everything else - the starter
/// buildings under Universal Tier 1, the Eco faction prefabs - was authored by hand, and had
/// drifted: several prefabs carried a 0x0 size (which <see cref="BuildingProperties.ResolveSize"/>
/// silently reads as a single cell) or a size that disagreed with their own BuildingData.
///
/// This pass resolves each building's footprint once and writes it back everywhere, and says
/// out loud which buildings have no footprint stated anywhere rather than letting them fall
/// through to 1x1.
/// </summary>
public static class BuildingFootprintSync
{
    /// <summary>
    /// Footprints for buildings the catalogue generator does not own, in grid cells.
    ///
    /// These are here because the building had no usable size of its own: a 0x0 on the prefab,
    /// or a 1x1 left over from before footprints were a thing, which is not a decision anyone
    /// made about the building. Sized against their catalogue counterparts - the starter
    /// residence matches Worker Barracks, the starter extractor matches Rotary Excavator - so
    /// upgrading from one to the other does not change how much room it needs.
    ///
    /// A building already carrying a deliberate footprint is NOT listed here and is left alone:
    /// City Center's 6x8 and the Depot's 2x2 (which grows a two-cell quay deck on every side)
    /// are authored numbers, not defaults.
    /// </summary>
    private static readonly Dictionary<string, Vector2Int> Footprints = new Dictionary<string, Vector2Int>
    {
        { "Worker Resident",     new Vector2Int(3, 3) },
        { "Gravel Extracter",    new Vector2Int(4, 4) },
        { "Ozone Maker station", new Vector2Int(4, 4) },
    };

    [MenuItem("Moonlight/Buildings/Sync Building Footprints")]
    public static void Sync()
    {
        List<GameObject> prefabs = AssetDatabase.FindAssets("t:Prefab")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(prefab => prefab != null && prefab.GetComponent<BuildingProperties>() != null)
            .ToList();

        int synced = 0, unchanged = 0;
        var unsized = new List<string>();

        foreach (GameObject prefab in prefabs)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            GameObject working = PrefabUtility.LoadPrefabContents(path);

            try
            {
                if (Apply(working, out bool changed, out bool hasFootprint))
                {
                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(working, path);
                        synced++;
                    }
                    else unchanged++;
                }

                if (!hasFootprint) unsized.Add(prefab.name);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(working);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Building footprints: {prefabs.Count} building prefab(s) checked, " +
                  $"{synced} rewritten, {unchanged} already in step.");

        if (unsized.Count > 0)
        {
            Debug.LogWarning("These buildings state no footprint anywhere and fall back to a " +
                             "single cell. Give them one on their BuildingData, or add them to " +
                             $"BuildingFootprintSync.Footprints: {string.Join(", ", unsized)}");
        }

        ReportBuildingsOutsideThePipeline();
    }

    /// <summary>
    /// Writes the resolved footprint onto the prefab. Returns false when the prefab turned out
    /// not to be a building after all.
    /// </summary>
    private static bool Apply(GameObject working, out bool changed, out bool hasFootprint)
    {
        changed = false;
        hasFootprint = false;

        BuildingProperties properties = working.GetComponent<BuildingProperties>();
        if (properties == null) return false;

        BuildingData data = properties.buildingData;
        Vector3 footprint = Resolve(properties, data, working.name, out hasFootprint);

        // The BuildingData wins at runtime - BuildingProperties.SetData copies it over the
        // prefab's field in Start - so an override has to land on the asset too, or it would
        // survive exactly until the building is placed.
        if (data != null && data.buildingSize != footprint)
        {
            data.buildingSize = footprint;
            EditorUtility.SetDirty(data);
            changed = true;
        }

        if (properties.buildingSize != footprint)
        {
            properties.buildingSize = footprint;
            changed = true;
        }

        // A building with no art of its own needs the stand-in, and the stand-in needs to be
        // built at the footprint rather than at the component's default 2x2.
        if (PlaceholderModelFactory.NeedsPlaceholder(working))
        {
            BuildingPlaceholderModel placeholder = working.GetComponent<BuildingPlaceholderModel>();
            if (placeholder == null)
            {
                placeholder = working.AddComponent<BuildingPlaceholderModel>();
                changed = true;
            }

            var so = new SerializedObject(placeholder);
            SerializedProperty fallback = so.FindProperty("fallbackFootprint");
            var asFootprint = new Vector2(footprint.x, footprint.z);
            if (fallback != null && fallback.vector2Value != asFootprint)
            {
                fallback.vector2Value = asFootprint;
                so.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            // Rebuild only when the stand-in is actually the wrong size. Rebuilding regardless
            // would work, but it recreates the primitives - and their file ids - on every run,
            // so every building prefab in the project would show up as changed each time.
            if (!StandInMatches(working, asFootprint))
            {
                placeholder.Build();
                changed = true;
            }
        }

        return true;
    }

    /// <summary>
    /// True when the generated stand-in already covers this footprint. Its collider is sized
    /// to the full footprint (the visible parts are inset by the profile's fill), so that is
    /// the one part of it worth comparing.
    /// </summary>
    private static bool StandInMatches(GameObject working, Vector2 footprint)
    {
        Transform root = working.transform.Find(PlaceholderModelFactory.PlaceholderRootName);
        if (root == null) return false;

        BoxCollider collider = root.GetComponent<BoxCollider>();
        if (collider == null) return false;

        return Mathf.Approximately(collider.size.x, footprint.x) &&
               Mathf.Approximately(collider.size.z, footprint.y);
    }

    private static Vector3 Resolve(BuildingProperties properties, BuildingData data, string prefabName,
                                   out bool hasFootprint)
    {
        // An explicit override outranks a stale 1x1 that nobody chose.
        if (Footprints.TryGetValue(prefabName, out Vector2Int listed) ||
            (data != null && Footprints.TryGetValue(data.buildingName ?? string.Empty, out listed)))
        {
            hasFootprint = true;
            return new Vector3(listed.x, 1, listed.y);
        }

        if (data != null && data.buildingSize != Vector3.zero)
        {
            hasFootprint = true;
            return WithUnitHeight(data.buildingSize);
        }

        if (properties != null && properties.buildingSize != Vector3.zero)
        {
            hasFootprint = true;
            return WithUnitHeight(properties.buildingSize);
        }

        hasFootprint = false;
        return Vector3.one;
    }

    /// <summary>
    /// Only X and Z are read as cells; Y is along for the ride. Normalising it to 1 keeps the
    /// odd authored 2 or 0 from reading as a claim about height that nothing honours.
    /// </summary>
    private static Vector3 WithUnitHeight(Vector3 size)
    {
        return new Vector3(size.x, 1f, size.z);
    }

    /// <summary>
    /// Building prefabs that carry no BuildingProperties at all. They cannot be placed, sized
    /// or costed by any of this, so a footprint is not what they are missing - naming them is
    /// more useful than quietly skipping them.
    /// </summary>
    private static void ReportBuildingsOutsideThePipeline()
    {
        List<string> orphans = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Building Prefabs" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(prefab => prefab != null && prefab.GetComponent<BuildingProperties>() == null)
            .Select(prefab => prefab.name)
            .ToList();

        if (orphans.Count == 0) return;

        Debug.Log("Building prefabs with no BuildingProperties - not placeable, so not sized " +
                  $"either: {string.Join(", ", orphans)}");
    }
}
