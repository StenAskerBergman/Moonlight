using UnityEngine;
using UnityEngine.UI;

public class AnimationCurveTopBarGraphic : MaskableGraphic
{
    [SerializeField, Min(2)]
    private int segments = 64;

    [SerializeField]
    private float curveDepth = 80f;

    [SerializeField]
    private AnimationCurve curve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.33f, 1f),
        new Keyframe(0.66f, -1f),
        new Keyframe(1f, 0f)
    );

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;

            float x = Mathf.Lerp(rect.xMin, rect.xMax, t);

            float curveValue = curve.Evaluate(t);
            float curvedBottom = rect.yMin + curveValue * curveDepth;

            vh.AddVert(
                new Vector3(x, rect.yMax),
                color,
                new Vector2(t, 1f)
            );

            vh.AddVert(
                new Vector3(x, curvedBottom),
                color,
                new Vector2(t, 0f)
            );
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