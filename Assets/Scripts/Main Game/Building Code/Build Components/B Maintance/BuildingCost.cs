using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class BuildingCost : MonoBehaviour
{
    public CostData costData;  // Reference to the CostData SO

    public List<ItemData> GetAllCostItems()
    {
        return costData != null && costData.costItems != null
            ? new List<ItemData>(costData.costItems)
            : new List<ItemData>();
    }

    /// <summary>
    /// The item cost of this building, and whether it has one at all.
    ///
    /// This is the single place that decides what an unassigned CostData means. No
    /// building prefab in the project currently carries one, and every reader used to
    /// dereference costData directly - BaseStorageManager.CanAffordBuilding threw the
    /// moment an island had a warehouse and placement started routing through island
    /// storage. A building with no CostData asset states no cost, so it is free; it is
    /// not "unaffordable", which would make the whole island unbuildable.
    /// </summary>
    public bool TryGetCosts(out Dictionary<ItemData, int> costItems)
    {
        if (costData == null)
        {
            costItems = EmptyCosts;
            return false;
        }

        costItems = costData.GetCostItemsDictionary();
        return costItems.Count > 0;
    }

    private static readonly Dictionary<ItemData, int> EmptyCosts = new Dictionary<ItemData, int>();

    /// <summary>
    /// Whether an island's goods cover this building's item cost. The island's Inventory is
    /// the stockpile the warehouse panel shows and logistics delivers into, so construction
    /// pays from the same place the player can see. A building with no item cost is free.
    /// </summary>
    public bool CanBePaidFrom(Inventory goods)
    {
        if (!TryGetCosts(out Dictionary<ItemData, int> costItems)) return true;
        if (goods == null) return false;

        foreach (var cost in costItems)
        {
            if (cost.Key == null || cost.Value <= 0) continue;
            if (!goods.HasItem(cost.Key, cost.Value)) return false;
        }
        return true;
    }

    /// <summary>Takes the item cost out of the island's goods. All or nothing.</summary>
    public bool TryPayFrom(Inventory goods)
    {
        Dictionary<ItemData, int> positiveCosts = BuildPositiveCostMap();
        if (positiveCosts.Count == 0) return true;
        if (goods == null) return false;

        return goods.TryRemoveItems(positiveCosts);
    }

    /// <summary>Whether the specified vessel holds enough cargo to fund construction.</summary>
    public bool CanBePaidFromBoat(Unit boat)
    {
        Dictionary<ItemData, int> positiveCosts = BuildPositiveCostMap();
        if (positiveCosts.Count == 0) return true;
        if (boat == null) return false;

        UnitInventory boatInv = boat.GetComponent<UnitInventory>();
        if (boatInv == null) return false;

        Dictionary<ItemData, int> boatItems = boatInv.GetAllItems();
        foreach (var cost in positiveCosts)
        {
            if (!boatItems.TryGetValue(cost.Key, out int available) || available < cost.Value)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Deducts the building's goods cost atomically from a founding vessel.</summary>
    public bool TryPayFromBoat(Unit boat)
    {
        Dictionary<ItemData, int> positiveCosts = BuildPositiveCostMap();
        if (positiveCosts.Count == 0) return true;
        if (!CanBePaidFromBoat(boat)) return false;

        UnitInventory boatInv = boat.GetComponent<UnitInventory>();
        if (boatInv == null) return false;

        var deducted = new List<KeyValuePair<ItemData, int>>();
        foreach (var cost in positiveCosts)
        {
            if (!boatInv.RemoveItem(cost.Key, cost.Value))
            {
                foreach (var rollback in deducted)
                {
                    boatInv.AddItem(rollback.Key, rollback.Value, "Rollback");
                }
                return false;
            }
            deducted.Add(cost);
        }
        return true;
    }

    /// <summary>
    /// Verifies and deducts both credits and island/boat goods atomically, rolling back
    /// any spent credits if goods deduction fails.
    /// </summary>
    public bool TryPayAtomic(Inventory islandGoods, Bank bank, Unit foundingBoat, bool isUnsettledHarbor)
    {
        if (isUnsettledHarbor)
        {
            if (foundingBoat == null || !CanBePaidFromBoat(foundingBoat)) return false;
        }
        else if (!CanBePaidFrom(islandGoods))
        {
            return false;
        }

        int creditPrice = Mathf.Max(0, GetPrice());
        if (creditPrice > 0 && bank != null && !bank.TrySpend(creditPrice, GetBuildingName()))
        {
            return false;
        }

        bool goodsPaid = isUnsettledHarbor ? TryPayFromBoat(foundingBoat) : TryPayFrom(islandGoods);
        if (!goodsPaid)
        {
            if (creditPrice > 0 && bank != null)
            {
                bank.Deposit(creditPrice, $"{GetBuildingName()} rollback");
            }
            return false;
        }

        return true;
    }

    private Dictionary<ItemData, int> BuildPositiveCostMap()
    {
        var positive = new Dictionary<ItemData, int>();
        if (!TryGetCosts(out Dictionary<ItemData, int> costItems)) return positive;

        foreach (var cost in costItems)
        {
            if (cost.Key == null || cost.Value <= 0) continue;
            positive.TryGetValue(cost.Key, out int existing);
            positive[cost.Key] = existing + cost.Value;
        }
        return positive;
    }

    [SerializeField] private string buildingName; 
    [SerializeField] private int expense;          
    [SerializeField] private int price;             
    [SerializeField] private int revenue;              
    
    public delegate void BuildingPlacedHandler(BuildingCost buildingCost);
    public static event BuildingPlacedHandler OnBuildingPlaced;

    void Awake(){
        SetBuildingName();
    }

    void Start(){
        BuildingPlaced();
    }

    public void BuildingPlaced()
    {
        OnBuildingPlaced?.Invoke(this);
        //Debug.Log("Building placed: " + this.name); // Add this line
    }

    // General Building Data
    private BuildingProperties buildingProperties;
    private BuildingData buildingData;

    public ItemEnums.ResourceType resourceType; // The type of resource needed to build the building

    #region Set Methods - SetResourceType, SetBuildingName, SetExpense, SetPrice, SetCost 

        // costData Sends Cost Related Data
        // BuildingData Sends Building Data

        public ItemEnums.ResourceType SetResourceType() 
        {
            return resourceType;
        }
        
        /// <summary>
        /// What this building is called, for the bank's ledger and the UI.
        ///
        /// The sibling BuildingProperties is the prefab's authority on identity; this
        /// component's own buildingData field is never assigned by anything. Falling
        /// straight through to costData.name meant every building was booked under its
        /// cost asset - "Basalt Crusher Cost" rather than "Basalt Crusher".
        /// </summary>
        public string SetBuildingName()
        {
            if (buildingData != null && !string.IsNullOrEmpty(buildingData.buildingName))
            {
                buildingName = buildingData.buildingName;
                return buildingName;
            }

            BuildingProperties properties = GetComponent<BuildingProperties>();
            if (properties != null)
            {
                if (properties.buildingData != null && !string.IsNullOrEmpty(properties.buildingData.buildingName))
                {
                    buildingName = properties.buildingData.buildingName;
                    return buildingName;
                }
                if (!string.IsNullOrEmpty(properties.buildingName))
                {
                    buildingName = properties.buildingName;
                    return buildingName;
                }
            }

            // Instantiated objects carry a "(Clone)" suffix that has no place in a ledger.
            buildingName = gameObject.name.Replace("(Clone)", string.Empty).Trim();
            return buildingName;
        }
        
        public int SetExpense()
        {
            if (costData != null) expense = costData.expense;
            return expense;
        }
    
        public int SetPrice()
        {
            if (costData != null) price = costData.price;
            return price;   
        }

        public int SetRevenue()
        {
            if (costData != null) revenue = costData.revenue;
            return revenue;
        }



    #endregion


    #region Get Methods - GetBuildingNames, GetExpense, GetPrice, GetRevenue


        public string GetBuildingName()
        {
            return SetBuildingName();
        }

        public int GetExpense()
        {
            return SetExpense();
        }

        public int GetPrice()
        {
            return SetPrice();
        }

        public int GetRevenue()
        {
            return SetRevenue();
        }

    #endregion

}
