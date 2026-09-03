using System;
using UnityEngine;

public class Owner : MonoBehaviour, IUniqueIdentifier
{
    // Owner Identification
    public string ID { get; private set; } 
    
    // Sets a PlayerName & Obj. Ingame
    public string playerName;  
    public GameObject owner;

    [SerializeField, Tooltip("The colour used by every unit and structure belonging to this player.")]
    private Color playerColor = new Color(1f, 0.87f, 0.15f, 1f);

    public Color PlayerColor => playerColor;

    private void Awake()
    {
        // Generate a unique ID for this Owner. 
        ID = Guid.NewGuid().ToString(); 

        // Set Owner Object Name to Player Name.
        if (owner != null && !string.IsNullOrWhiteSpace(playerName))
        {
            owner.name = playerName;
        }
    }

    public void SetPlayerColor(Color color)
    {
        color.a = 1f;
        playerColor = color;
    }

    public string GetOwnerID()
    {
        return ID;
    }

}
