// tldr: Island-wide drainage map. A priority-flood from the ocean gives every land cell one downstream
// neighbour, the coast cell it pours out at, its flow length to the sea and how much ground drains through it.

using System.Collections.Generic;
using UnityEngine;

/// <summary>Bilinear access to the finished pre-river terrain cache in planner (cache-local) units.</summary>
internal sealed class RiverGround
{
    private readonly TerrainSampleCache cache;
    public int Size => cache.GridSize;
    public TerrainSampleCache Cache => cache;
    public RiverGround(TerrainSampleCache cache) { this.cache = cache; }
    public float Height(Vector2 p) => Bilinear(cache.Heights, p);
    public float Shore(Vector2 p) => Bilinear(cache.ShoreDistances, p);
    public float MountainBoost(Vector2 p) => Bilinear(cache.MountainBoosts, p);
    public float Headland(Vector2 p) => Bilinear(cache.HeadlandMasks, p);

    private float Bilinear(float[] field, Vector2 p)
    {
        float x = Mathf.Clamp(p.x / cache.Step, 0f, cache.Resolution - 1.001f);
        float z = Mathf.Clamp(p.y / cache.Step, 0f, cache.Resolution - 1.001f);
        int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z), r = cache.Resolution;
        return Mathf.Lerp(Mathf.Lerp(field[iz * r + ix], field[iz * r + ix + 1], x - ix),
            Mathf.Lerp(field[(iz + 1) * r + ix], field[(iz + 1) * r + ix + 1], x - ix), z - iz);
    }
}

/// <summary>
/// One drainage graph for the whole island, solved once on the pre-river terrain.
///
/// Priority-flood (lowest first, outward from every coastal ocean cell) visits each land cell
/// exactly once and assigns it the already-drained neighbour it was reached from. Following
/// receivers therefore always reaches the sea, never climbs above the depression-filled surface
/// and never loops. Mountains need no special case: they are simply high ground the flood
/// reaches last, so their flanks drain down and around them. Closed hollows are filled to their
/// spill level; the fill depth is what marks natural lake basins.
///
/// Because every course follows the same receiver tree, two sources whose water meets really do
/// share a path from that point on - the network's confluences and shared outlets come from the
/// terrain instead of being negotiated between independently routed rivers.
/// </summary>
internal sealed class RiverDrainageField
{
    /// <summary>Receiver of an ocean cell or an unreached cell.</summary>
    public const int None = -1;

