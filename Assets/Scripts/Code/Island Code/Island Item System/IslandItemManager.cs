using System.Collections.Generic;
using UnityEngine;

/*
    File Role: Managing island items & buildings on one island  

    Author: Sten

    This script manages items and buildings on an individual island.
    It gets the Island object from the Game Manager and initializes 
    resources and buildings based on the Island's data.

    This script also manages the id assigment of the island.

*/
/*
[RequireComponent(typeof(Island))]
public class IslandItemManager : MonoBehaviour
{
    // Events
    public delegate void ResourceCountChangedHandler(ItemEnums.ResourceType resource, int count);
    public event ResourceCountChangedHandler OnResourceCountChanged;

    // Dictionaries
    private Dictionary<int, IslandStorage> islandResources = new Dictionary<int, IslandStorage>();

    // Public Fields
    public Building[] buildings;
    public ItemEnums.ItemType[] itemTypes;
    public ItemEnums.ResourceType[] resourcesType;
    public PlayerStorageManager playerStorageManager;
    public Island island; // The Island object associated with this script.
    public int islandID; // The ID number of the associated Island.

    // Private Fields
    private Island islandReference;

    // Section 1
    public IslandItemManager(Island island)
    {
        island = GetComponentInParent<Island>(); // Assign the Parent Island to the local Island variable.
    }

    public void SetIsland(Island island)
    {
        this.island = island; // Assign the given Island to the local Island variable.
    }

    // Awake
    private void Awake()
    {
        InitializeReferences();
    }

    // Start
    private void Start()
    {
        ValidateReferences();
        islandID = islandReference.id;
    }

    // Section 2
    private void InitializeReferences()
    {
        playerStorageManager = FindObjectOfType<StorageManager>();
        islandReference = GetComponentInParent<Island>() ?? GetComponent<Island>();
    }

    private void ValidateReferences()
    {
        if (islandReference == null)
        {
            Debug.LogError("IslandItemManager: Could not find associated Island object.");
        }

        if (ItemManager == null)
        {
            Debug.LogError("IslandItemManager: No PlayerItemManager found.");
        }

        if (GameManager.instance == null)
        {
            Debug.LogError("IslandItemManager: No GameManager found.");
        }
    }

    public bool CheckResourceAvailability(ItemEnums.ResourceType resource, int amount)
    {
        int islandId = islandReference.id;

        if (islandResources.TryGetValue(islandId, out StorageManager playerStorageManager))
        {
            // Relies on the GetResourceCount method in IslandStorage.
            int count = islandStorageManager.GetResourceCount(resource); 
            return count >= amount;
        }
        return false;
    }


    #region Set Item Methods
    public void SetResourceCount(ItemEnums.ResourceType resource, int count)
    {
        // Implementation for setting the resource count for the given resource type.
    }

    public void SetMaterialCount(ItemEnums.MaterialType material, int count)
    {
        // Implementation for setting the material count for the given material type.
    }

    public void SetGoodCount(ItemEnums.GoodType good, int count)
    {
        // Implementation for setting the good count for the given good type.
    }
    #endregion

    #region Get Item Methods
    public int GetResourceCount(int islandId, ItemEnums.ResourceType resource)
    {
        // Implementation for fetching resource count.
        return 0;
    }

    public int GetMaterialCount(int islandId, ItemEnums.MaterialType material)
    {
        // Implementation for fetching material count.
        return 0;
    }

    public int GetGoodCount(int islandId, ItemEnums.GoodType good)
    {
        // Implementation for fetching good count.
        return 0;
    }
    #endregion
}
*/
