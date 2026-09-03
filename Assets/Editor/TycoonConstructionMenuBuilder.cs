using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Configures and wires the authentic 3-Lane Tycoon Construction Menu across all four tier pages:
/// AB.Tier 2 (Workers), AB.Tier 3 (Employees), AB.Tier 4 (Engineers), AB.Tier 5 (Executives).
///
/// Lane 1: Production (handled by ProductionSectionUI / ProductionTierPageIntegration)
/// Lane 2: Public Buildings (handled by TycoonCivicSectionUI)
/// Lane 3: Special Buildings (handled by TycoonCivicSectionUI)
/// </summary>
public static class TycoonConstructionMenuBuilder
{
    private const string SessionKey = "Moonlight.TycoonConstructionMenuBuilder.Configured";

    private const string ScenePath = "Assets/Scenes/Match.unity";
    private const string PrefabBPath = "Assets/Prefabs/Interface Prefabs/HUD Testing/User Interface (B).prefab";

    private const string IconFolder = "Assets/Imports/Anno2070/WEBP - Item Icons/";
    private const string PrefabFolder = "Assets/Prefabs/Building Prefabs/Faction Prefabs/Tycoon Faction/Generated/";

    private struct SlotDef
    {
        public string BuildingName;
        public string IconPath;
        public string PrefabPath;

        public SlotDef(string name, string icon, string prefab)
        {
            BuildingName = name;
            IconPath = icon;
            PrefabPath = prefab;
        }
    }

    private struct TierDef
    {
        public string PageName;
        public PopulationClass PopulationClass;
        public List<SlotDef> PublicSlots;
        public List<SlotDef> SpecialSlots;
    }

    private static readonly TierDef[] Tiers = new TierDef[]
    {
        // Tier 1: Tycoon Workers (AB.Tier 2)
        new TierDef
        {
            PageName = "AB.Tier 2",
            PopulationClass = PopulationClass.Workers,
            PublicSlots = new List<SlotDef>
            {
                new SlotDef("Casino", IconFolder + "Casino-icon.png", PrefabFolder + "Worker/Casino.prefab"),
                new SlotDef("City Center", IconFolder + "Tyco-ctr-icon.png", PrefabFolder + "Worker/City Center.prefab"),
                new SlotDef("Tycoon Worker Barracks", IconFolder + "Tyco-res-icon.png", PrefabFolder + "Worker/Worker Barracks.prefab")
            },
            SpecialSlots = new List<SlotDef>() // 0 slots
        },

        // Tier 2: Tycoon Employees (AB.Tier 3)
        new TierDef
        {
            PageName = "AB.Tier 3",
            PopulationClass = PopulationClass.Employees,
            PublicSlots = new List<SlotDef>
            {
                new SlotDef("Ministry of Truth", IconFolder + "Ministry-truth-icon.png", PrefabFolder + "Employee/Ministry of Truth.prefab")
            },
            SpecialSlots = new List<SlotDef>
            {
                new SlotDef("Tycoon Shipyard", IconFolder + "Tycoon-shipyard-icon.png", PrefabFolder + "Employee/Tycoon Shipyard.prefab"),
                new SlotDef("Waste Compactor", IconFolder + "Waste-comp-icon.png", PrefabFolder + "Employee/Waste Compactor.prefab")
            }
        },

        // Tier 3: Tycoon Engineers (AB.Tier 4)
        new TierDef
        {
            PageName = "AB.Tier 4",
            PopulationClass = PopulationClass.Engineers,
            PublicSlots = new List<SlotDef>
            {
                new SlotDef("Banes Avenue", IconFolder + "Banes-ave-icon.png", PrefabFolder + "Engineer/Banes Avenue.prefab"),
                new SlotDef("Financial Center", IconFolder + "Financial-ctr-icon.png", PrefabFolder + "Engineer/Financial Center.prefab")
            },
            SpecialSlots = new List<SlotDef>
            {
                new SlotDef("Deacidification Station", IconFolder + "Deacid-stn-icon.png", PrefabFolder + "Engineer/Deacidification Station.prefab")
            }
        },

        // Tier 4: Tycoon Executives (AB.Tier 5)
        new TierDef
        {
            PageName = "AB.Tier 5",
            PopulationClass = PopulationClass.Executives,
            PublicSlots = new List<SlotDef>
            {
                new SlotDef("Corporate Headquarters Foundation", IconFolder + "Congress-ctr-icon.png", PrefabFolder + "Executive/Corporate HQ Foundation.prefab")
            },
            SpecialSlots = new List<SlotDef>
            {
                new SlotDef("CO2 Reservoir", IconFolder + "Co2-res-icon.png", PrefabFolder + "Executive/CO2 Reservoir.prefab"),
                new SlotDef("Missile Launch Pad", IconFolder + "Missile-pad-icon.png", PrefabFolder + "Executive/Missile Launch Pad.prefab")
            }
        }
    };

