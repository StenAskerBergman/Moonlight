using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds connections from reusable horizontal and vertical Image pieces.</summary>
public sealed class ProductionConnectorView : MonoBehaviour
{
    public static ProductionConnectorView Create(RectTransform parent, Color color)
    {
        var root = new GameObject("Production Connection", typeof(RectTransform), typeof(ProductionConnectorView));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        var view = root.GetComponent<ProductionConnectorView>();
        view.color = color;
        return view;
    }

    private Color color = Color.white;

    public void Draw(Vector2 from, Vector2 to, ProductionConnectionType type, float junctionPosition, float thickness)
    {
        junctionPosition = Mathf.Clamp(junctionPosition, 0.1f, 0.9f);

        if (type == ProductionConnectionType.Horizontal && Mathf.Approximately(from.y, to.y))
        {
            AddHorizontal(from.x, to.x, from.y, thickness);
            return;
        }

        float junctionX = Mathf.Lerp(from.x, to.x, junctionPosition);

        switch (type)
        {
            case ProductionConnectionType.VerticalJoin:
                AddVertical(junctionX, from.y, to.y, thickness);
                AddHorizontal(from.x, junctionX, from.y, thickness);
                AddHorizontal(junctionX, to.x, to.y, thickness);
                break;

            case ProductionConnectionType.MergeFromAbove:
            case ProductionConnectionType.MergeFromBelow:
            case ProductionConnectionType.Horizontal:
                AddHorizontal(from.x, junctionX, from.y, thickness);
                AddVertical(junctionX, from.y, to.y, thickness);
                AddHorizontal(junctionX, to.x, to.y, thickness);
                break;
        }
    }

    private void AddHorizontal(float x0, float x1, float y, float thickness)
    {
        AddPiece(new Vector2((x0 + x1) * 0.5f, y), new Vector2(Mathf.Abs(x1 - x0) + thickness, thickness));
    }

    private void AddVertical(float x, float y0, float y1, float thickness)
    {
        AddPiece(new Vector2(x, (y0 + y1) * 0.5f), new Vector2(thickness, Mathf.Abs(y1 - y0) + thickness));
    }

    private void AddPiece(Vector2 position, Vector2 size)
    {
        var piece = new GameObject("Connector Piece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)piece.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = piece.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }
}
