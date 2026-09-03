using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Right-Click Context Menu (3x3 grid, 9 slots) providing quick access to
/// core tools and customizable shortcuts. Directly reproduces the Anno 1800/2070
/// context menu shown in the reference design.
/// </summary>
public class ContextMenuUI : MonoBehaviour
{
    public static ContextMenuUI Instance { get; private set; }

    [Header("UI Structure")]
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private TMP_Text headerLabel;
    [SerializeField] private Transform gridRoot;

    private readonly List<ShortcutSlotUI> slots = new List<ShortcutSlotUI>();
    private Canvas parentCanvas;

    public bool IsOpen => panelRect != null && panelRect.gameObject.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        parentCanvas = GetComponentInParent<Canvas>();
        EnsureVisualTree();
        InitializeSlots();
        LoadCustomizations();
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void EnsureVisualTree()
    {
        if (panelRect != null) return;

        // Ensure RectTransform on this root
        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // Panel Window
        var panelObj = new GameObject("Context Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObj.transform.SetParent(transform, false);
        panelRect = (RectTransform)panelObj.transform;
        panelRect.sizeDelta = new Vector2(204f, 236f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);

        Image bg = panelObj.GetComponent<Image>();
        bg.sprite = ContextMenuIcons.SlotFrame;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.06f, 0.09f, 0.12f, 0.96f);

        // Header Label for Title / Tooltip info
        var headerObj = new GameObject("Header", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        headerObj.transform.SetParent(panelRect, false);
        var headerRect = (RectTransform)headerObj.transform;
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -4f);
        headerRect.sizeDelta = new Vector2(0f, 22f);

        headerLabel = headerObj.GetComponent<TextMeshProUGUI>();
        headerLabel.fontSize = 11f;
        headerLabel.alignment = TextAlignmentOptions.Center;
        headerLabel.color = new Color(0.65f, 0.82f, 0.95f, 0.95f);
        headerLabel.text = "QUICK ACTIONS";

        // 3x3 Grid Root
        var gridObj = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridObj.transform.SetParent(panelRect, false);
        var gRect = (RectTransform)gridObj.transform;
        gRect.anchorMin = new Vector2(0.5f, 0.5f);
        gRect.anchorMax = new Vector2(0.5f, 0.5f);
        gRect.pivot = new Vector2(0.5f, 0.5f);
        gRect.anchoredPosition = new Vector2(0f, -10f);
        gRect.sizeDelta = new Vector2(186f, 186f);

        var grid = gridObj.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(56f, 56f);
        grid.spacing = new Vector2(6f, 6f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.MiddleCenter;

        gridRoot = gridObj.transform;
    }

    private void InitializeSlots()
    {
        if (slots.Count > 0) return;

        for (int i = 0; i < 9; i++)
        {
            bool isCore = (i >= 3 && i <= 5); // Center row: 3 (Demolish), 4 (BuildMenu), 5 (Pipette)

            var slotObj = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ShortcutSlotUI));
            slotObj.transform.SetParent(gridRoot, false);

            var slotUI = slotObj.GetComponent<ShortcutSlotUI>();
            slotUI.Initialize(i, isCore);
            slotUI.OnSlotChanged += HandleSlotChanged;
            slotUI.OnHoverChanged += HandleSlotHoverChanged;
            slotUI.OnExecuted += Hide;

            slots.Add(slotUI);
        }

