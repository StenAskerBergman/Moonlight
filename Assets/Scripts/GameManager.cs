using System.Collections.Generic;
using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public int currentIslandID;
    public MapManager mapManager;
    public PlayerMaterialManager playerMaterialManager;
    private IslandManager islandManager;
    private UIManager uiManager;
    public Island currentIsland;
    private Dictionary<int, GameObject> islandObjects = new Dictionary<int, GameObject>(); // Added dictionary for island game objects
    private Island previousIsland;

    private void Awake()
    {
        if (instance != null && instance != this) {
            Destroy(this.gameObject);
            return;
        }

        instance = this;
        playerMaterialManager = FindObjectOfType<PlayerMaterialManager>();
        islandManager = FindObjectOfType<IslandManager>();
    }



    public IslandResourceManager GetIslandResourceManager(int islandID)
    {
        Island islandData = islandManager.GetIslandByID(islandID);
        if (islandData != null)
        {
            IslandResourceManager islandResourceManager = islandData.islandObject.GetComponent<IslandResourceManager>();
            if (islandResourceManager != null)
            {
                return islandData.islandObject.GetComponent<IslandResourceManager>();
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