    [InitializeOnLoadMethod]
    private static void OnDomainReload()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            ConfigureAll();
        };
    }

    [MenuItem("Moonlight/UI/Configure Tycoon 3-Lane Construction Menu")]
    public static void ConfigureAll()
    {
        Debug.Log("[TycoonConstructionMenuBuilder] Starting 3-lane construction menu configuration...");

        Scene activeScene = SceneManager.GetActiveScene();
        bool isMatch = activeScene.IsValid() && activeScene.path == ScenePath;
        if (!isMatch)
        {
            if (File.Exists(ScenePath))
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                isMatch = true;
            }
        }

        if (isMatch)
        {
            GameObject hud = GameObject.Find("HUD (Bot Building Window)");
            if (hud == null)
            {
                Debug.LogError("[TycoonConstructionMenuBuilder] 'HUD (Bot Building Window)' not found in Match scene!");
            }
            else
            {
                Transform tycoonRoot = hud.transform.Find("Faction A: Tyc");
                if (tycoonRoot == null)
                {
                    Debug.LogError("[TycoonConstructionMenuBuilder] 'Faction A: Tyc' not found under HUD!");
                }
                else
                {
                    ConfigureHierarchy(tycoonRoot);
                    EditorSceneManager.MarkSceneDirty(activeScene);
                    EditorSceneManager.SaveScene(activeScene);
                    Debug.Log("[TycoonConstructionMenuBuilder] Successfully configured and saved Match.unity!");
                }
            }
        }

        if (File.Exists(PrefabBPath))
        {
            try
            {
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabBPath);
                Transform hudInPrefab = prefabRoot.transform.Find("HUD (Bot Building Window)");
                if (hudInPrefab != null)
                {
                    Transform tycoonInPrefab = hudInPrefab.Find("Faction A: Tyc");
                    if (tycoonInPrefab != null)
                    {
                        ConfigureHierarchy(tycoonInPrefab);
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabBPath);
                        Debug.Log("[TycoonConstructionMenuBuilder] Successfully configured and saved User Interface (B).prefab!");
                    }
                }
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TycoonConstructionMenuBuilder] Could not update User Interface (B).prefab: " + ex.Message);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TycoonConstructionMenuBuilder] Configuration complete for all Tycoon tiers!");
    }

    private static void ConfigureHierarchy(Transform tycoonRoot)
    {
        foreach (var tier in Tiers)
        {
            Transform pageT = tycoonRoot.Find(tier.PageName);
            if (pageT == null)
            {
                Debug.LogWarning($"[TycoonConstructionMenuBuilder] Page '{tier.PageName}' not found under '{tycoonRoot.name}'!");
                continue;
            }

            var pageRect = (RectTransform)pageT;

            // Remove legacy Mid Row and Bot Row
            Transform midRow = pageT.Find("Mid Row");
            if (midRow != null)
            {
                Undo.DestroyObjectImmediate(midRow.gameObject);
            }

            Transform botRow = pageT.Find("Bot Row");
            if (botRow != null)
            {
                Undo.DestroyObjectImmediate(botRow.gameObject);
            }

            // Ensure Top Row is present and inactive
            Transform topRow = pageT.Find("Top Row");
            if (topRow != null)
            {
                topRow.gameObject.SetActive(false);
            }

            // Ensure Production Section
            Transform prodT = pageT.Find("Production Section");
            if (prodT == null)
            {
                var prodObj = new GameObject("Production Section", typeof(RectTransform), typeof(ProductionSectionUI));
                prodT = prodObj.transform;
                prodT.SetParent(pageRect, false);
            }
            var prodRect = (RectTransform)prodT;
            prodRect.anchorMin = new Vector2(0.5f, 0f);
            prodRect.anchorMax = new Vector2(0.5f, 0f);
            prodRect.pivot = new Vector2(0.5f, 0f);
            prodRect.anchoredPosition = new Vector2(0f, 233f);
            prodRect.sizeDelta = new Vector2(480f, 84f);

            var prodUI = prodT.GetComponent<ProductionSectionUI>();
            if (prodUI == null) prodUI = prodT.gameObject.AddComponent<ProductionSectionUI>();

            // Ensure ProductionTierPageIntegration
            var pageIntegration = pageT.GetComponent<ProductionTierPageIntegration>();
            if (pageIntegration == null)
            {
                pageIntegration = pageT.gameObject.AddComponent<ProductionTierPageIntegration>();
            }

            // Ensure Public Buildings Lane (Lane 2)
            Transform pubT = pageT.Find("Public");
            if (pubT == null)
            {
                var pubObj = new GameObject("Public", typeof(RectTransform), typeof(TycoonCivicSectionUI));
                pubT = pubObj.transform;
                pubT.SetParent(pageRect, false);
            }
            var pubRect = (RectTransform)pubT;
            pubRect.anchorMin = new Vector2(0.5f, 0f);
            pubRect.anchorMax = new Vector2(0.5f, 0f);
            pubRect.pivot = new Vector2(0.5f, 0f);
            pubRect.anchoredPosition = new Vector2(0f, 125f);
            pubRect.sizeDelta = new Vector2(480f, 84f);

            var pubUI = pubT.GetComponent<TycoonCivicSectionUI>();
            if (pubUI == null) pubUI = pubT.gameObject.AddComponent<TycoonCivicSectionUI>();
            pubUI.Setup("Public");
            pubUI.ClearSlots();
            foreach (var slotDef in tier.PublicSlots)
            {
                Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(slotDef.IconPath);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(slotDef.PrefabPath);
                if (icon == null) Debug.LogWarning($"[TycoonConstructionMenuBuilder] Missing icon: {slotDef.IconPath}");
                if (prefab == null) Debug.LogWarning($"[TycoonConstructionMenuBuilder] Missing prefab: {slotDef.PrefabPath}");
                pubUI.AddSlot(slotDef.BuildingName, icon, prefab);
            }

            // Ensure Special Buildings Lane (Lane 3)
            Transform specT = pageT.Find("Special");
            if (specT == null)
            {
                var specObj = new GameObject("Special", typeof(RectTransform), typeof(TycoonCivicSectionUI));
                specT = specObj.transform;
                specT.SetParent(pageRect, false);
            }
            var specRect = (RectTransform)specT;
            specRect.anchorMin = new Vector2(0.5f, 0f);
            specRect.anchorMax = new Vector2(0.5f, 0f);
            specRect.pivot = new Vector2(0.5f, 0f);
            specRect.anchoredPosition = new Vector2(0f, 20f);
            specRect.sizeDelta = new Vector2(480f, 84f);

            var specUI = specT.GetComponent<TycoonCivicSectionUI>();
            if (specUI == null) specUI = specT.gameObject.AddComponent<TycoonCivicSectionUI>();
            specUI.Setup("Special");
            specUI.ClearSlots();
            foreach (var slotDef in tier.SpecialSlots)
            {
                Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(slotDef.IconPath);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(slotDef.PrefabPath);
                if (icon == null) Debug.LogWarning($"[TycoonConstructionMenuBuilder] Missing icon: {slotDef.IconPath}");
                if (prefab == null) Debug.LogWarning($"[TycoonConstructionMenuBuilder] Missing prefab: {slotDef.PrefabPath}");
                specUI.AddSlot(slotDef.BuildingName, icon, prefab);
            }

            // Fix sibling ordering: Production Section (0), Top Row (1), Public (2), Special (3)
            prodT.SetSiblingIndex(0);
            if (topRow != null) topRow.SetSiblingIndex(1);
            pubT.SetSiblingIndex(2);
            specT.SetSiblingIndex(3);

            EditorUtility.SetDirty(pageT.gameObject);
            Debug.Log($"[TycoonConstructionMenuBuilder] Configured {tier.PageName} ({tier.PopulationClass}): Public={tier.PublicSlots.Count} slots, Special={tier.SpecialSlots.Count} slots.");
        }
    }
}
