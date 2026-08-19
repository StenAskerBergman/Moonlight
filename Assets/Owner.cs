using System;
using UnityEngine;

public class Owner : MonoBehaviour, IUniqueIdentifier
{
    // Owner Identification
    public string ID { get; private set; } 
    
    // Sets a PlayerName & Obj. Ingame
    public string playerName;  
    public GameObject owner;

    private void Awake()
    {
        // Generate a unique ID for this Owner. 
        ID = Guid.NewGuid().ToString(); 

        // Set Owner Object Name to Player Name.
        owner.name = playerName;
    }

    public string GetOwnerID()
    {
        return ID;
    }

}
