using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


/*

    Name: 
    InventoryViewer.cs
    
    Role:
    The main role of this file is just to 
    track the unit that we are looking at.

    Redacted:
    Simple, the logic is flawed atm, with
    this file so I redact it until future
    fixes are found to the issue.     

    Edit:
    Why? 

    Edit Edit: 
    Explain why its flawed in the future

*/


public class InventoryViewer : MonoBehaviour
{
    //// Lists
    //public Unit InspectedUnit;
    //public UnityEvent<List<Unit>> selectionChanged;

    //private void Start()
    //{
    //    selectionChanged.AddListener(SelectionChangedHandler);
    //}

    //private void OnDestroy()
    //{
    //    selectionChanged.RemoveListener(SelectionChangedHandler);
    //}
    //public void SelectionChangedHandler(List<Unit> selectedUnits)
    //{
    //    // Retrieve Current Selected Unit 
    //    var Local_Instance = UnitSelections.Instance; 
    //    var unitSelections = Local_Instance.GetComponent<UnitSelections>();
    //    var InspectedUnitInventory = unitSelections.GetSelectedUnitInventory();

    //    // Set the current inspected Unit
    //    InspectedUnit = unitSelections.GetSelectedUnit();

    //    // Log the current inspected Unit Change!
    //    Debug.Log($"Current Selected Unit Changed - Inspecting Unit: {InspectedUnit.gameObject.name}");
    //}

}
