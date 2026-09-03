using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A single interactive shortcut slot. Used in the 3x3 Right-Click Context Menu
/// and the Action Bar. Supports execution, drag-and-drop customization from the
/// Building Menu, slot swapping, and clearing.
/// </summary>
public class ShortcutSlotUI : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IDropHandler
{
    [SerializeField] private int slotIndex;
    [SerializeField] private bool isCoreTool;

    public int SlotIndex
    {
        get => slotIndex;
        set => slotIndex = value;
    }

    public bool IsCoreTool
    {
        get => isCoreTool;
        set => isCoreTool = value;
    }

    public ShortcutData Data { get; private set; } = ShortcutData.CreateEmpty();

    public event Action<ShortcutSlotUI> OnSlotChanged;
    public event Action<ShortcutSlotUI, bool> OnHoverChanged;
    public event Action OnExecuted;

    // Visual references
    private Image borderImage;
    private Image fillImage;
    private Image iconImage;
    private bool isHovered;
    private bool isDragTarget;

    // Colors matching Anno 1800 aesthetic in media_1788407016174.png
    private static readonly Color ColorBorderNormal = new Color(0.24f, 0.32f, 0.38f, 0.90f);
    private static readonly Color ColorBorderCore = new Color(0.22f, 0.55f, 0.85f, 1.0f);
    private static readonly Color ColorBorderHover = new Color(0.45f, 0.75f, 0.95f, 1.0f);
    private static readonly Color ColorBorderDropValid = new Color(0.95f, 0.80f, 0.25f, 1.0f);

    private static readonly Color ColorFillEmpty = new Color(0.08f, 0.10f, 0.12f, 0.90f);
    private static readonly Color ColorFillFilled = new Color(0.14f, 0.18f, 0.22f, 0.95f);
    private static readonly Color ColorFillCore = new Color(0.10f, 0.22f, 0.35f, 0.98f);

    private static ShortcutSlotUI activeDraggingSlot;

    public void Initialize(int index, bool coreTool)
    {
        slotIndex = index;
        isCoreTool = coreTool;
        EnsureVisualComponents();
        RefreshVisuals();
    }

    public void SetShortcut(ShortcutData shortcut, bool notify = true)
    {
        Data = shortcut ?? ShortcutData.CreateEmpty();
        RefreshVisuals();
        if (notify) OnSlotChanged?.Invoke(this);
    }

    public void ClearShortcut(bool notify = true)
    {
        if (isCoreTool) return; // Core tools shouldn't be cleared
        SetShortcut(ShortcutData.CreateEmpty(), notify);
    }

    private void EnsureVisualComponents()
    {
        if (borderImage != null) return;

        // Root is the border
        borderImage = GetComponent<Image>();
        if (borderImage == null) borderImage = gameObject.AddComponent<Image>();
        borderImage.sprite = ContextMenuIcons.SlotFrame;
        borderImage.type = Image.Type.Sliced;

        // Inner Fill
        Transform fillTf = transform.Find("Fill");
        GameObject fillObj = fillTf != null ? fillTf.gameObject : new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (fillTf == null)
        {
            fillObj.transform.SetParent(transform, false);
            var fr = (RectTransform)fillObj.transform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(2f, 2f);
            fr.offsetMax = new Vector2(-2f, -2f);
        }
        fillImage = fillObj.GetComponent<Image>();
        fillImage.sprite = ContextMenuIcons.SlotFrame;
        fillImage.type = Image.Type.Sliced;
        fillImage.raycastTarget = false;

        // Icon Image
        Transform iconTf = fillObj.transform.Find("Icon");
        GameObject iconObj = iconTf != null ? iconTf.gameObject : new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (iconTf == null)
        {
            iconObj.transform.SetParent(fillObj.transform, false);
            var ir = (RectTransform)iconObj.transform;
            ir.anchorMin = new Vector2(0.1f, 0.1f);
            ir.anchorMax = new Vector2(0.9f, 0.9f);
            ir.offsetMin = Vector2.zero;
            ir.offsetMax = Vector2.zero;
        }
        iconImage = iconObj.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
    }

