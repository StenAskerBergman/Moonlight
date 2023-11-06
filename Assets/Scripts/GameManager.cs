using System.Collections.Generic;
using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public int currentIslandID;
    public MapManager mapManager;
    public PlayerItemManager playerItemManager;
    public Island currentIsland;

    private IslandManager islandManager;
    private HUDManager hudManager;
    private Dictionary<int, GameObject> islandObjects = new Dictionary<int, GameObject>(); // Added dictionary for island game objects
    private Island previousIsland;
    
    // Games Return Rate Policy
    public int ReturnRate;
    public int previousIslandID;

    private void Awake()
    {
        if (instance != null && instance != this) {
            Destroy(this.gameObject);
            return;
        }

        instance = this;
        islandManager = FindObjectOfType<IslandManager>();
        hudManager = FindObjectOfType<HUDManager>();
    }

    public Inventory GetIslandInventory(int islandID)
    {
        Island islandData = islandManager.GetIslandByID(islandID);
        if (islandData != null)
        {
            Inventory islandInventory = islandData.islandObject.GetComponent<Inventory>();
            if (islandInventory != null)
            {
                return islandData.islandObject.GetComponent<Inventory>();
            }
        }
        return null;
    }

    public int GetCurrentIslandID(Vector3 playerPosition)
    {
        Island islandData = islandManager.GetIsland(playerPosition);
        if (islandData != null)
        {
            return islandData.id;
        }

        return -1;
    }

    public Island GetIslandByID(int id)
    {
        return islandManager.GetIslandByID(id);
    }

    public Island GetCurrentIsland()
    {
        return currentIsland;
    }
    
    public Island GetPreviousIsland()
    {
        return previousIsland;
    }


    private void OnDestroy()
    {
        instance = null;
    }

    public GameObject GetIslandGameObjectByID(int id)
    {
        if (islandObjects.TryGetValue(id, out GameObject islandGO))
        {
            return islandGO;
        }
        return null;
    }
}