using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitClick : MonoBehaviour
{
    // Responsibilities: Unit Clicking
    // If Clicking Units Isn't working then this is where the fault should always lie!
    // All this script does is unit clicking

    // Q: What about clicking interactions? 

    // Errors List
    // Done: #1 ERROR: Selection is out of bound? -> Set a Constant Selection Range 
    // Open: #2 ERROR: Ground Marker isn't Active? -> Transform.Position is correct but the Active states are not
    // Done: #3 ERROR: Sometimes you can't deselect? ->  Seems to be fixed by Asserting This.
    // Done: #4 ERROR: Fixed the Coroutine() Start Bug

    private Camera myCam;
    public GameObject groundMarker;
    //public bool Blinked;
    //public GameObject GroundMarkerGraphics;

    private const float SelectionRange = 50.0f;
    public LayerMask clickable;
    public LayerMask ground;

    void Start()
    {
        myCam = Camera.main;    
    }

    void Update()
    {
        #region One Click Selection

        // Left Click - Primary Button Down
        if (Input.GetMouseButtonDown(0))
        {
            // When you click 
            RaycastHit hit;
            Ray ray = myCam.ScreenPointToRay(Input.mousePosition);
            // Shoot ray from the Camera to the mouse position

            //if(Physics.Raycast(ray, out hit, Mathf.Infinity, clickable))

            if (Physics.Raycast(ray, out hit, SelectionRange, clickable))
            {
                // if we hit a clickable object
                if(Input.GetKey(KeyCode.LeftShift))
                {
                    //Shift Clicked
                    UnitSelections.Instance.ShiftClickSelect(hit.collider.GetComponent<Unit>());
                }
                else
                {
                    //Normal Click
                    UnitSelections.Instance.ShiftClickSelect(hit.collider.GetComponent<Unit>());
                }
            }
            else
            {
                if (!Input.GetKey(KeyCode.LeftShift)) 
                { 
                UnitSelections.Instance.DeselectAll();
                }
            }
        }

        #endregion

        #region Ground Marker Movement + Mouse Position

        // Open: #2 ERROR: Ground Marker's Graphic Child doesn't Active OnClick?

        // Right Click Down - Secondary Button Down
        if (Input.GetMouseButtonDown(1))
        {
            RaycastHit hit;
            Ray ray = myCam.ScreenPointToRay(Input.mousePosition);
            

            //if (Physics.Raycast(ray, out hit, Mathf.Infinity, ground))

            // If Ray hits anything then...
            if (Physics.Raycast(ray, out hit, SelectionRange, ground))
            {
                // Moves the Ground Marker Target
                groundMarker.transform.position = hit.point;
                StartCoroutine(BlinkCoroutine());

                //Debug.Log("Hit.point Location: " + hit.point);



                //groundMarker.GetComponentInChildren<ClickMarker>().unit_waypoint = true;
                //Debug.Log("ClickMarker.unit_waypoint =" + groundMarker.GetComponentInChildren<ClickMarker>().unit_waypoint);

                /*
                 * Old Code Didn't work
                 * 
                 * groundMarker.SetActive(false);
                 * groundMarker.SetActive(true);
                 * 
                 */
            }
        }
        #endregion
    }
    IEnumerator BlinkCoroutine()
    {
        groundMarker.SetActive(true);
        //Debug.Log("Blink!");
        yield return new WaitForSeconds(0.2f);
        groundMarker.SetActive(false);
        //groundMarker.transform.GetChild(0).gameObject.SetActive(false);
    }
}
