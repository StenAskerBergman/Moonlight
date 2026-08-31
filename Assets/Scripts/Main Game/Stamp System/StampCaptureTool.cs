using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles rectangular world-space selection for capturing existing buildings
/// and roads into a new <see cref="StampData"/>. Activated via hotkey (B) or UI.
///
/// Flow:
/// 1. Player presses B → enters StampCapture mode.
/// 2. Player clicks and drags on the terrain to define a rectangle.
/// 3. On mouse-up, all buildings and roads inside the rectangle are collected.
/// 4. A naming dialog is opened to finalise the stamp.
/// 5. The stamp is saved to <see cref="StampLibrary"/>.
/// </summary>
public class StampCaptureTool : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode activateKey = KeyCode.B;
    [SerializeField] private LayerMask groundLayer = 1 << 6;
    [SerializeField] private float maxRaycastDistance = 1000f;

    [Header("Dependencies")]
    [SerializeField] private StampSelectionRenderer selectionRenderer;
    [SerializeField] private StampCreationDialog creationDialog;

    public bool IsCapturing { get; private set; }

    private bool _isDragging;
    private Vector3 _dragStart;
    private Vector3 _dragEnd;

    // ───────── Unity Lifecycle ─────────

    private void Update()
    {
        // Toggle activation
        if (Input.GetKeyDown(activateKey))
        {
            if (IsCapturing)
                CancelCapture();
            else
                ActivateCapture();
        }

        if (!IsCapturing) return;

        // Cancel on Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelCapture();
            return;
        }

        // Don't capture when clicking on UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        HandleDragInput();
    }

    // ───────── Public API ─────────

    public void ActivateCapture()
    {
        if (ToolModeManager.Instance != null)
        {
            if (!ToolModeManager.Instance.RequestMode(ToolModeManager.ToolMode.StampCapture))
                return;
        }

        IsCapturing = true;
        _isDragging = false;
        Debug.Log("[StampCaptureTool] Capture mode activated. Click and drag to select an area.");
    }

    public void CancelCapture()
    {
        IsCapturing = false;
        _isDragging = false;

        if (selectionRenderer != null)
            selectionRenderer.Hide();

        if (ToolModeManager.Instance != null)
            ToolModeManager.Instance.ReleaseMode(ToolModeManager.ToolMode.StampCapture);
    }

    // ───────── Input Handling ─────────

    private void HandleDragInput()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // Start drag
        if (Input.GetMouseButtonDown(0))
        {
            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, groundLayer))
            {
                _isDragging = true;
                _dragStart = hit.point;
                _dragEnd = hit.point;
            }
        }

        // Continue drag
        if (_isDragging && Input.GetMouseButton(0))
        {
            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, groundLayer))
            {
                _dragEnd = hit.point;
                if (selectionRenderer != null)
                    selectionRenderer.UpdateSelection(_dragStart, _dragEnd);
            }
        }

        // End drag → collect objects
        if (_isDragging && Input.GetMouseButtonUp(0))
        {
            _isDragging = false;

            if (selectionRenderer != null)
                selectionRenderer.Hide();

            CollectAndCreateStamp(_dragStart, _dragEnd);
        }
    }

    // ───────── Collection ─────────

    private void CollectAndCreateStamp(Vector3 worldStart, Vector3 worldEnd)
    {
        // Determine the island we're on
        Island island = IslandManager.instance != null ? IslandManager.instance.GetHoveredIsland() : null;
        if (island == null)
        {
            Debug.LogWarning("[StampCaptureTool] No island found under selection. Cancelling.");
            CancelCapture();
            return;
        }

        GridSystem gridSystem = island.GetComponentInChildren<GridSystem>();
        if (gridSystem == null)
        {
            Debug.LogWarning("[StampCaptureTool] Island has no GridSystem. Cancelling.");
            CancelCapture();
            return;
        }

        // Compute axis-aligned bounding box in grid coordinates
        Vector3Int cellStart = gridSystem.WorldToCell(worldStart);
        Vector3Int cellEnd = gridSystem.WorldToCell(worldEnd);

        int minX = Mathf.Min(cellStart.x, cellEnd.x);
        int maxX = Mathf.Max(cellStart.x, cellEnd.x);
        int minZ = Mathf.Min(cellStart.z, cellEnd.z);
        int maxZ = Mathf.Max(cellStart.z, cellEnd.z);

        // Stamp origin = min corner
        Vector3Int originCell = new Vector3Int(minX, 0, minZ);
        Vector3 originWorld = gridSystem.transform.TransformPoint(
            new Vector3(originCell.x + 0.5f, 0f, originCell.z + 0.5f));

        StampData stamp = StampData.CreateNew("New Stamp");

        // Track buildings we've already added (a multi-cell building should appear once)
        HashSet<Building> collectedBuildings = new HashSet<Building>();

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                Cell cell = gridSystem.GetCell(x, z);
                if (cell == null) continue;

                // Collect roads
                if (cell.isRoad)
                {
                    stamp.roads.Add(new StampRoadEntry
                    {
                        relativeCell = new Vector2Int(x - originCell.x, z - originCell.z)
                    });
                }

                // Collect buildings
                if (cell.occupyingBuilding != null && !collectedBuildings.Contains(cell.occupyingBuilding))
                {
                    Building building = cell.occupyingBuilding;
                    collectedBuildings.Add(building);

                    BuildingProperties props = building.GetComponent<BuildingProperties>();
                    BuildingData data = props != null ? props.buildingData : building.buildingData;

                    if (data == null)
                    {
                        Debug.LogWarning($"[StampCaptureTool] Building '{building.name}' has no BuildingData. Skipping.");
                        continue;
                    }

                    // Calculate position relative to stamp origin
                    Vector3 relativePos = building.transform.position - originWorld;

                    stamp.buildings.Add(new StampBuildingEntry
                    {
                        buildingIdentifier = data.Id.ToString(),
                        relativePosition = relativePos,
                        rotationY = building.transform.eulerAngles.y,
                        buildingSize = data.buildingSize
                    });
                }
            }
        }

        if (stamp.TotalEntries == 0)
        {
            Debug.LogWarning("[StampCaptureTool] No objects found in selection.");
            CancelCapture();
            return;
        }

        stamp.RecalculateFootprint();

        Debug.Log($"[StampCaptureTool] Captured {stamp.buildings.Count} building(s) and {stamp.roads.Count} road(s).");

        // Open the creation dialog or save directly
        if (creationDialog != null)
        {
            creationDialog.Open(stamp);
        }
        else
        {
            // Fallback: save immediately with default name
            if (StampLibrary.Instance != null)
            {
                StampLibrary.Instance.SaveStamp(stamp);
                Debug.Log($"[StampCaptureTool] Stamp '{stamp.stampName}' saved to library.");
            }
        }

        CancelCapture();
    }
}
