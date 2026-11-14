// tldr: Owns complete world-positioned landforms and selects one canonical terrain sample for every world point.
using System;
using System.Collections.Generic;
using UnityEngine;

// A chunk is a view of these world-positioned landforms; their sampling domains are not meshes.
public sealed class WorldTerrainField
{
    private readonly List<IslandTerrainProvider> sources = new List<IslandTerrainProvider>();
    private readonly Dictionary<int, IslandTerrainProvider> bySlot = new Dictionary<int, IslandTerrainProvider>();
    public int SamplesPerCell { get; private set; }
    internal IReadOnlyList<IslandTerrainProvider> Sources => sources;
    public float AbyssHeight => sources.Count > 0 ? sources[0].Settings.abyssHeight : 0f;
    private readonly Dictionary<int, (long reservations, long sampling)> timings = new Dictionary<int, (long, long)>();
    public (long reservations, long sampling) Timing(int slot) => timings.TryGetValue(slot, out var value) ? value : default;
    public void Add(int slot, IslandTerrainProvider source, long reservationMilliseconds)
    {
        if (sources.Count > 0 && source.Settings.visualSamplesPerCell != SamplesPerCell)
            throw new InvalidOperationException("World terrain requires one visual sampling density across slots.");
        if (sources.Count > 0 && (source.Settings.abyssHeight != AbyssHeight
            || source.Settings.waterHeight != sources[0].Settings.waterHeight))
            throw new InvalidOperationException("Slots must share the ocean surface and abyss datum.");
        SamplesPerCell = source.Settings.visualSamplesPerCell;
        sources.Add(source);
        bySlot.Add(slot, source);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        source.GetOrCreateSampleCache(SamplesPerCell);
        timings.Add(slot, (reservationMilliseconds, watch.ElapsedMilliseconds));
    }
    public IslandTerrainProvider SourceForSlot(int slot) => bySlot.TryGetValue(slot, out var source) ? source : null;
    internal IslandTerrainProvider Find(Vector2 world, out int index)
    {
        IslandTerrainProvider winner = null;
        float height = float.NegativeInfinity;
        index = -1;
        foreach (var source in sources)
        {
            if (!source.WorldIndex(world, out int candidate)) continue;
            float h = source.BuiltCache.Heights[candidate];
            if (h <= height) continue;
            winner = source; index = candidate; height = h;
        }
        return winner;
    }
    public IslandTerrainProvider.CoastalBands Bands(Vector2 world)
    {
        var source = Find(world, out int index);
        if (source == null) return default;
        var cache = source.BuiltCache;
        Vector2 local = world - source.WorldOrigin;
        return source.ResolveCoastalBands(local.x, local.y, cache.HeadlandMasks[index], cache.ShoreCurvatures[index]);
    }
    public bool TrySampleRiver(Vector2 world, out RiverSurfaceSample river)
    {
        var source = Find(world, out _);
        if (source != null) return source.TrySampleRiver(world - source.WorldOrigin, out river);
        river = default; return false;
    }
}

