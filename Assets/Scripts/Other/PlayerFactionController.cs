using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFactionController : MonoBehaviour
{
    [SerializeField] private bool A, B, C = false; // Faction Unlocked Status
    [SerializeField] private GameObject TYC, ECO, TEC; // Faction Game Object References
    [SerializeField] [Range(0, 3)] public int startFaction = 0; // Faction Starting Preference
    
    void Awake( ){
        if (A == true){startFaction++;}
        if (B == true){startFaction++;}
        if (C == true){startFaction++;}
    }
    
    void Start(){

        // Starting Faction
        switch(startFaction){
            case 0: // Tycoon Start
                //Debug.Log("No Faction Selected: #"+ startFaction);
                // Unlock Nothing 

            break;

            // Choosing a Option: 

                    case 1: // Tycoon Start
                        //Debug.Log("Global Trust: #"+ startFaction);
                        A = true; // Unlock Tycoons 
                        FactionUnlock();

                    break;

                    case 2: // Eco Start
                        //Debug.Log("Eden Initative: #"+ startFaction);
                        B = true; // Unlocks Eco
                        FactionUnlock();
                        
                    break;

                    case 3: // Tech Start
                        //Debug.Log("Science Faction: #"+ startFaction);
                        C = true; // Unlock Tech
                        FactionUnlock();

                    break;

            // Error Input 

            default:
                //Debug.LogError("Error: ``No Start Faction Assigned´´ #" + startFaction);
                // Error Code Occurs if (>3||<0), Bigger than 3 lesser than 0 
       
            break;
        }
    }
    
    public void FactionUnlock() { 
    // Note: Just Called Once
        TYC.SetActive(A);
        ECO.SetActive(B);
        TEC.SetActive(C);
    }
}
