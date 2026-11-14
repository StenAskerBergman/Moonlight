// tldr: Input and preview for the player's plant/clear foliage tool. Mirrors
// CropFieldPlacementController so the two grid-painting tools behave the same way.
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drives <see cref="FoliagePlacer"/> from the mouse while
/// <see cref="ToolModeManager.ToolMode.FoliagePlacement"/> is held.
///
/// Left button plants the selected species, right button clears whatever is under the cursor. Both
/// support drag painting, guarded by a per-cell latch so one sweep does not plant repeatedly on the
/// same tile.
/// </summary>
public class FoliagePlacementController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FoliagePlacer placer;
    [SerializeField] private Camera activeCamera;

    [Header("Palette")]
    [Tooltip("Species the player can plant. Index selected by SelectSpecies.")]
    [SerializeField] private FoliageSpecies[] palette;
    [SerializeField] private int selectedSpeciesIndex;

    [Header("Raycast")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float maxRaycastDistance = 500f;
    [Tooltip("How far from the cursor to look for a plant when clearing, in world units.")]
    [SerializeField] private float clearSearchRadius = 1.5f;

    [Header("Preview (Optional)")]
    [SerializeField] private GameObject previewIndicator;
    [SerializeField] private Color validColor = new Color(0.35f, 0.9f, 0.4f, 0.6f);
    [SerializeField] private Color invalidColor = new Color(0.9f, 0.35f, 0.35f, 0.6f);
    [Tooltip("Lifts the indicator clear of the terrain surface so it does not z-fight.")]
    [SerializeField] private float previewHeightOffset = 0.05f;

    [Header("Debug")]
    [Tooltip("Toggles foliage placement with no UI, for testing in play mode. None disables it.")]
    [SerializeField] private KeyCode debugToggleKey = KeyCode.T;
    [Tooltip("While the tool is active, number keys 1-9 pick the matching palette entry.")]
    [SerializeField] private bool debugSpeciesHotkeys = true;

    private Renderer _previewRenderer;
    private MaterialPropertyBlock _previewPropBlock;
    private Cell _lastPaintedCell;
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProp = Shader.PropertyToID("_Color");

    public FoliageSpecies SelectedSpecies =>
        palette != null && selectedSpeciesIndex >= 0 && selectedSpeciesIndex < palette.Length
            ? palette[selectedSpeciesIndex]
            : null;

    private Camera ActiveCamera => activeCamera != null ? activeCamera : Camera.main;

    private bool IsActive => ToolModeManager.Instance != null
        && ToolModeManager.Instance.CurrentMode == ToolModeManager.ToolMode.FoliagePlacement;

    private void Awake()
    {
        _previewPropBlock = new MaterialPropertyBlock();
        if (previewIndicator != null) _previewRenderer = previewIndicator.GetComponentInChildren<Renderer>();
        if (placer == null) placer = GetComponent<FoliagePlacer>();
    }

    /// <summary>Enters foliage mode with the given palette entry, for the build menu to call.</summary>
    public bool BeginPlacement(int speciesIndex)
    {
        if (ToolModeManager.Instance == null) return false;
        selectedSpeciesIndex = speciesIndex;
        return ToolModeManager.Instance.RequestMode(ToolModeManager.ToolMode.FoliagePlacement);
    }

    public void EndPlacement()
    {
        if (ToolModeManager.Instance != null)
            ToolModeManager.Instance.ReleaseMode(ToolModeManager.ToolMode.FoliagePlacement);
        if (previewIndicator != null) previewIndicator.SetActive(false);
        _lastPaintedCell = null;
    }

    private void Update()
    {
        HandleDebugKeys();

        if (!IsActive)
        {
            if (previewIndicator != null && previewIndicator.activeSelf) previewIndicator.SetActive(false);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) { EndPlacement(); return; }

        UpdateCursorAndPreview();

        // Without this, the click that presses the menu slot also plants a tree under the HUD.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (Input.GetMouseButton(0)) PaintUnderCursor(true);
        else if (Input.GetMouseButton(1)) PaintUnderCursor(false);
        else _lastPaintedCell = null;
    }

    /// <summary>
    /// Keyboard-only entry point so the tool can be exercised in play mode before any UI exists.
    /// </summary>
    private void HandleDebugKeys()
    {
        if (debugToggleKey != KeyCode.None && Input.GetKeyDown(debugToggleKey))
        {
            if (IsActive) EndPlacement();
            else BeginPlacement(selectedSpeciesIndex);
        }

        if (!debugSpeciesHotkeys || !IsActive || palette == null) return;
        int count = Mathf.Min(palette.Length, 9);
        for (int i = 0; i < count; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) selectedSpeciesIndex = i;
    }

    private bool TryHitGround(out RaycastHit hit)
    {
        hit = default;
        Camera cam = ActiveCamera;
        if (cam == null) return false;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, out hit, maxRaycastDistance, groundLayer);
    }

    private void UpdateCursorAndPreview()
    {
        if (previewIndicator == null) return;

        if (placer == null || !TryHitGround(out RaycastHit hit))
        {
            previewIndicator.SetActive(false);
            return;
        }

        IslandFoliagePlacer island = placer.GetIslandAt(hit.collider);
        Cell cell = placer.GetCellAt(hit.collider, hit.point);
        if (cell == null)
        {
            previewIndicator.SetActive(false);
            return;
        }

        bool canPlant = placer.CanPlant(island, cell, SelectedSpecies, out _);
        previewIndicator.SetActive(true);
        // Cell coordinates are MapGrid-local; islands sit at world offsets, so the indicator has to go
        // through the island's transform rather than being used as a world position directly.
        Vector3 localCentre = cell.localCenter + Vector3.up * previewHeightOffset;
        previewIndicator.transform.position = island != null
            ? island.transform.TransformPoint(localCentre)
            : localCentre;

        if (_previewRenderer == null) return;
        Color targetColor = canPlant ? validColor : invalidColor;
        _previewRenderer.GetPropertyBlock(_previewPropBlock);
        _previewPropBlock.SetColor(BaseColorProp, targetColor);
        _previewPropBlock.SetColor(ColorProp, targetColor);
        _previewRenderer.SetPropertyBlock(_previewPropBlock);
    }

    private void PaintUnderCursor(bool plant)
    {
        if (placer == null || !TryHitGround(out RaycastHit hit)) return;

        IslandFoliagePlacer island = placer.GetIslandAt(hit.collider);
        if (island == null) return;

        if (plant)
        {
            Cell cell = placer.GetCellAt(hit.collider, hit.point);
            if (cell == null || cell == _lastPaintedCell) return;
            _lastPaintedCell = cell;
            placer.Plant(island, cell, SelectedSpecies, hit.point);
            return;
        }

        // Clearing is point-based rather than cell-based, so dragging thins a stand out one plant at
        // a time instead of emptying whole tiles.
        placer.ClearNearest(island, hit.point, clearSearchRadius);
    }
}
