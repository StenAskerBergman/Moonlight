using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decides which buildings wear which highlight, and is the only thing that does.
///
/// Clicking a building turns it blue and turns everything inside its influence green;
/// clicking away clears both. Keeping the decision in one place is what guarantees the
/// green set is actually cleared - a building that stops being in range while nothing
/// is watching would otherwise stay lit forever.
/// </summary>
[DisallowMultipleComponent]
public class BuildingHighlightController : MonoBehaviour
{
    public static BuildingHighlightController Instance { get; private set; }

    [Tooltip("Re-check the influence set on this interval, so buildings finished or destroyed during a selection are picked up.")]
    [SerializeField, Min(0f)] private float refreshInterval = 0.5f;

    private readonly List<BuildingHighlighter> litHighlighters = new List<BuildingHighlighter>();
    private Building currentSelection;
    private float nextRefreshTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Creates the controller on load so no scene has to be edited to get highlights,
    /// and so it cannot be missing from one scene and present in another.
    /// </summary>
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

    /// <summary>
    /// The selection is polled rather than subscribed to. BuildingSelections is a scene
    /// object whose Awake order relative to this one is not guaranteed, and a missed
    /// subscription would strand a building lit with no way to clear it.
    /// </summary>
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
            // The selected building was destroyed out from under us.
            if (litHighlighters.Count > 0) ClearAll();
            return;
        }

        if (refreshInterval <= 0f || Time.time < nextRefreshTime) return;

        nextRefreshTime = Time.time + refreshInterval;
        Apply(currentSelection);
    }

    /// <summary>
    /// Entry point for anything that changes the selection. Also safe to call directly
    /// if building selection is ever driven from somewhere other than BuildingSelections.
    /// </summary>
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

        foreach (Building influenced in GetInfluencedBuildings(selected))
        {
            if (influenced == null || influenced == selected) continue;

            BuildingHighlighter highlighter = GetHighlighter(influenced.gameObject);

            // Selected wins - the clicked building never greens itself through its own zone.
            if (highlighter == null || wanted.ContainsKey(highlighter)) continue;

            wanted[highlighter] = BuildingHighlight.Influence;
        }

        // Clear anything lit last pass that no longer belongs, before lighting the new set.
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

    /// <summary>
    /// The buildings the selection reaches. A building's own InfluenceZone is the
    /// relationship being shown - what this harbor or depot services.
    /// </summary>
    private static IEnumerable<Building> GetInfluencedBuildings(Building selected)
    {
        InfluenceZone zone = selected.GetComponent<InfluenceZone>() ?? selected.GetComponentInChildren<InfluenceZone>();
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

        // Added on demand so no building prefab needs the component placed by hand.
        return target.GetComponent<BuildingHighlighter>() ?? target.AddComponent<BuildingHighlighter>();
    }

    private void ClearAll()
    {
        foreach (BuildingHighlighter highlighter in litHighlighters)
        {
            if (highlighter != null) highlighter.SetHighlight(BuildingHighlight.None);
        }
        litHighlighters.Clear();
    }
}
