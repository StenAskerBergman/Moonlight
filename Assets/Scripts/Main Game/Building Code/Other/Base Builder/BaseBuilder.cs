using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseBuilder : MonoBehaviour
{
    /* Fix Later
    GameObject playerBasePrefab;

    // WHAT IS THE PLAYER BASE:?: THE PLAYERBASE IS A GO THAT IS ADDED TO THE ISLAND AS A CHILD "ENTITIY" CEGO USING (A UNIT TYPE (BOAT / SHIP)) && BASEBUILDER.cs |or| [Plan to be added later] (World Generation) 
    
    public Inventory AddPlayerBase(Island island)
    {
        // Generate a Placeholder Name
        string baseName = NameGenerator();

        // Add the player base to the island index
        island.BaseIndex();

        // Create the player base
        GameObject playerBase = new GameObject($"PlayerBase: {baseName}");
        
        // Setting the player base parent to the island
        playerBase.transform.SetParent(island.transform);

        // Setting the player base position to the island position
        playerBase.transform.position = island.transform.position;
        
        // Inventory.cs - Generic ( Class for all inventories )
        playerBase.AddComponent<Inventory>();

        // IslandInventory.cs - Future ( Island Inventory Class for all Island inventories )
        // playerBase.AddComponent<IslandInventory>();

        // Returning Requested Inventory 
        return playerBase.GetComponent<Inventory>();
    }

    // New Name Generator
    private string NameGenerator()
    {
        // Code that Generate New Name For Player Base

        return "Island Name";
    }
    */

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
