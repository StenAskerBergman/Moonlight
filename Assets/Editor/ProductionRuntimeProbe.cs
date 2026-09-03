using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// [InitializeOnLoad]
internal static class ProductionRuntimeProbe
{
    private const string SessionKey = "Moonlight.ProductionRuntimeProbe.Completed.V3";
    private const string ScreenshotPath = "E:/GitHub/Moonlight/Temp/ProductionWorkerRuntime.png";
    private const string ResultPath = "E:/GitHub/Moonlight/Temp/ProductionWorkerRuntime.txt";

    private sealed class RectState
    {
        public string Name;
        public RectTransform Rect;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
    }

    private static readonly List<RectState> Baselines = new List<RectState>();
    private static readonly List<string> Results = new List<string>();
    private static int phase;
    private static int frames;
    private static int completedCycles;
    private static GameObject root;
    private static ProductionSectionUI section;
    private static Button buildingModules;

    static ProductionRuntimeProbe()
    {
        Debug.Log("PRODUCTION_RUNTIME_PROBE_INITIALIZED");
        EditorApplication.delayCall += Start;
    }

    private static void Start()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    private static void Update()
    {
        if (EditorApplication.isCompiling) return;

        if (!EditorApplication.isPlaying)
        {
            EditorApplication.EnterPlaymode();
            return;
        }

        if (++frames < 10) return;
        frames = 0;

        try
        {
            switch (phase)
            {
                case 0: OpenAndSelectWorkers(); break;
                case 1: ExpandAndInspect(); break;
                case 2: InspectExpandedAndCollapse(); break;
                case 3: InspectCollapseAndRepeat(); break;
                case 4: ClickPlacementNode(); break;
                case 5: InspectPlacementAndFinish(); break;
                case 6: CollapseAfterScreenshot(); break;
            }
        }
        catch (Exception exception)
        {
            Results.Add("EXCEPTION=" + exception);
            Finish();
        }
    }

    private static void OpenAndSelectWorkers()
    {
        root = Resources.FindObjectsOfTypeAll<GameObject>()
            .First(gameObject => gameObject.scene.IsValid() && gameObject.name == "HUD (Bot Building Window)");

        bool startedInactive = !root.activeSelf;
        Button build = Resources.FindObjectsOfTypeAll<Button>()
            .First(button => button.gameObject.scene.IsValid() && button.name == "Build btn - Building" && button.gameObject.activeInHierarchy);
        string[] buildEvents = Enumerable.Range(0, build.onClick.GetPersistentEventCount())
            .Select(index => build.onClick.GetPersistentMethodName(index)).ToArray();
        if (!root.activeSelf) build.onClick.Invoke();

        Button faction = root.GetComponentsInChildren<Button>(true).First(button => button.name == "Faction Button A");
        Button tier = root.GetComponentsInChildren<Button>(true).First(button => button.name == "T2A");
        faction.onClick.Invoke();
        tier.onClick.Invoke();

        ProductionTierPageIntegration integration = root.GetComponentsInChildren<ProductionTierPageIntegration>(true)
            .First(component => component.transform.parent != null && component.transform.parent.name == "Faction A: Tyc" && component.name == "AB.Tier 2");
        section = integration.ProductionSection;
        RectTransform page = (RectTransform)integration.transform;
        Baselines.Clear();
        Baselines.Add(Capture("PAGE", page));
        for (int index = 0; index < page.childCount; index++)
        {
            RectTransform child = page.GetChild(index) as RectTransform;
            if (child != null) Baselines.Add(Capture(child.name, child));
        }

        Transform strip = section.transform.Find("Production Line Selectors");
        string[] selectors = strip.Cast<Transform>().Select(child => child.name).ToArray();
        buildingModules = strip.Cast<Transform>()
            .Select(child => child.GetComponent<Button>())
            .First(button => button != null && button.name == "Line (Building Modules)");

        Results.Add("STARTED_INACTIVE=" + startedInactive);
        Results.Add("BUILD_EVENTS=" + string.Join(",", buildEvents));
        Results.Add("ROOT_ACTIVE_AFTER_BUILD=" + root.activeInHierarchy);
        Results.Add("FACTION_EVENT_COUNT=" + faction.onClick.GetPersistentEventCount());
        Results.Add("TIER_EVENT_COUNT=" + tier.onClick.GetPersistentEventCount());
        Results.Add("WORKERS_PAGE_ACTIVE=" + integration.gameObject.activeInHierarchy);
        Results.Add("SELECTORS=" + string.Join("|", selectors));
        phase = 1;
    }

    private static void ExpandAndInspect()
    {
        buildingModules.onClick.Invoke();
        phase = 2;
    }

