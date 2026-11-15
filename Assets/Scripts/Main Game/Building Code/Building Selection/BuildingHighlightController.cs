using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decides which buildings wear which highlight and syncs static zone terrain overlays
/// when an InfluenceZone emitter (Harbor, Warehouse, City Center) is selected.
///
/// Priority: Selected (blue) > PublisherSupplier (gold) > SubscriberConsumer (mint/cyan) > Influence (green).
/// </summary>
[DisallowMultipleComponent]
public class BuildingHighlightController : MonoBehaviour
{
    public static BuildingHighlightController Instance { get; private set; }

    [Tooltip("Re-check highlights on this interval while a building is selected.")]
    [SerializeField, Min(0f)] private float refreshInterval = 0.5f;

    private readonly List<BuildingHighlighter> litHighlighters = new List<BuildingHighlighter>();
    private readonly List<Building> scratchSuppliers = new List<Building>();
    private readonly List<Building> scratchConsumers = new List<Building>();
    private Building currentSelection;
    private float nextRefreshTime;
    private bool showingStaticZoneOverlay;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject host = new GameObject(nameof(BuildingHighlightController));
        host.AddComponent<BuildingHighlightController>();
        DontDestroyOnLoad(host);
    }

    private void OnDisable()
    {
        ClearAll();
    }

    private void Update()
    {
        Building selected = BuildingSelections.Instance != null ? BuildingSelections.Instance.SelectedBuilding : null;

        if (selected != currentSelection)
        {
            OnSelectionChanged(selected);
            return;
        }

        if (currentSelection == null)
        {
            if (litHighlighters.Count > 0 || showingStaticZoneOverlay) ClearAll();
            return;
        }

        if (refreshInterval <= 0f || Time.time < nextRefreshTime) return;

        nextRefreshTime = Time.time + refreshInterval;
        Apply(currentSelection);
    }

    public void OnSelectionChanged(Building selected)
    {
        currentSelection = selected;
        nextRefreshTime = Time.time + refreshInterval;

        if (selected == null)
        {
            ClearAll();
            return;
        }

        Apply(selected);
    }

    private void Apply(Building selected)
    {
        var wanted = new Dictionary<BuildingHighlighter, BuildingHighlight>();

        BuildingHighlighter selectedHighlighter = GetHighlighter(selected.gameObject);
        if (selectedHighlighter != null) wanted[selectedHighlighter] = BuildingHighlight.Selected;

        // Producer-consumer links (warm gold suppliers, mint/cyan consumers).
        DependencyGraphRegistry.QueryDependencies(selected, scratchSuppliers, scratchConsumers);

        for (int i = 0; i < scratchSuppliers.Count; i++)
        {
            Building supplier = scratchSuppliers[i];
            if (supplier == null || supplier == selected) continue;
            BuildingHighlighter highlighter = GetHighlighter(supplier.gameObject);
            if (highlighter != null && !wanted.ContainsKey(highlighter))
            {
                wanted[highlighter] = BuildingHighlight.PublisherSupplier;
            }
        }

        for (int i = 0; i < scratchConsumers.Count; i++)
        {
            Building consumer = scratchConsumers[i];
            if (consumer == null || consumer == selected) continue;
            BuildingHighlighter highlighter = GetHighlighter(consumer.gameObject);
            if (highlighter != null && !wanted.ContainsKey(highlighter))
            {
                wanted[highlighter] = BuildingHighlight.SubscriberConsumer;
            }
        }

        // Spatial influence zone (green) + stepped white perimeter terrain projection when idle.
        InfluenceZone zone = selected.GetComponent<InfluenceZone>() ?? selected.GetComponentInChildren<InfluenceZone>();
        if (zone != null)
        {
            foreach (Building influenced in GetInfluencedBuildings(zone))
            {
                if (influenced == null || influenced == selected) continue;

                BuildingHighlighter highlighter = GetHighlighter(influenced.gameObject);
                if (highlighter == null || wanted.ContainsKey(highlighter)) continue;

                wanted[highlighter] = BuildingHighlight.Influence;
            }
        }

        SyncStaticZoneOverlay(selected, zone);

        for (int i = litHighlighters.Count - 1; i >= 0; i--)
        {
            BuildingHighlighter lit = litHighlighters[i];
            if (lit == null || !wanted.ContainsKey(lit))
            {
                if (lit != null) lit.SetHighlight(BuildingHighlight.None);
                litHighlighters.RemoveAt(i);
            }
        }

        foreach (var pair in wanted)
        {
            pair.Key.SetHighlight(pair.Value);
            if (!litHighlighters.Contains(pair.Key)) litHighlighters.Add(pair.Key);
        }
    }

    private void SyncStaticZoneOverlay(Building selected, InfluenceZone zone)
    {
        bool placementActive = BuildingChecker.instance != null && BuildingChecker.instance.IsPlacingBuilding;
        if (placementActive)
        {
            showingStaticZoneOverlay = false;
            return;
        }

        if (zone != null && selected != null)
        {
            BuildingProperties props = selected.GetComponent<BuildingProperties>();
            Island island = props != null && props.currentIsland != null
                ? props.currentIsland
                : selected.GetComponentInParent<Island>();
            GridSystem grid = props != null && props.gridSystem != null
                ? props.gridSystem
                : (island != null ? island.GetComponent<GridSystem>() : null);

            if (grid != null)
            {
                PlacementValidityOverlay.ShowStaticZone(grid, island, zone);
                showingStaticZoneOverlay = true;
                return;
            }
        }

        if (showingStaticZoneOverlay)
        {
            PlacementValidityOverlay.HideAll();
            showingStaticZoneOverlay = false;
        }
    }

    private static IEnumerable<Building> GetInfluencedBuildings(InfluenceZone zone)
    {
        if (zone == null) yield break;

        foreach (Building building in FindObjectsOfType<Building>())
        {
            if (building != null && zone.ContainsPoint(building.transform.position))
            {
                yield return building;
            }
        }
    }

    private static BuildingHighlighter GetHighlighter(GameObject target)
    {
        if (target == null) return null;
        return target.GetComponent<BuildingHighlighter>() ?? target.AddComponent<BuildingHighlighter>();
    }

    private void ClearAll()
    {
        foreach (BuildingHighlighter highlighter in litHighlighters)
        {
            if (highlighter != null) highlighter.SetHighlight(BuildingHighlight.None);
        }
        litHighlighters.Clear();

        if (showingStaticZoneOverlay)
        {
            bool placementActive = BuildingChecker.instance != null && BuildingChecker.instance.IsPlacingBuilding;
            if (!placementActive)
            {
                PlacementValidityOverlay.HideAll();
            }
            showingStaticZoneOverlay = false;
        }
    }
}