    private const float FloodEpsilon = 0.0005f;
    /// <summary>Order perturbation on near-flat ground, so plains drain in curves, not BFS rays.</summary>
    private const float FlatMeander = 0.045f;
    private static readonly int[] DX = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] DZ = { 0, 1, 1, 1, 0, -1, -1, -1 };

    public int Size { get; }
    public bool[] Land { get; }
    public float[] Height { get; }
    /// <summary>Depression-filled drainage surface. Filled - Height is the natural pond depth.</summary>
    public float[] Filled { get; }
    /// <summary>Downstream neighbour index; an ocean cell for coastal land; None for ocean/unreached.</summary>
    public int[] Receiver { get; }
    /// <summary>The land cell where this cell's water crosses into the sea.</summary>
    public int[] Outlet { get; }
    /// <summary>Path length along receivers to the sea, in world units.</summary>
    public float[] FlowLength { get; }
    /// <summary>Rain-weighted contributing area. High ground catches more (orographic weighting).</summary>
    public float[] Accumulation { get; }

    public RiverDrainageField(RiverGround ground, float seaLevel, float flatland, int seed)
    {
        int n = Size = ground.Size;
        int count = n * n;
        Land = new bool[count]; Height = new float[count]; Filled = new float[count];
        Receiver = new int[count]; Outlet = new int[count];
        FlowLength = new float[count]; Accumulation = new float[count];
        var visited = new bool[count];
        var sea = new bool[count];

        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            int i = z * n + x;
            var p = new Vector2(x, z);
            float h = ground.Height(p), shore = ground.Shore(p);
            Height[i] = h; Filled[i] = h;
            Receiver[i] = None; Outlet[i] = None;
            // Same admissibility the single-river search used: interior, above water, inland of the shoreline.
            Land[i] = x >= 2 && z >= 2 && x < n - 2 && z < n - 2
                && shore >= -0.05f && h >= seaLevel + 0.015f;
            sea[i] = shore < -0.05f || h < seaLevel;
        }

        var open = new MinHeap();
        // Only the real sea may receive water. A low inland cell or the chunk border is not an outlet.
        for (int z = 2; z < n - 2; z++)
        for (int x = 2; x < n - 2; x++)
        {
            int i = z * n + x;
            if (Land[i] || !sea[i] || !HasLandNeighbour(x, z)) continue;
            visited[i] = true;
            Filled[i] = seaLevel;
            open.Push(i, seaLevel);
        }

        var order = new List<int>(count);
        float sx = (seed & 255) * 0.37f, sz = ((seed >> 8) & 255) * 0.29f;
        while (open.Count > 0)
        {
            int current = open.Pop(out _);
            bool currentLand = Land[current];
            if (currentLand) order.Add(current);
            int cx = current % n, cz = current / n;
            for (int d = 0; d < 8; d++)
            {
                int x = cx + DX[d], z = cz + DZ[d];
                if (x < 0 || z < 0 || x >= n || z >= n) continue;
                int next = z * n + x;
                if (visited[next] || !Land[next]) continue;
                visited[next] = true;
                float length = (d & 1) == 0 ? 1f : 1.414214f;
                Receiver[next] = current;
                Filled[next] = Mathf.Max(Height[next], Filled[current] + FloodEpsilon * length);
                Outlet[next] = currentLand ? Outlet[current] : next;
                FlowLength[next] = (currentLand ? FlowLength[current] : 0f) + length;
                // The key only orders the flood; Filled stays monotone along every receiver chain.
                float meander = Mathf.PerlinNoise(x * 0.11f + sx, z * 0.11f + sz) * FlatMeander;
                open.Push(next, Filled[next] + meander);
            }
        }

        // Contributing area: each cell passes its own rain plus everything above it downstream.
        for (int k = 0; k < order.Count; k++)
        {
            int i = order[k];
            Accumulation[i] = 1f + Mathf.Max(0f, Height[i] - flatland) * 0.6f;
        }
        for (int k = order.Count - 1; k >= 0; k--)
        {
            int i = order[k], r = Receiver[i];
            if (r >= 0 && Land[r]) Accumulation[r] += Accumulation[i];
        }

        bool HasLandNeighbour(int x, int z)
        {
            for (int d = 0; d < 8; d++)
            {
                int nx = x + DX[d], nz = z + DZ[d];
                if (nx >= 0 && nz >= 0 && nx < n && nz < n && Land[nz * n + nx]) return true;
            }
            return false;
        }
    }

    public int Index(int x, int z) => z * Size + x;
    public Vector2 Point(int index) => new Vector2(index % Size, index / Size);
    public bool Drains(int index) => index >= 0 && Land[index] && Receiver[index] != None;
    public float PondDepth(int index) => Filled[index] - Height[index];

    /// <summary>Receiver chain from a land cell to its coast cell, inclusive.</summary>
    public List<Vector2> Trace(int start)
    {
        var path = new List<Vector2>();
        for (int at = start; at >= 0 && Land[at]; at = Receiver[at])
        {
            path.Add(Point(at));
            if (path.Count > Size * Size) break;
        }
        return path;
    }

    internal sealed class MinHeap
    {
        private readonly List<(int id, float cost)> items = new List<(int, float)>();
        public int Count => items.Count;
        public void Push(int id, float cost)
        {
            items.Add((id, cost)); int i = items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (items[parent].cost <= cost) break;
                items[i] = items[parent]; i = parent;
            }
            items[i] = (id, cost);
        }
        public int Pop(out float cost)
        {
            var result = items[0]; var last = items[items.Count - 1]; items.RemoveAt(items.Count - 1);
            if (items.Count > 0)
            {
                int i = 0;
                while (i * 2 + 1 < items.Count)
                {
                    int child = i * 2 + 1;
                    if (child + 1 < items.Count && items[child + 1].cost < items[child].cost) child++;
                    if (items[child].cost >= last.cost) break;
                    items[i] = items[child]; i = child;
                }
                items[i] = last;
            }
            cost = result.cost; return result.id;
        }
    }
}
