using UnityEngine;
using Moonlight.Rendering;

public class CameraRig : MonoBehaviour
{
    #region Variables

        // Camera Refs.
        public Transform cameraTransform;
        public GameBorder gameBorder;
    
        // Camera Int Value
        public float normalSpeed = 42f;                 // Camera Speed Rate (m/s)
        public float fastSpeed = 90f;                   // Fast Camera Speed (m/s)
        public float movementSpeed = 42f;               // Active Camera Speed (m/s)
        public float movementTime = 12f;                // IMPORTANT: The Higher the Value the Snappier the Camera Move
        public float zoomTime = 5f;                     // IMPORTANT: The Higher the Value the Faster the Zoom
        public float rotationAmount = 90f;              // IMPORTANT: Rotation Speed (deg/s) for Q/E
        public float maxZoomDistance = 25f;             // IMPORTANT: Zoom in Range
        public float minZoomDistance = 10f;             // IMPORTANT: Zoom Out Range
        public float zoomDistance;

        [Header("Pan & Edge Scroll")]
        [Tooltip("Enable moving the camera when holding Left Alt and dragging, or dragging with the middle/mouse button when rotating isn't active.")]
        public bool enableAltDragPan = true;
        [Tooltip("Optional RTS screen-edge panning (disabled by default so Editor viewport edges aren't triggered).")]
        public bool enableEdgePan = false;
        [Min(2f)] public float edgePanMargin = 14f;

        [Header("Ocean Depth Range")]
        public bool useOceanDepthRange = true;
        [Tooltip("Optional visible ocean surface object. When assigned and active, its renderer upper bound supplies the authoritative sea level.")]
        [SerializeField] private Transform waterSurface;
        [Tooltip("World-space height of the ocean surface.")]
        public float seaLevel = 0f;
        [Tooltip("Lowest world-space height the camera can reach (keeps comfortable clearance above -28m plateaus).")]
        public float abyssFloor = -22f;
        [Tooltip("Highest world-space height the camera can reach.")]
        public float skyLimit = 50f;

        [Header("Underwater Framing")]
        [Tooltip("Depth below the ocean surface where the camera arrives after diving, preserving island-scale planning distance above deep plateaus.")]
        [Min(0.5f)] public float underwaterRestDepthBelowSurface = 3.5f;
        [Tooltip("Wider tactical FOV used when submerged to compensate for water refraction and preserve spatial awareness.")]
        [Range(45f, 85f)] public float underwaterFieldOfView = 67f;
        [Tooltip("Gentle downward pitch compensation added when submerged so the buildable plateau stays centered.")]
        [Range(-10f, 20f)] public float underwaterPitchOffset = 4f;
        [Min(0.5f)] public float underwaterFramingLerpSpeed = 4.5f;

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
        private Camera controlledCamera;
        private float surfaceFieldOfView = 60f;
        private float surfacePitchDegrees = 45f;
        private float surfaceYawDegrees;
        private float surfaceRollDegrees;
        private float underwaterFramingBlend;
        private bool restingAtSurface;
        private bool crossingInputReleased;
        private bool crossingCommitted;
        private float surfaceRestUntil;
        private int restingSide;
        private Vector3 panDragAnchorWorld;
        private bool isDraggingPan;

    //public BlueprintScript blueprintScript;
    //public Vector3 dragStartPosition;
    //public Vector3 dragCurrentPosition;
    //bool rotationMode;

    #endregion

