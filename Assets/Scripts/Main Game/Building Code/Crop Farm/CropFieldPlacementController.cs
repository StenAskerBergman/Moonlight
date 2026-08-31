using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Player-facing input controller for cultivating and removing 1x1 field modules.
/// When in field placement mode for a selected CropFarmCore, holding the left mouse button
/// paints fields outward organically, while right mouse button clears fields.
/// </summary>
public class CropFieldPlacementController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private CropFieldPlacer fieldPlacer;
    [SerializeField] private Camera worldCamera;

    [Header("Target Farm Core")]
    [SerializeField] private CropFarmCore activeFarmCore;

    [Header("Input Settings")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F;
    [SerializeField] private LayerMask groundLayer = 1 << 6;
    [SerializeField] private float maxRaycastDistance = 1000f;

    [Header("Preview Indicator")]
    [SerializeField] private GameObject previewIndicator;
    [SerializeField] private Color validColor = new Color(0.2f, 0.9f, 0.2f, 0.6f);
    [SerializeField] private Color invalidColor = new Color(0.9f, 0.2f, 0.2f, 0.6f);

    public bool FieldModeActive { get; private set; }
    public CropFarmCore ActiveFarmCore => activeFarmCore;

    public static event Action<bool, CropFarmCore> OnFieldModeChanged;

    private Cell _lastPaintedCell;
    private Renderer _previewRenderer;
    private MaterialPropertyBlock _previewPropBlock;
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProp = Shader.PropertyToID("_Color");

    private CropFieldPlacer ActivePlacer
    {
        get
        {
            if (fieldPlacer != null) return fieldPlacer;
            fieldPlacer = GetComponent<CropFieldPlacer>();
            if (fieldPlacer == null) fieldPlacer = FindObjectOfType<CropFieldPlacer>();
            if (fieldPlacer == null) fieldPlacer = gameObject.AddComponent<CropFieldPlacer>();
            return fieldPlacer;
        }
    }

    private Camera ActiveCamera => worldCamera != null ? worldCamera : Camera.main;

    private void Awake()
    {
        _previewPropBlock = new MaterialPropertyBlock();
        CreatePreviewIndicator();
    }

    private void Start()
    {
        if (BuildingSelections.Instance != null)
        {
            BuildingSelections.Instance.selectionChanged.AddListener(OnBuildingSelected);
        }
    }

    private void OnDestroy()
    {
        if (BuildingSelections.Instance != null)
        {
            BuildingSelections.Instance.selectionChanged.RemoveListener(OnBuildingSelected);
        }
        if (previewIndicator != null)
        {
            Destroy(previewIndicator);
        }
    }

    private void OnBuildingSelected(Building building)
    {
        if (building == null)
        {
            if (FieldModeActive)
            {
                ExitFieldMode();
            }
            activeFarmCore = null;
            return;
        }

        CropFarmCore farm = building.GetComponent<CropFarmCore>();
        if (farm != null)
        {
            activeFarmCore = farm;
        }
        else
        {
            if (FieldModeActive)
            {
                ExitFieldMode();
            }
            activeFarmCore = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleFieldMode();
        }

        if (!FieldModeActive)
        {
            if (previewIndicator != null && previewIndicator.activeSelf)
            {
                previewIndicator.SetActive(false);
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitFieldMode();
            return;
        }

        // A building being placed owns the cursor
        if (BuildingChecker.instance != null && BuildingChecker.instance.IsPlacingBuilding)
        {
            if (previewIndicator != null && previewIndicator.activeSelf)
            {
                previewIndicator.SetActive(false);
            }
            return;
        }

        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
        {
            _lastPaintedCell = null;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (previewIndicator != null && previewIndicator.activeSelf)
            {
                previewIndicator.SetActive(false);
            }
            return;
        }

        UpdateCursorAndPreview();

        if (Input.GetMouseButton(0))
        {
            PaintCellUnderCursor(true);
        }
        else if (Input.GetMouseButton(1))
        {
            PaintCellUnderCursor(false);
        }
    }

    private void UpdateCursorAndPreview()
    {
        CropFieldPlacer placer = ActivePlacer;
        Camera cam = ActiveCamera;
        if (placer == null || cam == null || activeFarmCore == null)
        {
            if (previewIndicator != null) previewIndicator.SetActive(false);
            return;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, groundLayer))
        {
            Cell cell = placer.GetCellAtWorldPosition(hit.point);
            if (cell != null)
            {
                bool canPlace = placer.CanPlaceField(activeFarmCore, cell, out _);

                if (previewIndicator != null)
                {
                    previewIndicator.SetActive(true);
                    previewIndicator.transform.position = new Vector3(cell.cellPosition.x + 0.5f, cell.height + 0.05f, cell.cellPosition.z + 0.5f);

                    Color targetColor = canPlace ? validColor : invalidColor;
                    if (_previewRenderer != null)
                    {
                        _previewRenderer.GetPropertyBlock(_previewPropBlock);
                        _previewPropBlock.SetColor(BaseColorProp, targetColor);
                        _previewPropBlock.SetColor(ColorProp, targetColor);
                        _previewRenderer.SetPropertyBlock(_previewPropBlock);
                    }
                }
                return;
            }
        }

        if (previewIndicator != null)
        {
            previewIndicator.SetActive(false);
        }
    }

    private void PaintCellUnderCursor(bool place)
    {
        CropFieldPlacer placer = ActivePlacer;
        Camera cam = ActiveCamera;
        if (placer == null || cam == null || activeFarmCore == null) return;

        RaycastHit hit;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out hit, maxRaycastDistance, groundLayer)) return;

        Cell cell = placer.GetCellAtWorldPosition(hit.point);
        if (cell == null || cell == _lastPaintedCell) return;

        _lastPaintedCell = cell;

        if (place)
        {
            placer.PlaceField(activeFarmCore, cell);
        }
        else
        {
            placer.RemoveField(activeFarmCore, cell);
        }
    }

    public void ToggleFieldMode()
    {
        if (FieldModeActive)
        {
            ExitFieldMode();
        }
        else
        {
            // Auto-acquire selected farm if available
            if (activeFarmCore == null && BuildingSelections.Instance != null && BuildingSelections.Instance.SelectedBuilding != null)
            {
                activeFarmCore = BuildingSelections.Instance.SelectedBuilding.GetComponent<CropFarmCore>();
            }

            if (activeFarmCore != null)
            {
                EnterFieldMode(activeFarmCore);
            }
        }
    }

    public void EnterFieldMode(CropFarmCore farmCore)
    {
        activeFarmCore = farmCore;
        if (activeFarmCore == null) return;

        FieldModeActive = true;
        _lastPaintedCell = null;
        if (previewIndicator != null) previewIndicator.SetActive(true);
        OnFieldModeChanged?.Invoke(true, activeFarmCore);
    }

    public void ExitFieldMode()
    {
        if (!FieldModeActive) return;

        FieldModeActive = false;
        _lastPaintedCell = null;
        if (previewIndicator != null) previewIndicator.SetActive(false);
        OnFieldModeChanged?.Invoke(false, activeFarmCore);
    }

    private void CreatePreviewIndicator()
    {
        if (previewIndicator != null) return;

        previewIndicator = GameObject.CreatePrimitive(PrimitiveType.Quad);
        previewIndicator.name = "CropField_PlacementPreview";
        previewIndicator.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        previewIndicator.transform.localScale = new Vector3(0.98f, 0.98f, 1f);

        Collider col = previewIndicator.GetComponent<Collider>();
        if (col != null) Destroy(col);

        _previewRenderer = previewIndicator.GetComponent<Renderer>();
        previewIndicator.SetActive(false);
    }
}
