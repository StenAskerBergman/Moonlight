using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tracks which Building is currently selected via click. Mirrors the
/// UnitSelections/ISelectable pattern (Unit/Unit Scripts/UnitSelections.cs) but
/// trimmed to single-select, since only one building's HUD panel is shown at a time.
///
/// Deliberately independent of UnitSelections — selecting a building does not
/// deselect a selected unit and vice versa. The ISelectable.cs priority comment
/// ("Mil > Units > Buildings") implies these should eventually be reconciled, but
/// that ordering isn't implemented anywhere yet, so this doesn't invent it either.
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
        if (SelectedBuilding == null) return;

        (SelectedBuilding as ISelectable)?.OnDeselect();
        SelectedBuilding = null;
        selectionChanged?.Invoke(null);
    }
}
