using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingEnums : MonoBehaviour
{

    // Define What type of cell the Building
    // Should be standing on, then define it
    // from there. 


    public enum BuildingStyle
    {
        Residential,    // Houses People    -   Unique:     Tax + Redline
        Industrial,     // Produces Items   -   Unique:     Produce
        Spawner,        // Spawns a Unit    -   Unique:     Spawn Units
        Emitter,        // Zone Emitters    -   Unique:     Emit Zone
    }

    public enum BuildingType
    {
        // Debug Type
        Default,        // Just for Debugging

        // Unique
        SiteBased,      // Requires Site

        // Normal
        LandBased,      // On Land
        OnShore,        // On Beach
        OffShore,       // On Shallows

        // Below Sea
        DeepSea,        // Below-sea category; terrain legality comes from building requirements

        // Above Land
        Orbital,        // In Atmosphere
        Space,          // In Space
        LunarBased,     // On Moon

    }

    public enum BuildingState
    {
        UnderConstruction,  // placed, not yet complete
        Active,             // operating normally, producing output
        Inactive,           // manually disabled or no power/resources
        Paused,             // temporarily halted (e.g. missing supply)
        Destroyed           // has been destroyed, pending removal
    }


}
