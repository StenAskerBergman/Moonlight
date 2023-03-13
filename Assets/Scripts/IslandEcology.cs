using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandEcology : MonoBehaviour
{
    // Ints
        public int NegEco, PosEco, EcoValue;  
        public int DefaultEcoValue;
    // Default Value
        public void Start()
        {
            // Sets Start Values
            EcoValue += DefaultEcoValue;
        }

        public void ChangeEco(int amount)
        {
            if(amount > 0){
                PosEco = DefaultEcoValue + amount;
            } 
            
            if (amount < 0){
                NegEco = DefaultEcoValue - amount;
            }
        }

        public void EcoCalc(){
            EcoValue = NegEco + PosEco;
        }

        // User interface
            // Method to get the current power amount
            public int GetCurrentEco()
            {
                return EcoValue;
            }
            public int GetNegativeEco()
            {
                return NegEco;
            }
            public int GetPositiveEco()
            {
                return PosEco;
            }
            
        // More Methods
}
