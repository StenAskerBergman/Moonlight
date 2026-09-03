using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Installs Production as a child feature of the legacy Tycoon civilisation pages.
///
/// This component deliberately has no menu or navigation authority. ReverseBool owns
/// the construction-window active state, while the scene's persistent faction and tier
/// button events continue to own which legacy page is active. Because each Production
/// section lives under its page, normal legacy deactivation also clears its transient DAG.
/// </summary>
[DisallowMultipleComponent]
public sealed class ConstructionMenuProductionHost : MonoBehaviour
{
    [SerializeField] private ConstructionPageDefinition tycoonProductionPage;
    private bool isInstalled;

    private void Awake()
    {
        InstallAll();
    }

    private void OnEnable()
    {
        InstallAll();
    }

    private void InstallAll()
    {
        if (isInstalled) return;

        if (tycoonProductionPage == null)
        {
#if UNITY_EDITOR
            tycoonProductionPage = UnityEditor.AssetDatabase.LoadAssetAtPath<ConstructionPageDefinition>(
                "Assets/Data/Construction/Pages/Tycoon Production.asset");
#endif
        }

        Transform tycoonRoot = transform.Find("Faction A: Tyc");
        if (tycoonRoot == null)
        {
            Debug.LogWarning(
                "Production integration could not find the legacy 'Faction A: Tyc' hierarchy.",
                this);
            return;
        }

        Install(tycoonRoot, "AB.Tier 2", PopulationClass.Workers);
        Install(tycoonRoot, "AB.Tier 3", PopulationClass.Employees);
        Install(tycoonRoot, "AB.Tier 4", PopulationClass.Engineers);
        Install(tycoonRoot, "AB.Tier 5", PopulationClass.Executives);
        isInstalled = true;
    }

    private void Install(Transform factionRoot, string pageName, PopulationClass populationClass)
    {
        Transform page = FindDirectChild(factionRoot, pageName);
        if (page == null)
        {
            Debug.LogWarning(
                $"Production integration could not find legacy page '{pageName}' under '{factionRoot.name}'.",
                this);
            return;
        }

        var integration = page.GetComponent<ProductionTierPageIntegration>();
        if (integration == null)
        {
            integration = page.gameObject.AddComponent<ProductionTierPageIntegration>();
        }

        integration.Initialize(tycoonProductionPage, populationClass);
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName) return child;
        }

        return null;
    }
}

