using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enums : MonoBehaviour
{
    public enum Faction
    {
        None,
        Tyc,
        Eco,
        Sci,
    }
    
    public enum View 
    { 
        Tycoon,
        Ecology,
        Science,

        Pirate,
        Tech,

        Beyond,
        Under,
        Above,
        Over,

        Other,
        Extra,
        Film,
        None
    }

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
    }

    public enum Direction 
    { 
        North, 
        East, 
        South, 
        West 
    };

}
