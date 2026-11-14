using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Configures and styles the authentic Anno 2070 3-Lane Tycoon Construction Menu across all four tier pages:
/// AB.Tier 2 (Workers), AB.Tier 3 (Employees), AB.Tier 4 (Engineers), AB.Tier 5 (Executives).
///
/// Aesthetic upgrades applied:
/// 1. Dark tech-grid page background (Active Tier Page.png) replacing prototype pastel tints.
/// 2. Header bar with red Tycoon corner accent (House Menu Topbar RedCorner.png) and "Baumenü" title.
/// 3. Row backplates with authentic bracket accents [ ] (House Menu Row.png) on all 3 lanes.
/// 4. Beveled metallic building slots (Building Button Unlocked.png) with chevron badges.
/// 5. Dark glass shelf (Tier Bar.png) with authentic high-res character portraits and crate icon.
/// 6. Bottom faction navigation bar (Class Bar.png & Civ Bt active/passive tabs: ECOS, TYCOONS, TECHS).
/// </summary>
public static class TycoonConstructionMenuBuilder
{
    private const string SessionKey = "Moonlight.TycoonConstructionMenuBuilder.Configured";

    private const string ScenePath = "Assets/Scenes/Match.unity";
    private const string PrefabBPath = "Assets/Prefabs/Interface Prefabs/HUD Testing/User Interface (B).prefab";

    private const string IconFolder = "Assets/Imports/Anno2070/WEBP - Item Icons/";
    private const string PrefabFolder = "Assets/Prefabs/Building Prefabs/Faction Prefabs/Tycoon Faction/Generated/";
    private const string PortraitsFolder = "Assets/Imports/Anno2070/Icons/People Portraits/";

    private const string TechBgPath = "Assets/GUI/Figma/Building Menu/Tier bar/Active Tier Page.png";
    private const string HeaderBarPath = "Assets/GUI/Figma/Building Menu/House Menu Topbar RedCorner.png";
    private const string RowBackplatePath = "Assets/GUI/Figma/Building Menu/Building bar/House Menu Row.png";
    private const string SlotButtonPath = "Assets/GUI/Figma/Building Menu/Building bar/Building Button Unlocked.png";
    private const string TierShelfPath = "Assets/GUI/Figma/Building Menu/Tier Bar.png";
    private const string FactionBarPath = "Assets/GUI/Figma/Building Menu/Faction bar/Class Bar.png";
    private const string FactionActiveBtnPath = "Assets/GUI/Figma/Building Menu/Faction bar/Civ Bt active 3.png";
    private const string FactionPassiveBtnPath = "Assets/GUI/Figma/Building Menu/Faction bar/Civ Bt passive 3.png";

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

