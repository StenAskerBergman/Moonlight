using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Island : MonoBehaviour, IUniqueIdentifier
{
    // Island Configuration
    public IslandConfiguration islandConfig;

    // Island Terrain
    private TerrainManager terrainManager;

    // Island Events
    public delegate void IslandValueChangedHandler();
    public event IslandValueChangedHandler OnIslandValueChanged;


    // Interface 
    public string ID { get; private set; }
    public int id;

    // Variables
    public string islandName = "Uninhabited Islands"; // default name
    public IslandType islandType;
    public Bounds bounds;

    // Refs
    public GameObject islandObject { get; set; }
    Inventory islandInventory;
    MapManager mapManager;

    

    public void Start()
    {
        islandInventory = gameObject.GetComponent<Inventory>();

        // bounds is owned by GridSystem.SetIslandBounds(), which derives it from the
        // real grid size. Seeding it here raced that call - Start() order between two
        // components on the same GameObject is undefined - and the placeholder extents
        // collapsed the island to a 10x10x10 box whenever this happened to run last.

        // Old Code
        // MapManager As Island Parent
        // mapManager = FindObjectOfType<MapManager>(); // locate the amount of islands to be generated
        // this.transform.SetParent(mapManager.transform);
        // LogBounds(); // Bounds Debugging

        //terrainManager = new TerrainManager();
        //terrainManager.GenerateTerrain(this);
    }

    public void LogBounds()
    {
        Debug.Log($"Island: {islandName + "id: " + id} Bounds: {bounds}");                          // island names
        Debug.Log($"Island: {islandName + "id: " + id} Set Bounds center: {bounds.center}");        // island centers
        Debug.Log($"Island: {islandName + "id: " + id} Set Bounds extents: {bounds.extents}");      // island extents
    }

    // Get the specific inventory for a player base - using Island.cs
    public Inventory GetPlayerBaseInventory(string baseID)
    {
        foreach (Transform child in transform)
        {
            IUniqueIdentifier identifier = child.GetComponent<IUniqueIdentifier>();
            if (identifier != null && identifier.ID == baseID)
            {
                return child.GetComponent<Inventory>();
            }
        }
        return null;
    }

    // In Island.cs
    public BaseStorageManager GetBaseStorageManagerForID(string baseID)
    {
        foreach (Transform child in transform)
        {
            IUniqueIdentifier identifier = child.GetComponent<IUniqueIdentifier>();
            if (identifier != null && identifier.ID == baseID)
            {
                // Returns the specific BaseStorageManager
                return child.GetComponent<BaseStorageManager>();
            }
            else if (identifier == null)
            {
                return null;
            }
            /*else if (identifier != null || identifier.ID != baseID)
            {
                // There Might be Too many for this to be viable so lets not add this yet...
                Debug.Log("Incorrect ID Found!");
            }*/

        }
        return null;
    }

    public List<ItemData> GetActivatedSeeds()
    {
        return ActiveSeeds;
    }

    // Inside CurrentIsland class:
    public BaseStorageManager GetBaseStorageManager()
    {
        return GetComponent<BaseStorageManager>();
    }

    /// <summary>
    /// Returns every deposit cell of the given type on this island's grid, so systems
    /// like IslandAssignmentManager or UI can ask "does this island have a mine?"
    /// without scanning the whole grid themselves.
    /// </summary>
    public List<Cell> GetDepositCells(ResourceNodeType type)
    {
        List<Cell> depositCells = new List<Cell>();

        MapGrid mapGrid = GetComponent<MapGrid>();
        if (mapGrid == null || mapGrid.Grid == null) return depositCells;

        foreach (Cell cell in mapGrid.Grid)
        {
            if (cell.isDeposit && cell.depositNodeType == type)
            {
                depositCells.Add(cell);
            }
        }

        return depositCells;
    }

    // Trade Rules
    public IslandTradeRules TradeRules { get; private set; }

    // Population per faction/demographic. Backs the warehouse panel's tier tabs and
    // every PopulationUnlock check on this island.
    public IslandPopulation Population { get; private set; }

    // Pool of unsocketed modifier items. Deliberately separate from the commodity
    // Inventory - see IslandItemStorage.
    public IslandItemStorage Items { get; private set; }

    /// <summary>
    /// Sets up a freshly instantiated island. This replaces what used to be a
    /// constructor: Island is a MonoBehaviour, so only Unity may create it (via the
    /// prefab or AddComponent). Calling 'new Island(...)' produced a second, orphaned
    /// instance that held all the data while the component actually on the GameObject
    /// held none. Call this on the Island component of the instantiated prefab.
    /// </summary>
    public void Initialize(IslandType type)
    {
        // Initialize the required fields
        this.islandType = type;
        this.IslandItems = new Dictionary<ItemData, int>();
        this.IslandSeeds = new Dictionary<ItemData, int>();

        this.buildings = new List<Building>();

        TradeRules = GetComponent<IslandTradeRules>();
        if (TradeRules == null)
        {
            TradeRules = gameObject.AddComponent<IslandTradeRules>();
        }

        // Fetched-then-added like TradeRules above, so an island that was authored with
        // these components in the prefab keeps its Inspector values (starting population,
        // faction, starting items) and a procedurally generated one still gets them.
        Population = GetComponent<IslandPopulation>();
        if (Population == null)
        {
            Population = gameObject.AddComponent<IslandPopulation>();
        }

        Items = GetComponent<IslandItemStorage>();
        if (Items == null)
        {
            Items = gameObject.AddComponent<IslandItemStorage>();
        }

        // Future Idea:
        // idea use Bool List to Allow / Disallow certain items / seeds from a island
        // this.AllowedIslandItems = new Dictionary<ItemData, bool>(); // Default everything to false - most stuff shouldn't be allowed
    
    }

    #region Seed Related Code

    // list of buildings on the island
    public List<Building> buildings = new List<Building>(); // List of buildings on the island

    // list of Items on the island
    public Dictionary<ItemData, int> IslandItems = new Dictionary<ItemData, int>();    // Dictionary List of Normal Items on the island

    // list of Seeds on the island
    public Dictionary<ItemData, int> IslandSeeds = new Dictionary<ItemData, int>();    // Dictionary List of Seed Items on the island - Should Always Be 0 to 3 seeds / Seeds Can't be Removed / Only Seed Items Can Be Activated

    // New fields for managing seeds
    public List<ItemData> PotentialSeeds = new List<ItemData>();
    public List<ItemData> ActiveSeeds = new List<ItemData>();

    // Method to add a seed to the PotentialSeeds list
    public void AddPotentialSeed(ItemData seed)
    {
        if (!PotentialSeeds.Contains(seed))
        {
            PotentialSeeds.Add(seed);
        }
        else
        {
            Debug.LogWarning("Seed already in PotentialSeeds list.");
        }
    }

    // Method to activate a seed
    public bool ActivateSeed(ItemData seed)
    {
        // Ensure the seed is available on the island (using SeedManager).
        // Seeds are activated using the Harbour Building or Island Intialization
        // Ensure the seed has not been previously activated.
        // Activate the seed which should update related buildings’ production.

        // Example:
        foreach (var building in buildings)
        {
            if (building.IsCompatibleWithSeed(seed))
            {
                building.SeedActivate(seed);
            }
        }

        // Check if there's space in the ActiveSeeds list
        if (ActiveSeeds.Count < 3)
        {
            // Check if the seed type is not already activated
            if (!IsSeedTypeActivated(seed))
            {
                ActiveSeeds.Add(seed);
                // Remove from PotentialSeeds list if it's there
                PotentialSeeds.Remove(seed);
                return true;
            }
            else
            {
                Debug.LogWarning("Seed type already activated.");
                return false;
            }
        }
        else
        {
            Debug.LogWarning("No space to activate more seeds.");
            return false;
        }
    }

    // Method to check if a seed type is already activated
    public bool IsSeedTypeActivated(ItemData seed)
    {
        return ActiveSeeds.Any(activeSeed => activeSeed.type == seed.type);
    }

    // Method to remove a seed from the PotentialSeeds list
    public void RemovePotentialSeed(ItemData seed)
    {
        if (PotentialSeeds.Contains(seed))
        {
            PotentialSeeds.Remove(seed);
        }
        else
        {
            Debug.LogWarning("Seed not found in PotentialSeeds list.");
        }
    }


    #endregion

}


