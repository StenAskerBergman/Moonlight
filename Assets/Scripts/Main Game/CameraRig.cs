using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraRig : MonoBehaviour
{
    public Transform cameraTransform;
    public GameBorder gameBorder;
    
    // Camera Speed Value
    public float normalSpeed = 0.5f;                // Camera Speed Rate
    public float fastSpeed = 3f;                    // Fast Camera Speed
    public float movementSpeed = 1f;                // Default Camera Speed 
    public float movementTime = 5f;                 // IMPORTANT: The Higher the Value the Snappier the Camera Move
    public float zoomTime = 5f;                     // IMPORTANT: The Higher the Value the Faster the Zoom
    public float rotationAmount;                    // IMPORTANT: Amount of Rotation Per Time Unit
    public float maxZoomDistance = 10f;             // IMPORTANT: Zoom Range
    public float minZoomDistance = 1f;              // IMPORTANT: Zoom Range
    
    // Note: Should be based off the current Map Size
    public Vector2 _range = new Vector2(100,100);   // IMPORTANT: Map Boarder

    public Vector3 zoomAmount; 
    public Vector3 newZoom;
    public Vector3 newPosition;
    public Quaternion newRotation;
    public Vector3 rotateStartPosition;
    public Vector3 rotateCurrentPosition;
    //public BlueprintScript blueprintScript;
    //public Vector3 dragStartPosition;
    //public Vector3 dragCurrentPosition;
    //bool rotationMode;
    void Awake()
    {
        BuildingPreview buildingPreview = FindObjectOfType<BuildingPreview>(); // Find Solution Tmr
        // Debug.Log(blueprintScript.RotationMode);
        // Debug.Log(rotationMode);
    }
    void Start(){
        
        newPosition = transform.position;
        newRotation = transform.rotation;
        newZoom = cameraTransform.localPosition;
        
    }

    // Update is called once per frame
    void Update()
    {
        
        HandleMouseInput();
        HandleMovementInput();
    }
    
    void HandleMouseInput()
    {   
        #region Scrolling 

            // // Ref to BlueprintScript
            // BuildingPreview buildingPreview = FindObjectOfType<BuildingPreview>();
            
            // if(buildingPreview != null) 
            // {
            //     // Do Nothing
            //     //Debug.Log(buildingPreview); // get triggered correctly
            // } 
            // else 
            // {   
            //     // Debug.Log("Building...");
                if (Input.mouseScrollDelta.y != 0)
                {   
                    // Issue: Can't Scroll Up or Down
                    //newZoom += Input.mouseScrollDelta.y * zoomAmount;             // <-- Not Working 
                    //newZoom += Input.GetAxis("Mouse ScrollWheel") * zoomAmount;   // <-- Not Working 

                    float distance = Vector3.Distance(Vector3.zero, newZoom);

                    if (distance > maxZoomDistance)
                    {
                        newZoom = newZoom.normalized * maxZoomDistance;
                    }
                    else if (distance < minZoomDistance)
                    {
                        newZoom = newZoom.normalized * minZoomDistance;
                    }
                }
                
            // }

        #endregion

        #region Click to Rotate 

            if (Input.GetMouseButtonDown(2))
            {
                rotateStartPosition = Input.mousePosition;
            }

            if (Input.GetMouseButton(2))
            {
                rotateCurrentPosition = Input.mousePosition;

                Vector3 difference = rotateStartPosition - rotateCurrentPosition;

                rotateStartPosition = rotateCurrentPosition;

                newRotation *= Quaternion.Euler(Vector3.up * (-difference.x / 5f));
            }
            
            /* if (Input.GetMouseButtonDown(0))
            {
                Plane plane = new Plane(Vector3.up, Vector3.zero);

                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                float entry;

                if(plane.Raycast(ray, out entry))
                {
                    dragStartPosition = ray.GetPoint(entry);
                }
            }

            if (Input.GetMouseButton(0))
            {
                Plane plane = new Plane(Vector3.up, Vector3.zero);

                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                float entry;

                if (plane.Raycast(ray, out entry))
                {
                    dragCurrentPosition = ray.GetPoint(entry);

                    newPosition = transform.position + dragStartPosition - dragCurrentPosition;
                }
            }*/

        #endregion

    }
    private bool IsInBounds(Vector3 position)
    {
        return position.x > -_range.x &&
            position.x < _range.x &&
            position.z > -_range.y &&
            position.z < _range.y;
    }

    private Vector3 GetNearestPointOnBounds(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, -_range.x, _range.x);
        position.z = Mathf.Clamp(position.z, -_range.y, _range.y);
        return position;
    }
    
    void HandleMovementInput() 
    { 
        #region Shift input

            if (Input.GetKey(KeyCode.LeftShift))
            {
                movementSpeed = fastSpeed;
            }
            else
            {
                movementSpeed = normalSpeed;
            }

        #endregion

        #region Move input

        Vector3 direction = Vector3.zero;
        if (Input.GetKey(KeyCode.W)|| Input.GetKey(KeyCode.UpArrow))
        {
            direction += (transform.forward * movementSpeed);
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            direction += (transform.forward * -movementSpeed);
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            direction += (transform.right * movementSpeed);
        }
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            direction += (transform.right * -movementSpeed);
        }

        newPosition += direction;

        /* Old Version
            if (!IsInBounds(newPosition))
            {
                // Set newPosition to the nearest point on the bounds
                newPosition = GetNearestPointOnBounds(newPosition);
            }
        */

        if (gameBorder != null && !gameBorder.IsInBounds(newPosition))
        {
            // Set newPosition to the nearest point on the bounds
            newPosition = gameBorder.GetNearestPointOnBounds(newPosition);
        }


        #endregion

        #region Rotate Input

            if (Input.GetKey(KeyCode.Q))
            {
                newRotation *= Quaternion.Euler(Vector3.up * rotationAmount);
            }
            if (Input.GetKey(KeyCode.E))
            {
                newRotation *= Quaternion.Euler(Vector3.up * -rotationAmount);
            }

        #endregion

        #region Zoom Input
        
            if (Input.GetKey(KeyCode.R))
            {
                newZoom += zoomAmount;
            }
            
            if (Input.GetKey(KeyCode.F))
            {
                newZoom -= zoomAmount;
            }

        #endregion

        #region Larping Section

            transform.position = Vector3.Lerp(transform.position, newPosition, Time.deltaTime * movementTime);
            transform.rotation = Quaternion.Lerp(transform.rotation, newRotation, Time.deltaTime * movementTime);
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, newZoom, Time.deltaTime * zoomTime);

        #endregion
    }
    
}