    public static HashSet<string> GetCivicLanePrefabPaths()
    {
        var paths = new HashSet<string>();

        foreach (TierDef tier in Tiers)
        {
            if (tier.PublicSlots != null)
                foreach (SlotDef slot in tier.PublicSlots) paths.Add(slot.PrefabPath);

            if (tier.SpecialSlots != null)
                foreach (SlotDef slot in tier.SpecialSlots) paths.Add(slot.PrefabPath);
        }

        return paths;
    }

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
                    ConfigureTierShelf(tycoonRoot);
                    ConfigureFactionBar(hud.transform);
                    AdjustActionBar();

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
                        ConfigureTierShelf(tycoonInPrefab);
                        ConfigureFactionBar(hudInPrefab);
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
        Debug.Log("[TycoonConstructionMenuBuilder] Visual overhaul complete for Tycoon construction menu!");
    }

    private static void ConfigureHierarchy(Transform tycoonRoot)
    {
        Sprite techBg = AssetDatabase.LoadAssetAtPath<Sprite>(TechBgPath);
        Sprite headerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HeaderBarPath);

        foreach (var tier in Tiers)
        {
            Transform pageT = tycoonRoot.Find(tier.PageName);
            if (pageT == null)
            {
                Debug.LogWarning($"[TycoonConstructionMenuBuilder] Page '{tier.PageName}' not found under '{tycoonRoot.name}'!");
                continue;
            }

            var pageRect = (RectTransform)pageT;

            // 1. Tech-grid page background
            Image pageBg = pageT.GetComponent<Image>();
            if (pageBg != null && techBg != null)
            {
                pageBg.sprite = techBg;
                pageBg.type = Image.Type.Sliced;
                pageBg.color = Color.white;
            }

            // 2. Menu Header with red Tycoon corner tab
            Transform headerT = pageT.Find("Menu Header");
            if (headerT == null)
            {
                var headerObj = new GameObject("Menu Header", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                headerT = headerObj.transform;
                headerT.SetParent(pageRect, false);
            }
            var headerRect = (RectTransform)headerT;
            headerRect.anchorMin = new Vector2(0.5f, 0f);
            headerRect.anchorMax = new Vector2(0.5f, 0f);
            headerRect.pivot = new Vector2(0.5f, 0f);
            headerRect.anchoredPosition = new Vector2(0f, 318f);
            headerRect.sizeDelta = new Vector2(480f, 32f);

            Image headerImg = headerT.GetComponent<Image>();
            if (headerImg != null && headerSprite != null)
            {
                headerImg.sprite = headerSprite;
                headerImg.type = Image.Type.Sliced;
                headerImg.color = Color.white;
            }

            // Header Title
            Transform titleT = headerT.Find("Menu Title");
            if (titleT == null)
            {
                var titleObj = new GameObject("Menu Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleT = titleObj.transform;
                titleT.SetParent(headerT, false);
            }
            var titleRect = (RectTransform)titleT;
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(38f, 0f);
            titleRect.offsetMax = new Vector2(-40f, 0f);
            var titleTmp = titleT.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "Baumenü";
            titleTmp.fontSize = 13f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = new Color(0.88f, 0.94f, 1.0f, 0.95f);
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            titleTmp.raycastTarget = false;

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

            // Ensure Production Section (Lane 1)
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

            // Fix sibling ordering: Menu Header (0), Production Section (1), Top Row (2), Public (3), Special (4)
            headerT.SetSiblingIndex(0);
            prodT.SetSiblingIndex(1);
            if (topRow != null) topRow.SetSiblingIndex(2);
            pubT.SetSiblingIndex(3);
            specT.SetSiblingIndex(4);

            EditorUtility.SetDirty(pageT.gameObject);
            Debug.Log($"[TycoonConstructionMenuBuilder] Configured {tier.PageName} ({tier.PopulationClass}): Public={tier.PublicSlots.Count} slots, Special={tier.SpecialSlots.Count} slots.");
        }
    }

    private static void ConfigureTierShelf(Transform tycoonRoot)
    {
        Transform tierManagerT = tycoonRoot.Find("Tier Button Manager");
        if (tierManagerT == null)
        {
            Debug.LogWarning("[TycoonConstructionMenuBuilder] 'Tier Button Manager' not found under tycoon root!");
            return;
        }

        // Set shelf background
        Image shelfImg = tierManagerT.GetComponent<Image>();
        Sprite shelfSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TierShelfPath);
        if (shelfImg != null && shelfSprite != null)
        {
            shelfImg.sprite = shelfSprite;
            shelfImg.type = Image.Type.Sliced;
            shelfImg.color = Color.white;
        }

        string[] portraitPaths = new string[]
        {
            PortraitsFolder + "E.V.E._60px.png",
            PortraitsFolder + "Tycoon_Worker_60px.png",
            PortraitsFolder + "Tycoon_Employee_60px.png",
            PortraitsFolder + "Tycoon_Engineer_60px.png",
            PortraitsFolder + "Tycoon_Executive_60px.png",
            IconFolder + "Tycoon_Ornamental_Buildings.png"
        };

        for (int i = 0; i < tierManagerT.childCount && i < portraitPaths.Length; i++)
        {
            Transform btnT = tierManagerT.GetChild(i);

            // Make the button background transparent so portrait rests directly on shelf
            Image btnImg = btnT.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.color = new Color(0f, 0f, 0f, 0f);
            }

            // Find Icon child
            Transform iconT = btnT.Find("Icon");
            if (iconT != null)
            {
                Image iconImg = iconT.GetComponent<Image>();
                if (iconImg != null)
                {
                    Sprite pSprite = AssetDatabase.LoadAssetAtPath<Sprite>(portraitPaths[i]);
                    if (pSprite != null)
                    {
                        iconImg.sprite = pSprite;
                        iconImg.preserveAspect = true;
                        iconImg.color = Color.white;
                    }
                }
            }
        }

        EditorUtility.SetDirty(tierManagerT.gameObject);
        Debug.Log("[TycoonConstructionMenuBuilder] Configured portrait shelf on Tier Button Manager.");
    }

    private static void ConfigureFactionBar(Transform hudRoot)
    {
        Transform factionCtrlT = hudRoot.Find("Player Faction Controller");
        if (factionCtrlT == null)
        {
            Debug.LogWarning("[TycoonConstructionMenuBuilder] 'Player Faction Controller' not found under HUD!");
            return;
        }

        // Set faction bar background
        Image barImg = factionCtrlT.GetComponent<Image>();
        Sprite barSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FactionBarPath);
        if (barImg != null && barSprite != null)
        {
            barImg.sprite = barSprite;
            barImg.type = Image.Type.Sliced;
            barImg.color = Color.white;
        }

        Sprite activeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FactionActiveBtnPath);
        Sprite passiveSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FactionPassiveBtnPath);

        // Tycoon button (A)
        Transform tycBtn = factionCtrlT.Find("Faction Button A");
        if (tycBtn != null)
        {
            tycBtn.gameObject.SetActive(true);
            Image img = tycBtn.GetComponent<Image>();
            if (img != null && activeSprite != null)
            {
                img.sprite = activeSprite;
                img.color = Color.white;
                img.type = Image.Type.Simple;
            }
            var tmp = tycBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = "TYCOONS";
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = Color.white;
            }
        }

        // Eco button (B)
        Transform ecoBtn = factionCtrlT.Find("Faction Button B");
        if (ecoBtn != null)
        {
            ecoBtn.gameObject.SetActive(true);
            Image img = ecoBtn.GetComponent<Image>();
            if (img != null && passiveSprite != null)
            {
                img.sprite = passiveSprite;
                img.color = Color.white;
                img.type = Image.Type.Simple;
            }
            var tmp = ecoBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = "ECOS";
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = new Color(0.65f, 0.90f, 0.65f);
            }
        }

        // Tech button (C)
        Transform sciBtn = factionCtrlT.Find("Faction Button C");
        if (sciBtn != null)
        {
            sciBtn.gameObject.SetActive(true);
            Image img = sciBtn.GetComponent<Image>();
            if (img != null && passiveSprite != null)
            {
                img.sprite = passiveSprite;
                img.color = Color.white;
                img.type = Image.Type.Simple;
            }
            var tmp = sciBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = "TECHS";
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = new Color(0.65f, 0.85f, 0.95f);
            }
        }

        EditorUtility.SetDirty(factionCtrlT.gameObject);
        Debug.Log("[TycoonConstructionMenuBuilder] Configured faction bar on Player Faction Controller.");
    }

    private static void AdjustActionBar()
    {
        GameObject actionbar = GameObject.Find("HUD (Bot Actionbar)");
        if (actionbar != null)
        {
            var rect = actionbar.GetComponent<RectTransform>();
            if (rect != null && rect.anchoredPosition.y > -450f)
            {
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -500f);
                EditorUtility.SetDirty(actionbar);
                Debug.Log("[TycoonConstructionMenuBuilder] Adjusted HUD (Bot Actionbar) position to prevent overlap.");
            }
        }
    }
}