    #region Awake + Start Method
    void Awake()
    {
        BuildingPreview buildingPreview = FindObjectOfType<BuildingPreview>(); // Find Solution Tmr
        ResolveSeaLevelFromOcean();
        if (gameBorder == null)
            gameBorder = FindObjectOfType<GameBorder>();

        // Repair legacy frame-rate-dependent serialized scene defaults cleanly.
        if (normalSpeed < 5f) normalSpeed = 42f;
        if (fastSpeed < 10f) fastSpeed = 90f;
        if (movementTime < 5f) movementTime = 12f;
        if (Mathf.Approximately(rotationAmount, 0f)) rotationAmount = 90f;

        underwaterTransition = GetComponent<UnderwaterTransitionController>();
        if (underwaterTransition == null)
            underwaterTransition = gameObject.AddComponent<UnderwaterTransitionController>();

        controlledCamera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : GetComponentInChildren<Camera>();
        if (controlledCamera != null)
            surfaceFieldOfView = controlledCamera.fieldOfView;
        if (cameraTransform != null)
        {
            Vector3 localEuler = cameraTransform.localEulerAngles;
            surfacePitchDegrees = localEuler.x > 180f ? localEuler.x - 360f : localEuler.x;
            surfaceYawDegrees = localEuler.y;
            surfaceRollDegrees = localEuler.z;
        }
        underwaterTransition.Configure(controlledCamera, seaLevel, waterSurface);
    }

    private void ResolveSeaLevelFromOcean()
    {
        if (waterSurface == null)
        {
            GameObject oceanCube = GameObject.Find("Ocean Cube - Water Cube");
            if (oceanCube != null && oceanCube.activeInHierarchy)
            {
                waterSurface = oceanCube.transform;
            }
        }

        if (waterSurface == null || !waterSurface.gameObject.activeInHierarchy)
            return;

        Renderer oceanRenderer = waterSurface.GetComponent<Renderer>();
        if (oceanRenderer != null && oceanRenderer.enabled)
        {
            seaLevel = oceanRenderer.bounds.max.y;
        }
    }

    void OnEnable()
    {
        if (underwaterTransition != null)
            underwaterTransition.OnSubmersionChanged += HandleSubmersionChanged;
    }

    void OnDisable()
    {
        if (underwaterTransition != null)
            underwaterTransition.OnSubmersionChanged -= HandleSubmersionChanged;
    }

    private void HandleSubmersionChanged(bool isSubmerged)
    {
        // When crossing is committed at 100% concealment, place camera target onto the arriving shelf
        if (isSubmerged)
        {
            float currentTargetY = transform.TransformPoint(newZoom).y;
            if (currentTargetY >= seaLevel)
            {
                float targetY = Mathf.Max(abyssFloor, seaLevel - Mathf.Max(surfaceRestDistance, underwaterRestDepthBelowSurface));
                SetZoomWorldY(targetY);
            }
        }
        else
        {
            float currentTargetY = transform.TransformPoint(newZoom).y;
            if (currentTargetY <= seaLevel)
            {
                SetZoomWorldY(seaLevel + surfaceRestDistance);
            }
        }
    }

    private void SetZoomWorldY(float targetWorldY)
    {
        Vector3 worldPos = transform.TransformPoint(newZoom);
        worldPos.y = targetWorldY;
        newZoom = transform.InverseTransformPoint(worldPos);
        if (cameraTransform != null)
            cameraTransform.position = worldPos;
        zoomDistance = newZoom.magnitude;
    }

