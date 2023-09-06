using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Author: Sten
// Task: Turn on the correct menu on start
// Issue: Can't know if this is the current primary player faction
// So it opens every menu "avaliable in order" .... Maybe not a issue
// Maybe this is a feature.... yes... this is must likely a intended 
// feature.... surely... mao beifun. Shaolin Magic.... 
// Ancient Chinese Secret.... Inshallah

public class onStartActivate : MonoBehaviour
{
    [SerializeField]
    private Enums.Faction thisFaction; // set this in the inspector to the faction this script is associated with

    [SerializeField]private GameObject Faction;   
    [SerializeField]private bool activationBool = false; 

    private PlayerFactionController factionController;

    void Start()
    {
        // Legacy Method
        // if (activationBool == true) { Faction.SetActive(true); }

        // New Method
        factionController = FindObjectOfType<PlayerFactionController>();

        if (factionController == null)
        {
            Debug.LogError("No PlayerFactionController found in the scene!");
            return;
        }

        activationBool = factionController.IsFactionActive(thisFaction);
        Faction.SetActive(activationBool);
    }

}
