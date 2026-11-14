using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Construction-menu slot that starts foliage placement instead of spawning a building preview.
///
/// Mirrors <see cref="BuildingButton"/> deliberately: ornamental planting belongs in the same menu
/// as everything else the player places, and the slot is built the same way, so the only difference
/// is which tool the click hands off to.
/// </summary>
public class FoliageButton : MonoBehaviour
{
    [Tooltip("Index into FoliagePlacementController's palette.")]
    [SerializeField] private int speciesIndex;

    [Tooltip("Optional. Left empty, the scene's FoliagePlacementController is used.")]
    [SerializeField] private FoliagePlacementController controller;

    // Resolved rather than required, for the same reason BuildingButton resolves its selector: a
    // button coming from a prefab asset cannot carry a scene reference.
    private FoliagePlacementController ActiveController
    {
        get
        {
            if (controller == null) controller = FindObjectOfType<FoliagePlacementController>();
            return controller;
        }
    }

    public int GetSpeciesIndex() => speciesIndex;
    public void SetSpeciesIndex(int index) => speciesIndex = index;

    private void Start()
    {
        Button button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnButtonClick);
    }

    private void OnButtonClick()
    {
        FoliagePlacementController active = ActiveController;
        if (active == null)
        {
            Debug.LogWarning($"FoliageButton on '{name}' found no FoliagePlacementController in the scene.", this);
            return;
        }

        // A building preview left running would keep consuming clicks under the foliage tool.
        if (BuildingSelector.Active != null) BuildingSelector.Active.CancelPreview();

        active.BeginPlacement(speciesIndex);
    }
}
