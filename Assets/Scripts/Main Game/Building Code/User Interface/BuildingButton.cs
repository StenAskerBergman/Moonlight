using UnityEngine;
using UnityEngine.UI;

public class BuildingButton : MonoBehaviour
{
    [SerializeField] private GameObject buildingPrefab;
    [SerializeField] private BuildingSelector buildingSelector;

    /// <summary>Returns the building prefab this button is configured to place.</summary>
    public GameObject GetBuildingPrefab() => buildingPrefab;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnButtonClick);
    }

    private void OnButtonClick()
    {
        // Debug.Log("Building button clicked: " + this.name); // Logs Button Selection

        if (buildingPrefab == null) return;

        // buildingSelector points at a scene object, so a button dropped in from a
        // prefab asset starts unassigned. Report that instead of throwing on click.
        if (buildingSelector == null)
        {
            Debug.LogWarning(
                $"BuildingButton on '{name}' has no BuildingSelector assigned, so '{buildingPrefab.name}' " +
                "cannot be previewed. Assign the scene's Building Handler selector to this button.",
                this);
            return;
        }

        // Cancel any previous preview object
        buildingSelector.CancelPreview();

        // Spawn new preview object
        buildingSelector.SpawnPreview(buildingPrefab);
    }
}
