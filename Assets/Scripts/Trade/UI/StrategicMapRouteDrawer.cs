using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Custom UI Graphic that renders the selected trade route's path on the 2D Strategic Map.
/// Connects station nodes in sequence (Station 1 -> Station 2 -> ... -> Station 1)
/// with direction-indicating lines and station node markers.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class StrategicMapRouteDrawer : MaskableGraphic
{
    [Header("Line Styling")]
    [SerializeField] private float lineWidth = 3.5f;
    [SerializeField] private Color routeColor = new Color(0.25f, 0.75f, 1f, 0.9f);
    [SerializeField] private Color nodeColor = new Color(0.9f, 0.95f, 1f, 1f);
    [SerializeField] private float nodeRadius = 6f;
    [SerializeField] private bool drawLoop = true;

    private readonly List<Vector2> points = new List<Vector2>();

    public float LineWidth
    {
        get => lineWidth;
        set { lineWidth = value; SetVerticesDirty(); }
    }

    public Color RouteColor
    {
        get => routeColor;
        set { routeColor = value; color = value; SetVerticesDirty(); }
    }

    public void SetPoints(List<Vector2> newPoints, bool loop = true)
    {
        points.Clear();
        if (newPoints != null)
        {
            points.AddRange(newPoints);
        }
        drawLoop = loop && points.Count > 1;
        SetVerticesDirty();
    }

    public void Clear()
    {
        points.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (points == null || points.Count < 2)
        {
            // If single point, just draw node
            if (points != null && points.Count == 1)
            {
                DrawCircle(vh, points[0], nodeRadius, nodeColor);
            }
            return;
        }

        int count = points.Count;
        int segments = drawLoop ? count : count - 1;

        // Draw Line Segments
        for (int i = 0; i < segments; i++)
        {
            Vector2 p1 = points[i];
            Vector2 p2 = points[(i + 1) % count];

            DrawLineSegment(vh, p1, p2, lineWidth, routeColor);
            DrawArrowHead(vh, p1, p2, lineWidth * 2.5f, routeColor);
        }

        // Draw Node Circles
        for (int i = 0; i < count; i++)
        {
            DrawCircle(vh, points[i], nodeRadius, nodeColor);
        }
    }

    private void DrawLineSegment(VertexHelper vh, Vector2 start, Vector2 end, float width, Color col)
    {
        Vector2 dir = (end - start).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x) * (width * 0.5f);

        int startIndex = vh.currentVertCount;

        vh.AddVert(start - normal, col, Vector2.zero);
        vh.AddVert(start + normal, col, Vector2.zero);
        vh.AddVert(end + normal, col, Vector2.zero);
        vh.AddVert(end - normal, col, Vector2.zero);

        vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }

    private void DrawArrowHead(VertexHelper vh, Vector2 start, Vector2 end, float size, Color col)
    {
        Vector2 mid = (start + end) * 0.5f;
        Vector2 dir = (end - start).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x);

        Vector2 tip = mid + dir * (size * 0.7f);
        Vector2 left = mid - dir * (size * 0.3f) + normal * (size * 0.5f);
        Vector2 right = mid - dir * (size * 0.3f) - normal * (size * 0.5f);

        int startIndex = vh.currentVertCount;

        vh.AddVert(tip, col, Vector2.zero);
        vh.AddVert(left, col, Vector2.zero);
        vh.AddVert(right, col, Vector2.zero);

        vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
    }

    private void DrawCircle(VertexHelper vh, Vector2 center, float radius, Color col, int segments = 12)
    {
        int startIndex = vh.currentVertCount;
        vh.AddVert(center, col, Vector2.zero);

        for (int i = 0; i <= segments; i++)
        {
            float rad = (i / (float)segments) * Mathf.PI * 2f;
            Vector2 pos = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
            vh.AddVert(pos, col, Vector2.zero);
        }

        for (int i = 1; i <= segments; i++)
        {
            vh.AddTriangle(startIndex, startIndex + i, startIndex + i + 1);
        }
    }
}
