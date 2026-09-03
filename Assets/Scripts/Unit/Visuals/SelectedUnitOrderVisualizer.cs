using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Read-only presentation component that visualizes a selected unit's current and queued orders in world-space.
/// Renders:
/// 1. A floating buoy marker at every ordered destination, including the currently active one
/// 2. A chained path of directional triangle arrows running ship -> buoy 1 -> buoy 2 -> ...
/// 3. Persistent autonomous trading route waypoints (distinct amber tint)
/// Subscribes to UnitSelections and UnitCommandExecutor. Does NOT own or alter command state:
/// the queue keeps executing while the unit is deselected, and reappears when it is selected again.
/// </summary>
public class SelectedUnitOrderVisualizer : MonoBehaviour
{
    private static SelectedUnitOrderVisualizer _instance;
    public static SelectedUnitOrderVisualizer Instance => _instance;

    [Header("Order Colors")]
    // Player order colours are not serialized: they come from PlayerColors so the buoys
    // always match the selection ring and the player's chosen colour. Active is the hue at
    // full strength, queued is the same hue dimmed - order position reads without a second hue.
    [Tooltip("Hue offset applied to the player colour for autonomous trade routes. " +
             "Derived rather than fixed so the route stays distinguishable whatever colour the player is - " +
             "a fixed amber would be invisible against a yellow player.")]
    [Range(0f, 1f)]
    [SerializeField] private float autonomousHueOffset = 0.5f;

    [Header("Route Arrows")]
    [SerializeField] private Color arrowColor = new Color(1f, 1f, 1f, 0.9f);
    [Tooltip("Off by default: the direction arrows read as white travel markers, " +
             "while ownership is carried by the buoys and the selection ring.")]
    [SerializeField] private bool tintArrowsWithPlayerColor = false;
    [Tooltip("World-space distance between consecutive triangle arrows along the route.")]
    [SerializeField] private float arrowSpacing = 4f;
    [SerializeField] private float arrowLength = 2.0f;
    [SerializeField] private float arrowWidth = 1.3f;
    [Tooltip("Arrows are not drawn within this distance of the unit itself.")]
    [SerializeField] private float arrowStartClearance = 2.5f;
    [Tooltip("Arrows stop this far short of each buoy so the marker stays readable.")]
    [SerializeField] private float arrowEndClearance = 1.5f;
    [SerializeField] private int maxArrows = 512;

    [Header("Buoy Markers")]
    [SerializeField] private float buoyPoleHeight = 3.2f;
    [SerializeField] private float buoyBobAmplitude = 0.12f;
    [SerializeField] private float buoyBobSpeed = 1.4f;
    [SerializeField] private bool showSequenceLabels = true;

    [Header("Placement")]
    [Tooltip("Lift applied to markers and arrows so they read above the water surface.")]
    [SerializeField] private float surfaceYOffset = 0.2f;

    private Unit currentObservedUnit;
    private UnitCommandExecutor currentExecutor;

    // Ordered destinations of the current route, excluding the unit's own position.
    private readonly List<Vector3> routePoints = new List<Vector3>();
    private Color routeArrowColor = Color.white;

    // Buoy marker pool
    private readonly List<GameObject> markerPool = new List<GameObject>();
    private readonly List<GameObject> activeMarkers = new List<GameObject>();

