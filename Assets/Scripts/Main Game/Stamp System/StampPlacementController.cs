using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles previewing and placing a saved <see cref="StampData"/> layout.
/// When a stamp is selected from the library, this controller creates ghost
/// silhouettes for every building and road, follows the cursor, supports
/// 90° rotation, per-building validity colouring, and batch placement.
/// </summary>
public class StampPlacementController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private LayerMask groundLayer = 1 << 6;
    [SerializeField] private float maxRaycastDistance = 1000f;
    [SerializeField] private int rotationStep = 90;

    [Header("Preview Materials")]
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;

    [Header("Dependencies")]
    [SerializeField] private RoadPlacer roadPlacer;

    /// <summary>True while a stamp preview is attached to the cursor.</summary>
    public bool IsPlacing { get; private set; }

    private StampData _activeStamp;
    private GameObject _previewContainer;
    private float _currentRotation; // accumulated stamp rotation in degrees

    // Per-building preview data
    private struct BuildingGhost
    {
        public StampBuildingEntry entry;
        public GameObject ghostObj;
        public GameObject sourcePrefab;
        public bool isValid;
    }

    // Per-road preview data
    private struct RoadGhost
    {
        public StampRoadEntry entry;
        public GameObject ghostObj;
        public bool isValid;
    }

    private List<BuildingGhost> _buildingGhosts = new List<BuildingGhost>();
    private List<RoadGhost> _roadGhosts = new List<RoadGhost>();

    // Cached state
    private Island _currentIsland;
    private GridSystem _currentGridSystem;

    // ───────── Unity Lifecycle ─────────

    private void Update()
    {
        if (!IsPlacing) return;

        // Cancel
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
            return;
        }

        // Rotation via scroll wheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            float delta = scroll > 0 ? rotationStep : -rotationStep;
            _currentRotation = (_currentRotation + delta) % 360f;
        }

        // Don't interact when over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        UpdatePreviewPosition();

        // Confirm placement
        if (Input.GetMouseButtonDown(0))
        {
            ConfirmPlacement();
        }
    }

    // ───────── Public API ─────────

    /// <summary>
    /// Begins placement mode for the given stamp. Creates ghost previews for
    /// every entry in the stamp and attaches them to the cursor.
    /// </summary>
    public void BeginPlacement(StampData stamp)
    {
        if (stamp == null || stamp.TotalEntries == 0) return;

        // Request tool mode
        if (ToolModeManager.Instance != null)
        {
            if (!ToolModeManager.Instance.RequestMode(ToolModeManager.ToolMode.StampPlacement))
                return;
        }

        _activeStamp = stamp;
        _currentRotation = 0f;
        IsPlacing = true;

        CreateGhostPreviews();

        Debug.Log($"[StampPlacement] Placing stamp '{stamp.stampName}' ({stamp.buildings.Count} buildings, {stamp.roads.Count} roads).");
    }

    /// <summary>Cancels the current placement and destroys all ghost objects.</summary>
    public void CancelPlacement()
    {
        DestroyGhosts();
        _activeStamp = null;
        IsPlacing = false;

        if (ToolModeManager.Instance != null)
            ToolModeManager.Instance.ReleaseMode(ToolModeManager.ToolMode.StampPlacement);
    }

    // ───────── Ghost Preview Creation ─────────

    private void CreateGhostPreviews()
    {
        DestroyGhosts();

        _previewContainer = new GameObject("StampPreview");

        BuildingPrefabRegistry registry = BuildingPrefabRegistry.Instance;

        // Building ghosts
        foreach (var entry in _activeStamp.buildings)
        {
            GameObject prefab = registry != null ? registry.GetPrefab(entry.buildingIdentifier) : null;
            if (prefab == null)
            {
                Debug.LogWarning($"[StampPlacement] No prefab found for '{entry.buildingIdentifier}'. Skipping ghost.");
                continue;
            }

            // Clone the visuals (same technique as BuildingPreview.SetBuildingPrefab)
            GameObject ghost = Instantiate(prefab, _previewContainer.transform);
            ghost.name = $"Ghost_{entry.buildingIdentifier}";

            // Strip logic, physics, and audio
            foreach (var col in ghost.GetComponentsInChildren<Collider>(true)) Destroy(col);
            foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
            foreach (var aud in ghost.GetComponentsInChildren<AudioSource>(true)) Destroy(aud);

            _buildingGhosts.Add(new BuildingGhost
            {
                entry = entry,
                ghostObj = ghost,
                sourcePrefab = prefab,
                isValid = true
            });
        }

        // Road ghosts — use a simple cube placeholder if no road tile prefab available
        GameObject roadTilePrefab = GetRoadTilePrefab();
        foreach (var entry in _activeStamp.roads)
        {
            GameObject ghost;
            if (roadTilePrefab != null)
            {
                ghost = Instantiate(roadTilePrefab, _previewContainer.transform);
                // Strip logic from road ghost too
                foreach (var col in ghost.GetComponentsInChildren<Collider>(true)) Destroy(col);
                foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
            }
            else
            {
                ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ghost.transform.SetParent(_previewContainer.transform);
                ghost.transform.localScale = new Vector3(1f, 0.1f, 1f);
                Destroy(ghost.GetComponent<Collider>());
            }
            ghost.name = $"RoadGhost_{entry.relativeCell.x}_{entry.relativeCell.y}";

            _roadGhosts.Add(new RoadGhost
            {
                entry = entry,
                ghostObj = ghost,
                isValid = true
            });
        }
    }

    private void DestroyGhosts()
    {
        _buildingGhosts.Clear();
        _roadGhosts.Clear();

        if (_previewContainer != null)
        {
            Destroy(_previewContainer);
            _previewContainer = null;
        }
    }

    // ───────── Preview Update ─────────

    private void UpdatePreviewPosition()
    {
        Camera cam = Camera.main;
        if (cam == null || _previewContainer == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, groundLayer))
        {
            _previewContainer.SetActive(false);
            return;
        }

        _previewContainer.SetActive(true);

        // Resolve current island/grid
        Island hoveredIsland = IslandManager.instance != null ? IslandManager.instance.GetHoveredIsland() : null;
        if (hoveredIsland != null)
        {
            _currentIsland = hoveredIsland;
            _currentGridSystem = hoveredIsland.GetComponentInChildren<GridSystem>();
        }

        // Snap the stamp origin to the grid
        Vector3 stampOrigin = hit.point;
        if (_currentGridSystem != null)
        {
            stampOrigin = _currentGridSystem.GetNearestPointOnGrid(stampOrigin);
        }

        Quaternion stampRotation = Quaternion.Euler(0f, _currentRotation, 0f);

        // Position each building ghost
        for (int i = 0; i < _buildingGhosts.Count; i++)
        {
            var ghost = _buildingGhosts[i];
            if (ghost.ghostObj == null) continue;

            Vector3 rotatedOffset = stampRotation * ghost.entry.relativePosition;
            float entryRotation = ghost.entry.rotationY + _currentRotation;

            ghost.ghostObj.transform.position = stampOrigin + rotatedOffset;
            ghost.ghostObj.transform.rotation = Quaternion.Euler(0f, entryRotation, 0f);

            // Validate placement
            bool valid = ValidateBuildingPlacement(ghost.ghostObj.transform.position, ghost.entry, ghost.sourcePrefab);
            ghost.isValid = valid;
            ApplyGhostMaterial(ghost.ghostObj, valid);

            _buildingGhosts[i] = ghost;
        }

        // Position each road ghost
        for (int i = 0; i < _roadGhosts.Count; i++)
        {
            var ghost = _roadGhosts[i];
            if (ghost.ghostObj == null) continue;

            // Road cells are grid-aligned, so rotate the cell offset
            Vector3 cellOffset = new Vector3(
                ghost.entry.relativeCell.x + 0.5f,
                0f,
                ghost.entry.relativeCell.y + 0.5f);
            Vector3 rotatedOffset = stampRotation * cellOffset;

            Vector3 worldPos = stampOrigin + rotatedOffset;
            ghost.ghostObj.transform.position = worldPos;

            // Validate road placement
            bool valid = ValidateRoadPlacement(worldPos);
            ghost.isValid = valid;
            ApplyGhostMaterial(ghost.ghostObj, valid);

            _roadGhosts[i] = ghost;
        }
    }

    // ───────── Validation ─────────

    private bool ValidateBuildingPlacement(Vector3 worldPos, StampBuildingEntry entry, GameObject prefab)
    {
        if (_currentGridSystem == null) return false;

        Vector3Int gridPos = _currentGridSystem.WorldToCell(worldPos);
        Vector3 size = entry.buildingSize;

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.z; z++)
            {
                Cell cell = _currentGridSystem.GetCell(gridPos.x + x, gridPos.z + z);
                if (cell == null || cell.isBlocked || cell.isOccupied)
                    return false;
            }
        }

        // Check influence
        if (_currentIsland != null)
        {
            InfluenceManager influence = _currentIsland.GetComponent<InfluenceManager>();
            if (influence == null && _currentIsland.islandObject != null)
                influence = _currentIsland.islandObject.GetComponent<InfluenceManager>();

            if (influence != null && !influence.IsWithinBuildableArea(worldPos))
                return false;
        }

        return true;
    }

    private bool ValidateRoadPlacement(Vector3 worldPos)
    {
        if (_currentGridSystem == null) return false;

        Cell cell = _currentGridSystem.GetCellAtWorldPosition(worldPos);
        if (cell == null) return false;

        // Road can go on unoccupied, unblocked Land or Beach cells that aren't already roads
        if (cell.isRoad || cell.isOccupied || cell.isBlocked) return false;
        if (cell.currentTerrainType != Cell.TerrainType.Land &&
            cell.currentTerrainType != Cell.TerrainType.Beach)
            return false;

        return true;
    }

    private void ApplyGhostMaterial(GameObject ghost, bool valid)
    {
        Material mat = valid ? validMaterial : invalidMaterial;
        if (mat == null) return;

        MeshRenderer[] renderers = ghost.GetComponentsInChildren<MeshRenderer>();
        foreach (var rend in renderers)
        {
            rend.material = mat;
        }
    }

    // ───────── Placement Execution ─────────

    private void ConfirmPlacement()
    {
        if (_activeStamp == null || _currentIsland == null || _currentGridSystem == null)
        {
            Debug.LogWarning("[StampPlacement] Cannot place: no island or grid system.");
            return;
        }

        int buildingsPlaced = 0;
        int roadsPlaced = 0;
        int skipped = 0;

        // Place buildings
        for (int i = 0; i < _buildingGhosts.Count; i++)
        {
            var ghost = _buildingGhosts[i];
            if (!ghost.isValid || ghost.sourcePrefab == null)
            {
                skipped++;
                continue;
            }

            if (PlaceSingleBuilding(ghost))
                buildingsPlaced++;
            else
                skipped++;
        }

        // Place roads
        RoadPlacer activePlacer = GetActiveRoadPlacer();
        for (int i = 0; i < _roadGhosts.Count; i++)
        {
            var ghost = _roadGhosts[i];
            if (!ghost.isValid)
            {
                skipped++;
                continue;
            }

            if (activePlacer != null && PlaceSingleRoad(ghost, activePlacer))
                roadsPlaced++;
            else
                skipped++;
        }

        Debug.Log($"[StampPlacement] Placed {buildingsPlaced} building(s), {roadsPlaced} road(s). Skipped {skipped}.");

        CancelPlacement();
    }

    private bool PlaceSingleBuilding(BuildingGhost ghost)
    {
        Vector3 position = ghost.ghostObj.transform.position;
        Quaternion rotation = ghost.ghostObj.transform.rotation;

        // Re-validate just before placing
        if (!ValidateBuildingPlacement(position, ghost.entry, ghost.sourcePrefab))
            return false;

        // Check cost affordability
        BaseStorageManager storage = _currentIsland.GetBaseStorageManager();
        BuildingCost costPrefab = ghost.sourcePrefab.GetComponent<BuildingCost>();
        if (costPrefab != null && storage != null && !storage.CanAffordBuilding(costPrefab))
        {
            Debug.Log($"[StampPlacement] Cannot afford '{ghost.entry.buildingIdentifier}'. Skipping.");
            return false;
        }

        // Instantiate under island transform
        Transform islandTransform = _currentIsland.transform;
        GameObject instance = Instantiate(ghost.sourcePrefab, position, rotation, islandTransform);

        // Set properties
        BuildingProperties props = instance.GetComponent<BuildingProperties>();
        if (props != null)
        {
            props.currentIsland = _currentIsland;
            props.gridSystem = _currentGridSystem;
        }

        // Deduct costs
        BuildingCost buildingCost = instance.GetComponent<BuildingCost>();
        Bank bank = FindObjectOfType<Bank>();
        if (buildingCost != null && storage != null && bank != null)
        {
            storage.DeductBuildingCosts(buildingCost, bank);
        }

        // Mark grid cells
        Vector3 buildingSize = ghost.entry.buildingSize;
        Vector3Int gridPos = _currentGridSystem.WorldToCell(position);
        Building buildingComponent = instance.GetComponent<Building>();
        for (int x = 0; x < buildingSize.x; x++)
        {
            for (int z = 0; z < buildingSize.z; z++)
            {
                Vector3 cellWorldPos = new Vector3(
                    (gridPos.x + x) * _currentGridSystem.cellSize,
                    0,
                    (gridPos.z + z) * _currentGridSystem.cellSize);
                _currentGridSystem.MarkCellAsOccupied(cellWorldPos, buildingComponent);
            }
        }

        // Register influence zone if present
        InfluenceZone zone = instance.GetComponent<InfluenceZone>();
        if (zone != null)
        {
            InfluenceManager manager = islandTransform.GetComponent<InfluenceManager>();
            if (manager == null)
                manager = islandTransform.gameObject.AddComponent<InfluenceManager>();
            manager.RegisterZone(zone);
        }

        // Add construction site to start the build process
        if (instance.GetComponent<ConstructionSite>() == null)
            instance.AddComponent<ConstructionSite>();

        return true;
    }

    private bool PlaceSingleRoad(RoadGhost ghost, RoadPlacer placer)
    {
        Cell cell = _currentGridSystem.GetCellAtWorldPosition(ghost.ghostObj.transform.position);
        if (cell == null) return false;

        return placer.PlaceRoad(cell);
    }

    // ───────── Helpers ─────────

    private RoadPlacer GetActiveRoadPlacer()
    {
        if (roadPlacer != null) return roadPlacer;
        roadPlacer = FindObjectOfType<RoadPlacer>();
        return roadPlacer;
    }

    private GameObject GetRoadTilePrefab()
    {
        RoadPlacer placer = GetActiveRoadPlacer();
        if (placer == null) return null;

        // RoadPlacer.roadTilePrefab is serialized private — we can't access it directly.
        // Return null to use fallback cube geometry for road ghosts.
        return null;
    }
}
