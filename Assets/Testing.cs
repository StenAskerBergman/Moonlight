using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CodeMonkey.Utils;
public class Testing : MonoBehaviour
{
    private Grid grid;
    public int LocValue = 56;
    private void Start()
    {
        grid = new Grid(4, 2, 10f, new Vector3(0, 0));
    }

    private void Update() {
        if (Input.GetMouseButtonDown(0)) {
            grid.SetValue(UtilsClass.GetMouseWorldPositionWithZ(), LocValue);
            Debug.Log("Setting Value: " + LocValue);
        }

        if (Input.GetMouseButtonDown(1)){
            Debug.Log(grid.GetValue(UtilsClass.GetMouseWorldPositionWithZ()));
           
        }
    }
}
