using UnityEngine;
using UnityEngine.UI;

public class CurvedTopBarGraphic : MaskableGraphic
{
    [SerializeField, Min(2)]
    private int segments = 32;

    [SerializeField]
    private float curveDepth = 80f;

    [SerializeField]
    private bool invert;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;

        float left = rect.xMin;
        float right = rect.xMax;
        float top = rect.yMax;
        float bottom = rect.yMin;

        float direction = invert ? 1f : -1f;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float x = Mathf.Lerp(left, right, t);

            float curve = Mathf.Sin(t * Mathf.PI);
            float curvedBottom = bottom + (curve * curveDepth * direction);

            vh.AddVert(new Vector3(x, top), color, new Vector2(t, 1f));
            vh.AddVert(new Vector3(x, curvedBottom), color, new Vector2(t, 0f));
        }

        for (int i = 0; i < segments; i++)
        {
            int topLeft = i * 2;
            int bottomLeft = topLeft + 1;
            int topRight = topLeft + 2;
            int bottomRight = topLeft + 3;

            vh.AddTriangle(topLeft, topRight, bottomLeft);
            vh.AddTriangle(topRight, bottomRight, bottomLeft);
        }
    }
}