using UnityEngine;

/// <summary>
/// Renders a rectangular selection overlay on the terrain surface while
/// the player is dragging to define a stamp capture region.
/// Uses a <see cref="LineRenderer"/> to draw the rectangle outline.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class StampSelectionRenderer : MonoBehaviour
{
    private LineRenderer _lineRenderer;

    [Header("Appearance")]
    [SerializeField] private Color selectionColor = new Color(0.2f, 0.8f, 1f, 0.8f);
    [SerializeField] private float lineWidth = 0.15f;
    [SerializeField] private float yOffset = 0.5f;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        ConfigureLineRenderer();
        Hide();
    }

    /// <summary>
    /// Updates the four corners of the selection rectangle.
    /// Both positions are in world space; the rectangle is axis-aligned on XZ.
    /// </summary>
    public void UpdateSelection(Vector3 startWorld, Vector3 endWorld)
    {
        float minX = Mathf.Min(startWorld.x, endWorld.x);
        float maxX = Mathf.Max(startWorld.x, endWorld.x);
        float minZ = Mathf.Min(startWorld.z, endWorld.z);
        float maxZ = Mathf.Max(startWorld.z, endWorld.z);
        float y = Mathf.Max(startWorld.y, endWorld.y) + yOffset;

        _lineRenderer.positionCount = 5; // closed rectangle
        _lineRenderer.SetPosition(0, new Vector3(minX, y, minZ));
        _lineRenderer.SetPosition(1, new Vector3(maxX, y, minZ));
        _lineRenderer.SetPosition(2, new Vector3(maxX, y, maxZ));
        _lineRenderer.SetPosition(3, new Vector3(minX, y, maxZ));
        _lineRenderer.SetPosition(4, new Vector3(minX, y, minZ)); // close the loop

        _lineRenderer.enabled = true;
    }

    public void Hide()
    {
        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = false;
            _lineRenderer.positionCount = 0;
        }
    }

    private void ConfigureLineRenderer()
    {
        _lineRenderer.loop = false;
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.startWidth = lineWidth;
        _lineRenderer.endWidth = lineWidth;
        _lineRenderer.startColor = selectionColor;
        _lineRenderer.endColor = selectionColor;
        _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;

        // Use a simple unlit material if none assigned
        if (_lineRenderer.sharedMaterial == null)
        {
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }
        _lineRenderer.material.color = selectionColor;
    }
}
