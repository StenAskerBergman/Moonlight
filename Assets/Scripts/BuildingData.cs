using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Holds the data for a building
[CreateAssetMenu(fileName = "New Building Data", menuName = "Building Data")]
public class BuildingData : ScriptableObject
{   
    public int costResource1 = 20; // The cost of the building material
}
