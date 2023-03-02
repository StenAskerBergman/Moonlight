using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandEnterDetector : MonoBehaviour
{
    private Camera mainCamera;
    private Island currentIsland;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            // Get the island that the detector hit
            Island island = hit.collider.GetComponent<Island>();
            Debug.Log(hit);
            if (island != null && island != currentIsland)
            {
                currentIsland = island;
                Debug.Log("Player entered island: " + currentIsland.name);
                IslandManager.instance.InvokeOnPlayerEnterIsland(currentIsland);
            }
        }
        else
        {
            currentIsland = null;
        }
    }

    // Ray Cast Notes
        
        // Get the island that the detector is attached to
        // Island island = GetComponentInParent<Island>();
        
        //if (hit.transform.gameObject.CompareTag("Player"))
        
        // Raise the OnPlayerEnterIsland event in the IslandManager
        // IslandManager.instance.PlayerEnterIsland(island);
        
}