    public void RefreshVisuals()
    {
        EnsureVisualComponents();

        bool empty = Data == null || Data.IsEmpty;

        // Border color
        if (isDragTarget)
        {
            borderImage.color = ColorBorderDropValid;
        }
        else if (isHovered)
        {
            borderImage.color = ColorBorderHover;
        }
        else if (isCoreTool)
        {
            borderImage.color = ColorBorderCore;
        }
        else
        {
            borderImage.color = ColorBorderNormal;
        }

        // Fill color
        if (empty)
        {
            fillImage.color = ColorFillEmpty;
        }
        else if (isCoreTool)
        {
            fillImage.color = ColorFillCore;
        }
        else
        {
            fillImage.color = ColorFillFilled;
        }

        // Icon
        if (!empty && Data.Icon != null)
        {
            iconImage.sprite = Data.Icon;
            iconImage.enabled = true;
            iconImage.color = isCoreTool ? new Color(0.75f, 0.95f, 1f, 1f) : Color.white;
        }
        else
        {
            iconImage.enabled = false;
        }
    }

    public void Execute()
    {
        if (Data == null || Data.IsEmpty) return;

        if (Data.Type == ShortcutType.Tool)
        {
            switch (Data.ToolType)
            {
                case ContextMenuToolType.Demolish:
                    if (DemolitionManager.Instance != null)
                    {
                        DemolitionManager.Instance.ToggleMode();
                    }
                    OnExecuted?.Invoke();
                    break;

                case ContextMenuToolType.BuildMenu:
                    ToggleBuildingMenu();
                    OnExecuted?.Invoke();
                    break;

                case ContextMenuToolType.Pipette:
                    if (PipetteTool.Instance != null)
                    {
                        PipetteTool.Instance.ToggleMode();
                    }
                    OnExecuted?.Invoke();
                    break;
            }
        }
        else if (Data.Type == ShortcutType.Building)
        {
            if (Data.BuildingPrefab != null && BuildingSelector.Active != null)
            {
                BuildingSelector.Active.CancelPreview();
                BuildingSelector.Active.SpawnPreview(Data.BuildingPrefab);
                OnExecuted?.Invoke();
            }
            else if (Data.BuildingData != null)
            {
                ProductionPlacementAdapter adapter = FindObjectOfType<ProductionPlacementAdapter>();
                if (adapter != null)
                {
                    adapter.BeginPlacement(Data.BuildingData);
                    OnExecuted?.Invoke();
                }
            }
        }
    }

    private static void ToggleBuildingMenu()
    {
        // Try finding HUD (Bot Building Window)
        GameObject buildingWin = GameObject.Find("HUD (Bot Building Window)");
        if (buildingWin != null)
        {
            buildingWin.SetActive(!buildingWin.activeSelf);
            return;
        }

        // Try ReverseBool on Build btn
        GameObject buildBtn = GameObject.Find("Build btn - Building");
        if (buildBtn != null)
        {
            var rev = buildBtn.GetComponent<ReverseBool>();
            if (rev != null)
            {
                rev.InvertBool_Method();
                return;
            }
        }

        // Try ProductionTierPageIntegration
        var prodPage = FindObjectOfType<ProductionTierPageIntegration>(includeInactive: true);
        if (prodPage != null)
        {
            prodPage.gameObject.SetActive(!prodPage.gameObject.activeSelf);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            Execute();
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            // Right clicking a custom shortcut clears it
            if (!isCoreTool && Data != null && !Data.IsEmpty)
            {
                ClearShortcut();
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (BuildingSlotDragHandler.CurrentDraggedShortcut != null || activeDraggingSlot != null)
        {
            isDragTarget = true;
        }
        RefreshVisuals();
        OnHoverChanged?.Invoke(this, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isDragTarget = false;
        RefreshVisuals();
        OnHoverChanged?.Invoke(this, false);
    }

    public void OnDrop(PointerEventData eventData)
    {
        isDragTarget = false;

        // 1. Dropped from building menu slot
        if (BuildingSlotDragHandler.CurrentDraggedShortcut != null)
        {
            SetShortcut(BuildingSlotDragHandler.CurrentDraggedShortcut);
            return;
        }

        // 2. Dropped from another shortcut slot
        if (activeDraggingSlot != null && activeDraggingSlot != this)
        {
            ShortcutData sourceData = activeDraggingSlot.Data;
            ShortcutData targetData = this.Data;

            // Swap shortcuts
            this.SetShortcut(sourceData);
            activeDraggingSlot.SetShortcut(targetData);
            return;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Data == null || Data.IsEmpty) return;
        activeDraggingSlot = this;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Drag in progress
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // If dropped outside of any UI slot, clear it!
        if (eventData.pointerCurrentRaycast.gameObject == null)
        {
            ClearShortcut();
        }
        else
        {
            ShortcutSlotUI dropSlot = eventData.pointerCurrentRaycast.gameObject.GetComponentInParent<ShortcutSlotUI>();
            if (dropSlot == null)
            {
                ClearShortcut();
            }
        }

        activeDraggingSlot = null;
        RefreshVisuals();
    }
}
