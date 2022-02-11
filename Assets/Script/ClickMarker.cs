using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClickMarker : MonoBehaviour
{
    /*
    public GameObject GroundMarkerGraphics;
    public bool unit_waypoint = false;
    public bool reached = false;
    void Start()
    {
        GroundMarkerGraphics = GameObject.Find("GroundClickMarker");
        //Start the coroutine we define below named ExampleCoroutine.
        //GroundMarkerGraphics.SetActive(false);

    }

    
    private void OnEnable()
    {
        
    }

    void OnCollisionEnter(Collision otherObj)
    {
        if (otherObj.gameObject.tag == "Unit")
        {
            unit_waypoint = false;
            reached = true;
            Debug.Log("Unit Reached");
            //Destroy(gameObject, 0.5f);
        }

    }

    private void FixedUpdate()
    {
        // unit_waypoint == true && reached == true are the condition for below
        if (unit_waypoint == false)
        {
            StartCoroutine(BlinkCoroutine());
        }
        
        if (unit_waypoint == false && reached == true)
        {
            StartCoroutine(PointReachedCoroutine());
        }

    }

    IEnumerator BlinkCoroutine()
    {
        yield return new WaitForSeconds(0.2f);
        GroundMarkerGraphics.SetActive(false);
    }
        IEnumerator BlinkCoroutine()
    {
        groundMarker.SetActive(true);

        yield return new WaitForSeconds(0.2f);
        groundMarker.transform.GetChild(0).gameObject.SetActive(false);
    }

    IEnumerator PointReachedCoroutine()
    {
        GroundMarkerGraphics.SetActive(false);
        unit_waypoint = false;
        yield return new WaitForSeconds(0.2f);
    }*/


}
