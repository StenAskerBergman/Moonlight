using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders an Anno 2070 style right-facing production arrow silhouette behind the DAG nodes.
/// Dynamically updates its mesh to fit the variable width and height of the production canvas.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public class ProductionArrowBackground : MaskableGraphic
{
    [SerializeField] private float tipWidth = 54f;
    [SerializeField] private Color bodyColor = new Color(0.09f, 0.17f, 0.23f, 0.85f);
    [SerializeField] private Color tipColor = new Color(0.12f, 0.22f, 0.30f, 0.90f);
    [SerializeField] private Color borderColor = new Color(0.20f, 0.34f, 0.44f, 0.60f);
    [SerializeField] private float borderWidth = 1.5f;

    public float TipWidth
    {
        get => tipWidth;
        set { tipWidth = value; SetVerticesDirty(); }
    }

    public Color BodyColor
    {
        get => bodyColor;
        set { bodyColor = value; SetVerticesDirty(); }
    }

    public Color TipColor
    {
        get => tipColor;
        set { tipColor = value; SetVerticesDirty(); }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = GetPixelAdjustedRect();
        if (r.width <= 0f || r.height <= 0f) return;

        float effectiveTipWidth = Mathf.Clamp(tipWidth, 24f, r.width * 0.4f);
        float neckX = r.xMax - effectiveTipWidth;
        float centerY = r.yMin + r.height * 0.5f;

        // Base fill arrow vertices
        // 0: BL (r.xMin, r.yMin)
        // 1: TL (r.xMin, r.yMax)
        // 2: Top Neck (neckX, r.yMax)
        // 3: Tip (r.xMax, centerY)
        // 4: Bot Neck (neckX, r.yMin)
        AddArrowQuadAndTip(vh, r.xMin, neckX, r.xMax, r.yMin, r.yMax, centerY, bodyColor, tipColor);

        // Optional subtle contour border
        if (borderWidth > 0f)
        {
            float bw = borderWidth;
            // Draw border lines along: TL -> NeckTop -> Tip -> NeckBot -> BL -> TL
            AddLine(vh, new Vector2(r.xMin, r.yMax), new Vector2(neckX, r.yMax), bw, borderColor);
            AddLine(vh, new Vector2(neckX, r.yMax), new Vector2(r.xMax, centerY), bw, borderColor);
            AddLine(vh, new Vector2(r.xMax, centerY), new Vector2(neckX, r.yMin), bw, borderColor);
            AddLine(vh, new Vector2(neckX, r.yMin), new Vector2(r.xMin, r.yMin), bw, borderColor);
            AddLine(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), bw, borderColor);
        }
    }

    private static void AddArrowQuadAndTip(
        VertexHelper vh,
        float x0, float xNeck, float xTip,
        float y0, float y1, float yMid,
        Color colorBody, Color colorTip)
    {
        int startIndex = vh.currentVertCount;

        UIVertex v0 = UIVertex.simpleVert;
        v0.position = new Vector3(x0, y0);
        v0.color = colorBody;

        UIVertex v1 = UIVertex.simpleVert;
        v1.position = new Vector3(x0, y1);
        v1.color = colorBody;

        UIVertex v2 = UIVertex.simpleVert;
        v2.position = new Vector3(xNeck, y1);
        v2.color = colorBody;

        UIVertex v3 = UIVertex.simpleVert;
        v3.position = new Vector3(xTip, yMid);
        v3.color = colorTip;

        UIVertex v4 = UIVertex.simpleVert;
        v4.position = new Vector3(xNeck, y0);
        v4.color = colorBody;

        vh.AddVert(v0);
        vh.AddVert(v1);
        vh.AddVert(v2);
        vh.AddVert(v3);
        vh.AddVert(v4);

        // Body quad: (0, 1, 2) and (0, 2, 4)
        vh.AddTriangle(startIndex + 0, startIndex + 1, startIndex + 2);
        vh.AddTriangle(startIndex + 0, startIndex + 2, startIndex + 4);

        // Tip triangle: (4, 2, 3)
        vh.AddTriangle(startIndex + 4, startIndex + 2, startIndex + 3);
    }

    private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color color)
    {
        Vector2 dir = (b - a).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

        int startIndex = vh.currentVertCount;

        UIVertex v0 = UIVertex.simpleVert;
        v0.position = a - normal;
        v0.color = color;

        UIVertex v1 = UIVertex.simpleVert;
        v1.position = a + normal;
        v1.color = color;

        UIVertex v2 = UIVertex.simpleVert;
        v2.position = b + normal;
        v2.color = color;

        UIVertex v3 = UIVertex.simpleVert;
        v3.position = b - normal;
        v3.color = color;

        vh.AddVert(v0);
        vh.AddVert(v1);
        vh.AddVert(v2);
        vh.AddVert(v3);

        vh.AddTriangle(startIndex + 0, startIndex + 1, startIndex + 2);
        vh.AddTriangle(startIndex + 0, startIndex + 2, startIndex + 3);
    }
}
