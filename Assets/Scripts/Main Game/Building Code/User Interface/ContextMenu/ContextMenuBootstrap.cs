using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Automatically bootstraps the ContextMenu, PipetteTool, and ensures all
/// BuildingButtons in the scene have BuildingSlotDragHandler attached so
/// they can be dragged into shortcut slots immediately.
/// </summary>
public static class ContextMenuBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SetupPipetteTool();
        SetupContextMenu();
        EnhanceBuildingButtons();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetupPipetteTool();
        SetupContextMenu();
        EnhanceBuildingButtons();
    }

    private static void SetupPipetteTool()
    {
        if (PipetteTool.Instance != null) return;

        GameObject host = new GameObject(nameof(PipetteTool));
        host.AddComponent<PipetteTool>();
        Object.DontDestroyOnLoad(host);
    }

    private static void SetupContextMenu()
    {
        if (ContextMenuUI.Instance != null) return;

        // Find primary canvas
        Canvas canvas = null;
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
        foreach (Canvas c in canvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.isRootCanvas)
            {
                canvas = c;
                break;
            }
        }

        if (canvas == null && canvases.Length > 0)
        {
            canvas = canvases[0];
        }

        if (canvas == null)
        {
            // Create a canvas if none exists
            GameObject cObj = new GameObject("ContextCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas = cObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
        }

        GameObject menuObj = new GameObject(nameof(ContextMenuUI), typeof(RectTransform));
        menuObj.transform.SetParent(canvas.transform, false);

        // Ensure it renders on top
        menuObj.transform.SetAsLastSibling();
        menuObj.AddComponent<ContextMenuUI>();
    }

    public static void EnhanceBuildingButtons()
    {
        BuildingButton[] buttons = Object.FindObjectsOfType<BuildingButton>(includeInactive: true);
        foreach (BuildingButton btn in buttons)
        {
            if (btn == null) continue;
            var dragHandler = btn.GetComponent<BuildingSlotDragHandler>();
            if (dragHandler == null)
            {
                dragHandler = btn.gameObject.AddComponent<BuildingSlotDragHandler>();
                dragHandler.SetPayload(btn.GetBuildingPrefab(), null, btn.name);
            }
        }
    }
}
