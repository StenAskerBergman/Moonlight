using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HouseBuilderScript : MonoBehaviour
{
    public GameObject house_blueprint;
    public bool BuildMode = false;

    // BluePrint Method
    public void spawn_house_blueprint()
    {
        Instantiate(house_blueprint);
    }

    // Update Method
    void Update() {

        if(Input.GetKeyDown(KeyCode.Tab)){
            
            BuildMode = !BuildMode;
            Debug.Log(BuildMode);
        }

    } 

}