    void Start(){
        ResolveSeaLevelFromOcean();
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
        #endregion

        #region Alt-Drag Pan (World Plane)

            if (enableAltDragPan && controlledCamera != null && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
            {
                float dragPlaneY = (underwaterTransition != null && underwaterTransition.IsUnderwater)
                    ? seaLevel - 27f
                    : seaLevel;
                Plane panPlane = new Plane(Vector3.up, new Vector3(0f, dragPlaneY, 0f));
                Ray ray = controlledCamera.ScreenPointToRay(Input.mousePosition);
                if (Input.GetMouseButtonDown(0))
                {
                    if (panPlane.Raycast(ray, out float enter))
                    {
                        panDragAnchorWorld = ray.GetPoint(enter);
                        isDraggingPan = true;
                    }
                }
                else if (isDraggingPan && Input.GetMouseButton(0))
                {
                    if (panPlane.Raycast(ray, out float enter))
                    {
                        Vector3 currentWorld = ray.GetPoint(enter);
                        Vector3 delta = panDragAnchorWorld - currentWorld;
                        delta.y = 0f;
                        newPosition += delta;
                        ClampTargetPosition(ref newPosition);
                    }
                }
            }

            if (Input.GetMouseButtonUp(0) || (!Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt)))
            {
                isDraggingPan = false;
            }

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

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 direction = Vector3.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) direction += forward;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) direction -= forward;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) direction += right;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) direction -= right;

        if (enableEdgePan && Application.isFocused)
        {
            Vector3 mp = Input.mousePosition;
            if (mp.x >= 0f && mp.x <= Screen.width && mp.y >= 0f && mp.y <= Screen.height)
            {
                if (mp.x <= edgePanMargin) direction -= right;
                else if (mp.x >= Screen.width - edgePanMargin) direction += right;
                if (mp.y <= edgePanMargin) direction -= forward;
                else if (mp.y >= Screen.height - edgePanMargin) direction += forward;
            }
        }

        if (direction.sqrMagnitude > 1e-5f)
        {
            direction.Normalize();
            float camY = cameraTransform != null ? cameraTransform.position.y : seaLevel + 10f;
            float groundRef = (underwaterTransition != null && underwaterTransition.IsUnderwater)
                ? seaLevel - 27f
                : seaLevel;
            float heightAboveFloor = Mathf.Max(4f, camY - groundRef);
            float heightScale = Mathf.Clamp(heightAboveFloor / 20f, 0.7f, 2.25f);
            newPosition += direction * (movementSpeed * heightScale * Time.unscaledDeltaTime);
        }

        ClampTargetPosition(ref newPosition);

        #endregion
        
        #region Rotate Input

            if (Input.GetKey(KeyCode.Q))
            {
                newRotation *= Quaternion.Euler(Vector3.up * (rotationAmount * Time.unscaledDeltaTime));
            }
            if (Input.GetKey(KeyCode.E))
            {
                newRotation *= Quaternion.Euler(Vector3.up * (-rotationAmount * Time.unscaledDeltaTime));
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

            float dt = Time.unscaledDeltaTime;
            transform.position = Vector3.Lerp(transform.position, newPosition, Mathf.Clamp01(dt * movementTime));
            transform.rotation = Quaternion.Lerp(transform.rotation, newRotation, Mathf.Clamp01(dt * movementTime));
            float activeZoomSpeed = restingAtSurface ? Mathf.Min(zoomTime, surfaceApproachSpeed) : zoomTime;
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, newZoom, Mathf.Clamp01(dt * activeZoomSpeed));

            // While transition cover is active (before commitment), strictly prevent camera position from dipping across sea level
            if (underwaterTransition != null && underwaterTransition.IsTransitioning && !underwaterTransition.IsCrossingConcealed)
            {
                Vector3 currentCamPos = cameraTransform.position;
                if (!underwaterTransition.IsUnderwater && currentCamPos.y < seaLevel + 0.05f)
                {
                    currentCamPos.y = seaLevel + 0.05f;
                    cameraTransform.position = currentCamPos;
                }
                else if (underwaterTransition.IsUnderwater && currentCamPos.y > seaLevel - 0.05f)
                {
                    currentCamPos.y = seaLevel - 0.05f;
                    cameraTransform.position = currentCamPos;
                }
            }

            UpdateUnderwaterFraming();

        #endregion
    }
    #endregion

    /// <summary>
    /// While a blueprint is out the wheel belongs to it - BuildingRotator turns the
    /// building with the same input. Zooming at the same time made every rotation shove
    /// the camera up or down.
    /// </summary>
    private static bool BlueprintOwnsZoomInput =>
        BuildingChecker.instance != null && BuildingChecker.instance.IsPlacingBuilding;

    private void ApplyZoomInput(float input)
    {
        if (Mathf.Approximately(input, 0f) || cameraTransform == null)
            return;

        if (BlueprintOwnsZoomInput)
            return;

        Vector3 proposedZoom = newZoom + input * zoomAmount;
        if (useOceanDepthRange)
            proposedZoom = ApplyOceanLimitsAndSurfaceGate(proposedZoom, Mathf.Sign(input * zoomAmount.y));
        else
            proposedZoom = ClampLegacyZoom(proposedZoom);

        newZoom = proposedZoom;
        zoomDistance = newZoom.magnitude;
    }

    private void UpdateUnderwaterFraming()
    {
        float targetBlend = underwaterTransition != null && underwaterTransition.IsUnderwater ? 1f : 0f;
        underwaterFramingBlend = Mathf.MoveTowards(
            underwaterFramingBlend,
            targetBlend,
            Time.unscaledDeltaTime * underwaterFramingLerpSpeed);
        float smoothBlend = underwaterFramingBlend * underwaterFramingBlend * (3f - 2f * underwaterFramingBlend);

        if (controlledCamera != null)
        {
            controlledCamera.fieldOfView = Mathf.Lerp(surfaceFieldOfView, underwaterFieldOfView, smoothBlend);
        }

        if (cameraTransform != null)
        {
            float pitch = Mathf.Lerp(surfacePitchDegrees, surfacePitchDegrees + underwaterPitchOffset, smoothBlend);
            cameraTransform.localRotation = Quaternion.Euler(pitch, surfaceYawDegrees, surfaceRollDegrees);
        }
    }

    private Vector3 ApplyOceanLimitsAndSurfaceGate(Vector3 proposedZoom, float verticalDirection)
    {
        float proposedWorldY = transform.TransformPoint(proposedZoom).y;
        float currentTargetWorldY = transform.TransformPoint(newZoom).y;

        bool divingAcross = currentTargetWorldY > seaLevel && proposedWorldY <= seaLevel;
        bool surfacingAcross = currentTargetWorldY < seaLevel && proposedWorldY >= seaLevel;

        if (pauseAtSeaSurface && verticalDirection != 0f)
        {
            if (!crossingCommitted)
            {
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
                        restingAtSurface = false;
                        crossingCommitted = true;

                        if (divingAcross && underwaterTransition != null)
                            underwaterTransition.RequestDive();
                        else if (surfacingAcross && underwaterTransition != null)
                            underwaterTransition.RequestSurface();

                        proposedWorldY = seaLevel + side * surfaceRestDistance;
                    }
                    else
                    {
                        proposedWorldY = seaLevel + side * surfaceRestDistance;
                    }
                }
            }
            else
            {
                // Crossing is committed. While transition cover ramps, hold zoom on departing side
                if (underwaterTransition != null && underwaterTransition.IsTransitioning && !underwaterTransition.IsCrossingConcealed)
                {
                    int departingSide = underwaterTransition.IsUnderwater ? -1 : 1;
                    proposedWorldY = seaLevel + departingSide * surfaceRestDistance;
                }
            }
        }
        else if (divingAcross || surfacingAcross)
        {
            if (divingAcross && underwaterTransition != null && !underwaterTransition.IsCrossingConcealed)
            {
                underwaterTransition.RequestDive();
                proposedWorldY = seaLevel + surfaceRestDistance;
            }
            else if (surfacingAcross && underwaterTransition != null && !underwaterTransition.IsCrossingConcealed)
            {
                underwaterTransition.RequestSurface();
                proposedWorldY = seaLevel - surfaceRestDistance;
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

        if (crossingCommitted && (underwaterTransition == null || !underwaterTransition.IsTransitioning))
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

    public void FocusOnWorldPosition(Vector3 worldPosition)
    {
        newPosition = new Vector3(worldPosition.x, newPosition.y, worldPosition.z);
        ClampTargetPosition(ref newPosition);
    }

    private void ClampTargetPosition(ref Vector3 targetPosition)
    {
        if (gameBorder != null)
        {
            if (!gameBorder.IsInBounds(targetPosition))
                targetPosition = gameBorder.GetNearestPointOnBounds(targetPosition);
        }
        else if (!IsInBounds(targetPosition))
        {
            targetPosition = GetNearestPointOnBounds(targetPosition);
        }
    }
    
    
}