/// <summary>
/// Adapts one manually positioned legacy civilisation page to Production without
/// becoming a page or navigation controller. The legacy Top Row is the Production
/// row; Mid Row and Bot Row remain untouched non-Production content.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProductionTierPageIntegration : MonoBehaviour
{
    private struct RectBaseline
    {
        public RectTransform Rect;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
    }

    private readonly List<RectBaseline> legacyContentBaselines = new List<RectBaseline>();

    private RectTransform pageRect;
    private RectTransform legacyProductionRow;
    private RectTransform productionRect;
    private ProductionSectionUI productionSection;
    private ConstructionPageDefinition pageDefinition;
    private PopulationClass populationClass;

    private Vector2 pageBaselinePosition;
    private Vector2 pageBaselineSize;
    private Vector2 productionBaselinePosition;
    private bool legacyProductionRowWasActive;
    private bool initialized;

    public ProductionSectionUI ProductionSection => productionSection;

    public void Initialize(ConstructionPageDefinition definition, PopulationClass classFilter)
    {
        if (initialized)
        {
            pageDefinition = definition;
            populationClass = classFilter;
            productionSection.SetPage(pageDefinition, ResolvePopulation(), populationClass);
            ApplyLayout();
            return;
        }

        pageRect = transform as RectTransform;
        legacyProductionRow = FindDirectChild("Top Row") as RectTransform;
        if (pageRect == null || legacyProductionRow == null)
        {
            Debug.LogWarning(
                $"Production cannot bind '{name}': its legacy page or direct 'Top Row' is missing.",
                this);
            return;
        }

        pageDefinition = definition;
        populationClass = classFilter;
        CacheBaselines();
        CreateProductionSection();

        legacyProductionRowWasActive = legacyProductionRow.gameObject.activeSelf;
        legacyProductionRow.gameObject.SetActive(false);

        productionSection.ExpandedLineChanged.AddListener(ApplyLayout);
        productionSection.SetPage(pageDefinition, ResolvePopulation(), populationClass);
        initialized = true;
        ApplyLayout();
    }

    private void OnEnable()
    {
        if (!initialized) return;
        productionSection.SetPage(pageDefinition, ResolvePopulation(), populationClass);
        ApplyLayout();
    }

    private void OnDisable()
    {
        if (!initialized) return;
        productionSection.ClearExpandedChain();
        RestoreLegacyGeometry();
    }

    private void OnDestroy()
    {
        if (!initialized) return;

        productionSection.ExpandedLineChanged.RemoveListener(ApplyLayout);
        RestoreLegacyGeometry();
        if (legacyProductionRow != null)
        {
            legacyProductionRow.gameObject.SetActive(legacyProductionRowWasActive);
        }
    }

    private void CacheBaselines()
    {
        pageBaselinePosition = pageRect.anchoredPosition;
        pageBaselineSize = pageRect.sizeDelta;

        legacyContentBaselines.Clear();
        for (int i = 0; i < pageRect.childCount; i++)
        {
            RectTransform child = pageRect.GetChild(i) as RectTransform;
            if (child == null || child == legacyProductionRow || child.name == "Production Section") continue;

            legacyContentBaselines.Add(new RectBaseline
            {
                Rect = child,
                AnchoredPosition = child.anchoredPosition,
                SizeDelta = child.sizeDelta,
            });
        }
    }

    private void CreateProductionSection()
    {
        Transform existing = FindDirectChild("Production Section");
        if (existing != null)
        {
            productionRect = existing as RectTransform;
            productionSection = existing.GetComponent<ProductionSectionUI>();
        }

        if (productionSection == null)
        {
            var root = new GameObject(
                "Production Section",
                typeof(RectTransform),
                typeof(ProductionSectionUI));
            productionRect = (RectTransform)root.transform;
            productionRect.SetParent(pageRect, false);
            productionSection = root.GetComponent<ProductionSectionUI>();
        }

        productionRect.SetSiblingIndex(legacyProductionRow.GetSiblingIndex());
        productionRect.anchorMin = legacyProductionRow.anchorMin;
        productionRect.anchorMax = legacyProductionRow.anchorMax;
        productionRect.pivot = new Vector2(0.5f, 0f);

        productionBaselinePosition = new Vector2(
            legacyProductionRow.anchoredPosition.x,
            legacyProductionRow.anchoredPosition.y - productionSection.CollapsedHeight * 0.5f);
    }

    private void ApplyLayout()
    {
        if (productionSection == null) return;

        float sectionHeight = productionSection.PreferredHeight;
        float expansion = Mathf.Max(0f, sectionHeight - productionSection.CollapsedHeight);

        // The selector baseline and page bottom are invariant. Extra DAG height grows
        // upward from those cached baselines, never from the previous frame's values.
        productionRect.anchoredPosition = productionBaselinePosition;
        productionRect.sizeDelta = new Vector2(pageBaselineSize.x, sectionHeight);

        pageRect.sizeDelta = new Vector2(pageBaselineSize.x, pageBaselineSize.y + expansion);
        pageRect.anchoredPosition = pageBaselinePosition + Vector2.up * (pageRect.pivot.y * expansion);

        RestoreLegacyContentBaselines();
    }

    private void RestoreLegacyGeometry()
    {
        if (pageRect == null) return;

        pageRect.anchoredPosition = pageBaselinePosition;
        pageRect.sizeDelta = pageBaselineSize;

        if (productionRect != null && productionSection != null)
        {
            productionRect.anchoredPosition = productionBaselinePosition;
            productionRect.sizeDelta = new Vector2(pageBaselineSize.x, productionSection.CollapsedHeight);
        }

        RestoreLegacyContentBaselines();
    }

    private void RestoreLegacyContentBaselines()
    {
        foreach (RectBaseline baseline in legacyContentBaselines)
        {
            if (baseline.Rect == null) continue;
            baseline.Rect.anchoredPosition = baseline.AnchoredPosition;
            baseline.Rect.sizeDelta = baseline.SizeDelta;
        }
    }

    private Transform FindDirectChild(string childName)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name == childName) return child;
        }

        return null;
    }

    private static IslandPopulation ResolvePopulation()
    {
        if (IslandManager.instance == null || Camera.main == null) return null;
        Island island = IslandManager.instance.GetIslandInFrontOfCamera(Camera.main);
        if (island == null) return null;
        return island.Population != null ? island.Population : island.GetComponent<IslandPopulation>();
    }
}
