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
    [SerializeField]private GameObject Faction;   
    [SerializeField]private bool activationBool = false; 
    void Start()
    {
        if(activationBool==true){Faction.SetActive(true);}
    }
}
