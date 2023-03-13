using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
public class IslandPower : MonoBehaviour
{
    // Starting Amount 
        public bool Settled;
        public int PowerSpent;      // Current Input Amount - Spent Power
        public int PowerOutput;     // Current Output Amount - Produced Power
        public int CurrentPower;    // Current Power Amount
        public int TotalPower;      // Current Total Power

    // Starting Power
        public void Start()
        {
            // Sets Start Values
            CurrentPower = 0;
        }

    // Power Methods

        // Buildings 
        
            // Method to Produce Power to the island (Adds Power)
                public void AddPower(int amount)
                {
                    TotalPower = PowerOutput + amount;  // Adds Power to the Total Power Pool when Building is Added or Unpaused
                }

            // Method to Remove Power to the island (Removes Power)
                public void RemovePower(int amount)
                {
                    TotalPower = PowerOutput - amount;  // Removes Power from the Total Power Pool when house is Destroyed or Paused
                }

            // Method for Spend Power on a island (Spends Power)
                public void ConsumePower(int PowerSpent)
                {
                    CurrentPower = TotalPower - PowerSpent; // Equation for Current Island Power Level 
                }

        // User interface
        
            // Method to get the current power amount
            public int GetCurrentPower()
            {
                return CurrentPower;
            }
            // Method to get the spent power amount
            public int GetPowerSpent()
            {
                return PowerSpent;
            }
            // Method to get the total power amount
            public int GetTotalPower()
            {
                return TotalPower;
            }
        // More Methods
}