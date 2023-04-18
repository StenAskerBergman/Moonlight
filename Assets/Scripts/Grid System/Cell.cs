using Grpc.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cell
{
    public Vector3Int cellPosition;
    //public Transform transform;
    public Vector3 position;
    public Building building;
    public bool isBlocked;
    public bool isDeposit;
    public bool isOccupied;

    public enum status
    {
        isFull, 
        isNull, 
        isWater
    }

    public bool isRiver;
    public bool nearRiver;

    public Cell(Vector3 position, Building building, bool isBlocked, bool isDeposit, bool isOccupied)
    {
        this.position = position;
        this.building = building;
        this.isBlocked = isBlocked;
        this.isDeposit = isDeposit;
        this.isOccupied = isOccupied;

        // Set the cellPosition based on the position
        this.cellPosition = new Vector3Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y), Mathf.RoundToInt(position.z));
    }

    public Cell(Vector3 pos, Building obj, bool a)
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
