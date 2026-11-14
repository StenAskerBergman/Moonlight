// tldr: Adapts the shared world landforms into chunk caches, probes, normals and translated gameplay features.
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class IslandTerrainProvider
{
    private WorldTerrainField worldField;
    private IslandTerrainProvider ownedLandform;
    internal TerrainSampleCache BuiltCache => sampleCache;
    internal Vector2 WorldOrigin => chunkWorldOrigin;
    internal int WorldSeed => worldSeed;
    internal int SampleCachePadding => worldField != null ? 0 : samplingPadding;
    public bool IsWorldView => worldField != null;
    // Surface decisions must use the same canonical landform as the copied height samples.
    internal TerrainGenerationSettings SurfaceSettingsAt(Vector2 localPoint)
    {
        if (worldField == null) return settings;
        var source = worldField.Find(chunkWorldOrigin + localPoint, out _);
        return source != null ? source.settings : settings;
    }

    public IEnumerable<Vector2> MinePositions()
    {
        if (worldField == null)
        {
            if (featureReservations != null)
                foreach (var anchor in featureReservations.MineAnchors) yield return anchor.Position;
            yield break;
        }
        foreach (var source in worldField.Sources)
        {
            if (source.featureReservations == null) continue;
            foreach (var anchor in source.featureReservations.MineAnchors)
                yield return source.chunkWorldOrigin + anchor.Position - chunkWorldOrigin;
        }
    }
    public void ProjectRivers(Cell[,] grid, System.Random random)
    {
        if (worldField == null) { new RiverArea().GenerateRiver(grid, featureReservations, random); return; }
        foreach (var source in worldField.Sources)
        {
            Vector2 offset = source.chunkWorldOrigin - chunkWorldOrigin - Vector2.one * source.samplingPadding;
            new RiverArea().GenerateRiver(grid, source.featureReservations, random, offset);
        }
    }
    public Vector3 WorldNormal(float localX, float localZ)
    {
        if (worldField == null) return Vector3.up;
        var source = worldField.Find(chunkWorldOrigin + new Vector2(localX, localZ), out int index);
        if (source == null) return Vector3.up;
        var cache = source.BuiltCache;
        int x = index % cache.Resolution, z = index / cache.Resolution;
        float dx = (cache.GetHeight(x + 1, z) - cache.GetHeight(x - 1, z)) / (2f * cache.Step);
        float dz = (cache.GetHeight(x, z + 1) - cache.GetHeight(x, z - 1)) / (2f * cache.Step);
        return new Vector3(-dx, 1f, -dz).normalized;
    }
    public Vector2 InlandWaterOffset => ownedLandform == null ? Vector2.zero
        : ownedLandform.chunkWorldOrigin - chunkWorldOrigin - Vector2.one * ownedLandform.samplingPadding;
    public void UseWorldField(WorldTerrainField field, IslandTerrainProvider owned)
    {
        worldField = field; ownedLandform = owned; sampleCache = null;
    }
    internal bool WorldIndex(Vector2 world, out int index)
    {
        var cache = sampleCache;
        Vector2 local = world - chunkWorldOrigin + Vector2.one * samplingPadding;
        if (local.x < 0f || local.y < 0f || local.x > cache.GridSize || local.y > cache.GridSize)
        { index = -1; return false; }
        int x = Mathf.Clamp(Mathf.RoundToInt(local.x * cache.VisualSamplesPerCell), 0, cache.Resolution - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(local.y * cache.VisualSamplesPerCell), 0, cache.Resolution - 1);
        index = cache.GetIndex(x, z); return true;
    }
    public bool TrySampleRiver(Vector2 local, out RiverSurfaceSample river)
    {
        if (worldField != null) return worldField.TrySampleRiver(chunkWorldOrigin + local, out river);
        if (featureReservations != null)
            return featureReservations.TrySampleRiver(local + Vector2.one * samplingPadding, out river);
        river = default; return false;
    }
    private TerrainSampleCache GetWorldCache(int density, bool attribution)
    {
        if (density != worldField.SamplesPerCell)
            throw new InvalidOperationException("Chunk mesh density must match the shared world field.");
        if (sampleCache != null && (!attribution || sampleCache.HasAttribution)) return sampleCache;
        var target = new TerrainSampleCache(size, density, attribution, includePlateauData: true);
        System.Threading.Tasks.Parallel.For(0, target.Resolution, z =>
        {
            for (int x = 0; x < target.Resolution; x++)
            {
                int i = target.GetIndex(x, z);
                Vector2 world = chunkWorldOrigin + new Vector2(x * target.Step, z * target.Step);
                var source = worldField.Find(world, out int j);
                if (source == null)
                {
                    target.Heights[i] = worldField.AbyssHeight;
                    target.ShoreDistances[i] = -10000f;
                    target.TerrainTypes[i] = Cell.TerrainType.Abyssal;
                    continue;
                }
                var from = source.BuiltCache;
                target.Heights[i] = from.Heights[j];
                target.BaseFields[i] = from.BaseFields[j];
                target.SmoothFields[i] = from.SmoothFields[j];
                target.ShoreDistances[i] = from.ShoreDistances[j];
                target.HeadlandMasks[i] = from.HeadlandMasks[j];
                target.ShoreCurvatures[i] = from.ShoreCurvatures[j];
                target.TerrainTypes[i] = from.TerrainTypes[j];
                target.Slopes[i] = from.Slopes[j];
                target.MountainAllowances[i] = from.MountainAllowances[j];
                target.MountainBoosts[i] = from.MountainBoosts[j];
                target.RiverCarveDepths[i] = from.RiverCarveDepths[j];
                target.LakeMasks[i] = from.LakeMasks[j];
                target.PlateauInfluences[i] = from.PlateauInfluences[j];
                target.PlateauData[i] = from.PlateauData != null ? from.PlateauData[j] : default;
                if (target.Attribution != null)
                {
                    target.Attribution.RawBaseHeights[i] = from.Attribution != null ? from.Attribution.RawBaseHeights[j] : from.Heights[j];
                    if (from.Attribution != null)
                    {
                        target.Attribution.ReliefDeltas[i] = from.Attribution.ReliefDeltas[j];
                        target.Attribution.PlateauDeltas[i] = from.Attribution.PlateauDeltas[j];
                        target.Attribution.DominantRidgeIds[i] = from.Attribution.DominantRidgeIds[j];
                        target.Attribution.DominantRiverIds[i] = from.Attribution.DominantRiverIds[j];
                    }
                }
            }
        });
        sampleCache = target;
        return target;
    }
}
