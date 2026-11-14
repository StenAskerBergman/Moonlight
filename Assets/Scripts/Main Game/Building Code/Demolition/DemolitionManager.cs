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

    [Header("Foliage Clearing")]
    [Tooltip("Let the destroy tool also brush away trees and other foliage by dragging.")]
    [SerializeField] private bool clearFoliage = true;

    [Tooltip("Set to the 'Ground' layer, matching the foliage tool.")]
    [SerializeField] private LayerMask groundLayer = 1 << 6;

    [Tooltip("Radius of the foliage brush in world units.")]
    [SerializeField, Min(0.1f)] private float foliageBrushRadius = 2.5f;

    private readonly List<BuildingDemolition> marked = new List<BuildingDemolition>();
    private readonly List<int> foliageScratch = new List<int>();
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

        // Held rather than pressed, so foliage can be brushed away in a single drag.
        if (!Input.GetMouseButton(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (cachedCamera == null) cachedCamera = Camera.main;
        if (cachedCamera == null) return;

        Ray ray = cachedCamera.ScreenPointToRay(Input.mousePosition);

        // Buildings win the click, and only on the initial press - dragging across one must not
        // toggle its mark on and off every frame.
        if (Input.GetMouseButtonDown(0)
            && Physics.Raycast(ray, out RaycastHit buildingHit, Mathf.Infinity, buildingLayer))
        {
            Building building = buildingHit.collider.GetComponentInParent<Building>();
            if (building != null)
            {
                ToggleMark(building);
                return;
            }
        }

        if (!clearFoliage) return;
        if (Physics.Raycast(ray, out RaycastHit groundHit, Mathf.Infinity, groundLayer))
            BrushFoliage(groundHit);
    }

    /// <summary>
    /// Clears foliage within the brush around a terrain hit.
    ///
    /// Unlike buildings this takes effect immediately rather than being marked for confirmation:
    /// clearing scrub returns nothing, and staging hundreds of trees for a confirm step would make
    /// "drag away a forest" unusable. Buildings keep the mark-then-confirm flow because demolishing
    /// one is irreversible and refunds part of its cost.
    /// </summary>
    private void BrushFoliage(RaycastHit hit)
    {
        IslandFoliagePlacer island = hit.collider.GetComponentInParent<IslandFoliagePlacer>();
        if (island == null) return;

        Vector3 local = island.transform.InverseTransformPoint(hit.point);
        if (island.Store.QueryRadius(local, foliageBrushRadius, foliageScratch) == 0) return;

        // QueryRadius fills a list we own, so removing while walking it is safe.
        for (int i = 0; i < foliageScratch.Count; i++)
            island.Store.Remove(foliageScratch[i], FoliageRemovalCause.PlayerClear);
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
