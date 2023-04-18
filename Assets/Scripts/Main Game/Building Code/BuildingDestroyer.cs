using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingDestroyer : MonoBehaviour
{
    public Bank bank;
    public BuildingCost buildingCost;
    public GameObject building;

    public void Initialize(Bank playerBank, BuildingCost cost, GameObject buildingToDestroy)
    {
        bank = playerBank;
        buildingCost = cost;
        building = buildingToDestroy;
    }

    public void OnDestroyBuilding()
    {
        // Return the direct cost to the player's balance
        bank.AddIncome(buildingCost.GetPrice());
        
        // Remove the indirect cost from the monthly expenses
        bank.RemoveMonthlyExpense(buildingCost.GetCost());
        
        // Destroy the building GameObject
        Destroy(building);
    }
}
