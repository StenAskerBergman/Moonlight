using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuilderScript : MonoBehaviour
{
    // Responsibilities: Building Instantiation

    public GameObject Building_Blueprint;

    public void spawn_building_blueprint()
    { 
        Instantiate(Building_Blueprint);
    }
}
