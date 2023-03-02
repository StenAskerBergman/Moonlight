using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class IslandData
{
    public Enums.IslandType islandType;
    public List<Building> buildings;
    public List<Enums.Resource> resources;
    public Bounds bounds;
    public int id;
    public string name;
    //public List<Building> buildings = new List<Building>();
    //public List<Enums.Resource> resources = new List<Enums.Resource>();

}