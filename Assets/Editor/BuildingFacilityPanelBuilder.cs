using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor utility to construct the complete Building Facility GUI Panel prefab
/// and instantiate it under the Match scene's Canvas.
/// Menu: Tools > Moonlight > Build Building Facility Panel Prefab
/// </summary>
public static class BuildingFacilityPanelBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/UI";
    private const string PrefabPath = PrefabFolder + "/Building Facility Panel.prefab";

    private static TMP_FontAsset fontAsset;

    [MenuItem("Tools/Moonlight/Build Building Facility Panel Prefab")]
    public static void BuildPrefab()
    {
        // First ensure assets exist
        BuildingFacilityAssetGenerator.GenerateAll();

        fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        GameObject root = new GameObject("Building Facility Panel", typeof(RectTransform), typeof(BuildingFacilityPanelUI));
        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = new Vector2(-20f, 20f);
            rootRect.sizeDelta = new Vector2(380f, 240f);

            BuildPanelContent(root);

            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory("Assets/Resources/UI");
            AssetDatabase.Refresh();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/UI/Building Facility Panel.prefab");
            AssetDatabase.SaveAssets();

            Debug.Log($"Successfully built Building Facility Panel prefab at: {PrefabPath} and Assets/Resources/UI/");

            // Optionally install in the active scene if Match is open
            InstallInActiveScene(savedPrefab);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [MenuItem("Tools/Moonlight/Wire All Building Prefabs with Facility Info")]
    public static void WireAllBuildingPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Building Prefabs" });
        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            Building b = prefab.GetComponent<Building>();
            if (b == null) continue;

            // Don't wire depots/warehouses
            if (prefab.GetComponent<Depot>() != null || prefab.GetComponent<WarehouseSockets>() != null) continue;

            BuildingFacilityInfo info = prefab.GetComponent<BuildingFacilityInfo>();
            if (info == null) info = prefab.AddComponent<BuildingFacilityInfo>();

            string name = prefab.name;
            string lower = name.ToLowerInvariant();

            if (lower.Contains("ozone") || lower.Contains("deacidification") || lower.Contains("co2") || lower.Contains("weather"))
            {
                info.panelMode = FacilityPanelMode.Ecobalance;
                info.categoryTitle = "ECOBALANCE BUILDINGS";
                info.buildingDisplayName = name;
                info.effectText = "+100";
                info.ecobalanceValue = "+100";
                info.energyValue = -60;
                info.upkeepCredits = -120;
                info.maxHealth = 3000;
                info.currentHealth = 3000;
                info.effectIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/Anno2070/Icons/Ecobal-icon.png");
                info.headerIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/Anno2070/Icons/Ecobal-icon.png");
            }
            else
            {
                info.panelMode = FacilityPanelMode.Production;
                info.categoryTitle = "PRODUCTION BUILDINGS";
                info.buildingDisplayName = name;
                info.upkeepCredits = -50;
                info.energyValue = -20;
                info.ecobalanceValue = "-";
                info.maxHealth = 1000;
                info.currentHealth = 1000;
                info.inputAmount = 594484;

                if (lower.Contains("oil"))
                {
                    var oilSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/Anno2070/PNG - Item Icons/CrudeOil.png");
                    info.headerIcon = oilSprite;
                    info.inputIcon = oilSprite;
                    info.outputIcon = oilSprite;
                }
            }

            EditorUtility.SetDirty(prefab);
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Successfully auto-wired {count} building prefabs with BuildingFacilityInfo!");
    }

    private static void BuildPanelContent(GameObject root)
    {
        BuildingFacilityPanelUI controller = root.GetComponent<BuildingFacilityPanelUI>();
        SerializedObject so = new SerializedObject(controller);

        // 1. Panel Background
        GameObject panelRoot = CreateUIObject("Panel Root", root.transform);
        Stretch(panelRoot.GetComponent<RectTransform>());
        Image bgImg = panelRoot.AddComponent<Image>();
        bgImg.color = new Color(0.043f, 0.082f, 0.133f, 0.95f); // #0b1522 deep navy
        so.FindProperty("panelRoot").objectReferenceValue = panelRoot;

        // Red Accent Corner (top right)
        GameObject redAccent = CreateUIObject("Red Accent", panelRoot.transform);
        RectTransform redRt = redAccent.GetComponent<RectTransform>();
        redRt.anchorMin = new Vector2(1f, 1f);
        redRt.anchorMax = new Vector2(1f, 1f);
        redRt.pivot = new Vector2(1f, 1f);
        redRt.anchoredPosition = Vector2.zero;
        redRt.sizeDelta = new Vector2(90f, 32f);
        Image redImg = redAccent.AddComponent<Image>();
        redImg.color = new Color(0.72f, 0.18f, 0.18f, 0.9f); // #b82e2e
        redImg.raycastTarget = false;

        // 2. Header Bar
        GameObject headerBar = CreateUIObject("Header Bar", panelRoot.transform);
        RectTransform headerRt = headerBar.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = new Vector2(0f, 0f);
        headerRt.sizeDelta = new Vector2(0f, 44f);

        // Header Icon
        GameObject headerIconGo = CreateUIObject("Header Icon", headerBar.transform);
        RectTransform hiRt = headerIconGo.GetComponent<RectTransform>();
        hiRt.anchorMin = new Vector2(0f, 0.5f);
        hiRt.anchorMax = new Vector2(0f, 0.5f);
        hiRt.pivot = new Vector2(0f, 0.5f);
        hiRt.anchoredPosition = new Vector2(10f, 0f);
        hiRt.sizeDelta = new Vector2(30f, 30f);
        Image hiImg = headerIconGo.AddComponent<Image>();
        hiImg.preserveAspect = true;
        so.FindProperty("headerIcon").objectReferenceValue = hiImg;

        // Header Titles Container
        GameObject titleContainer = CreateUIObject("Title Container", headerBar.transform);
        RectTransform tcRt = titleContainer.GetComponent<RectTransform>();
        tcRt.anchorMin = new Vector2(0f, 0f);
        tcRt.anchorMax = new Vector2(1f, 1f);
        tcRt.offsetMin = new Vector2(46f, 2f);
        tcRt.offsetMax = new Vector2(-70f, -2f);

        // Category Label
        GameObject catGo = CreateUIObject("Category Label", titleContainer.transform);
        RectTransform catRt = catGo.GetComponent<RectTransform>();
        catRt.anchorMin = new Vector2(0f, 0.6f);
        catRt.anchorMax = new Vector2(1f, 1f);
        catRt.offsetMin = Vector2.zero;
        catRt.offsetMax = Vector2.zero;
        TextMeshProUGUI catTmp = catGo.AddComponent<TextMeshProUGUI>();
        catTmp.font = fontAsset;
        catTmp.fontSize = 10f;
        catTmp.fontStyle = FontStyles.Bold;
        catTmp.color = new Color(0.65f, 0.8f, 0.92f); // #a6cceb
        catTmp.text = "PRODUCTION BUILDINGS";
        so.FindProperty("categoryLabel").objectReferenceValue = catTmp;

        // Building Name Label
        GameObject nameGo = CreateUIObject("Building Name Label", titleContainer.transform);
        RectTransform nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 0f);
        nameRt.anchorMax = new Vector2(1f, 0.65f);
        nameRt.offsetMin = Vector2.zero;
        nameRt.offsetMax = Vector2.zero;
        TextMeshProUGUI nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
        nameTmp.font = fontAsset;
        nameTmp.fontSize = 17f;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.color = new Color(0.92f, 0.96f, 1f); // #ebf5ff
        nameTmp.text = "Oil rig";
        so.FindProperty("buildingNameLabel").objectReferenceValue = nameTmp;

        // 3. Middle Content Area
        GameObject contentArea = CreateUIObject("Content Area", panelRoot.transform);
        RectTransform caRt = contentArea.GetComponent<RectTransform>();
        caRt.anchorMin = new Vector2(0f, 0f);
        caRt.anchorMax = new Vector2(1f, 1f);
        caRt.offsetMin = new Vector2(0f, 68f);
        caRt.offsetMax = new Vector2(0f, -44f);

        BuildEcobalanceSubpanel(contentArea, so);
        BuildProductionSubpanel(contentArea, so);

        // 4. Bottom Status Row
        GameObject statusBar = CreateUIObject("Status Bar", panelRoot.transform);
        RectTransform sbRt = statusBar.GetComponent<RectTransform>();
        sbRt.anchorMin = new Vector2(0f, 0f);
        sbRt.anchorMax = new Vector2(1f, 0f);
        sbRt.pivot = new Vector2(0.5f, 0f);
        sbRt.anchoredPosition = new Vector2(0f, 36f);
        sbRt.sizeDelta = new Vector2(0f, 30f);

        // Thin divider
        GameObject divider = CreateUIObject("Divider", statusBar.transform);
        RectTransform divRt = divider.GetComponent<RectTransform>();
        divRt.anchorMin = new Vector2(0f, 1f);
        divRt.anchorMax = new Vector2(1f, 1f);
        divRt.sizeDelta = new Vector2(0f, 1.5f);
        Image divImg = divider.AddComponent<Image>();
        divImg.color = new Color(0.12f, 0.25f, 0.38f, 0.8f);

        BuildStatusBarItems(statusBar, so);

        // 5. Bottom Action Bar
        GameObject actionBar = CreateUIObject("Action Bar", panelRoot.transform);
        RectTransform abRt = actionBar.GetComponent<RectTransform>();
        abRt.anchorMin = new Vector2(0f, 0f);
        abRt.anchorMax = new Vector2(1f, 0f);
        abRt.pivot = new Vector2(0.5f, 0f);
        abRt.anchoredPosition = Vector2.zero;
        abRt.sizeDelta = new Vector2(0f, 36f);
        Image abBg = actionBar.AddComponent<Image>();
        abBg.color = new Color(0.06f, 0.12f, 0.2f, 0.95f); // #0f1f33

        BuildActionBarButtons(actionBar, so);

        so.ApplyModifiedProperties();
    }

    private static void BuildEcobalanceSubpanel(GameObject parent, SerializedObject so)
    {
        GameObject ecoRoot = CreateUIObject("Ecobalance Root", parent.transform);
        Stretch(ecoRoot.GetComponent<RectTransform>());
        so.FindProperty("ecobalanceRoot").objectReferenceValue = ecoRoot;

        // Building Portrait Image (Left side)
        GameObject portraitGo = CreateUIObject("Portrait Image", ecoRoot.transform);
        RectTransform pRt = portraitGo.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0f, 0.5f);
        pRt.anchorMax = new Vector2(0f, 0.5f);
        pRt.pivot = new Vector2(0f, 0.5f);
        pRt.anchoredPosition = new Vector2(10f, 0f);
        pRt.sizeDelta = new Vector2(145f, 100f);
        Image pImg = portraitGo.AddComponent<Image>();
        pImg.preserveAspect = false;

        Sprite ozonePortrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/Anno2070/Energy_and_Ecology/Ozonmaker.png");
        if (ozonePortrait != null) pImg.sprite = ozonePortrait;
        so.FindProperty("portraitImage").objectReferenceValue = pImg;

        // Center Ring Group (Progress circle)
        GameObject ringGroup = CreateUIObject("Eco Ring Group", ecoRoot.transform);
        RectTransform rRt = ringGroup.GetComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0f, 0.5f);
        rRt.anchorMax = new Vector2(0f, 0.5f);
        rRt.pivot = new Vector2(0.5f, 0.5f);
        rRt.anchoredPosition = new Vector2(200f, 0f);
        rRt.sizeDelta = new Vector2(72f, 72f);

        Sprite ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prefabs/UI/Sprites/ProgressRing.png");

        // Background Ring
        GameObject ringBg = CreateUIObject("Ring BG", ringGroup.transform);
        Stretch(ringBg.GetComponent<RectTransform>());
        Image rbImg = ringBg.AddComponent<Image>();
        rbImg.sprite = ringSprite;
        rbImg.color = new Color(0.12f, 0.22f, 0.32f, 0.7f);

        // Fill Ring
        GameObject ringFill = CreateUIObject("Ring Fill", ringGroup.transform);
        Stretch(ringFill.GetComponent<RectTransform>());
        Image rfImg = ringFill.AddComponent<Image>();
        rfImg.sprite = ringSprite;
        rfImg.type = Image.Type.Filled;
        rfImg.fillMethod = Image.FillMethod.Radial360;
        rfImg.fillOrigin = (int)Image.Origin360.Top;
        rfImg.fillAmount = 1f;
        rfImg.color = new Color(0.4f, 0.8f, 1f, 1f);
        so.FindProperty("ecoProgressRing").objectReferenceValue = rfImg;

        // Percentage Text
        GameObject pctGo = CreateUIObject("Percentage Text", ringGroup.transform);
        Stretch(pctGo.GetComponent<RectTransform>());
        TextMeshProUGUI pctTmp = pctGo.AddComponent<TextMeshProUGUI>();
        pctTmp.font = fontAsset;
        pctTmp.fontSize = 17f;
        pctTmp.fontStyle = FontStyles.Bold;
        pctTmp.alignment = TextAlignmentOptions.Center;
        pctTmp.color = Color.white;
        pctTmp.text = "100%";
        so.FindProperty("ecoPercentageText").objectReferenceValue = pctTmp;

        // Right Effect Display (+100 🍃)
        GameObject effectGroup = CreateUIObject("Effect Group", ecoRoot.transform);
        RectTransform egRt = effectGroup.GetComponent<RectTransform>();
        egRt.anchorMin = new Vector2(1f, 0.5f);
        egRt.anchorMax = new Vector2(1f, 0.5f);
        egRt.pivot = new Vector2(1f, 0.5f);
        egRt.anchoredPosition = new Vector2(-15f, 0f);
        egRt.sizeDelta = new Vector2(120f, 60f);

        GameObject effTextGo = CreateUIObject("Effect Text", effectGroup.transform);
        RectTransform etRt = effTextGo.GetComponent<RectTransform>();
        etRt.anchorMin = new Vector2(0f, 0f);
        etRt.anchorMax = new Vector2(0.65f, 1f);
        etRt.offsetMin = Vector2.zero;
        etRt.offsetMax = Vector2.zero;
        TextMeshProUGUI effTmp = effTextGo.AddComponent<TextMeshProUGUI>();
        effTmp.font = fontAsset;
        effTmp.fontSize = 28f;
        effTmp.fontStyle = FontStyles.Bold;
        effTmp.alignment = TextAlignmentOptions.MidlineRight;
        effTmp.color = new Color(0.75f, 0.92f, 1f);
        effTmp.text = "+100";
        so.FindProperty("ecoEffectText").objectReferenceValue = effTmp;

        GameObject effIconGo = CreateUIObject("Effect Icon", effectGroup.transform);
        RectTransform eiRt = effIconGo.GetComponent<RectTransform>();
        eiRt.anchorMin = new Vector2(0.68f, 0.5f);
        eiRt.anchorMax = new Vector2(0.68f, 0.5f);
        eiRt.pivot = new Vector2(0f, 0.5f);
        eiRt.sizeDelta = new Vector2(36f, 36f);
        eiRt.anchoredPosition = Vector2.zero;
        Image eiImg = effIconGo.AddComponent<Image>();
        eiImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/Anno2070/Icons/Ecobal-icon.png");
        eiImg.color = new Color(0.55f, 0.85f, 1f);
        eiImg.preserveAspect = true;
        so.FindProperty("ecoEffectIcon").objectReferenceValue = eiImg;
    }

    private static void BuildProductionSubpanel(GameObject parent, SerializedObject so)
    {
        GameObject prodRoot = CreateUIObject("Production Root", parent.transform);
        Stretch(prodRoot.GetComponent<RectTransform>());
        so.FindProperty("productionRoot").objectReferenceValue = prodRoot;

        // 1. Animated Cog Wheels in Background
        GameObject cogHolder = CreateUIObject("Cog Wheels Background", prodRoot.transform);
        Stretch(cogHolder.GetComponent<RectTransform>());
        CogWheelAnimator cogAnim = cogHolder.AddComponent<CogWheelAnimator>();
        so.FindProperty("cogAnimator").objectReferenceValue = cogAnim;

        SerializedObject cogSo = new SerializedObject(cogAnim);
        Sprite cogSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prefabs/UI/Sprites/CogWheel.png");

        // Cog Left
        GameObject cogLeft = CreateUIObject("Cog Left", cogHolder.transform);
        RectTransform clRt = cogLeft.GetComponent<RectTransform>();
        clRt.anchorMin = new Vector2(0.5f, 0.5f);
        clRt.anchorMax = new Vector2(0.5f, 0.5f);
        clRt.pivot = new Vector2(0.5f, 0.5f);
        clRt.anchoredPosition = new Vector2(-60f, -5f);
        clRt.sizeDelta = new Vector2(175f, 175f);
        Image clImg = cogLeft.AddComponent<Image>();
        clImg.sprite = cogSprite;
        clImg.color = new Color(0.08f, 0.15f, 0.23f, 0.75f); // #14263b
        clImg.raycastTarget = false;
        cogSo.FindProperty("cogLeft").objectReferenceValue = clRt;

        // Cog Right
        GameObject cogRight = CreateUIObject("Cog Right", cogHolder.transform);
        RectTransform crRt = cogRight.GetComponent<RectTransform>();
        crRt.anchorMin = new Vector2(0.5f, 0.5f);
        crRt.anchorMax = new Vector2(0.5f, 0.5f);
        crRt.pivot = new Vector2(0.5f, 0.5f);
        crRt.anchoredPosition = new Vector2(75f, -18f);
        crRt.sizeDelta = new Vector2(145f, 145f);
        Image crImg = cogRight.AddComponent<Image>();
        crImg.sprite = cogSprite;
        crImg.color = new Color(0.08f, 0.15f, 0.23f, 0.75f); // #14263b
        crImg.raycastTarget = false;
        cogSo.FindProperty("cogRight").objectReferenceValue = crRt;

        cogSo.ApplyModifiedProperties();

        Sprite slotBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prefabs/UI/Sprites/SlotBackground.png");
        Sprite oilSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/Anno2070/PNG - Item Icons/CrudeOil.png");

        // 2. Input / Deposit Slot (Left)
        GameObject inputSlot = CreateUIObject("Input Slot", prodRoot.transform);
        RectTransform inRt = inputSlot.GetComponent<RectTransform>();
        inRt.anchorMin = new Vector2(0.5f, 0.5f);
        inRt.anchorMax = new Vector2(0.5f, 0.5f);
        inRt.pivot = new Vector2(0.5f, 0.5f);
        inRt.anchoredPosition = new Vector2(-110f, 0f);
        inRt.sizeDelta = new Vector2(74f, 74f);
        Image inBgImg = inputSlot.AddComponent<Image>();
        inBgImg.sprite = slotBg;
        inBgImg.type = Image.Type.Sliced;
        inBgImg.color = new Color(0.48f, 0.54f, 0.59f, 0.9f); // #7b8a96

        // Input Icon
        GameObject inIconGo = CreateUIObject("Input Icon", inputSlot.transform);
        RectTransform inIconRt = inIconGo.GetComponent<RectTransform>();
        inIconRt.anchorMin = new Vector2(0.5f, 0.5f);
        inIconRt.anchorMax = new Vector2(0.5f, 0.5f);
        inIconRt.pivot = new Vector2(0.5f, 0.5f);
        inIconRt.anchoredPosition = new Vector2(0f, 10f);
        inIconRt.sizeDelta = new Vector2(40f, 40f);
        Image inIconImg = inIconGo.AddComponent<Image>();
        inIconImg.sprite = oilSprite;
        inIconImg.preserveAspect = true;
        so.FindProperty("inputIcon").objectReferenceValue = inIconImg;

        // Input Amount
        GameObject inAmountGo = CreateUIObject("Input Amount Label", inputSlot.transform);
        RectTransform inAmRt = inAmountGo.GetComponent<RectTransform>();
        inAmRt.anchorMin = new Vector2(0f, 0f);
        inAmRt.anchorMax = new Vector2(1f, 0f);
        inAmRt.pivot = new Vector2(0.5f, 0f);
        inAmRt.anchoredPosition = new Vector2(0f, 4f);
        inAmRt.sizeDelta = new Vector2(0f, 20f);
        TextMeshProUGUI inAmTmp = inAmountGo.AddComponent<TextMeshProUGUI>();
        inAmTmp.font = fontAsset;
        inAmTmp.fontSize = 11f;
        inAmTmp.fontStyle = FontStyles.Bold;
        inAmTmp.alignment = TextAlignmentOptions.Center;
        inAmTmp.color = new Color(0.1f, 0.12f, 0.15f); // dark on light slot
        inAmTmp.text = "594,484";
        so.FindProperty("inputAmountText").objectReferenceValue = inAmTmp;

        // 3. Center Circular Rate Ring
        GameObject ringGroup = CreateUIObject("Prod Ring Group", prodRoot.transform);
        RectTransform rRt = ringGroup.GetComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0.5f, 0.5f);
        rRt.anchorMax = new Vector2(0.5f, 0.5f);
        rRt.pivot = new Vector2(0.5f, 0.5f);
        rRt.anchoredPosition = Vector2.zero;
        rRt.sizeDelta = new Vector2(74f, 74f);

        Sprite ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prefabs/UI/Sprites/ProgressRing.png");

        GameObject ringBg = CreateUIObject("Ring BG", ringGroup.transform);
        Stretch(ringBg.GetComponent<RectTransform>());
        Image rbImg = ringBg.AddComponent<Image>();
        rbImg.sprite = ringSprite;
        rbImg.color = new Color(0.12f, 0.22f, 0.32f, 0.8f);

        GameObject ringFill = CreateUIObject("Ring Fill", ringGroup.transform);
        Stretch(ringFill.GetComponent<RectTransform>());
        Image rfImg = ringFill.AddComponent<Image>();
        rfImg.sprite = ringSprite;
        rfImg.type = Image.Type.Filled;
        rfImg.fillMethod = Image.FillMethod.Radial360;
        rfImg.fillOrigin = (int)Image.Origin360.Top;
        rfImg.fillAmount = 1f;
        rfImg.color = new Color(0.45f, 0.82f, 1f);
        so.FindProperty("prodProgressRing").objectReferenceValue = rfImg;

        GameObject pctGo = CreateUIObject("Percentage Text", ringGroup.transform);
        Stretch(pctGo.GetComponent<RectTransform>());
        TextMeshProUGUI pctTmp = pctGo.AddComponent<TextMeshProUGUI>();
        pctTmp.font = fontAsset;
        pctTmp.fontSize = 17f;
        pctTmp.fontStyle = FontStyles.Bold;
        pctTmp.alignment = TextAlignmentOptions.Center;
        pctTmp.color = Color.white;
        pctTmp.text = "100%";
        so.FindProperty("prodPercentageText").objectReferenceValue = pctTmp;

        // 4. Output Slot (Right)
        GameObject outputSlot = CreateUIObject("Output Slot", prodRoot.transform);
        RectTransform outRt = outputSlot.GetComponent<RectTransform>();
        outRt.anchorMin = new Vector2(0.5f, 0.5f);
        outRt.anchorMax = new Vector2(0.5f, 0.5f);
        outRt.pivot = new Vector2(0.5f, 0.5f);
        outRt.anchoredPosition = new Vector2(110f, 0f);
        outRt.sizeDelta = new Vector2(74f, 74f);
        Image outBgImg = outputSlot.AddComponent<Image>();
        outBgImg.sprite = slotBg;
        outBgImg.type = Image.Type.Sliced;
        outBgImg.color = new Color(0.48f, 0.54f, 0.59f, 0.9f);

        // Output Icon
        GameObject outIconGo = CreateUIObject("Output Icon", outputSlot.transform);
        RectTransform outIconRt = outIconGo.GetComponent<RectTransform>();
        outIconRt.anchorMin = new Vector2(0.5f, 0.5f);
        outIconRt.anchorMax = new Vector2(0.5f, 0.5f);
        outIconRt.pivot = new Vector2(0.5f, 0.5f);
        outIconRt.anchoredPosition = new Vector2(-4f, 10f);
        outIconRt.sizeDelta = new Vector2(40f, 40f);
        Image outIconImg = outIconGo.AddComponent<Image>();
        outIconImg.sprite = oilSprite;
        outIconImg.preserveAspect = true;
        so.FindProperty("outputIcon").objectReferenceValue = outIconImg;

        // Output Amount
        GameObject outAmountGo = CreateUIObject("Output Amount Label", outputSlot.transform);
        RectTransform outAmRt = outAmountGo.GetComponent<RectTransform>();
        outAmRt.anchorMin = new Vector2(0f, 0f);
        outAmRt.anchorMax = new Vector2(1f, 0f);
        outAmRt.pivot = new Vector2(0.5f, 0f);
        outAmRt.anchoredPosition = new Vector2(-4f, 4f);
        outAmRt.sizeDelta = new Vector2(0f, 20f);
        TextMeshProUGUI outAmTmp = outAmountGo.AddComponent<TextMeshProUGUI>();
        outAmTmp.font = fontAsset;
        outAmTmp.fontSize = 12f;
        outAmTmp.fontStyle = FontStyles.Bold;
        outAmTmp.alignment = TextAlignmentOptions.Center;
        outAmTmp.color = new Color(0.1f, 0.12f, 0.15f);
        outAmTmp.text = "1";
        so.FindProperty("outputAmountText").objectReferenceValue = outAmTmp;

        // Output Vertical Fill Meter (right edge of slot)
        GameObject meterBg = CreateUIObject("Meter BG", outputSlot.transform);
        RectTransform mRt = meterBg.GetComponent<RectTransform>();
        mRt.anchorMin = new Vector2(1f, 0.5f);
        mRt.anchorMax = new Vector2(1f, 0.5f);
        mRt.pivot = new Vector2(1f, 0.5f);
        mRt.anchoredPosition = new Vector2(-4f, 0f);
        mRt.sizeDelta = new Vector2(6f, 56f);
        Image mBgImg = meterBg.AddComponent<Image>();
        mBgImg.color = new Color(0.2f, 0.25f, 0.28f, 0.9f);

        GameObject meterFill = CreateUIObject("Meter Fill", meterBg.transform);
        Stretch(meterFill.GetComponent<RectTransform>());
        Image mFillImg = meterFill.AddComponent<Image>();
        mFillImg.type = Image.Type.Filled;
        mFillImg.fillMethod = Image.FillMethod.Vertical;
        mFillImg.fillOrigin = (int)Image.OriginVertical.Bottom;
        mFillImg.fillAmount = 0.35f;
        mFillImg.color = new Color(0.48f, 0.82f, 0.25f); // green meter
        so.FindProperty("outputFillBar").objectReferenceValue = mFillImg;
    }

    private static void BuildStatusBarItems(GameObject parent, SerializedObject so)
    {
        HorizontalLayoutGroup hlg = parent.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 12, 4, 4);
        hlg.spacing = 10f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        // 1. Credits
        var cItem = CreateStatItem(parent.transform, "Credits Item",
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/Anno2070/Icons/Credits-icon.png"),
            "-50", new Color(0.85f, 0.92f, 1f));
        so.FindProperty("creditsIcon").objectReferenceValue = cItem.icon;
        so.FindProperty("creditsText").objectReferenceValue = cItem.text;

        // 2. Energy
        var eItem = CreateStatItem(parent.transform, "Energy Item",
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/Anno2070/Icons/Energy-icon.png"),
            "-20", new Color(0.85f, 0.92f, 1f));
        so.FindProperty("energyIcon").objectReferenceValue = eItem.icon;
        so.FindProperty("energyText").objectReferenceValue = eItem.text;

        // 3. Ecobalance
        var ecoItem = CreateStatItem(parent.transform, "Eco Item",
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/Anno2070/Icons/Ecobal-icon.png"),
            "-", new Color(0.85f, 0.92f, 1f));
        so.FindProperty("ecoIcon").objectReferenceValue = ecoItem.icon;
        so.FindProperty("ecoText").objectReferenceValue = ecoItem.text;

        // 4. Health (Green)
        var hItem = CreateStatItem(parent.transform, "Health Item",
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/Anno2070/Icons/Health-icon.png"),
            "1,000/1,000", new Color(0.29f, 0.87f, 0.45f)); // vibrant green #4ade80
        so.FindProperty("healthIcon").objectReferenceValue = hItem.icon;
        so.FindProperty("healthText").objectReferenceValue = hItem.text;
    }

    private static (Image icon, TextMeshProUGUI text) CreateStatItem(
        Transform parent, string name, Sprite iconSprite, string defaultText, Color textColor)
    {
        GameObject item = CreateUIObject(name, parent);
        HorizontalLayoutGroup hlg = item.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 5f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        GameObject iconGo = CreateUIObject("Icon", item.transform);
        RectTransform iRt = iconGo.GetComponent<RectTransform>();
        iRt.sizeDelta = new Vector2(18f, 18f);
        Image img = iconGo.AddComponent<Image>();
        img.sprite = iconSprite;
        img.preserveAspect = true;

        GameObject textGo = CreateUIObject("Text", item.transform);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.font = fontAsset;
        tmp.fontSize = 13f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = textColor;
        tmp.text = defaultText;

        return (img, tmp);
    }

    private static void BuildActionBarButtons(GameObject parent, SerializedObject so)
    {
        HorizontalLayoutGroup hlg = parent.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(10, 10, 4, 4);
        hlg.spacing = 14f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        so.FindProperty("homeButton").objectReferenceValue = CreateActionButton(parent.transform, "Home Btn",
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prefabs/UI/Sprites/HomeIcon.png"));

        so.FindProperty("pickaxeButton").objectReferenceValue = CreateActionButton(parent.transform, "Pickaxe Btn",
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prefabs/UI/Sprites/PickaxeIcon.png"));

        so.FindProperty("diplomacyButton").objectReferenceValue = CreateActionButton(parent.transform, "Diplomacy Btn",
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/Anno2070/Icons/Diplomacy_icon.png"));

        so.FindProperty("cycleButton").objectReferenceValue = CreateActionButton(parent.transform, "Cycle Btn",
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prefabs/UI/Sprites/CycleIcon.png"));

        so.FindProperty("plusButton").objectReferenceValue = CreateActionButton(parent.transform, "Plus Btn",
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/Anno2070/Icons/Upgrade.png"));

        so.FindProperty("infoButton").objectReferenceValue = CreateActionButton(parent.transform, "Info Btn",
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/Anno2070/Icons/Needs & Desires/Building Based/Information.png"));
    }

    private static Button CreateActionButton(Transform parent, string name, Sprite iconSprite)
    {
        GameObject btnGo = CreateUIObject(name, parent);
        Button btn = btnGo.AddComponent<Button>();

        GameObject iconGo = CreateUIObject("Icon", btnGo.transform);
        Stretch(iconGo.GetComponent<RectTransform>());
        RectTransform rt = iconGo.GetComponent<RectTransform>();
        rt.offsetMin = new Vector2(4f, 4f);
        rt.offsetMax = new Vector2(-4f, -4f);

        Image img = iconGo.AddComponent<Image>();
        img.sprite = iconSprite;
        img.preserveAspect = true;
        img.color = new Color(0.7f, 0.85f, 0.95f); // #b3d9f2

        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 0.7f);
        colors.pressedColor = new Color(0.5f, 0.7f, 1f);
        btn.colors = colors;

        return btn;
    }

    private static void InstallInActiveScene(GameObject prefab)
    {
        GameObject hudBot = GameObject.Find("== Player =====================/User Interface (Canvas)/Graphical User Interface/HUD Bot");
        if (hudBot == null)
        {
            hudBot = GameObject.Find("HUD Bot");
        }

        if (hudBot != null)
        {
            // Check if already installed
            Transform existing = hudBot.transform.Find("Building Facility Panel");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hudBot.transform);
            instance.name = "Building Facility Panel";

            RectTransform rt = instance.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-15f, 15f);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log($"Installed Building Facility Panel in scene under {hudBot.name}.");
        }
    }

    #region Utilities

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    #endregion
}
