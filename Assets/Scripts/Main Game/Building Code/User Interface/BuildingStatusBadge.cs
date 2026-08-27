using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space billboard indicator that observes simulation shutdown reasons and displays
/// high-contrast status icons (No Road, Missing Input, Storage Full, Missing Workers, Unsupported, Paused).
/// </summary>
public class BuildingStatusBadge : MonoBehaviour
{
    [Header("Simulation Reference")]
    [SerializeField] private BuildingSimulation simulation;

    [Header("Badge Visuals")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private Vector3 overheadOffset = new Vector3(0, 3f, 0);

    [Header("Icon Deliverables (Optional)")]
    [SerializeField] private Sprite noRoadIcon;
    [SerializeField] private Sprite missingInputIcon;
    [SerializeField] private Sprite storageFullIcon;
    [SerializeField] private Sprite missingWorkersIcon;
    [SerializeField] private Sprite unsupportedAnchorIcon;
    [SerializeField] private Sprite pausedIcon;
    [SerializeField] private Sprite damagedIcon;

    private Camera _mainCamera;

    private void Awake()
    {
        if (simulation == null)
        {
            simulation = GetComponentInParent<BuildingSimulation>();
        }

        if (iconRenderer == null)
        {
            iconRenderer = GetComponent<SpriteRenderer>();
            if (iconRenderer == null)
            {
                GameObject iconObj = new GameObject("BadgeIcon");
                iconObj.transform.SetParent(transform);
                iconObj.transform.localPosition = overheadOffset;
                iconRenderer = iconObj.AddComponent<SpriteRenderer>();
            }
        }

        _mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (simulation == null) return;

        simulation.OnShutdownReasonChanged += HandleShutdownReason;
        HandleShutdownReason(simulation.CurrentShutdownReason);
    }

    private void OnDisable()
    {
        if (simulation == null) return;

        simulation.OnShutdownReasonChanged -= HandleShutdownReason;
    }

    private void LateUpdate()
    {
        // Billboard rotation facing active camera
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera != null && iconRenderer != null && iconRenderer.enabled)
        {
            transform.rotation = _mainCamera.transform.rotation;
        }
    }

    private void HandleShutdownReason(BuildingEnums.BuildingShutdownReason reason)
    {
        if (iconRenderer == null) return;

        if (reason == BuildingEnums.BuildingShutdownReason.None)
        {
            iconRenderer.enabled = false;
            return;
        }

        Sprite targetSprite = GetIconForReason(reason);
        if (targetSprite != null)
        {
            iconRenderer.sprite = targetSprite;
            iconRenderer.enabled = true;
        }
        else
        {
            iconRenderer.enabled = false;
            AssetFallback.LogMissingDeliverable("Sprite", $"Icon for {reason}", this);
        }
    }

    private Sprite GetIconForReason(BuildingEnums.BuildingShutdownReason reason)
    {
        switch (reason)
        {
            case BuildingEnums.BuildingShutdownReason.NoRoadAccess:
                return noRoadIcon;
            case BuildingEnums.BuildingShutdownReason.MissingInput:
                return missingInputIcon;
            case BuildingEnums.BuildingShutdownReason.StorageFull:
                return storageFullIcon;
            case BuildingEnums.BuildingShutdownReason.MissingWorkers:
                return missingWorkersIcon;
            case BuildingEnums.BuildingShutdownReason.UnsupportedAnchor:
                return unsupportedAnchorIcon;
            case BuildingEnums.BuildingShutdownReason.PausedByPlayer:
                return pausedIcon;
            case BuildingEnums.BuildingShutdownReason.Damaged:
                return damagedIcon;
            default:
                return null;
        }
    }
}
