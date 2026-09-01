using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Left-click-to-select for buildings, mirroring UnitClick.cs's raycast pattern.
/// Kept as a separate component (rather than folded into UnitClick) so unit
/// selection (layer "Clickable") and building selection (layer "Buildings" —
/// see ProjectSettings/TagManager.asset) stay independent: a single click can only
/// hit whichever one of the two layer masks the clicked collider is actually on.
/// </summary>
public class BuildingClick : MonoBehaviour
{
    private Camera myCam;

    [Tooltip("Set to the 'Buildings' layer (Navigation/Layers) in the Inspector.")]
    public LayerMask buildingClickable;

    private void Start()
    {
        myCam = Camera.main;
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (BuildingSelections.Instance == null) return;

        // While demolition mode is on, a click marks a building for removal instead of
        // selecting it. Both raycast the same layer, so without this one click would do
        // both - marking a building and opening its panel.
        if (DemolitionManager.Instance != null && DemolitionManager.Instance.IsActive) return;

        Ray ray = myCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, buildingClickable))
        {
            Building building = hit.collider.GetComponentInParent<Building>();
            if (building != null)
            {
                BuildingSelections.Instance.ClickSelect(building);
                return;
            }
        }

        BuildingSelections.Instance.DeselectAll();
    }
}