        SetDefaultShortcuts();
    }

    private void SetDefaultShortcuts()
    {
        // Middle row: Core tools
        slots[3].SetShortcut(ShortcutData.CreateTool(ContextMenuToolType.Demolish, ContextMenuIcons.Pickaxe, "Demolish Mode"), false);
        slots[4].SetShortcut(ShortcutData.CreateTool(ContextMenuToolType.BuildMenu, ContextMenuIcons.HouseSilhouette, "Building Menu"), false);
        slots[5].SetShortcut(ShortcutData.CreateTool(ContextMenuToolType.Pipette, ContextMenuIcons.Pipette, "Pipette Tool"), false);

        // Find common prefabs in scene or registry
        GameObject housePrefab = FindPrefabByName("House", "Worker", "Residence", "Pioneer");
        GameObject roadPrefab = FindPrefabByName("Road", "Street", "Paved");
        GameObject warehousePrefab = FindPrefabByName("Warehouse", "Depot");
        GameObject dirtRoadPrefab = FindPrefabByName("Dirt", "Path", "Road");

        // Row 0: House, Road, Warehouse
        slots[0].SetShortcut(ShortcutData.CreateBuilding(housePrefab, ContextMenuIcons.HouseBuilding, "Residence"), false);
        slots[1].SetShortcut(ShortcutData.CreateBuilding(roadPrefab, ContextMenuIcons.Road, "Paved Street"), false);
        slots[2].SetShortcut(ShortcutData.CreateBuilding(warehousePrefab, ContextMenuIcons.Warehouse, "Warehouse"), false);

        // Row 2: Empty, Dirt Road, Empty
        slots[6].SetShortcut(ShortcutData.CreateEmpty(), false);
        slots[7].SetShortcut(ShortcutData.CreateBuilding(dirtRoadPrefab, ContextMenuIcons.DirtRoad, "Street"), false);
        slots[8].SetShortcut(ShortcutData.CreateEmpty(), false);
    }

    private static GameObject FindPrefabByName(params string[] searchTerms)
    {
        BuildingButton[] buttons = FindObjectsOfType<BuildingButton>(includeInactive: true);
        foreach (var term in searchTerms)
        {
            foreach (var btn in buttons)
            {
                var p = btn.GetBuildingPrefab();
                if (p != null && p.name.ToLower().Contains(term.ToLower()))
                {
                    return p;
                }
            }
        }
        return null;
    }

    private void Update()
    {
        // 1. Right Click Handling
        if (Input.GetMouseButtonDown(1))
        {
            // If preview object is active, building placement cancels preview (handled by BuildingChecker)
            if (BuildingSelector.Active != null && BuildingSelector.Active.previewPrefab != null)
            {
                return;
            }

            // If demolition mode active, right click cancels demolition
            if (DemolitionManager.Instance != null && DemolitionManager.Instance.IsActive)
            {
                return;
            }

            // If pipette tool active, right click cancels pipette
            if (PipetteTool.Instance != null && PipetteTool.Instance.IsActive)
            {
                return;
            }

            // If Context Menu is open, right click closes it
            if (IsOpen)
            {
                Hide();
            }
            else
            {
                // Open at mouse position
                Show(Input.mousePosition);
            }
            return;
        }

        if (!IsOpen) return;

        // 2. Escape closes
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
            return;
        }

        // 3. Left click outside closes
        if (Input.GetMouseButtonDown(0))
        {
            if (panelRect != null && !RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition))
            {
                Hide();
            }
        }
    }

    public void Show(Vector2 screenPos)
    {
        if (panelRect == null) return;

        PositionAtScreenPoint(screenPos);
        panelRect.gameObject.SetActive(true);
        if (headerLabel != null) headerLabel.text = "QUICK ACTIONS";
    }

    public void Hide()
    {
        if (panelRect != null)
        {
            panelRect.gameObject.SetActive(false);
        }
    }

    private void PositionAtScreenPoint(Vector2 screenPos)
    {
        if (panelRect == null) return;

        // Clamp to screen borders so the menu never goes off-screen
        float w = panelRect.rect.width * 0.5f + 10f;
        float h = panelRect.rect.height * 0.5f + 10f;

        float clampedX = Mathf.Clamp(screenPos.x, w, Screen.width - w);
        float clampedY = Mathf.Clamp(screenPos.y, h, Screen.height - h);

        panelRect.position = new Vector3(clampedX, clampedY, 0f);
    }

    private void HandleSlotHoverChanged(ShortcutSlotUI slot, bool hovered)
    {
        if (headerLabel == null) return;

        if (hovered && slot.Data != null && !slot.Data.IsEmpty)
        {
            headerLabel.text = slot.Data.DisplayName.ToUpper();
        }
        else
        {
            headerLabel.text = "QUICK ACTIONS";
        }
    }

    private void HandleSlotChanged(ShortcutSlotUI slot)
    {
        SaveCustomizations();
    }

    private void SaveCustomizations()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            // Do not override core tools
            if (slots[i].IsCoreTool) continue;

            string key = $"ContextMenu_Slot_{i}";
            if (slots[i].Data != null)
            {
                PlayerPrefs.SetString(key, slots[i].Data.Serialize());
            }
        }
        PlayerPrefs.Save();
    }

    private void LoadCustomizations()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsCoreTool) continue;

            string key = $"ContextMenu_Slot_{i}";
            if (PlayerPrefs.HasKey(key))
            {
                string ser = PlayerPrefs.GetString(key);
                ShortcutData loaded = ShortcutData.Deserialize(ser);
                if (loaded != null && !loaded.IsEmpty)
                {
                    // Restore fallback icon if deserialization couldn't resolve Sprite
                    if (loaded.Icon == null)
                    {
                        if (i == 0) loaded.Icon = ContextMenuIcons.HouseBuilding;
                        else if (i == 1) loaded.Icon = ContextMenuIcons.Road;
                        else if (i == 2) loaded.Icon = ContextMenuIcons.Warehouse;
                        else if (i == 7) loaded.Icon = ContextMenuIcons.DirtRoad;
                        else loaded.Icon = ContextMenuIcons.HouseBuilding;
                    }
                    slots[i].SetShortcut(loaded, false);
                }
            }
        }
    }
}
