// tldr: The hand-off to river generation: runs the planner AFTER all terrain exists, then carves the accepted
// plan into the finished height cache and recomputes the slopes the carve invalidated.

using UnityEngine;

public sealed partial class IslandTerrainProvider
{
    public string RiverGenerationReport { get; private set; }

    /// <summary>
    /// Runs last, after coastal geometry AND all mountain contributions. This is deliberately
    /// not a reservation-base-height carve. Nothing may reapply the beach profile after this.
    /// </summary>
    private void FinalizeRiverLandscape(TerrainSampleCache cache)
    {
        if (gridType != GridType.Type.Island || featureReservations == null) return;
        featureReservations.SetRiverNetwork(null);
        if (!settings.riverCorridors.enabled || settings.riverCorridors.maxRivers == 0) return;

        RiverNetwork network = RiverPlanner.BuildNetwork(cache, settings, chunkSeed, out string report);
        RiverGenerationReport = report;
        if (network == null)
        {
            Debug.LogWarning($"[RiverPlan seed {chunkSeed}] {report}");
            return;
        }
        featureReservations.SetRiverNetwork(network);
        if (network.Courses.Count < settings.riverCorridors.minRivers)
            Debug.LogWarning($"[RiverPlan seed {chunkSeed}] {report}");
        else
            Debug.Log($"[RiverPlan seed {chunkSeed}] {report}");
        int r = cache.Resolution; float step = cache.Step;
        System.Threading.Tasks.Parallel.For(0, r, z =>
        {
            for (int x = 0; x < r; x++)
            {
                int index = z * r + x;
                if (!network.TrySample(new Vector2(x * step, z * step), out RiverSurfaceSample sample)) continue;
                float original = cache.Heights[index];
                cache.Heights[index] = sample.Apply(original);
                cache.RiverCarveDepths[index] = Mathf.Max(0f, original - cache.Heights[index]);
                cache.TerrainTypes[index] = sample.Classify(cache.TerrainTypes[index]);
                cache.LakeMasks[index] = sample.IsWater && sample.Kind == RiverReachKind.Lake;
                if (cache.Attribution != null) cache.Attribution.DominantRiverIds[index] = (short)sample.Course;
            }
        });

        // Mountain-only validation has already run on the uncarved field. Its slope limit
        // must not reject a designed waterfall. Final gameplay metrics use these new slopes.
        System.Threading.Tasks.Parallel.For(0, r, z =>
        {
            for (int x = 0; x < r; x++)
            {
                float dx = (cache.GetHeight(x + 1, z - 1) + 2f * cache.GetHeight(x + 1, z) + cache.GetHeight(x + 1, z + 1)
                    - cache.GetHeight(x - 1, z - 1) - 2f * cache.GetHeight(x - 1, z) - cache.GetHeight(x - 1, z + 1)) / (8f * step);
                float dz = (cache.GetHeight(x - 1, z + 1) + 2f * cache.GetHeight(x, z + 1) + cache.GetHeight(x + 1, z + 1)
                    - cache.GetHeight(x - 1, z - 1) - 2f * cache.GetHeight(x, z - 1) - cache.GetHeight(x + 1, z - 1)) / (8f * step);
                cache.Slopes[z * r + x] = Mathf.Sqrt(dx * dx + dz * dz);
            }
        });
    }

    // Public probes must see the rendered terrain, not an uncut pre-cache foundation.
    private TerrainSample SampleFinishedIsland(float localX, float localZ)
    {
        TerrainSampleCache cache = GetOrCreateSampleCache(settings.visualSamplesPerCell);
        float x = Mathf.Clamp((localX + samplingPadding) / cache.Step, 0f, cache.Resolution - 1.001f);
        float z = Mathf.Clamp((localZ + samplingPadding) / cache.Step, 0f, cache.Resolution - 1.001f);
        int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
        float u = x - ix, v = z - iz;
        float a = cache.GetHeight(ix, iz), b = cache.GetHeight(ix + 1, iz);
        float c = cache.GetHeight(ix, iz + 1), d = cache.GetHeight(ix + 1, iz + 1);
        // Same bottom-left -> top-right diagonal as TerrainMeshBuilder.
        float height = v >= u ? a * (1f - v) + c * (v - u) + d * u
            : a * (1f - u) + b * (u - v) + d * v;
        int index = cache.GetIndex(Mathf.RoundToInt(x), Mathf.RoundToInt(z));
        Cell.TerrainType kind = cache.TerrainTypes[index];
        if (TrySampleRiver(new Vector2(localX, localZ), out RiverSurfaceSample river)) kind = river.Classify(kind);
        return cache.PlateauData != null
            ? new TerrainSample(kind, height, cache.BaseFields[index], cache.PlateauData[index])
            : new TerrainSample(kind, height, cache.BaseFields[index]);
    }
}
