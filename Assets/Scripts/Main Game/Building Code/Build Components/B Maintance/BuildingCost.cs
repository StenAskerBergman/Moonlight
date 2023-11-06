using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class BuildingCost : MonoBehaviour
{
    public CostData costData;  // Reference to the CostData SO

    public List<ItemData> GetAllCostItems()
    {
        return new List<ItemData>(costData.costItems);
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
        
        public string SetBuildingName()
        {
            buildingName = buildingData.buildingName;    

            return buildingName;
        }
        
        public int SetExpense()
        {
            expense = costData.expense;
            return expense;
        }
    
        public int SetPrice()
        {
            price = costData.price;
            return price;   
        }

        public int SetRevenue()
        {
            revenue = costData.revenue;
            return revenue;
        }



    #endregion


    #region Get Methods - GetBuildingNames, GetExpense, GetPrice, GetRevenue


        public string GetBuildingName()
        {
            return costData.name;
        }

        public int GetExpense()
        {
            return costData.expense;
        }

        public int GetPrice()
        {
            return costData.price;
        }

        public int GetRevenue()
        {
            return costData.revenue;
        }

    #endregion

}
