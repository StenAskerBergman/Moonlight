using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using static GridRequirement; Wasn't Active Atm

[System.Serializable]
public class IslandData
{

    #region Variable Data

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
        public GridType.Type gridType;  // Added to specify what type of terrain or island it is.
        public IslandType islandType;
        public Biome biome;

    // Constructor for setting the grid type explicitly
        public IslandData(GridType.Type type)
        {
            gridType = type;
        }

    // Island Ownership & Shareholders
        // public Stock[] stocks;
        // public Owner[] owners;
    #endregion

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
    #region Grid Data

    // This class is used to store all the Grid data

        public Vector3 gridPosition;
        public List<Vector3Int> occupiedCells = new List<Vector3Int>();

    // ... Add any other necessary grid-related data

    #endregion
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
