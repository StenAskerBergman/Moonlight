using UnityEngine;
using UnityEngine.UI;

public class BuildingButton : MonoBehaviour
{
    public GameObject buildingPrefab;
    private BuildingSelector buildingSelector;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnButtonClick);
        buildingSelector = FindObjectOfType<BuildingSelector>();
    }

    private void OnButtonClick()
    {
        buildingSelector.ChangeBuildingPrefab(buildingPrefab);
    }
}
