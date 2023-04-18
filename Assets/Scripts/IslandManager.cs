using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Handels Retriveal of Island by various methods

public class IslandManager : MonoBehaviour
{
    private GridSystem currentGridSystem;
    public Island islandPrefab;
    public List<Island> islands { get; private set; }
    private int nextIslandID;
    public Camera playerCamera;

    public static IslandManager instance;

    public void NotifyGridSystemDetected(GridSystem gridSystem)
    {
        Debug.Log("Invoking OnGridSystemDetected event."); // Add this debug log
        OnGridSystemDetected?.Invoke(gridSystem);
    }

    #region Awake + Start Methods (Instance Setting)
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    private void Start()
    {
        islands = new List<Island>();
        nextIslandID = 1;
        StartCoroutine(DetectInitialIsland(playerCamera));

        if (BuildingChecker.instance != null)
        {
            OnGridSystemDetected += BuildingChecker.instance.UpdateGridSystem;
        }
    }


    private void Update(){
        
        Island hoveredIsland = GetHoveredIsland();
        if (hoveredIsland != null && hoveredIsland.GetComponentInChildren<GridSystem>() != currentGridSystem)
        {
            ChangeActiveIsland(hoveredIsland);
            OnPlayerHoverIsland?.Invoke(hoveredIsland);

            //Debug.Log(hoveredIsland);
        }
        
        if (Input.GetMouseButtonDown(0))
        {
            Island clickedIsland = GetClickedIsland();
            if (clickedIsland != null)
            {
                OnPlayerHitIsland?.Invoke(clickedIsland);
            }
        }
    }

    #endregion

    #region Island +/- Methods 
        // Adds Island
        public void AddIsland(IslandData data)
        {
            Island island = new Island(Enums.IslandType.None);
            //island.buildings = data.buildings;
            island.bounds = data.bounds;
            island.id = GetNextIslandID(); // set the id of the island

            // Convert the resources list to a dictionary
            foreach (Enums.Resource resource in data.resources)
            {
                island.AddResource(resource, 0);
            }

            islands.Add(island);
        }

        // Removes
        public void RemoveIsland(Island island)
        {
            islands.Remove(island);
        }
        
    #endregion

    #region GetIsland Methods 

        // By Hover
        public Island GetHoveredIsland()
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit, 1000f))
            {
                Island hoveredIsland = hit.collider.GetComponent<Island>();
                if (hoveredIsland != null)
                {
                    return hoveredIsland;
                }
            }

            return null;
        }

        // By Camera
        public Island GetIslandInFrontOfCamera(Camera playerCamera)
        {
            
            RaycastHit hit;
            float maxDistance = 500f; // You can adjust this value as needed
            if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, maxDistance))
            {
                
                Island hitIsland = hit.collider.GetComponentInParent<Island>();
                if (hitIsland != null)
                {
                    return hitIsland;
                }
            }

            return null;
        }

        // By Name
        public Island GetIslandByName(string name)
        {
            return islands.Find(island => island.islandName == name);
        }

        // By ID
        public Island GetIsland(int id)
        {
            return islands.Find(island => island.id == id);
        }
        // By ID
        public Island GetIslandByID(int id)
        {
            return islands.Find(island => island.id == id);
        }

        public Island GetIsland(Vector3 position)
        {
            foreach (Island island in islands)
            {
                if (island.bounds.Contains(position))
                {
                    return island;
                }
            }

            return null;
        }

        public Enums.IslandType GetCurrentIslandType(Vector3 playerPosition)
        {
            foreach (Island island in islands)
            {
                if (island.bounds.Contains(playerPosition))
                {
                    return island.islandType;
                }
            }

            return Enums.IslandType.None;
        }

        // By Next ID
        private int GetNextIslandID()
        {
            return nextIslandID++;
        }

    #endregion

    #region Events Section + Initial IEnumerators 
    
    // Event Section in IslandManager.cs
        #region Event Refs + Basic Events
        // Add event for player entering island
        public event Action<Island> OnPlayerEnterIsland;
        public event Action<Island> OnPlayerHitIsland;
        public event Action<Island> OnPlayerHoverIsland;
        public delegate void GridSystemDetectedEventHandler(GridSystem gridSystem);
        public event GridSystemDetectedEventHandler OnGridSystemDetected;

        // Raise event when player enters island
        
            public void InvokeOnPlayerEnterIsland(Island island)
            {
                OnPlayerEnterIsland?.Invoke(island);
            }

            public void InvokeOnGridSystemDetected(GridSystem gridSystem)
            {
                currentGridSystem = gridSystem;
                OnGridSystemDetected?.Invoke(gridSystem);
            }
    
        #endregion

    
        #region Detection Events
        public GridSystem GetCurrentGridSystem()
        {
            return currentGridSystem;
        }
                
        public void ChangeActiveIsland(Island newActiveIsland)
        {
            GridSystem gridSystem = newActiveIsland.GetComponentInChildren<GridSystem>();
            if (gridSystem != null)
            {
                InvokeOnGridSystemDetected(gridSystem);
            }
        }

        public Island GetClickedIsland()
        {
            if (Input.GetMouseButtonDown(0))
            {
                return GetHoveredIsland();
            }

            return null;
        }

        public Island GetIslandForBuildingPreview(BuildingPreview buildingPreview)
        {
            if (buildingPreview != null)
            {
                RaycastHit hit;
                Ray ray = new Ray(buildingPreview.transform.position, Vector3.down);

                if (Physics.Raycast(ray, out hit, 1000f))
                {
                    Island islandForBuildingPreview = hit.collider.GetComponent<Island>();
                    if (islandForBuildingPreview != null)
                    {
                        return islandForBuildingPreview;
                    }
                }
            }

            return null;
        }


        private IEnumerator DetectInitialIsland(Camera playerCamera)
        {
            
            // Wait for a short time to let other systems initialize
            yield return new WaitForSeconds(0.1f);

            // Call the InvokeOnGridSystemDetected method here to detect the initial island and grid system
            Island initialIsland = GetIslandInFrontOfCamera(playerCamera);
            if (initialIsland != null)
            {
                GridSystem gridSystem = initialIsland.GetComponentInChildren<GridSystem>();
                if (gridSystem != null)
                {
                    InvokeOnGridSystemDetected(gridSystem);
                }

                // Invoke the OnPlayerEnterIsland event
                InvokeOnPlayerEnterIsland(initialIsland);
                
            }
        }
        
        #endregion



    #endregion

}
