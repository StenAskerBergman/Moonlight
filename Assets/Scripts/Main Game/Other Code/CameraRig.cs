using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraRig : MonoBehaviour
{
    #region Variables

        // Camera Refs.
        public Transform cameraTransform;
        public GameBorder gameBorder;
    
        // Camera Int Value
        public float normalSpeed = 0.5f;                // Camera Speed Rate
        public float fastSpeed = 3f;                    // Fast Camera Speed
        public float movementSpeed = 1f;                // Default Camera Speed 
        public float movementTime = 5f;                 // IMPORTANT: The Higher the Value the Snappier the Camera Move
        public float zoomTime = 5f;                     // IMPORTANT: The Higher the Value the Faster the Zoom
        public float rotationAmount;                    // IMPORTANT: Amount of Rotation Per Time Unit
        public float maxZoomDistance = 25f;             // IMPORTANT: Zoom in Range
        public float minZoomDistance = 10f;             // IMPORTANT: Zoom Out Range
        public float zoomDistance;

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

    #endregion

    #region Awake + Start Method
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
    #endregion

    #region Fixed + Update Method

    // Update is called once per frame
    void Update()
    {
        
        HandleMouseInput();
        HandleMovementInput();
    }
    void FixedUpdate()
    {

    }

    #endregion

    #region HandleMouse Method
    void HandleMouseInput()
    {
        #region Scrolling 

        Vector3 CameraStartOffset = cameraTransform.localPosition - newPosition;
   
        var tempMax = (maxZoomDistance + CameraStartOffset.y);
        var tempMin = (minZoomDistance + CameraStartOffset.y);
        var tempPos = cameraTransform.gameObject.transform.position.y;

        if (Input.mouseScrollDelta.y != 0)
        {   
            newZoom += Input.mouseScrollDelta.y * zoomAmount;            
            newZoom += Input.GetAxis("Mouse ScrollWheel") * zoomAmount;   

            float distance = Vector3.Distance(Vector3.zero, newZoom); zoomDistance = distance;

            if (distance > maxZoomDistance && tempPos < tempMax)
            {
                newZoom = newZoom.normalized * maxZoomDistance;
            }
            else if (distance < minZoomDistance && tempPos > tempMin)
            {
                newZoom = newZoom.normalized * minZoomDistance;
            }
        }
            
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
    #endregion

    #region HandleMovement Method
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
    #endregion

    #region IsInBounds Method
    private bool IsInBounds(Vector3 position)
    {
        return position.x > -_range.x &&
            position.x < _range.x &&
            position.z > -_range.y &&
            position.z < _range.y;
    }
    #endregion

    #region GetNearestPintOnBounds
    private Vector3 GetNearestPointOnBounds(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, -_range.x, _range.x);
        position.z = Mathf.Clamp(position.z, -_range.y, _range.y);
        return position;
    }
    #endregion
    
    
}
