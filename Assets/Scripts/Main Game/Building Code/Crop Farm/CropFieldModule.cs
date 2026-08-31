using System;
using UnityEngine;

/// <summary>
/// Lightweight 1x1 field module owned by one Farm Core.
/// Acts as a capacity module that reserves land and determines the farm's cultivation area.
/// Has zero per-frame update tick overhead — all simulation is driven by CropFarmCore.
/// </summary>
[SelectionBase]
public class CropFieldModule : MonoBehaviour
{
    [Header("Farm Ownership")]
    [SerializeField] private CropFarmCore owningFarm;

    [Header("State")]
    [SerializeField] private bool isConnectedToCore = true;
    [SerializeField] private Vector3Int gridCoordinates;

    [Header("Visual Feedback (Optional)")]
    [SerializeField] private Renderer[] cropRenderers;
    [SerializeField] private Color connectedColor = Color.white;
    [SerializeField] private Color disconnectedColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);

    private Cell _cell;
    private MaterialPropertyBlock _propBlock;
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProp = Shader.PropertyToID("_Color");

    public CropFarmCore OwningFarm => owningFarm;
    public bool IsConnectedToCore => isConnectedToCore;
    public Vector3Int GridCoordinates => gridCoordinates;
    public Cell GridCell => _cell;

    public event Action<bool> OnConnectionStateChanged;

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
        if (cropRenderers == null || cropRenderers.Length == 0)
        {
            cropRenderers = GetComponentsInChildren<Renderer>();
        }
    }

    /// <summary>
    /// Initializes the field module with its owning Farm Core, cell position, and grid cell.
    /// </summary>
    public void Initialize(CropFarmCore farmCore, Vector3Int coordinates, Cell cell)
    {
        this.owningFarm = farmCore;
        this.gridCoordinates = coordinates;
        this._cell = cell;
        this.isConnectedToCore = true;

        if (cropRenderers == null || cropRenderers.Length == 0)
        {
            cropRenderers = GetComponentsInChildren<Renderer>();
        }

        UpdateVisuals();
    }

    /// <summary>
    /// Updates whether this field is currently connected back to the Farm Core.
    /// Called during BFS graph recomputation in CropFarmCore.
    /// </summary>
    public void SetConnectedState(bool connected)
    {
        if (isConnectedToCore == connected) return;

        isConnectedToCore = connected;
        UpdateVisuals();
        OnConnectionStateChanged?.Invoke(isConnectedToCore);
    }

    private void UpdateVisuals()
    {
        if (cropRenderers == null || cropRenderers.Length == 0) return;

        Color targetColor = isConnectedToCore ? connectedColor : disconnectedColor;
        foreach (var r in cropRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorProp, targetColor);
            _propBlock.SetColor(ColorProp, targetColor);
            r.SetPropertyBlock(_propBlock);
        }
    }

    private void OnDestroy()
    {
        if (_cell != null && _cell.occupyingBuilding != null && _cell.occupyingBuilding.gameObject == gameObject)
        {
            _cell.ReleaseCell();
        }
    }
}
