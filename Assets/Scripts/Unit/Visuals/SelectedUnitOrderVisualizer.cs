using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Read-only presentation component that visualizes a selected unit's current and queued orders in world-space.
/// Renders:
/// 1. Active destination marker (solid glowing waypoint)
/// 2. Queued destination markers with sequence badges ([2], [3], ...)
/// 3. Persistent autonomous trading route waypoints (distinct amber overlay)
/// 4. Connecting order path lines from the unit through all waypoints
/// Subscribes to UnitSelections and UnitCommandExecutor. Does NOT own or alter command state.
/// </summary>
public class SelectedUnitOrderVisualizer : MonoBehaviour
{
    private static SelectedUnitOrderVisualizer _instance;
    public static SelectedUnitOrderVisualizer Instance => _instance;

    [Header("Visual Styling")]
    [SerializeField] private Color activePlayerOrderColor = new Color(0.15f, 0.90f, 1.0f, 0.95f);
    [SerializeField] private Color queuedPlayerOrderColor = new Color(0.25f, 0.65f, 0.95f, 0.65f);
    [SerializeField] private Color autonomousRouteColor = new Color(1.0f, 0.80f, 0.20f, 0.90f);
    [SerializeField] private float lineWidth = 0.5f;

    private Unit currentObservedUnit;
    private UnitCommandExecutor currentExecutor;
    private LineRenderer pathLineRenderer;

