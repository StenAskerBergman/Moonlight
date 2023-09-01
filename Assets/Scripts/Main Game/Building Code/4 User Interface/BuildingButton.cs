using UnityEngine;
using UnityEngine.UI;

public class BuildingButton : MonoBehaviour
{
    [SerializeField] private GameObject buildingPrefab;
    [SerializeField] private BuildingSelector buildingSelector;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnButtonClick);
    }

    private void OnButtonClick()
    {
        // Debug.Log("Building button clicked: " + this.name); // Logs Button Selection
        
        if(buildingPrefab != null)
        {
            // Cancel any previous preview object
            buildingSelector.CancelPreview();

            // Spawn new preview object
            buildingSelector.SpawnPreview(buildingPrefab);
        }
    }
}
