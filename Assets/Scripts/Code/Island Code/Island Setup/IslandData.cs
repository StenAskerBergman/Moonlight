using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class IslandData
{
    // This class is used to store all the data for an island

    // Island ID Data

        public Vector3 position;
        public float[,] heightMap;
        public string name; 
        public int id;

    // Island Gameplay Data
    public Dictionary<ItemData, int> items;
        public List<Building> buildings;
        public GridData gridData; 
        public Bounds bounds;

    // Island Visual Data

        public IslandType islandType;
        public Biome biome;

    #region Side Notes

        // Future Idea:
        // > Secondary Biomes?

        // Legacy code:
        //public List<Building> buildings = new List<Building>();
        //public List<Enums.Resource> resources = new List<Enums.Resource>();

    #endregion
}


[System.Serializable]
public class GridData
{
    // This class is used to store all the Grid data

    #region Main Data

        public Vector3 gridPosition;
        public List<Vector3Int> occupiedCells = new List<Vector3Int>();

    #endregion

    // ... Add any other necessary grid-related data
}

// Island Types
public enum IslandType
{
    None,
    Tropical,
    Arctic,
    Desert,
    Volcanic,
    Forest,
    Swamp,
    Mountainous,
    Industrial,
    Tundra,
    Rocky,
}

