using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class BuildingCost : MonoBehaviour
{
    [SerializeField] private string buildingName;   // name - Add this line to store the building's name
    [SerializeField] private int price;             // price - Direct cost of the building
    [SerializeField] private int cost;              // cost - Indirect cost (monthly maintenance cost) of the building
    [SerializeField] private int expense;           // expense - Really just another word for cost

    [SerializeField] private CostData costData;
    
    public delegate void BuildingPlacedHandler(BuildingCost buildingCost);
    public static event BuildingPlacedHandler OnBuildingPlaced;

    void Awake()
    {
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

        public int SetCost()
        {
            cost = costData.cost;
            return cost;
        }



    #endregion


    #region Get Methods - GetReources, GetBuildingNames, GetExpense, GetPrice, GetCost

        public ItemEnums.GoodType GetGoodType()
        {
            return costData.goodType;
        }
        
        public ItemEnums.MaterialType GetMaterialType()
        {
            return costData.materialType;
        }

        public ItemEnums.ResourceType GetResourceType()
        {
            return costData.resourceType;
        }

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

        public int GetCost()
        {
            return costData.cost;
        }

    #endregion
}