    // Arrow chain mesh
    private MeshFilter arrowMeshFilter;
    private MeshRenderer arrowMeshRenderer;
    private Mesh arrowMesh;
    private readonly List<Vector3> arrowVertices = new List<Vector3>();
    private readonly List<Color> arrowVertexColors = new List<Color>();
    private readonly List<int> arrowIndices = new List<int>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        SetupArrowRenderer();
    }

    private void Start()
    {
        if (UnitSelections.Instance != null)
        {
            UnitSelections.Instance.selectionChanged.AddListener(OnSelectionChanged);
        }
    }

    private void OnDestroy()
    {
        if (UnitSelections.Instance != null)
        {
            UnitSelections.Instance.selectionChanged.RemoveListener(OnSelectionChanged);
        }

        DetachFromObservedUnit();

        if (arrowMesh != null)
        {
            if (Application.isPlaying) Destroy(arrowMesh);
            else DestroyImmediate(arrowMesh);
            arrowMesh = null;
        }
    }

    private void LateUpdate()
    {
        // The unit moves every frame, so the leading segment of the chain is rebuilt continuously.
        RebuildArrowMesh();
        AnimateBuoys();
    }

    #region Selection Binding

    private void OnSelectionChanged(List<Unit> selectedUnits)
    {
        Unit targetUnit = null;

        if (selectedUnits != null && selectedUnits.Count > 0)
        {
            // Focus on FocusedUnit when it is part of the live selection, otherwise the first selected unit.
            Unit focused = UnitSelections.Instance != null ? UnitSelections.Instance.FocusedUnit : null;
            targetUnit = (focused != null && selectedUnits.Contains(focused)) ? focused : selectedUnits[0];
        }

        if (targetUnit != currentObservedUnit)
        {
            BindToUnit(targetUnit);
        }
        else
        {
            RebuildVisualization();
        }
    }

    private void BindToUnit(Unit unit)
    {
        DetachFromObservedUnit();

        currentObservedUnit = unit;

        if (currentObservedUnit != null)
        {
            currentExecutor = currentObservedUnit.GetComponent<UnitCommandExecutor>();
            if (currentExecutor != null)
            {
                currentExecutor.OnCommandsChanged += RebuildVisualization;
            }
        }

        RebuildVisualization();
    }

    private void DetachFromObservedUnit()
    {
        if (currentExecutor != null)
        {
            currentExecutor.OnCommandsChanged -= RebuildVisualization;
            currentExecutor = null;
        }

        currentObservedUnit = null;
        ClearVisualization();
    }

    #endregion

    #region Visualization Reconstruction

    /// <summary>
    /// Recomputes the ordered destination chain and respawns one buoy per ordered destination,
    /// including the currently active one. Called on selection changes and whenever the observed
    /// unit's command set changes (order issued, queued, or completed).
    /// </summary>
    public void RebuildVisualization()
    {
        ClearVisualization();

        if (currentObservedUnit == null) return;

        if (currentExecutor != null && currentExecutor.HasActiveOrders)
        {
            RenderPlayerCommands();
        }
        else if (currentExecutor != null && currentExecutor.AutonomousSource != null && currentExecutor.AutonomousSource.IsActive)
        {
            RenderAutonomousRoute(currentExecutor.AutonomousSource);
        }
    }

    private void RenderPlayerCommands()
    {
        Color activeOrderColor = PlayerColors.Active();
        Color queuedOrderColor = PlayerColors.Queued();
        routeArrowColor = tintArrowsWithPlayerColor ? activeOrderColor : arrowColor;

        int orderIndex = 1;

        // Active command: the destination the unit is travelling toward right now.
        var activeCmd = currentExecutor.ActiveCommand;
        if (activeCmd != null && TryResolveTarget(activeCmd, out Vector3 activePos))
        {
            routePoints.Add(activePos);
            SpawnBuoy(activePos, $"[{orderIndex}] {activeCmd.Description}", activeOrderColor, isActive: true);
            orderIndex++;
        }

        // Queued commands, in FIFO order.
        if (currentExecutor.CommandQueue != null)
        {
            foreach (var cmd in currentExecutor.CommandQueue)
            {
                if (cmd == null) continue;
                if (!TryResolveTarget(cmd, out Vector3 pos)) continue;

                routePoints.Add(pos);
                SpawnBuoy(pos, $"[{orderIndex}] {cmd.Description}", queuedOrderColor, isActive: false);
                orderIndex++;
            }
        }
    }

    private void RenderAutonomousRoute(IAutonomousBehaviorSource autoSource)
    {
        Color autonomousRouteColor = GetAutonomousRouteColor();
        routeArrowColor = autonomousRouteColor;

        var waypoints = autoSource.GetAutonomousWaypoints();
        if (waypoints == null || waypoints.Count == 0) return;

        var labels = autoSource.GetAutonomousWaypointLabels();

        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 wp = waypoints[i];
            routePoints.Add(wp);

            string label = (labels != null && i < labels.Count) ? labels[i] : $"Station {i + 1}";
            SpawnBuoy(wp, $"{autoSource.SourceName}\n[{i + 1}] {label}", autonomousRouteColor, isActive: i == 0);
        }

        // Trade routes cycle, so close the loop back to the first station.
        if (waypoints.Count > 1)
        {
            routePoints.Add(waypoints[0]);
        }
    }

    /// <summary>
    /// Trade-route markers are still the player's, so they keep the player's colour family,
    /// rotated by autonomousHueOffset to stay legible as "running on its own" rather than
    /// "ordered by hand". Derived rather than fixed so it never collides with the player hue.
    /// </summary>
    private Color GetAutonomousRouteColor()
    {
        Color.RGBToHSV(PlayerColors.Current, out float h, out float s, out float v);
        Color c = Color.HSVToRGB(Mathf.Repeat(h + autonomousHueOffset, 1f), s, v);
        c.a = 0.9f;
        return c;
    }

    private static bool TryResolveTarget(IUnitCommand command, out Vector3 position)
    {
        if (command.TargetPosition.HasValue)
        {
            position = command.TargetPosition.Value;
            return true;
        }

        if (command.TargetTransform != null)
        {
            position = command.TargetTransform.position;
            return true;
        }

        position = Vector3.zero;
        return false;
    }

    private void ClearVisualization()
    {
        routePoints.Clear();

        if (arrowMesh != null) arrowMesh.Clear();
        if (arrowMeshRenderer != null) arrowMeshRenderer.enabled = false;

        foreach (var marker in activeMarkers)
        {
            if (marker != null)
            {
                marker.SetActive(false);
                markerPool.Add(marker);
            }
        }
        activeMarkers.Clear();
    }

    #endregion

    #region Arrow Chain

    private void SetupArrowRenderer()
    {
        if (arrowMeshFilter != null) return;

        GameObject arrowObj = new GameObject("OrderPathArrows");
        arrowObj.transform.SetParent(transform, false);

        arrowMeshFilter = arrowObj.AddComponent<MeshFilter>();
        arrowMeshRenderer = arrowObj.AddComponent<MeshRenderer>();
        arrowMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        arrowMeshRenderer.receiveShadows = false;

        arrowMesh = new Mesh { name = "OrderPathArrowMesh" };
        arrowMesh.MarkDynamic();
        arrowMeshFilter.sharedMesh = arrowMesh;

        arrowMeshRenderer.sharedMaterial = CreateUnlitMaterial();
        arrowMeshRenderer.enabled = false;
    }

    /// <summary>
    /// Rebuilds the triangle chain one segment at a time (ship -> buoy 1 -> buoy 2 -> ...)
    /// so the arrows always read as a single ordered path pointing in the direction of travel.
    /// </summary>
    private void RebuildArrowMesh()
    {
        if (arrowMesh == null) SetupArrowRenderer();

        if (currentObservedUnit == null || routePoints.Count == 0)
        {
            if (arrowMeshRenderer != null && arrowMeshRenderer.enabled)
            {
                arrowMesh.Clear();
                arrowMeshRenderer.enabled = false;
            }
            return;
        }

        arrowVertices.Clear();
        arrowVertexColors.Clear();
        arrowIndices.Clear();

        Vector3 from = currentObservedUnit.transform.position;

        for (int i = 0; i < routePoints.Count; i++)
        {
            Vector3 to = routePoints[i];

            // The first segment leaves the hull, so it keeps clear of the unit itself.
            float startClearance = (i == 0) ? arrowStartClearance : arrowEndClearance;
            AppendSegmentArrows(from, to, startClearance);

            from = to;
            if (arrowVertices.Count >= maxArrows * 3) break;
        }

        if (arrowIndices.Count == 0)
        {
            arrowMesh.Clear();
            arrowMeshRenderer.enabled = false;
            return;
        }

        arrowMesh.Clear();
        arrowMesh.SetVertices(arrowVertices);
        arrowMesh.SetColors(arrowVertexColors);
        arrowMesh.SetTriangles(arrowIndices, 0);
        arrowMesh.RecalculateBounds();

        arrowMeshRenderer.enabled = true;
    }

    private void AppendSegmentArrows(Vector3 from, Vector3 to, float startClearance)
    {
        Vector3 flatDelta = new Vector3(to.x - from.x, 0f, to.z - from.z);
        float segmentLength = flatDelta.magnitude;
        if (segmentLength <= 0.001f) return;

        Vector3 forward = flatDelta / segmentLength;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        float usableStart = startClearance;
        float usableEnd = segmentLength - arrowEndClearance;
        if (usableEnd <= usableStart) return;

        float halfLength = arrowLength * 0.5f;
        float halfWidth = arrowWidth * 0.5f;
        float spacing = Mathf.Max(0.5f, arrowSpacing);

        for (float d = usableStart; d <= usableEnd; d += spacing)
        {
            if (arrowVertices.Count >= maxArrows * 3) return;

            float t = d / segmentLength;
            Vector3 center = new Vector3(
                from.x + forward.x * d,
                Mathf.Lerp(from.y, to.y, t) + surfaceYOffset,
                from.z + forward.z * d);

            int baseIndex = arrowVertices.Count;
            arrowVertices.Add(center + forward * halfLength);                    // tip, pointing along travel
            arrowVertices.Add(center - forward * halfLength + right * halfWidth);
            arrowVertices.Add(center - forward * halfLength - right * halfWidth);

            arrowVertexColors.Add(routeArrowColor);
            arrowVertexColors.Add(routeArrowColor);
            arrowVertexColors.Add(routeArrowColor);

            arrowIndices.Add(baseIndex);
            arrowIndices.Add(baseIndex + 1);
            arrowIndices.Add(baseIndex + 2);
        }
    }

    #endregion

    #region Buoy Markers

    private void SpawnBuoy(Vector3 position, string label, Color color, bool isActive)
    {
        GameObject buoy = GetOrCreateBuoy();
        buoy.transform.position = position + Vector3.up * surfaceYOffset;
        buoy.SetActive(true);

        TintBuoy(buoy, color, isActive);

        var textMesh = buoy.GetComponentInChildren<TextMesh>(true);
        if (textMesh != null)
        {
            textMesh.gameObject.SetActive(showSequenceLabels);
            textMesh.text = label;
            textMesh.color = isActive ? Color.white : new Color(0.85f, 0.92f, 1f, 0.9f);
        }

        var billboard = buoy.GetComponent<BillboardMarker>();
        if (billboard != null)
        {
            billboard.UpdateFacing();
        }

        activeMarkers.Add(buoy);
    }

    /// <summary>Gives each buoy a small out-of-phase bob so the markers read as floating on the water.</summary>
    private void AnimateBuoys()
    {
        if (buoyBobAmplitude <= 0f) return;

        for (int i = 0; i < activeMarkers.Count; i++)
        {
            var buoy = activeMarkers[i];
            if (buoy == null) continue;

            var body = buoy.transform.Find("Body");
            if (body == null) continue;

            float phase = Time.time * buoyBobSpeed + i * 0.7f;
            body.localPosition = new Vector3(0f, Mathf.Sin(phase) * buoyBobAmplitude, 0f);
            body.localRotation = Quaternion.Euler(Mathf.Sin(phase * 0.8f) * 4f, 0f, Mathf.Cos(phase * 0.6f) * 4f);
        }
    }

    private void TintBuoy(GameObject buoy, Color color, bool isActive)
    {
        var renderers = buoy.GetComponentsInChildren<MeshRenderer>(true);
        var mpb = new MaterialPropertyBlock();

        foreach (var renderer in renderers)
        {
            // The pole stays neutral; the float, beacon and ground ring carry the order color.
            Color target = renderer.gameObject.name == "Pole"
                ? new Color(0.92f, 0.94f, 0.96f, isActive ? 0.95f : 0.7f)
                : color;

            renderer.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", target);
            mpb.SetColor("_BaseColor", target);
            renderer.SetPropertyBlock(mpb);
        }
    }

    private GameObject GetOrCreateBuoy()
    {
        if (markerPool.Count > 0)
        {
            var reused = markerPool[markerPool.Count - 1];
            markerPool.RemoveAt(markerPool.Count - 1);
            return reused;
        }

        return BuildBuoy();
    }

    /// <summary>
    /// Constructs a buoy marker: a flat ring on the water, a floating body, a pole and a beacon on top.
    /// Built procedurally so no prefab wiring is required in the scene.
    /// </summary>
    private GameObject BuildBuoy()
    {
        GameObject buoy = new GameObject("OrderBuoy");
        buoy.transform.SetParent(transform, false);

        // Ground ring marking the exact ordered point.
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ring.name = "Ring";
        ring.transform.SetParent(buoy.transform, false);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ring.transform.localScale = new Vector3(3.5f, 3.5f, 1f);
        ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        StripCollider(ring);
        ApplyUnlitMaterial(ring);

        // Bobbing body: float + pole + beacon, moved as one by AnimateBuoys.
        GameObject body = new GameObject("Body");
        body.transform.SetParent(buoy.transform, false);

        GameObject floatBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floatBase.name = "Float";
        floatBase.transform.SetParent(body.transform, false);
        floatBase.transform.localScale = new Vector3(0.85f, 0.22f, 0.85f);
        floatBase.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        StripCollider(floatBase);
        ApplyUnlitMaterial(floatBase);

        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Pole";
        pole.transform.SetParent(body.transform, false);
        pole.transform.localScale = new Vector3(0.12f, buoyPoleHeight * 0.5f, 0.12f);
        pole.transform.localPosition = new Vector3(0f, buoyPoleHeight * 0.5f + 0.2f, 0f);
        StripCollider(pole);
        ApplyUnlitMaterial(pole);

        GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        beacon.name = "Beacon";
        beacon.transform.SetParent(body.transform, false);
        beacon.transform.localScale = Vector3.one * 0.45f;
        beacon.transform.localPosition = new Vector3(0f, buoyPoleHeight + 0.3f, 0f);
        StripCollider(beacon);
        ApplyUnlitMaterial(beacon);

        // Floating label above the beacon.
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(buoy.transform, false);
        labelObj.transform.localPosition = new Vector3(0f, buoyPoleHeight + 1.2f, 0f);

        var tm = labelObj.AddComponent<TextMesh>();
        tm.fontSize = 24;
        tm.characterSize = 0.2f;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.fontStyle = FontStyle.Bold;

        buoy.AddComponent<BillboardMarker>();

        return buoy;
    }

    private static void StripCollider(GameObject go)
    {
        var collider = go.GetComponent<Collider>();
        if (collider == null) return;

        if (Application.isPlaying) Destroy(collider);
        else DestroyImmediate(collider);
    }

    private void ApplyUnlitMaterial(GameObject go)
    {
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null) return;

        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = CreateUnlitMaterial();
    }

    private static Material CreateUnlitMaterial()
    {
        var shader = Shader.Find("Sprites/Default")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color");

        return shader != null ? new Material(shader) : null;
    }

    #endregion
}

/// <summary>
/// Helper to keep order marker labels facing Camera.main.
/// Only the label is billboarded; the buoy geometry itself stays upright in world space.
/// </summary>
public class BillboardMarker : MonoBehaviour
{
    private Camera cam;

    private void Start()
    {
        cam = Camera.main;
    }

    private void LateUpdate()
    {
        UpdateFacing();
    }

    public void UpdateFacing()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        var label = transform.Find("Label");
        if (label == null) return;

        label.rotation = Quaternion.LookRotation(label.position - cam.transform.position);
    }
}
