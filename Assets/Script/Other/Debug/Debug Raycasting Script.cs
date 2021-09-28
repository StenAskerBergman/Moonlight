using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugScript : MonoBehaviour
{
    Camera cam;
    Vector3 pos = new Vector3(200, 200, 0);


    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
    //  Ray ray = cam.ScreenPointToRay(pos);
    //  Debug.DrawRay(ray.origin, ray.direction * 10, Color.yellow);
    }
}