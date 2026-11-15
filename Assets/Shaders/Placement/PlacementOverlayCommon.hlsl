#ifndef MOONLIGHT_PLACEMENT_OVERLAY_COMMON_INCLUDED
#define MOONLIGHT_PLACEMENT_OVERLAY_COMMON_INCLUDED

// Shared Anno-style placement & territory overlay evaluation for both the terrain splat
// shader and the coastal ocean surface overlay sheet.
//
// Texture channel contract (_PlacementMask, RGBA32, Point-sampled, linear):
//   R: Settlement territory (1 inside territory / boat reach, 0 outside)
//   G: Candidate / Emitter influence disc (1 inside disc, 0 outside)
//   B: Cell buildability (1 usable/green, 0 blocked/red)
//   A: Footprint & Road Connector tag
//        < 0.25 : none
//        0.25..0.75 : active building footprint cell
//        >= 0.75    : orthogonal connected road cell highlight
//
// _PlacementOverlayState:
//   x: OverlayEnabled (0/1)
//   y: DimStrength (0..1, dims outside R territory)
//   z: ShowDiscGrid (0/1, draws internal 1x1 cell grid + stepped perimeter inside G)
//   w: FootprintValid (0/1, determines footprint curb green vs red)
//
// _PlacementGridParams:
//   x: gridSize (cells per axis)
//   y: cellSize (local units per grid cell)
//   z: waterSurfaceLocalY
//   w: time/pulse parameter

float4 SamplePlacementMaskCell(
    TEXTURE2D_PARAM(maskTex, maskSampler),
    float2 idx,
    float gridSize,
    float invGrid)
{
    if (idx.x < 0.0 || idx.y < 0.0 || idx.x >= gridSize || idx.y >= gridSize)
        return float4(0.0, 0.0, 0.0, 0.0);

    return SAMPLE_TEXTURE2D_LOD(maskTex, maskSampler, (idx + 0.5) * invGrid, 0);
}

