using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Pipette (Eyedropper) tool: allows sampling any existing placed building in the
/// world to immediately begin placing copies of it.
/// </summary>
[DisallowMultipleComponent]
public class PipetteTool : MonoBehaviour
{
    public static PipetteTool Instance { get; private set; }

    [SerializeField] private LayerMask buildingLayer = ~0; // Scan all or buildings
    private bool isActive;

    public bool IsActive => isActive;

    public delegate void PipetteModeHandler(bool active);
    public event PipetteModeHandler OnModeChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ToggleMode()
    {
        SetMode(!isActive);
    }

    public void SetMode(bool active)
    {
        if (isActive == active) return;
        isActive = active;
        OnModeChanged?.Invoke(isActive);
    }

    private void Update()
    {
        if (!isActive) return;

        // Cancel on Escape or Right-Click
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            SetMode(false);
            return;
        }

        // Sampling click on Left-Click (only when not interacting with UI)
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            SampleBuildingAtCursor();
        }
    }

    private void SampleBuildingAtCursor()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            GameObject target = hit.collider.gameObject;

            // Search up the hierarchy for Building or BaseBuilding
            Building building = target.GetComponentInParent<Building>();
            BaseBuilding baseBuilding = target.GetComponentInParent<BaseBuilding>();
            BuildingDemolition demolition = target.GetComponentInParent<BuildingDemolition>();

            GameObject prefab = null;

            // 1. Try BuildingPrefabRegistry lookup
            if (BuildingPrefabRegistry.Instance != null)
            {
                if (building != null)
                {
                    string cleanName = building.gameObject.name.Replace("(Clone)", "").Trim();
                    prefab = BuildingPrefabRegistry.Instance.GetPrefab(cleanName);
                }

                if (prefab == null && baseBuilding != null)
                {
                    string cleanName = baseBuilding.gameObject.name.Replace("(Clone)", "").Trim();
                    prefab = BuildingPrefabRegistry.Instance.GetPrefab(cleanName);
                }

                if (prefab == null && demolition != null)
                {
                    string cleanName = demolition.gameObject.name.Replace("(Clone)", "").Trim();
                    prefab = BuildingPrefabRegistry.Instance.GetPrefab(cleanName);
                }
            }

            // 2. Try scanning scene BuildingButtons to match name
            if (prefab == null)
            {
                string targetName = (building != null ? building.gameObject.name :
                    baseBuilding != null ? baseBuilding.gameObject.name :
                    demolition != null ? demolition.gameObject.name : target.name)
                    .Replace("(Clone)", "").Trim();

                BuildingButton[] buttons = FindObjectsOfType<BuildingButton>(includeInactive: true);
                foreach (BuildingButton btn in buttons)
                {
                    GameObject p = btn.GetBuildingPrefab();
                    if (p != null && (p.name == targetName || targetName.StartsWith(p.name)))
                    {
                        prefab = p;
                        break;
                    }
                }
            }

            if (prefab != null)
            {
                SetMode(false);
                if (BuildingSelector.Active != null)
                {
                    BuildingSelector.Active.CancelPreview();
                    BuildingSelector.Active.SpawnPreview(prefab);
                }
                return;
            }
        }
    }
}
