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
    public PlayerResourceManager playerResourceManager; // A reference to the PlayerResourceManager script.
    public Building[] buildings; // An array of Building objects.
    public Enums.ResourceType[] resources; // An array of Enums.Resource values.
    public Island island; // The Island object associated with this script.
    public int islandID; // The ID number of the associated Island.

    // Section 1
        public IslandResourceManager(Island island)
        {
            island = GetComponentInParent<Island>(); // Assign the Parent Island to the local Island variable.
        }
        
        public void SetIsland(Island island)
        {
            this.island = island; // Assign the given Island to the local Island variable.
        }
    
    // Awake Method
        private void Awake()
        {
            playerResourceManager = GameObject.FindObjectOfType<PlayerResourceManager>(); // Find and assign the PlayerResourceManager script.
            island = GetComponentInParent<Island>();
        }

    // Start Method
        void Start()
        {

            islandID = island.id; // Assign the ID number of the associated Island to the local islandID variable.

            island = transform.root.gameObject.GetComponent<Island>();
            if (island == null)
            {
                Debug.LogError("IslandResourceManager: Could not find parent Island object.");
                return;
            }

            playerResourceManager = FindObjectOfType<PlayerResourceManager>();
            
            GameManager gameManager = GameManager.instance; // Get the instance of the GameManager.
            if (gameManager == null)
            {
                Debug.LogError("IslandResourceManager: No GameManager found."); // Output an error message if the GameManager cannot be found.
                return;
            }
        }

    // Section 2 GetResourceCount

        public void UpdateResourceCount(Enums.Resource resource, int count)
        {
            // Update the resource count in the resource manager.
            playerResourceManager.GetResourceCount(islandID, resource, count);

            // Update the resource count in the island.
            if (island.Resource.ContainsKey(resource))
            {
                island.Resource[resource] = count;
            }

            // Invoke the OnResourceCountChanged event.
            OnResourceCountChanged?.Invoke(resource, count);
        }


        public void SetResourceCount(Enums.Resource resource, int count)
        {
            Dictionary<Enums.Resource, int> resourceDict = island.Resource;
            if (resourceDict.ContainsKey(resource))
            {
                resourceDict[resource] = count;
                OnResourceCountChanged?.Invoke(resource, count); // call event to notify listeners of the change
            }
        }


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
