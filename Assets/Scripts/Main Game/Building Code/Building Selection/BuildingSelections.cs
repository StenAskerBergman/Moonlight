using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tracks which Building is currently selected via click. Mirrors the
/// UnitSelections/ISelectable pattern (Unit/Unit Scripts/UnitSelections.cs) but
/// trimmed to single-select, since only one building's HUD panel is shown at a time.
///
/// Building and unit selections are generally independent. Selecting a boat is the
/// exception: UnitSelections clears the selected building so its menu closes.
/// </summary>
public class BuildingSelections : MonoBehaviour
{
    public static BuildingSelections Instance { get; private set; }

    public Building SelectedBuilding { get; private set; }

    [Space(10)]
    public UnityEvent<Building> selectionChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        // Poll for destroyed selection so external destruction (e.g. demolition) clears cleanly.
        if (SelectedBuilding == null && !ReferenceEquals(SelectedBuilding, null))
        {
            DeselectAll();
        }
    }

    public void ClickSelect(Building building)
    {
        if (building == SelectedBuilding) return;

        DeselectAll();

        SelectedBuilding = building;
        (building as ISelectable)?.OnSelect();
        selectionChanged?.Invoke(SelectedBuilding);
    }

    public void DeselectAll()
    {
        if (SelectedBuilding == null)
        {
            if (!ReferenceEquals(SelectedBuilding, null))
            {
                SelectedBuilding = null;
                selectionChanged?.Invoke(null);
            }
            return;
        }

        (SelectedBuilding as ISelectable)?.OnDeselect();
        SelectedBuilding = null;
        selectionChanged?.Invoke(null);
    }
}
