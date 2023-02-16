using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingCost : MonoBehaviour
{   
    public int costBM = 20; // Public variable for the cost of the building material

    public int GetValue(){
        return costBM; // Public method to get the value of the building material cost
    }

}
