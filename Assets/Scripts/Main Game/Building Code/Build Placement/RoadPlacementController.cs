using UnityEngine;
using UnityEngine.EventSystems;

// Player-facing input for laying roads. Toggle road mode, then hold the left
// mouse button to paint road tiles across the grid, or the right mouse button to
// clear them. Escape, or toggling again, leaves the mode.
//
// Whether a given cell may become a road (terrain type, occupancy, already a
// road) is RoadPlacer's decision - this only works out which cell the player is
// pointing at and how often to act on it.
public class RoadPlacementController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Optional. Left empty, the RoadPlacer on this GameObject is used, then any in the scene.")]
    [SerializeField] private RoadPlacer roadPlacer;
    [Tooltip("Optional. Left empty, Camera.main is used.")]
    [SerializeField] private Camera worldCamera;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.R;
    [Tooltip("Surfaces the cursor may pick a road cell from. Defaults to the 'Ground' layer.")]
    [SerializeField] private LayerMask groundLayer = 1 << 6;
    [SerializeField] private float maxRaycastDistance = 1000f;

    // Whether the player is currently in road-laying mode.
    public bool RoadModeActive { get; private set; }

    // Raised when road mode is entered or left, so a HUD can reflect the state.
    public static event System.Action<bool> OnRoadModeChanged;

    // While a drag is in progress, act only once per cell the cursor crosses.
    private Cell _lastPaintedCell;

    private RoadPlacer ActivePlacer
    {
        get
        {
            if (roadPlacer != null) return roadPlacer;
            roadPlacer = GetComponent<RoadPlacer>();
            if (roadPlacer == null) roadPlacer = FindObjectOfType<RoadPlacer>();
            return roadPlacer;
        }
    }

    private Camera ActiveCamera => worldCamera != null ? worldCamera : Camera.main;

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleRoadMode();
        }

        if (!RoadModeActive) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitRoadMode();
            return;
        }

        // A building being positioned owns the cursor - don't lay road under it.
        if (BuildingChecker.instance != null && BuildingChecker.instance.IsPlacingBuilding) return;

        // Releasing a button ends the current stroke, so the same cell can be
        // painted again on the next one.
        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
        {
            _lastPaintedCell = null;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (Input.GetMouseButton(0))
        {
            PaintCellUnderCursor(true);
        }
        else if (Input.GetMouseButton(1))
        {
            PaintCellUnderCursor(false);
        }
    }

    public void ToggleRoadMode()
    {
        if (RoadModeActive)
        {
            ExitRoadMode();
        }
        else
        {
            EnterRoadMode();
        }
    }

    public void EnterRoadMode()
    {
        if (RoadModeActive) return;

        RoadModeActive = true;
        _lastPaintedCell = null;
        OnRoadModeChanged?.Invoke(true);
    }

    public void ExitRoadMode()
    {
        if (!RoadModeActive) return;

        RoadModeActive = false;
        _lastPaintedCell = null;
        OnRoadModeChanged?.Invoke(false);
    }

    private void PaintCellUnderCursor(bool place)
    {
        RoadPlacer placer = ActivePlacer;
        Camera cam = ActiveCamera;
        if (placer == null || cam == null) return;

        RaycastHit hit;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out hit, maxRaycastDistance, groundLayer)) return;

        Cell cell = placer.GetCellAtWorldPosition(hit.point);
        if (cell == null || cell == _lastPaintedCell) return;

        _lastPaintedCell = cell;

        if (place)
        {
            placer.PlaceRoad(cell);
        }
        else
        {
            placer.RemoveRoad(cell);
        }
    }
}
