using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonthlyCost : MonoBehaviour
{ 
    /*public int maintenanceCost; // Monthly maintenance
    private Bank bank;

    private void Start()
    {
        bank = FindObjectOfType<Bank>();

        // Subscribe to the OnBuildingPlaced event of the BuildingCost script
        GetComponent<BuildingCost>().OnBuildingPlaced += OnBuildingPlaced;
    }

    private void OnDestroy()
    {
        // Unsubscribe from the OnBuildingPlaced event when the object is destroyed
        GetComponent<BuildingCost>().OnBuildingPlaced -= OnBuildingPlaced;
    }

    private void OnBuildingPlaced()
    {
        // Update the bank balance and monthly costs when the building is placed
        bank.RemoveMoney(GetComponent<BuildingCost>().GetValue());
        bank.AddExpense(maintenanceCost);
    }*/
}
