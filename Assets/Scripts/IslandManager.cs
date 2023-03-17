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
    }

    #endregion

    #region Island +/- Methods 
        // Adds Island
        public void AddIsland(IslandData data)
        {
            Island island = new Island(Enums.IslandType.None);
            island.buildings = data.buildings;
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

        // By Camera
        private Island GetIslandInFrontOfCamera(Camera playerCamera)
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;
            float maxDistance = 1000f; // Adjust this value based on the maximum distance you want to detect islands from
            LayerMask islandLayer = LayerMask.GetMask("Island"); // Replace "Island" with the name of your island layer

            if (Physics.Raycast(ray, out hit, maxDistance, islandLayer))
            {
                Island hitIsland = hit.collider.GetComponent<Island>();

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

    #region Events Section 
    
    // Event Section in IslandManager.cs
    
        // Add event for player entering island
        public event Action<Island> OnPlayerEnterIsland;
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

            public GridSystem GetCurrentGridSystem()
            {
                return currentGridSystem;
            }
            
    #endregion

    #region Initial IEnumerators 
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
            Debug.Log(initialIsland);
        }
    }


    #endregion
}
