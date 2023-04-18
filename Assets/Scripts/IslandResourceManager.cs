using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*  
    File Role: managing resources & buildings on one island  

    Author: Sten

    The IslandResourceManager script is responsible for 
    managing the resources and buildings on an individual 
    island. It gets the Island object from the GameManager 
    and initializes the resources and buildings based on 
    the Island's data.

*/

// is attached to instantiated islands 
public class IslandResourceManager : MonoBehaviour
{

    // Events
    public delegate void ResourceCountChangedHandler(Enums.Resource resource, int count);
    public event ResourceCountChangedHandler OnResourceCountChanged;

    // variables
    public PlayerMaterialManager playerMaterialManager; // A reference to the PlayerMaterialManager script.
    
    public Building[] buildings; // An array of Building objects.
    public Enums.ResourceType[] resourceTypes; // An array of Enums.Resource values.
    public Enums.Resource[] resources; // An array of Enums.Resource values.

    public Island island; // The Island object associated with this script.
    private Island currentIsland;
    public int islandID; // The ID number of the associated Island.

    // Section 1
        public IslandResourceManager(Island island)
        {
            island = GetComponentInParent<Island>(); // Assign the Parent Island to the local Island variable.
        }
        
        public void SetIsland(Island island)
        {
            this.island = island; // Assign the given Island to the local Island variable.
            this.island = currentIsland;
        }
    
    // Awake Method - IslandResourceManager
        private void Awake()
        {
            playerMaterialManager = GameObject.FindObjectOfType<PlayerMaterialManager>(); // Find and assign the PlayerMaterialManager script.
            island = GetComponentInParent<Island>(); // Prefab Ref
            currentIsland = GetComponent<Island>(); // Current Ref - somehow this works not sure why
        }

    //  Start Method - IslandResourceManager
        void Start()
        {

            islandID = island.id; // Assign the ID number of the associated Island to the local islandID variable.
            //Debug.Log("island.id: "+ island.id + " islandID: "+ islandID + " currentIsland.id: "+ currentIsland.id);
            currentIsland.id = island.id;
            

            island = transform.root.gameObject.GetComponent<Island>();
            if (island == null)
            {
                Debug.LogError("IslandResourceManager: Could not find parent Island object.");
                return;
            }

            playerMaterialManager = FindObjectOfType<PlayerMaterialManager>();
            
            GameManager gameManager = GameManager.instance; // Get the instance of the GameManager.
            if (gameManager == null)
            {
                Debug.LogError("IslandResourceManager: No GameManager found."); // Output an error message if the GameManager cannot be found.
                return;
            }
        }




    // public void SetResourceCount(Enums.Resource resource, int count)
    // {
    //     Dictionary<Enums.Resource, int> resourceDict = island.Resource;
    //     if (resourceDict.ContainsKey(resource))
    //     {
    //         resourceDict[resource] = count;
    //         OnResourceCountChanged?.Invoke(resource, count); // call event to notify listeners of the change
    //     }
    // }


    // public int GetResourceCount(int islandId, Enums.Resource resource)
    // {
    //     Enums.IslandType islandType = (Enums.IslandType)islandId;
    //     ResourceInventory resourceInventory = GetIslandStorage(islandType);
    //     if (resourceInventory != null)
    //     {
    //         int count = resourceInventory.GetResourceCount(resource);
    //         Debug.Log(string.Format("{0} on island {1}: {2}", resource, islandId, count));
    //         return count;
    //     }

    //     return 0;
    // }

}
