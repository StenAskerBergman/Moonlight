using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandEnterDetector : MonoBehaviour
{

    // This script uses the camera to detect island id

    private Camera mainCamera;
    private Island currentIsland;
    public delegate void GridSystemDetectedEventHandler(GridSystem gridSystem);
    public event GridSystemDetectedEventHandler OnGridSystemDetected;


    private void Start()
    {
        
        mainCamera = Camera.main;
        DetectInitialIsland();
    }

    private void DetectInitialIsland()
    {
        RaycastHit hit;

        // Ray Cast
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

        //Debug.Log("0");

        if (Physics.Raycast(ray, out hit, Mathf.Infinity))
        {
            
            //Debug.Log("1");
            
            Island island = hit.collider.GetComponent<Island>();
     
            if (island != null)
            {
                currentIsland = island;
                IslandManager.instance.InvokeOnPlayerEnterIsland(currentIsland);
                GridSystem gridSystem = island.GetComponent<GridSystem>();
                Debug.Log("2");

                if (gridSystem != null)
                {
                    IslandManager.instance.InvokeOnGridSystemDetected(gridSystem);
                    Debug.Log("3");
                }
                else
                {
                    Debug.Log("4");
                    return;
                }
            }
        }
    }




    private void Update()
    {
        
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            Island island = hit.collider.GetComponent<Island>();

            if (island != null && island != currentIsland)
            {
                currentIsland = island;
                IslandManager.instance.InvokeOnPlayerEnterIsland(currentIsland);

                // Find the GridSystem component in the detected island
                GridSystem gridSystem = island.GetComponent<GridSystem>();
                
                if (gridSystem != null)
                {
                    // Invoke the event with the detected GridSystem
                    IslandManager.instance.InvokeOnGridSystemDetected(gridSystem);

                } else {

                    return;
                    
                }
            }
        }
        else
        {
            currentIsland = null;
        }
    }
}