using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingEnums : MonoBehaviour
{

    // Define What type of cell the Building
    // Should be standing on, then define it
    // from there. 

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
        DeepSea,        // On Plateau

        // Above Land
        Orbital,        // In Atmosphere
        Space,          // In Space
        LunarBased,     // On Moon

    }


}
