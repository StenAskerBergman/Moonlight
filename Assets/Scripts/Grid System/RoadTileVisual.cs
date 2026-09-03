using UnityEngine;

public sealed class RoadTileVisual : MonoBehaviour
{
    private GameObject currentVisual;
    private GameObject currentPrefab;
    private float currentRotation;
    private float currentVerticalOffset;
    private int currentBridgeSignature;
    private int currentConnectionMask = -1;
    private int currentParallelMask = -1;
    private int currentBridgeAxisMask = -1;
    private int currentBridgeApproachMask = -1;
    private RoadVisualStyle currentStyle = (RoadVisualStyle)(-1);
    private float currentWear = -1f;

    public void Apply(RoadTopologyResolver.Result result)
    {
        Apply(result, default);
    }

    public void Apply(RoadTopologyResolver.Result result, BridgeAppearance bridge)
    {
        GameObject resolvedPrefab = bridge.IsBridge && bridge.Prefab != null ? bridge.Prefab : result.Prefab;
        float resolvedOffset = bridge.IsBridge ? bridge.DeckHeight : result.VerticalOffset;
        int bridgeSignature = bridge.IsBridge
            ? ((int)bridge.Structure * 100000 + (int)bridge.Tier * 10000 + bridge.SpanLength * 100 + bridge.SpanIndex)
            : 0;

        if (currentPrefab == resolvedPrefab
            && Mathf.Approximately(currentRotation, result.Rotation)
            && Mathf.Approximately(currentVerticalOffset, resolvedOffset)
            && currentBridgeSignature == bridgeSignature
            && currentConnectionMask == result.ConnectionMask
            && currentParallelMask == result.ParallelMask
            && currentBridgeAxisMask == result.BridgeAxisMask
            && currentBridgeApproachMask == result.BridgeApproachMask
            && currentStyle == result.VisualStyle
            && Mathf.Approximately(currentWear, result.Wear)) return;

        if (currentVisual != null) Destroy(currentVisual);
        currentPrefab = resolvedPrefab;
        currentRotation = result.Rotation;
        currentVerticalOffset = resolvedOffset;
        currentBridgeSignature = bridgeSignature;
        currentConnectionMask = result.ConnectionMask;
        currentParallelMask = result.ParallelMask;
        currentBridgeAxisMask = result.BridgeAxisMask;
        currentBridgeApproachMask = result.BridgeApproachMask;
        currentStyle = result.VisualStyle;
        currentWear = result.Wear;

        currentVisual = new GameObject("Route Visual");
        currentVisual.transform.SetParent(transform, false);
        if (currentPrefab != null)
        {
            GameObject route = Instantiate(currentPrefab, currentVisual.transform);
            route.name = currentPrefab.name;
            route.transform.localPosition = Vector3.up * currentVerticalOffset;
            RoadTileArt proceduralArt = route.GetComponent<RoadTileArt>();
            if (proceduralArt != null)
            {
                route.transform.localRotation = Quaternion.identity;
                route.transform.localScale = Vector3.one;
                proceduralArt.Configure(result, bridge);
            }
            else
            {
                route.transform.localRotation = Quaternion.Euler(0f, currentRotation, 0f);
            }
        }
        if (bridge.IsBridge && bridge.Prefab == null)
        {
            ProceduralBridgeTileBuilder.Build(currentVisual.transform, bridge, currentRotation, result.VisualStyle);
        }
    }
}
