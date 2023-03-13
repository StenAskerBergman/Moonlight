using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cell
{
    public Transform transform;
    public Vector3 position;
    public GameObject building;

    public Cell(Vector3 pos, GameObject obj, bool a)
    {
        isWater = a; 
        position = pos;
        building = obj;
    }

    public bool IsEmpty()
    {

        return building == null;                            // Returns one Factor
        //return (building == null && gameObject == null);  // Returns two Factor
    }


    // Noise Map Variables
    public bool isWater, isLand; // Am I Water? or Am I Land?

    // Sea Terrain
    public bool isShore, isSea; // Water Types
    public bool isCoast, isDeep, isPlateau; // Water Height

    // Land Terrain
    public bool isDesert, isForest, isBeach, isPlain, isRocky; // Land Types
    public bool isGround, isHill, isMountain; // Land Height

}
