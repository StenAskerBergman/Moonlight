using UnityEngine;
using UnityEngine.UI;

public class BuildingButton : MonoBehaviour
{
    [SerializeField] private GameObject buildingPrefab;
    [Tooltip("Optional. Left empty, the scene's BuildingSelector is used.")]
    [SerializeField] private BuildingSelector buildingSelector;

    // Resolved rather than required. A button dropped in from a prefab asset cannot carry
    // a scene reference, and there is only one selector to point at anyway.
    private BuildingSelector ActiveSelector
    {
        get
        {
            if (buildingSelector == null) buildingSelector = BuildingSelector.Active;
            return buildingSelector;
        }
    }

    /// <summary>Returns the building prefab this button is configured to place.</summary>
    public GameObject GetBuildingPrefab() => buildingPrefab;

    /// <summary>Configures the building prefab this button places.</summary>
    public void SetBuildingPrefab(GameObject prefab) => buildingPrefab = prefab;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnButtonClick);
    }

    private void OnButtonClick()
    {
        // Debug.Log("Building button clicked: " + this.name); // Logs Button Selection

        if (buildingPrefab == null) return;

        BuildingSelector selector = ActiveSelector;
        if (selector == null)
        {
            Debug.LogWarning(
                $"BuildingButton on '{name}' found no BuildingSelector in the scene, so " +
                $"'{buildingPrefab.name}' cannot be previewed.",
                this);
            return;
        }

        // Cancel any previous preview object
        selector.CancelPreview();

        // Spawn new preview object
        selector.SpawnPreview(buildingPrefab);
    }
}