void EvaluatePlacementOverlayCore(
    TEXTURE2D_PARAM(maskTex, maskSampler),
    float2 cellCoord,
    float gridSize,
    float4 overlayState,
    out half3 tintColor,
    out half tintWeight,
    out half3 emissionColor,
    out half waterAlpha)
{
    tintColor = half3(1.0, 1.0, 1.0);
    tintWeight = 0.0h;
    emissionColor = half3(0.0, 0.0, 0.0);
    waterAlpha = 0.0h;

    // Must be computed outside dynamic branches for valid quad derivatives.
    float2 fw = max(fwidth(cellCoord), float2(0.012, 0.012));

    UNITY_BRANCH if (overlayState.x < 0.5)
        return;

    float safeGrid = max(gridSize, 1.0);
    float invGrid = rcp(safeGrid);

    bool inBounds = cellCoord.x >= 0.0 && cellCoord.y >= 0.0
                 && cellCoord.x < safeGrid && cellCoord.y < safeGrid;

    float2 idx = floor(cellCoord);
    float4 center = inBounds
        ? SamplePlacementMaskCell(TEXTURE2D_ARGS(maskTex, maskSampler), idx, safeGrid, invGrid)
        : float4(0.0, 0.0, 0.0, 0.0);

    bool inTerritory = center.r >= 0.5;
    bool inDisc = center.g >= 0.5;
    bool isValid = center.b >= 0.5;
    bool isFootprint = center.a >= 0.25 && center.a < 0.75;
    bool isConnectedRoad = center.a >= 0.75;

    float2 fracCell = frac(cellCoord);
    float2 edgeDist = min(fracCell, 1.0 - fracCell);
    float lineAAScale = max(fw.x, fw.y);

    // 1. Outer territory dimming
    if (!inTerritory && overlayState.y > 0.001)
    {
        float dim = saturate(overlayState.y);
        tintColor = half3(0.24, 0.28, 0.34);
        tintWeight = (half)(dim * 0.85);
        waterAlpha = (half)(dim * 0.52);
    }

    // 2. Influence disc fill (green valid / red blocked)
    if (inDisc)
    {
        half3 discColor = isValid ? half3(0.16, 0.82, 0.28) : half3(0.92, 0.16, 0.14);
        half fillStrength = isValid ? 0.28h : 0.42h;
        half glowStrength = isValid ? 0.10h : 0.24h;

        tintColor = lerp(tintColor, discColor, fillStrength + (1.0h - saturate(tintWeight)) * 0.2h);
        tintWeight = max(tintWeight, fillStrength);
        emissionColor += discColor * glowStrength;
        waterAlpha = max(waterAlpha, isValid ? 0.26h : 0.36h);
    }

    // 3. Footprint pad & connected road pip tint
    if (isFootprint)
    {
        half3 fpColor = overlayState.w >= 0.5
            ? half3(0.14, 0.94, 0.38)
            : half3(0.98, 0.18, 0.16);
        tintColor = fpColor;
        tintWeight = max(tintWeight, 0.42h);
        emissionColor += fpColor * 0.22h;
        waterAlpha = max(waterAlpha, 0.45h);
    }
    else if (isConnectedRoad)
    {
        half3 roadMint = half3(0.26, 0.98, 0.62);
        tintColor = roadMint;
        tintWeight = max(tintWeight, 0.48h);
        emissionColor += roadMint * 0.34h;
        waterAlpha = max(waterAlpha, 0.48h);
    }

    // Read 4 orthogonal neighbor cells only when inside/adjacent to any active mask region
    float4 nW = SamplePlacementMaskCell(TEXTURE2D_ARGS(maskTex, maskSampler), idx + float2(-1.0,  0.0), safeGrid, invGrid);
    float4 nE = SamplePlacementMaskCell(TEXTURE2D_ARGS(maskTex, maskSampler), idx + float2( 1.0,  0.0), safeGrid, invGrid);
    float4 nS = SamplePlacementMaskCell(TEXTURE2D_ARGS(maskTex, maskSampler), idx + float2( 0.0, -1.0), safeGrid, invGrid);
    float4 nN = SamplePlacementMaskCell(TEXTURE2D_ARGS(maskTex, maskSampler), idx + float2( 0.0,  1.0), safeGrid, invGrid);

    // Thin 1x1 cell grid lines inside emitter / harbor disc (ShowDiscGrid > 0.5)
    if (inDisc && overlayState.z > 0.5)
    {
        float gridHalfW = 0.022 + lineAAScale * 0.9;
        float lineX = 1.0 - smoothstep(0.010, gridHalfW, edgeDist.x);
        float lineY = 1.0 - smoothstep(0.010, gridHalfW, edgeDist.y);
        float cellGrid = saturate(max(lineX, lineY));
        half3 gridCol = isValid ? half3(0.72, 0.98, 0.78) : half3(1.0, 0.58, 0.56);
        emissionColor = lerp(emissionColor, gridCol * 0.52h, (half)(cellGrid * 0.55));
        waterAlpha = max(waterAlpha, (half)(cellGrid * 0.45));
    }

    // Connected road cell frame
    if (isConnectedRoad)
    {
        float roadHalfW = 0.065 + lineAAScale * 1.1;
        float rLineX = 1.0 - smoothstep(0.02, roadHalfW, edgeDist.x);
        float rLineY = 1.0 - smoothstep(0.02, roadHalfW, edgeDist.y);
        float roadBorder = saturate(max(rLineX, rLineY));
        emissionColor += half3(0.28, 1.0, 0.68) * (half)(roadBorder * 0.55);
        waterAlpha = max(waterAlpha, (half)(roadBorder * 0.72));
    }

    // Footprint curb perimeter
    if (isFootprint)
    {
        float fpDist = 10.0;
        if (nW.a < 0.25 || nW.a >= 0.75) fpDist = min(fpDist, fracCell.x);
        if (nE.a < 0.25 || nE.a >= 0.75) fpDist = min(fpDist, 1.0 - fracCell.x);
        if (nS.a < 0.25 || nS.a >= 0.75) fpDist = min(fpDist, fracCell.y);
        if (nN.a < 0.25 || nN.a >= 0.75) fpDist = min(fpDist, 1.0 - fracCell.y);

        float curb = 1.0 - smoothstep(0.035, 0.085 + lineAAScale * 1.3, fpDist);
        half3 curbCol = overlayState.w >= 0.5 ? half3(0.32, 1.0, 0.48) : half3(1.0, 0.24, 0.22);
        emissionColor = lerp(emissionColor, curbCol * 0.92h, (half)curb);
        waterAlpha = max(waterAlpha, (half)(curb * 0.88));
    }

    // Stepped territory boundary ribbon (subtle translucent white around R boundary)
    if (inTerritory && overlayState.y > 0.001)
    {
        float terrDist = 10.0;
        if (nW.r < 0.5) terrDist = min(terrDist, fracCell.x);
        if (nE.r < 0.5) terrDist = min(terrDist, 1.0 - fracCell.x);
        if (nS.r < 0.5) terrDist = min(terrDist, fracCell.y);
        if (nN.r < 0.5) terrDist = min(terrDist, 1.0 - fracCell.y);

        float terrRibbon = 1.0 - smoothstep(0.030, 0.075 + lineAAScale * 1.4, terrDist);
        half3 terrCol = half3(0.86, 0.92, 0.98);
        tintColor = lerp(tintColor, terrCol, (half)(terrRibbon * 0.55));
        tintWeight = max(tintWeight, (half)(terrRibbon * 0.45));
        emissionColor += terrCol * (half)(terrRibbon * 0.28);
        waterAlpha = max(waterAlpha, (half)(terrRibbon * 0.55));
    }

    // Stepped influence disc outer perimeter ribbon (crisp bright Anno white outline)
    if (inDisc && overlayState.z > 0.5)
    {
        float discDist = 10.0;
        if (nW.g < 0.5) discDist = min(discDist, fracCell.x);
        if (nE.g < 0.5) discDist = min(discDist, 1.0 - fracCell.x);
        if (nS.g < 0.5) discDist = min(discDist, fracCell.y);
        if (nN.g < 0.5) discDist = min(discDist, 1.0 - fracCell.y);

        float discRibbon = 1.0 - smoothstep(0.042, 0.095 + lineAAScale * 1.5, discDist);
        half3 discPerimCol = half3(0.96, 1.0, 0.96);
        tintColor = lerp(tintColor, discPerimCol, (half)discRibbon);
        tintWeight = max(tintWeight, (half)(discRibbon * 0.85));
        emissionColor = lerp(emissionColor, discPerimCol * 0.85h, (half)discRibbon);
        waterAlpha = max(waterAlpha, (half)(discRibbon * 0.92));
    }
}

#endif
