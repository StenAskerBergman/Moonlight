using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingSelector : MonoBehaviour
{
    [SerializeField] private GameObject previewObject;
    private GameObject activeBlueprint;
    private bool buildMode;

    public void SpawnPreview()
    {
        if (activeBlueprint != null)
        {
            Destroy(activeBlueprint);
            
        }
        
        buildMode = true;
        activeBlueprint = Instantiate(previewObject);
    }

    private void Update()
    {
        ToggleBuildMode();
    }

    private void ToggleBuildMode()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            buildMode = !buildMode;
            Debug.Log("Build mode: " + buildMode);

            if (!buildMode && activeBlueprint != null)
            {
                Destroy(activeBlueprint);
                activeBlueprint = null;
            }
        }


    }

    public void ChangeBuildingPrefab(GameObject newBuildingPrefab)
    {
        activeBlueprint = newBuildingPrefab;
    }
}


