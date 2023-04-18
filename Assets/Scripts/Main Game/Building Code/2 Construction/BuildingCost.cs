using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class BuildingCost : MonoBehaviour
{
    [SerializeField] private string buildingName; // name - Add this line to store the building's name
    [SerializeField] private int price;  // price - Direct cost of the building
    [SerializeField] private int cost; // cost - Indirect cost (monthly maintenance cost) of the building
    [SerializeField] private int expense; // expense - Really just another word for cost
    
    public delegate void BuildingPlacedHandler(BuildingCost buildingCost);
    public static event BuildingPlacedHandler OnBuildingPlaced;
    
    void Start(){
        BuildingPlaced();
    }
    public void BuildingPlaced()
    {
        OnBuildingPlaced?.Invoke(this);
        //Debug.Log("Building placed: " + this.name); // Add this line
    }
    
    public string GetBuildingName()
    {
        return buildingName;
    }

    public int GetExpense()
    {
        // You should define the building's expense here, or pass it from another script
        expense = cost;
        return expense;
    }

    public int GetPrice()
    {
        return price;
    }

    public int GetCost()
    {
        return cost;
    }
    
    public Enums.Resource resourceType; // The type of resource needed to build the building
    public Enums.Resource GetResourceType()
    {
        return resourceType;
    }
}

