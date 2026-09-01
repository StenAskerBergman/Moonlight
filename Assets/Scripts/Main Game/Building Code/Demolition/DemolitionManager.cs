using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Demolition mode: the player toggles it on, clicks buildings to mark them, then
/// confirms to tear the whole batch down at once (or cancels to keep everything).
///
/// Marking rather than destroying on click is deliberate - demolition is irreversible
/// and refunds only part of the cost, so a misclick should be undoable right up until
/// the player confirms.
///
/// Buildings are marked on the same "Buildings" layer BuildingClick raycasts, and this
/// consumes the click while active so a marking click can't also select the building.
/// </summary>
[DisallowMultipleComponent]
public sealed class DemolitionManager : MonoBehaviour
{
    public static DemolitionManager Instance { get; private set; }

    [Tooltip("Set to the 'Buildings' layer, matching BuildingClick.")]
    [SerializeField] private LayerMask buildingLayer = 1 << 8;

    [Tooltip("Tear the building down the moment it is clicked, with no confirm step.")]
    [SerializeField] private bool instantDemolish;

    private readonly List<BuildingDemolition> marked = new List<BuildingDemolition>();
    private Camera cachedCamera;

    public bool IsActive { get; private set; }
    public int MarkedCount => marked.Count;

    public delegate void DemolitionChangedHandler();
    public event DemolitionChangedHandler OnModeChanged;
    public event DemolitionChangedHandler OnMarkedChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Creates the manager on load so no scene has to be edited to get demolition.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject host = new GameObject(nameof(DemolitionManager));
        host.AddComponent<DemolitionManager>();
        DontDestroyOnLoad(host);
    }

    #region Mode

    /// <summary>Button target for the HUD's Destroy button.</summary>
    public void ToggleMode() => SetMode(!IsActive);

    public void SetMode(bool active)
    {
        if (IsActive == active) return;

        IsActive = active;

        // Leaving the mode must not silently demolish or silently keep a stale batch.
        if (!IsActive) ClearMarks();

        OnModeChanged?.Invoke();
    }

    #endregion

    #region Marking

    private void Update()
    {
        if (!IsActive) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetMode(false);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ConfirmDemolition();
            return;
        }

        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (cachedCamera == null) cachedCamera = Camera.main;
        if (cachedCamera == null) return;

        Ray ray = cachedCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, buildingLayer)) return;

        Building building = hit.collider.GetComponentInParent<Building>();
        if (building == null) return;

        ToggleMark(building);
    }

    public void ToggleMark(Building building)
    {
        if (building == null) return;

        BuildingDemolition demolition = building.GetComponent<BuildingDemolition>()
            ?? building.gameObject.AddComponent<BuildingDemolition>();

        if (!demolition.Demolishable) return;

        if (instantDemolish)
        {
            demolition.Demolish();
            OnMarkedChanged?.Invoke();
            return;
        }

        if (marked.Remove(demolition)) SetRingMarked(demolition, false);
        else
        {
            marked.Add(demolition);
            SetRingMarked(demolition, true);
        }

        OnMarkedChanged?.Invoke();
    }

    public void ClearMarks()
    {
        foreach (BuildingDemolition demolition in marked)
        {
            if (demolition != null) SetRingMarked(demolition, false);
        }

        marked.Clear();
        OnMarkedChanged?.Invoke();
    }

    private static void SetRingMarked(BuildingDemolition demolition, bool value)
    {
        BuildingSelectionRing ring = demolition.GetComponent<BuildingSelectionRing>()
            ?? demolition.gameObject.AddComponent<BuildingSelectionRing>();

        ring.SetMarked(value);
    }

    #endregion

    #region Confirm

    /// <summary>
    /// Total refund the current batch would pay, for a confirmation prompt.
    /// </summary>
    public Dictionary<ItemData, int> PreviewBatchItemRefund()
    {
        Dictionary<ItemData, int> total = new Dictionary<ItemData, int>();

        foreach (BuildingDemolition demolition in marked)
        {
            if (demolition == null) continue;

            foreach (KeyValuePair<ItemData, int> entry in demolition.PreviewItemRefund())
            {
                total.TryGetValue(entry.Key, out int running);
                total[entry.Key] = running + entry.Value;
            }
        }

        return total;
    }

    public int PreviewBatchCurrencyRefund()
    {
        int total = 0;
        foreach (BuildingDemolition demolition in marked)
        {
            if (demolition != null) total += demolition.PreviewCurrencyRefund();
        }
        return total;
    }

    /// <summary>Button target for the confirm control. Returns how many came down.</summary>
    public int ConfirmDemolition()
    {
        int destroyed = 0;

        // Iterate a copy: Demolish destroys objects, and a building's OnDestroy can
        // touch systems that call back into here.
        foreach (BuildingDemolition demolition in new List<BuildingDemolition>(marked))
        {
            if (demolition == null) continue;
            if (demolition.Demolish()) destroyed++;
        }

        marked.Clear();
        OnMarkedChanged?.Invoke();

        SetMode(false);
        return destroyed;
    }

    #endregion
}
