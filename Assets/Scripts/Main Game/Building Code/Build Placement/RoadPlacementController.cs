using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Player-facing input for laying roads. Toggle road mode, then press the left mouse
// button on the cell the road should start from and drag to where it should end:
// the route is planned as you drag and only committed on release, so a long stretch
// costs one gesture instead of a careful hand-drawn sweep. The planned run takes the
// cheapest legal path, reusing road you already own and steering around buildings and
// unbuildable ground. The right mouse button clears road under the cursor, and
// cancels a drag in progress. Escape, or toggling again, leaves the mode.
//
// Whether a given cell may become a road (terrain type, occupancy, already a road)
// is RoadPlacer's decision, and which cells to string together is
// RoadRoutePlanner's - this only works out what the player is pointing at.
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

    [Header("Route")]
    [Tooltip("Longest run a single drag may build. Drags that would need more new tiles than this show as unroutable.")]
    [SerializeField] private int maxRouteLength = RoadRoutePlanner.DefaultMaxNewTiles;

    [Header("Route Preview")]
    [Tooltip("Cells the drag would build.")]
    [SerializeField] private Color plannedColor = new Color(0.2f, 0.9f, 0.2f, 0.6f);
    [Tooltip("Cells the route runs through that are already road, and cost nothing.")]
    [SerializeField] private Color reusedColor = new Color(0.25f, 0.6f, 0.95f, 0.5f);
    [Tooltip("Shown when the cursor is over a cell no road can reach or occupy.")]
    [SerializeField] private Color invalidColor = new Color(0.9f, 0.2f, 0.2f, 0.6f);
    [SerializeField] private float previewHeightOffset = 0.05f;

    // Whether the player is currently in road-laying mode.
    public bool RoadModeActive { get; private set; }

    // The run the current drag would build, empty when nothing is being dragged.
    public IReadOnlyList<Cell> PlannedRoute => _plannedRoute;

    // How many tiles the current drag would actually build - what a HUD should price.
    public int PlannedNewTileCount => RoadRoutePlanner.CountNewTiles(_plannedRoute);

    // Raised when road mode is entered or left, so a HUD can reflect the state.
    public static event System.Action<bool> OnRoadModeChanged;

    // Raised whenever the planned run changes during a drag, so a HUD can show its cost.
    public static event System.Action<IReadOnlyList<Cell>> OnPlannedRouteChanged;

    // The cell the current drag started on, and the one it currently ends on.
    private Cell _dragAnchor;
    private Cell _dragEnd;
    private readonly List<Cell> _plannedRoute = new List<Cell>();

    // While erasing, act only once per cell the cursor crosses.
    private Cell _lastErasedCell;

    // Which button, if any, began a stroke that this controller owns. A press that
    // happened while something else owned the cursor never becomes a road stroke.
    private int _activeStrokeButton = -1;

    // Road type this controller lays. Null falls back to the placer's default.
    private RoadDefinition _selectedDefinition;

    private readonly List<GameObject> _previewTiles = new List<GameObject>();
    private MaterialPropertyBlock _previewPropBlock;
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProp = Shader.PropertyToID("_Color");

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

    private RoadDefinition ActiveDefinition
    {
        get
        {
            if (_selectedDefinition != null) return _selectedDefinition;
            RoadPlacer placer = ActivePlacer;
            return placer != null ? placer.DefaultRoadDefinition : null;
        }
    }

    private void Awake()
    {
        _previewPropBlock = new MaterialPropertyBlock();
    }

    private void OnDestroy()
    {
        foreach (GameObject tile in _previewTiles)
        {
            if (tile != null) Destroy(tile);
        }
        _previewTiles.Clear();
    }

    // Lets a HUD pick which road is laid. Null restores the placer's default.
    public void SetRoadDefinition(RoadDefinition definition)
    {
        _selectedDefinition = definition;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleRoadMode();
        }

        if (!RoadModeActive)
        {
            CancelDrag();
            _activeStrokeButton = -1;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Escape backs out of the drag first, and only then out of road mode.
            if (_dragAnchor != null)
            {
                CancelDrag();
                _activeStrokeButton = -1;
            }
            else
            {
                ExitRoadMode();
            }
            return;
        }

        // A building being positioned owns the cursor - don't lay road under it, and do
        // not let a press made while it did turn into a stroke once it is gone.
        bool cursorOwnedByBlueprint = BuildingChecker.instance != null
                                      && BuildingChecker.instance.IsPlacingBuilding;
        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (cursorOwnedByBlueprint || pointerOverUI)
        {
            // Abandon a stroke in progress rather than resuming it on the far side,
            // and take the cursor marker off the map while the cursor is not on it.
            CancelDrag();
            HidePreview();
            _activeStrokeButton = -1;
            _lastErasedCell = null;
            return;
        }

        Cell hovered = GetCellUnderCursor();

        // Right-clicking during a drag throws the route away instead of erasing.
        if (Input.GetMouseButtonDown(1) && _dragAnchor != null)
        {
            CancelDrag();
            _activeStrokeButton = -1;
            return;
        }

        // A stroke starts on the press only. Holding a button that went down elsewhere -
        // over the HUD, or on the click that placed a building - carries no road intent.
        if (Input.GetMouseButtonDown(0))
        {
            _activeStrokeButton = 0;
            BeginDrag(hovered);
        }
        else if (Input.GetMouseButtonDown(1))
        {
            _activeStrokeButton = 1;
            _lastErasedCell = null;
        }

        switch (_activeStrokeButton)
        {
            case 0:
                if (Input.GetMouseButtonUp(0))
                {
                    CommitDrag();
                    _activeStrokeButton = -1;
                }
                else if (Input.GetMouseButton(0))
                {
                    UpdateDrag(hovered);
                }
                else
                {
                    // The release happened somewhere this controller never saw.
                    CancelDrag();
                    _activeStrokeButton = -1;
                }
                break;

            case 1:
                if (Input.GetMouseButton(1))
                {
                    EraseCell(hovered);
                }
                else
                {
                    _activeStrokeButton = -1;
                    _lastErasedCell = null;
                }
                break;

            default:
                // Idle: show what a click on this cell would do.
                ShowHoverPreview(hovered);
                break;
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
        _lastErasedCell = null;

        // Entering road mode on a click must not consume that same click as a road.
        _activeStrokeButton = -1;
        OnRoadModeChanged?.Invoke(true);
    }

    public void ExitRoadMode()
    {
        if (!RoadModeActive) return;

        RoadModeActive = false;
        CancelDrag();
        HidePreview();
        _lastErasedCell = null;
        _activeStrokeButton = -1;
        OnRoadModeChanged?.Invoke(false);
    }

    #region Drag

    private void BeginDrag(Cell cell)
    {
        _dragAnchor = cell;
        _dragEnd = null;
        UpdateDrag(cell);
    }

    private void UpdateDrag(Cell cell)
    {
        if (_dragAnchor == null)
        {
            // The press landed off-grid; a drag can still start once the cursor
            // reaches the island.
            if (cell == null) return;
            _dragAnchor = cell;
        }

        if (cell == _dragEnd && _plannedRoute.Count > 0) return;

        _dragEnd = cell;
        ReplanRoute();
        ShowRoutePreview();
    }

    private void ReplanRoute()
    {
        _plannedRoute.Clear();

        RoadPlacer placer = ActivePlacer;
        if (placer != null && _dragAnchor != null && _dragEnd != null)
        {
            List<Cell> route = RoadRoutePlanner.Plan(
                placer.Grid, placer, ActiveDefinition, _dragAnchor, _dragEnd, maxRouteLength);

            if (route != null) _plannedRoute.AddRange(route);
        }

        OnPlannedRouteChanged?.Invoke(_plannedRoute);
    }

    private void CommitDrag()
    {
        RoadPlacer placer = ActivePlacer;
        if (placer != null && _plannedRoute.Count > 0)
        {
            placer.PlaceRoute(_plannedRoute, ActiveDefinition);
        }

        ClearDragState();
    }

    private void CancelDrag()
    {
        if (_dragAnchor == null && _plannedRoute.Count == 0) return;

        ClearDragState();
    }

    private void ClearDragState()
    {
        _dragAnchor = null;
        _dragEnd = null;
        _plannedRoute.Clear();
        HidePreview();
        OnPlannedRouteChanged?.Invoke(_plannedRoute);
    }

    #endregion

    #region Erase

    private void EraseCell(Cell cell)
    {
        RoadPlacer placer = ActivePlacer;
        if (placer == null || cell == null || cell == _lastErasedCell) return;

        _lastErasedCell = cell;
        placer.RemoveRoad(cell);
        ShowHoverPreview(cell);
    }

    #endregion

    #region Cursor

    private Cell GetCellUnderCursor()
    {
        RoadPlacer placer = ActivePlacer;
        Camera cam = ActiveCamera;
        if (placer == null || cam == null) return null;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, groundLayer)) return null;

        return placer.GetCellAtWorldPosition(hit.point);
    }

    #endregion

    #region Preview

    private void ShowHoverPreview(Cell cell)
    {
        RoadPlacer placer = ActivePlacer;
        if (placer == null || cell == null)
        {
            HidePreview();
            return;
        }

        Color color = cell.isRoad
            ? reusedColor
            : placer.CanPlaceRoad(cell, ActiveDefinition) ? plannedColor : invalidColor;

        ApplyPreviewTile(0, cell, color);
        TrimPreviewTiles(1);
    }

    private void ShowRoutePreview()
    {
        RoadPlacer placer = ActivePlacer;
        if (placer == null)
        {
            HidePreview();
            return;
        }

        // No legal run to the cursor: mark both ends so it is clear which end is
        // the problem rather than silently showing nothing.
        if (_plannedRoute.Count == 0)
        {
            int shown = 0;
            if (_dragAnchor != null) ApplyPreviewTile(shown++, _dragAnchor, invalidColor);
            if (_dragEnd != null && _dragEnd != _dragAnchor) ApplyPreviewTile(shown++, _dragEnd, invalidColor);
            TrimPreviewTiles(shown);
            return;
        }

        for (int index = 0; index < _plannedRoute.Count; index++)
        {
            Cell cell = _plannedRoute[index];
            ApplyPreviewTile(index, cell, cell.isRoad ? reusedColor : plannedColor);
        }

        TrimPreviewTiles(_plannedRoute.Count);
    }

    private void HidePreview()
    {
        TrimPreviewTiles(0);
    }

    private void ApplyPreviewTile(int index, Cell cell, Color color)
    {
        RoadPlacer placer = ActivePlacer;
        if (placer == null || cell == null) return;

        GameObject tile = GetPreviewTile(index);
        tile.SetActive(true);
        tile.transform.position = placer.GetCellWorldCenter(cell) + Vector3.up * previewHeightOffset;

        Renderer tileRenderer = tile.GetComponent<Renderer>();
        if (tileRenderer != null)
        {
            tileRenderer.GetPropertyBlock(_previewPropBlock);
            _previewPropBlock.SetColor(BaseColorProp, color);
            _previewPropBlock.SetColor(ColorProp, color);
            tileRenderer.SetPropertyBlock(_previewPropBlock);
        }
    }

    private GameObject GetPreviewTile(int index)
    {
        while (_previewTiles.Count <= index)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
            tile.name = "Road_RoutePreview";
            tile.transform.SetParent(transform, false);
            tile.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            tile.transform.localScale = new Vector3(0.98f, 0.98f, 1f);

            Collider tileCollider = tile.GetComponent<Collider>();
            if (tileCollider != null) Destroy(tileCollider);

            tile.SetActive(false);
            _previewTiles.Add(tile);
        }

        return _previewTiles[index];
    }

    private void TrimPreviewTiles(int activeCount)
    {
        for (int index = activeCount; index < _previewTiles.Count; index++)
        {
            if (_previewTiles[index] != null && _previewTiles[index].activeSelf)
            {
                _previewTiles[index].SetActive(false);
            }
        }
    }

    #endregion
}