    // Marker Pool
    private readonly List<GameObject> markerPool = new List<GameObject>();
    private readonly List<GameObject> activeMarkers = new List<GameObject>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        SetupLineRenderer();
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
    }

    private void SetupLineRenderer()
    {
        if (pathLineRenderer != null) return;

        GameObject lineObj = new GameObject("OrderPathLine");
        lineObj.transform.SetParent(transform, false);

        pathLineRenderer = lineObj.AddComponent<LineRenderer>();
        pathLineRenderer.startWidth = lineWidth;
        pathLineRenderer.endWidth = lineWidth;
        pathLineRenderer.useWorldSpace = true;
        pathLineRenderer.positionCount = 0;

        // Use standard particle/unlit shader
        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (shader != null)
        {
            pathLineRenderer.material = new Material(shader);
        }

        pathLineRenderer.enabled = false;
    }

    private void Update()
    {
        if (currentObservedUnit == null) return;

        // Keep the start of the line anchored to the unit as it moves
        if (pathLineRenderer != null && pathLineRenderer.enabled && pathLineRenderer.positionCount > 0)
        {
            Vector3 unitPos = currentObservedUnit.transform.position + Vector3.up * 0.2f;
            pathLineRenderer.SetPosition(0, unitPos);
        }
    }

    #region Selection Binding

    private void OnSelectionChanged(List<Unit> selectedUnits)
    {
        Unit targetUnit = null;

        if (selectedUnits != null && selectedUnits.Count > 0)
        {
            // Focus on focusedUnit, or first selected unit
            targetUnit = UnitSelections.Instance.FocusedUnit != null
                ? UnitSelections.Instance.FocusedUnit
                : selectedUnits[0];
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

    public void RebuildVisualization()
    {
        if (pathLineRenderer == null) SetupLineRenderer();
        ClearVisualization();

        if (currentObservedUnit == null) return;

        var points = new List<Vector3>();
        points.Add(currentObservedUnit.transform.position + Vector3.up * 0.2f);

        // 1. Check Player Command Queue first
        if (currentExecutor != null && currentExecutor.HasActiveOrders)
        {
            RenderPlayerCommands(points);
        }
        // 2. Otherwise, check if unit is following an Autonomous Behavior (e.g. Trading Route)
        else if (currentExecutor != null && currentExecutor.AutonomousSource != null && currentExecutor.AutonomousSource.IsActive)
        {
            RenderAutonomousRoute(currentExecutor.AutonomousSource, points);
        }

        // Draw Line Path
        if (points.Count > 1 && pathLineRenderer != null)
        {
            pathLineRenderer.enabled = true;
            pathLineRenderer.positionCount = points.Count;
            pathLineRenderer.SetPositions(points.ToArray());
        }
        else if (pathLineRenderer != null)
        {
            pathLineRenderer.enabled = false;
        }
    }

    private void RenderPlayerCommands(List<Vector3> points)
    {
        if (pathLineRenderer != null)
        {
            pathLineRenderer.startColor = activePlayerOrderColor;
            pathLineRenderer.endColor = queuedPlayerOrderColor;
        }

        int orderIndex = 1;

        // Active Command
        if (currentExecutor.ActiveCommand != null)
        {
            var activeCmd = currentExecutor.ActiveCommand;
            Vector3 pos = activeCmd.TargetPosition ?? (activeCmd.TargetTransform != null ? activeCmd.TargetTransform.position : Vector3.zero);

            if (pos != Vector3.zero)
            {
                points.Add(pos + Vector3.up * 0.2f);
                SpawnMarker(pos, $"[1] {activeCmd.Description}", activePlayerOrderColor, isActive: true);
                orderIndex++;
            }
        }

        // Queued Commands
        if (currentExecutor.CommandQueue != null)
        {
            foreach (var cmd in currentExecutor.CommandQueue)
            {
                if (cmd == null) continue;

                Vector3 pos = cmd.TargetPosition ?? (cmd.TargetTransform != null ? cmd.TargetTransform.position : Vector3.zero);
                if (pos != Vector3.zero)
                {
                    points.Add(pos + Vector3.up * 0.2f);
                    SpawnMarker(pos, $"[{orderIndex}] {cmd.Description}", queuedPlayerOrderColor, isActive: false);
                    orderIndex++;
                }
            }
        }
    }

    private void RenderAutonomousRoute(IAutonomousBehaviorSource autoSource, List<Vector3> points)
    {
        if (pathLineRenderer != null)
        {
            pathLineRenderer.startColor = autonomousRouteColor;
            pathLineRenderer.endColor = autonomousRouteColor;
        }

        var waypoints = autoSource.GetAutonomousWaypoints();
        var labels = autoSource.GetAutonomousWaypointLabels();

        if (waypoints != null)
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector3 wp = waypoints[i];
                points.Add(wp + Vector3.up * 0.2f);

                string label = (labels != null && i < labels.Count) ? labels[i] : $"Station {i + 1}";
                bool isNextStation = (i == 0);

                SpawnMarker(wp, $"{autoSource.SourceName}\n[{i + 1}] {label}", autonomousRouteColor, isActive: isNextStation);
            }

            // Loop back to start if multiple waypoints
            if (waypoints.Count > 1)
            {
                points.Add(waypoints[0] + Vector3.up * 0.2f);
            }
        }
    }

    private void ClearVisualization()
    {
        if (pathLineRenderer != null)
        {
            pathLineRenderer.enabled = false;
            pathLineRenderer.positionCount = 0;
        }

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

    #region Marker Pooling & Construction

    private void SpawnMarker(Vector3 position, string label, Color color, bool isActive)
    {
        GameObject markerObj = GetOrCreateMarker();
        markerObj.transform.position = position;
        markerObj.SetActive(true);

        // Ground Ring Graphic
        var ring = markerObj.transform.Find("Ring")?.GetComponent<MeshRenderer>();
        if (ring != null)
        {
            var mpb = new MaterialPropertyBlock();
            ring.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", color);
            mpb.SetColor("_BaseColor", color);
            ring.SetPropertyBlock(mpb);
        }

        // Floating Label
        var textMesh = markerObj.GetComponentInChildren<TextMesh>();
        if (textMesh != null)
        {
            textMesh.text = label;
            textMesh.color = isActive ? Color.white : new Color(0.85f, 0.92f, 1f, 0.9f);
        }

        // Orient label towards camera
        var billboard = markerObj.GetComponent<BillboardMarker>();
        if (billboard != null)
        {
            billboard.UpdateFacing();
        }

        activeMarkers.Add(markerObj);
    }

    private GameObject GetOrCreateMarker()
    {
        if (markerPool.Count > 0)
        {
            var reused = markerPool[markerPool.Count - 1];
            markerPool.RemoveAt(markerPool.Count - 1);
            return reused;
        }

        // Construct new marker GameObject
        GameObject marker = new GameObject("OrderMarker");
        marker.transform.SetParent(transform, false);

        // Horizontal Ground Ring (Quad)
        GameObject ringObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ringObj.name = "Ring";
        ringObj.transform.SetParent(marker.transform, false);
        ringObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ringObj.transform.localScale = new Vector3(3.5f, 3.5f, 1f);
        ringObj.transform.localPosition = new Vector3(0f, 0.15f, 0f);

        // Remove collider
        var collider = ringObj.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }

        var ringRenderer = ringObj.GetComponent<MeshRenderer>();
        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (shader != null)
        {
            ringRenderer.sharedMaterial = new Material(shader);
        }

        // 3D Text Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(marker.transform, false);
        labelObj.transform.localPosition = new Vector3(0f, 2.5f, 0f);

        var tm = labelObj.AddComponent<TextMesh>();
        tm.fontSize = 24;
        tm.characterSize = 0.2f;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.fontStyle = FontStyle.Bold;

        marker.AddComponent<BillboardMarker>();

        return marker;
    }

    #endregion
}

/// <summary>
/// Helper to keep order marker labels facing Camera.main.
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
        if (cam != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
