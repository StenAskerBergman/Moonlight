using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Handels Retriveal of Island by various methods

public class IslandManager : MonoBehaviour
{
    public Island islandPrefab;
    public List<Island> islands { get; private set; }
    private int nextIslandID;

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

        // Raise event when player enters island
        public void InvokeOnPlayerEnterIsland(Island island)
        {
            OnPlayerEnterIsland?.Invoke(island);
        }
    #endregion
}
