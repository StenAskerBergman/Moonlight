using UnityEngine;

public readonly struct BridgeSpan
{
    public int AxisMask { get; }
    public int Length { get; }
    public int Index { get; }

    public BridgeSpan(int axisMask, int length, int index)
    {
        AxisMask = axisMask;
        Length = length;
        Index = index;
    }
}

public readonly struct BridgeAppearance
{
    public bool IsBridge { get; }
    public BridgeTransportMode TransportMode { get; }
    public BridgeTier Tier { get; }
    public BridgeStructureType Structure { get; }
    public int SpanLength { get; }
    public int SpanIndex { get; }
    public int PierSpacing { get; }
    public float DeckHeight { get; }
    public GameObject Prefab { get; }

    public BridgeAppearance(
        bool isBridge,
        BridgeTransportMode transportMode,
        BridgeTier tier,
        BridgeStructureType structure,
        int spanLength,
        int spanIndex,
        int pierSpacing,
        float deckHeight,
        GameObject prefab)
    {
        IsBridge = isBridge;
        TransportMode = transportMode;
        Tier = tier;
        Structure = structure;
        SpanLength = spanLength;
        SpanIndex = spanIndex;
        PierSpacing = Mathf.Max(1, pierSpacing);
        DeckHeight = Mathf.Max(0f, deckHeight);
        Prefab = prefab;
    }
}

/// <summary>
/// Resolves a whole bank-to-bank crossing, then selects a road- or railway-specific
/// bridge tier from its actual water span.
/// </summary>
public static class BridgeSpanResolver
{
    public static BridgeAppearance Resolve(GridSystem grid, Cell cell, RoadDefinition definition)
    {
        if (grid == null || cell == null || !BridgePlacementRules.IsBridgeTerrain(cell)) return default;
        int maxSpan = definition != null ? definition.MaxBridgeSpan : RoadPlacer.DefaultMaxBridgeSpan;
        if (!TryGetSpan(grid, cell, maxSpan, out BridgeSpan span)) return default;

        BridgeTransportMode mode = definition != null ? definition.TransportMode : BridgeTransportMode.Road;
        if (definition != null && definition.BridgeTiers != null)
        {
            foreach (BridgeVisualTier candidate in definition.BridgeTiers)
            {
                if (candidate == null || !candidate.Supports(span.Length)) continue;
                return new BridgeAppearance(true, mode, candidate.tier, candidate.structureType,
                    span.Length, span.Index, candidate.pierSpacing, candidate.deckHeight,
                    candidate.ResolvePrefab(span.Index, span.Length));
            }
        }

        BridgeTier tier;
        BridgeStructureType structure;
        if (mode == BridgeTransportMode.Railway)
        {
            tier = span.Length <= 3 ? BridgeTier.Tier1 : span.Length <= 7 ? BridgeTier.Tier2 : BridgeTier.Tier3;
            structure = span.Length <= 3 ? BridgeStructureType.SteelGirder : BridgeStructureType.SteelTruss;
        }
        else
        {
            tier = span.Length <= 2 ? BridgeTier.Tier1 : span.Length <= 5 ? BridgeTier.Tier2 : BridgeTier.Tier3;
            structure = span.Length <= 2
                ? BridgeStructureType.TimberTrestle
                : span.Length <= 5 ? BridgeStructureType.MasonryArch : BridgeStructureType.SteelGirder;
        }

        float deckHeight = definition != null ? definition.BridgeDeckHeight : RoadPlacer.DefaultBridgeDeckHeight;
        return new BridgeAppearance(true, mode, tier, structure, span.Length, span.Index, 2, deckHeight, null);
    }

    public static bool TryGetSpan(GridSystem grid, Cell origin, int maxSpan, out BridgeSpan span)
    {
        span = default;
        if (grid == null || !BridgePlacementRules.IsBridgeTerrain(origin)) return false;
        if (TryAxis(grid, origin, 0, 1, maxSpan, RoadTopologyResolver.North | RoadTopologyResolver.South, out span)) return true;
        return TryAxis(grid, origin, 1, 0, maxSpan, RoadTopologyResolver.East | RoadTopologyResolver.West, out span);
    }

    private static bool TryAxis(GridSystem grid, Cell origin, int dx, int dz, int maxSpan, int axis, out BridgeSpan span)
    {
        span = default;
        if (!FindBank(grid, origin, -dx, -dz, maxSpan, out int negative)
            || !FindBank(grid, origin, dx, dz, maxSpan, out int positive)) return false;
        int length = negative + 1 + positive;
        if (length > Mathf.Max(1, maxSpan)) return false;
        span = new BridgeSpan(axis, length, negative);
        return true;
    }

    private static bool FindBank(GridSystem grid, Cell origin, int dx, int dz, int maxSpan, out int waterCells)
    {
        waterCells = 0;
        for (int distance = 1; distance <= Mathf.Max(1, maxSpan); distance++)
        {
            Cell candidate = grid.GetCell(
                origin.cellPosition.x + dx * distance,
                origin.cellPosition.z + dz * distance);
            if (candidate == null) return false;
            if (BridgePlacementRules.IsBridgeTerrain(candidate)) { waterCells++; continue; }
            return candidate.currentTerrainType == Cell.TerrainType.Land
                || candidate.currentTerrainType == Cell.TerrainType.Beach;
        }
        return false;
    }
}