    private static void InspectExpandedAndCollapse()
    {
        Transform canvas = section.transform.Find("Production Chain Canvas");
        Transform nodes = canvas.Find("Nodes");
        Transform connectors = canvas.Find("Connectors");
        string[] nodeNames = nodes.Cast<Transform>().Select(child => child.name).ToArray();
        string[] labels = nodes.Cast<Transform>()
            .Select(child => child.GetComponentInChildren<TMPro.TMP_Text>(true))
            .Where(label => label != null).Select(label => label.text).ToArray();
        int connectorPieces = connectors.GetComponentsInChildren<Image>(true).Length;
        string[] nodeChildren = nodes.Cast<Transform>()
            .Select(node => node.name + "=[" + string.Join(",", node.Cast<Transform>().Select(child => child.name).ToArray()) + "]")
            .ToArray();
        RectTransform canvasRect = (RectTransform)canvas;
        RectTransform sectionRect = (RectTransform)section.transform;
        Transform strip = section.transform.Find("Production Line Selectors");

        Results.Add("EXPANDED=" + section.IsExpanded);
        Results.Add("EXPANDED_LINE=" + (section.ExpandedLine != null ? section.ExpandedLine.DisplayName : "null"));
        Results.Add("CANVAS_ACTIVE=" + canvas.gameObject.activeInHierarchy);
        Results.Add("CANVAS_SIZE=" + canvasRect.rect.size);
        Results.Add("SECTION_SIZE=" + sectionRect.rect.size);
        Results.Add("NODES=" + string.Join("|", nodeNames));
        Results.Add("NODE_LABELS=" + string.Join("|", labels));
        Results.Add("NODE_CHILDREN=" + string.Join("|", nodeChildren));
        Results.Add("CONNECTOR_ROOTS=" + connectors.childCount);
        Results.Add("CONNECTOR_PIECES=" + connectorPieces);
        Results.Add("SELECTOR_VISIBLE=" + strip.gameObject.activeInHierarchy);
        Results.Add("CANVAS_ON_SCREEN=" + IsOnScreen(canvasRect));
        Results.Add("SECTION_ON_SCREEN=" + IsOnScreen(sectionRect));

        Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath));
        ScreenCapture.CaptureScreenshot(ScreenshotPath);
        phase = 6;
    }

    private static void CollapseAfterScreenshot()
    {
        buildingModules.onClick.Invoke();
        phase = 3;
    }

    private static void InspectCollapseAndRepeat()
    {
        List<string> drift = Baselines.Where(state => !Matches(state))
            .Select(state => state.Name).ToList();
        Results.Add("COLLAPSE_" + (completedCycles + 1) + "_EXPANDED=" + section.IsExpanded);
        Results.Add("COLLAPSE_" + (completedCycles + 1) + "_DRIFT=" + (drift.Count == 0 ? "none" : string.Join("|", drift)));
        completedCycles++;

        if (completedCycles < 3)
        {
            buildingModules.onClick.Invoke();
            phase = 2;
            return;
        }

        buildingModules.onClick.Invoke();
        phase = 4;
    }

    private static void ClickPlacementNode()
    {
        Transform node = section.transform.Find("Production Chain Canvas/Nodes/Node (basalt_crusher)");
        Button button = node != null ? node.GetComponent<Button>() : null;
        Results.Add("BASALT_BUTTON_INTERACTABLE=" + (button != null && button.interactable));
        if (button != null) button.onClick.Invoke();
        phase = 5;
    }

    private static void InspectPlacementAndFinish()
    {
        BuildingSelector selector = BuildingSelector.Active;
        BuildingPreview preview = UnityEngine.Object.FindObjectOfType<BuildingPreview>();
        Results.Add("PLACEMENT_PREFAB=" + (selector != null && selector.previewPrefab != null ? selector.previewPrefab.name : "null"));
        Results.Add("PLACEMENT_PREVIEW=" + (preview != null));
        Finish();
    }

    private static RectState Capture(string name, RectTransform rect)
    {
        return new RectState { Name = name, Rect = rect, AnchoredPosition = rect.anchoredPosition, SizeDelta = rect.sizeDelta };
    }

    private static bool Matches(RectState state)
    {
        return state.Rect != null &&
               Vector2.Distance(state.AnchoredPosition, state.Rect.anchoredPosition) < 0.001f &&
               Vector2.Distance(state.SizeDelta, state.Rect.sizeDelta) < 0.001f;
    }

    private static bool IsOnScreen(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return corners.Select(corner => RectTransformUtility.WorldToScreenPoint(camera, corner))
            .All(point => point.x >= 0f && point.y >= 0f && point.x <= Screen.width && point.y <= Screen.height);
    }

    private static void Finish()
    {
        SessionState.SetBool(SessionKey, true);
        File.WriteAllLines(ResultPath, Results.ToArray());
        Debug.Log("PRODUCTION_RUNTIME_PROBE\n" + string.Join("\n", Results));
        EditorApplication.update -= Update;
    }
}
