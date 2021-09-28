using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HotkeyInput : MonoBehaviour
{
    public GameObject house_blueprint;

    public void spawn_house_blueprint()
    {
        Instantiate(house_blueprint);
    }

//   public Update() { } 
/*
    public void ChangeStatBy1()
    {
        //assign this function to your Button
        house_blueprint.BlueprintScript.i += 1;
    }
    public void ChangeStatBy2()
    {
        //assign this function to your Button
        house_blueprint.BlueprintScript.i += 2;
    }
    public void ChangeStatBy3()
    {
        //assign this function to your Button
        house_blueprint.BlueprintScript.i += 3;
    }
    public void ChangeStatBy4()
    {
        //assign this function to your Button
        house_blueprint.BlueprintScript.i += 4;
    }*/
}
