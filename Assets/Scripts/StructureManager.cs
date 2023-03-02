using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StructureManager {
    private static StructureManager instance;
    public static StructureManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new StructureManager();
            }
            return instance;
        }
    }

    private List<GameObject> buildings = new List<GameObject>();

    public void AddBuilding(GameObject building)
    {
        buildings.Add(building);
    }

    public void RemoveBuilding(GameObject building)
    {
        buildings.Remove(building);
    }

    public int GetBuildingCount()
    {
        return buildings.Count;
    }
}