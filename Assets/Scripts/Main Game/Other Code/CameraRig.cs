using UnityEngine;
using Moonlight.Rendering;

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

        [Header("Ocean Depth Range")]
        public bool useOceanDepthRange = true;
        [Tooltip("World-space height of the ocean surface.")]
        public float seaLevel = 0f;
        [Tooltip("Lowest world-space height the camera can reach.")]
        public float abyssFloor = -60f;
        [Tooltip("Highest world-space height the camera can reach.")]
        public float skyLimit = 50f;

        [Header("Deliberate Surface Crossing")]
        public bool pauseAtSeaSurface = true;
        [Min(0.1f)] public float surfaceRestDistance = 2f;
        [Min(0f)] public float surfaceRestDuration = 0.4f;
        [Tooltip("Lower values produce a softer slowdown into the surface shelf.")]
        [Min(0.1f)] public float surfaceApproachSpeed = 2f;

        // Note: Should be based off the current Map Size
        public Vector2 _range = new Vector2(100,100);   // IMPORTANT: Map Boarder

        public Vector3 zoomAmount; 
        public Vector3 newZoom;
        public Vector3 newPosition;
        public Quaternion newRotation;
        public Vector3 rotateStartPosition;
        public Vector3 rotateCurrentPosition;

        private UnderwaterTransitionController underwaterTransition;
        private bool restingAtSurface;
        private bool crossingInputReleased;
        private bool crossingCommitted;
        private float surfaceRestUntil;
        private int restingSide;

    //public BlueprintScript blueprintScript;
    //public Vector3 dragStartPosition;
    //public Vector3 dragCurrentPosition;
    //bool rotationMode;

    #endregion

    #region Awake + Start Method
    void Awake()
    {
        BuildingPreview buildingPreview = FindObjectOfType<BuildingPreview>(); // Find Solution Tmr
        underwaterTransition = GetComponent<UnderwaterTransitionController>();
        if (underwaterTransition == null)
            underwaterTransition = gameObject.AddComponent<UnderwaterTransitionController>();

        Camera controlledCamera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : GetComponentInChildren<Camera>();
        underwaterTransition.Configure(controlledCamera, seaLevel);
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
        UpdateSurfaceRestState();
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

        float scroll = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(scroll, 0f))
            ApplyZoomInput(scroll);
            
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
                ApplyZoomInput(1f);
            }
            
            if (Input.GetKey(KeyCode.F))
            {
                ApplyZoomInput(-1f);
            }

        #endregion

        #region Larping Section

            transform.position = Vector3.Lerp(transform.position, newPosition, Time.deltaTime * movementTime);
            transform.rotation = Quaternion.Lerp(transform.rotation, newRotation, Time.deltaTime * movementTime);
            float activeZoomSpeed = restingAtSurface ? Mathf.Min(zoomTime, surfaceApproachSpeed) : zoomTime;
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, newZoom, Time.deltaTime * activeZoomSpeed);

        #endregion
    }
    #endregion

    private void ApplyZoomInput(float input)
    {
        if (Mathf.Approximately(input, 0f) || cameraTransform == null)
            return;

        Vector3 proposedZoom = newZoom + input * zoomAmount;
        if (useOceanDepthRange)
            proposedZoom = ApplyOceanLimitsAndSurfaceGate(proposedZoom, Mathf.Sign(input * zoomAmount.y));
        else
            proposedZoom = ClampLegacyZoom(proposedZoom);

        newZoom = proposedZoom;
        zoomDistance = newZoom.magnitude;
    }

    private Vector3 ApplyOceanLimitsAndSurfaceGate(Vector3 proposedZoom, float verticalDirection)
    {
        float proposedWorldY = transform.TransformPoint(proposedZoom).y;
        float currentTargetWorldY = transform.TransformPoint(newZoom).y;

        if (pauseAtSeaSurface && !crossingCommitted && verticalDirection != 0f)
        {
            bool divingAcross = currentTargetWorldY > seaLevel && proposedWorldY <= seaLevel;
            bool surfacingAcross = currentTargetWorldY < seaLevel && proposedWorldY >= seaLevel;
            if (divingAcross || surfacingAcross)
            {
                int side = divingAcross ? 1 : -1;
                if (!restingAtSurface)
                {
                    BeginSurfaceRest(side);
                    proposedWorldY = seaLevel + side * surfaceRestDistance;
                }
                else if (restingSide == side && crossingInputReleased && Time.unscaledTime >= surfaceRestUntil)
                {
                    crossingCommitted = true;
                    restingAtSurface = false;
                }
                else
                {
                    proposedWorldY = seaLevel + side * surfaceRestDistance;
                }
            }
        }

        proposedWorldY = Mathf.Clamp(proposedWorldY, abyssFloor, skyLimit);
        Vector3 worldPosition = transform.TransformPoint(proposedZoom);
        worldPosition.y = proposedWorldY;
        return transform.InverseTransformPoint(worldPosition);
    }

    private Vector3 ClampLegacyZoom(Vector3 proposedZoom)
    {
        float distance = proposedZoom.magnitude;
        if (distance > maxZoomDistance)
            return proposedZoom.normalized * maxZoomDistance;
        if (distance < minZoomDistance)
            return proposedZoom.normalized * minZoomDistance;
        return proposedZoom;
    }

    private void BeginSurfaceRest(int side)
    {
        restingAtSurface = true;
        crossingInputReleased = false;
        restingSide = side;
        surfaceRestUntil = Time.unscaledTime + surfaceRestDuration;
    }

    private void UpdateSurfaceRestState()
    {
        bool hasZoomInput = !Mathf.Approximately(Input.mouseScrollDelta.y, 0f)
            || Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.F);
        if (restingAtSurface && !hasZoomInput)
            crossingInputReleased = true;

        if (crossingCommitted && cameraTransform != null
            && Mathf.Abs(cameraTransform.position.y - seaLevel) > surfaceRestDistance)
            crossingCommitted = false;
    }

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
