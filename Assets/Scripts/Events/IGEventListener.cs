using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

 // Side note: 
 // The gathering of the instance reference occurs twice, once inside of the start function
 // and a second time inside  of the privade void function
 
 // Therefore the gathering logic of the script needs to be written twice and changed in a
 // according manner to encompass change, otherwise you will run into a reference bug

public class IGEventListener : MonoBehaviour
{

    private void Start(){

        // Condition: Exists on the same GameObject
        IGEventStart startEvents = FindObjectOfType<IGEventStart>();

        // Subscribe to the OnSpacePressed event
        startEvents.OnSpacePressed += IGEventStart_OnSpacePressed; // += (this part is dependent on func below)

    }

    private void IGEventStart_OnSpacePressed(object sender, IGEventStart.OnSpacePressedEventArgs e) {
       
        // Space Was Pressed!
        Debug.Log("Space! " + e.spaceCount); 

        
        IGEventStart startEvents = FindObjectOfType<IGEventStart>(); // Creates a Instance Assigns it to a Event it Found
        // startEvents.OnSpacePressed -= IGEventStart_OnSpacePressed; // On Event, Unsubs From Events

        // Destroy(this); // Removes Component 
    }
 
    // Code: [SerializeField]
}
