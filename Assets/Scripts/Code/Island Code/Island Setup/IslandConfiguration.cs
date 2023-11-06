using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "IslandConfig", menuName = "Island/Configuration")]
public class IslandConfiguration : ScriptableObject
{
    public IslandClass islandClass;
    public List<ItemInitializer> seeds;
    public List<ItemInitializer> resources;

    // Default values, adjust as necessary
    public int totalPowerfulSeeds = 2;  
    public int totalSeedsToInitialize = 2;  
}

public enum IslandClass
{
    Starter,
    Expansion,
    Midgame,
    Lategame
}
