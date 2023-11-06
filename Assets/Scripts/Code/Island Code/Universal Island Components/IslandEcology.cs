using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class IslandEcology : MonoBehaviour
{

    // Todo
    // > Apply Eco changes over time
    // > Create +/- Eco System Events 
    // > Create Eco System Events

    // Events
    public UnityEvent OnPositiveEco;    // On Positive Added
    public UnityEvent OnNegativeEco;    // On Negative Added
    public UnityEvent OnEcoChange;      // On Ecology Change 

    // Sub - Unsub

    // Logic for Triggering Events on Change

    //  > OnPositiveEco: Eco Increased on Island
    //  > OnNegativeEco: Eco Decreased on Island
    //  > OnEcoChange: Eco Change for the Island


    // Ints
    public int NegEco, PosEco, EcoValue;  
    
    // Default Value
    public int DefaultEcoValue;

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

    // Method to get the current Ecology Rate
    public int GetCurrentEco()
    {
        return EcoValue;
    }
    public int GetPositiveEco()
    {
        return PosEco;
    }            
    public int GetNegativeEco()
    {
        return NegEco;
    }
}
