using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Allows any building slot / button (from the Building Menu or Production Chains)
/// to be dragged and dropped into Context Menu shortcut slots or Action Bar slots.
/// </summary>
public class BuildingSlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public static ShortcutData CurrentDraggedShortcut { get; private set; }
    public static GameObject CurrentDragSource { get; private set; }

    [SerializeField] private GameObject buildingPrefab;
    [SerializeField] private BuildingData buildingData;
    [SerializeField] private Sprite icon;
    [SerializeField] private string displayName;

    private static GameObject dragGhost;
    private static Canvas dragCanvas;

    public void SetPayload(GameObject prefab, Sprite customIcon, string name, BuildingData data = null)
    {
        buildingPrefab = prefab;
        icon = customIcon;
        displayName = name;
        buildingData = data;
    }

    public ShortcutData BuildShortcut()
    {
        // Auto-resolve building prefab if null
        if (buildingPrefab == null)
        {
            var btn = GetComponent<BuildingButton>();
            if (btn != null) buildingPrefab = btn.GetBuildingPrefab();
        }

        // Auto-resolve icon if null
        if (icon == null)
        {
            Image[] imgs = GetComponentsInChildren<Image>(true);
            foreach (Image img in imgs)
            {
                if (img.gameObject != gameObject && img.sprite != null)
                {
                    icon = img.sprite;
                    break;
                }
            }
            if (icon == null)
            {
                var mainImg = GetComponent<Image>();
                if (mainImg != null && mainImg.sprite != null) icon = mainImg.sprite;
            }
        }

        // Auto-resolve name if empty
        string disp = displayName;
        if (string.IsNullOrEmpty(disp))
        {
            if (buildingData != null && !string.IsNullOrEmpty(buildingData.buildingName))
                disp = buildingData.buildingName;
            else if (buildingPrefab != null)
                disp = buildingPrefab.name.Replace("(Clone)", "").Trim();
            else
                disp = name.Replace("(Clone)", "").Trim();
        }

        return ShortcutData.CreateBuilding(buildingPrefab, icon, disp, buildingData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        CurrentDraggedShortcut = BuildShortcut();
        CurrentDragSource = gameObject;

        CreateDragGhost(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragGhost != null)
        {
            dragGhost.transform.position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragGhost != null)
        {
            Destroy(dragGhost);
            dragGhost = null;
        }

        CurrentDraggedShortcut = null;
        CurrentDragSource = null;
    }

    private void CreateDragGhost(PointerEventData eventData)
    {
        if (dragCanvas == null)
        {
            GameObject canvasObj = new GameObject("DragGhostCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            dragCanvas = canvasObj.GetComponent<Canvas>();
            dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            dragCanvas.sortingOrder = 9999;
            DontDestroyOnLoad(canvasObj);
        }

        dragGhost = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        dragGhost.transform.SetParent(dragCanvas.transform, false);

        var rect = (RectTransform)dragGhost.transform;
        rect.sizeDelta = new Vector2(56f, 56f);
        rect.position = eventData.position;

        var img = dragGhost.GetComponent<Image>();
        img.sprite = CurrentDraggedShortcut?.Icon ?? icon ?? ContextMenuIcons.HouseBuilding;
        img.preserveAspect = true;
        img.raycastTarget = false;

        var cg = dragGhost.GetComponent<CanvasGroup>();
        cg.alpha = 0.75f;
        cg.blocksRaycasts = false;
    }
}
