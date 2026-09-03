using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UnitClick : MonoBehaviour
{
    // Responsibilities: Unit Clicking
    // If Clicking Units Isn't working then this is where the fault should always lie!
    // All this script does is unit clicking

    // Q: What about clicking interactions? 

    // Errors List
    // Done: #1 ERROR: Selection is out of bound? -> Set a Constant Selection Range 
    // Done: #2 ERROR: Ground Marker isn't Active? -> Transform.Position is correct but the Active states are not
    // Done: #3 ERROR: Sometimes you can't deselect? ->  Seems to be fixed by Asserting This.
    // Done: #4 ERROR: Fixed the Coroutine() Start Bug
    // Done: #5 ERROR: Selected Units Can't Move 

    // Done: #6 ERROR: Selected Units Don't Show their UI 

    private Camera myCam;
    public GameObject groundMarker;
    public bool Blinked;
    public GameObject GroundMarkerGraphics;

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
        
        bool isUiHit = false; // Class-level variable to track UI clicks

        // Left Click - Primary Button Down
        if (Input.GetMouseButtonDown(0))
        {
            // Check if the click is over a UI element
            if (EventSystem.current.IsPointerOverGameObject())
            {
                // Click is over UI
                isUiHit = true; // Set the flag since UI was clicked

                return;
            }
            else if (isUiHit)
            {
                // Previous click was on UI, check if current click is also on UI
                isUiHit = false; // Reset the flag since the click is no longer on UI
                return; // Ignore this click as it's the first click after UI interaction
            }

            // When you normal click - processing logic here
            RaycastHit hit;
            Ray ray = myCam.ScreenPointToRay(Input.mousePosition);

            // Shoot ray from the Camera to the mouse position
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, clickable))
            {
                // if we hit a clickable object
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    // Shift Clicked
                    UnitSelections.Instance.ShiftClickSelect(hit.collider.GetComponent<Unit>());
                }
                else
                {
                    // Normal Click
                    UnitSelections.Instance.ClickSelect(hit.collider.GetComponent<Unit>());
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
            if (Physics.Raycast(ray, out hit, SelectionRange, ground, QueryTriggerInteraction.Ignore))
            {
                // Moves the Ground Marker Target
                groundMarker.transform.position = hit.point;
                StartCoroutine(BlinkCoroutine());

                //Debug.Log("Hit.point Location: " + hit.point);

                //groundMarker.GetComponentInChildren<ClickMarker>().unit_waypoint = true;
                //Debug.Log("ClickMarker.unit_waypoint =" + groundMarker.GetComponentInChildren<ClickMarker>().unit_waypoint);

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
