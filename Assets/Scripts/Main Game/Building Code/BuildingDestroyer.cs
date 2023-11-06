using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingDestroyer : MonoBehaviour
{
    public Bank bank;
    public BuildingCost buildingCost;
    public GameObject building;
    private GameManager gameManager;
    public void Initialize(Bank playerBank, BuildingCost cost, GameObject buildingToDestroy)
    {
        bank = playerBank;
        buildingCost = cost;
        building = buildingToDestroy;
        gameManager = FindObjectOfType<GameManager>();
    }

    public void OnDestroyBuilding()
    {
        // Remove the building from the player's bank data (revenue and expense)
        // Depending on games current Destruction policy, the building returns
        // may change amount or may not be re added to the player's bank data


        // Get the current return rate from the GameManager
        int _ReturnRate = gameManager.ReturnRate; 


        switch (_ReturnRate)
        {
            case 0: // Return Rate 1 for 1 but No Cash - Meaning 100% of the building's cost returns but no cash is given

                // No Return the direct cost to the player's balance
                // bank.AddIncome(buildingCost.GetPrice()); // Commented out Since No Cash is Given

                // Remove the indirect cost from the monthly expenses
                bank.RemoveMonthlyExpense(buildingCost.GetExpense());

                // Destroy the building GameObject
                Destroy(building);
            break;

            case 1: // Return Rate 1 for 1 - Meaning 100% of the building's cost returns

                // Return the direct cost to the player's balance
                bank.AddIncome(buildingCost.GetPrice());

                // Remove the indirect cost from the monthly expenses
                bank.RemoveMonthlyExpense(buildingCost.GetExpense());

                // Destroy the building GameObject
                Destroy(building);
            break;

            case 2: // Return Rate 2 for 1 - Meaning 50% of the building's cost returns

                // Return half of the direct cost to the player's balance
                bank.AddIncome(buildingCost.GetPrice() / 2);

                // Remove the indirect cost from the monthly expenses
                bank.RemoveMonthlyExpense(buildingCost.GetExpense());

                // Destroy the building GameObject
                Destroy(building);
            break;

            case 3: // Return Rate 3 for 1 - Meaning 25% of the building's cost returns

                // Return a quarter of the direct cost to the player's balance
                bank.AddIncome(buildingCost.GetPrice() / 4);

                // Remove the indirect cost from the monthly expenses
                bank.RemoveMonthlyExpense(buildingCost.GetExpense());

                // Destroy the building GameObject
                Destroy(building);
            break;

            default: // Default Return Rate 0 - Meaning 0% of the building's cost returns

                // Return no direct cost to the player's balance
                bank.AddIncome(0);

                // Remove the indirect cost from the monthly expenses
                bank.RemoveMonthlyExpense(buildingCost.GetExpense());

                // Destroy the building GameObject
                Destroy(building);
            break;
        }

    }
}
