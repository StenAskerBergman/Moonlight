using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToggleLight : MonoBehaviour
{

    Light light;

    // Use this for initialization
    void Start()
    {
        light = GetComponent<Light>();
    }
    void OnTriggerStay(Collider other)
    {
        Debug.Log("Inside");
        if (other.CompareTag("Player") && Input.GetKeyUp(KeyCode.L))
        {
            light.enabled = !light.enabled;
        }
    }
    // Update is called once per frame
    /*void Update()
    {
        // Toggle light on/off when L key is pressed.
        if (Input.GetKeyUp(KeyCode.O))
        {
            light.enabled = !light.enabled;
        }
    }*/
}
