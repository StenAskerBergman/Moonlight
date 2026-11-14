// tldr: SEARCHES for a viable river: elevated sources -> drainage routes -> a full hydraulic course, rejecting
// candidates that cannot be built. Returns a finished RiverPlan or null. Owns acceptance policy.

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plans one complete source-to-ocean course against the finished, pre-river terrain cache.
/// Bounded drainage search -> smooth route -> supported longitudinal profile -> shared sections.
/// No Unity scene access and no mutation of the input terrain while candidates are evaluated.
/// </summary>
public static class RiverPlanner
{
    private const float StationSpacing = 0.4f;
    private const float MinimumFall = 0.65f;
    private const float MinimumHeadwaterLength = 5.2f;
    private const float PlungePoolLength = 4.4f;
    private const float MaximumFill = 1.25f;
    private const int MaximumSources = 12;
    /// <summary>
    /// Arc length upstream of the shoreline crossing reserved for the mouth transition. No
    /// waterfall, plunge basin or channel widening is admissible inside it: the coastal outlet
    /// is a tapered estuary, not a place to park an inland basin. The previous four-unit
    /// "lower river" test measured from the estuary station, so a pool could still land
    /// effectively at the shoreline and stamp its doubled width over the ocean.
    /// </summary>
    private const float MouthTransitionLength = 9f;
    /// <summary>Cap on the reserved interval for short courses, so it cannot eat the whole river.</summary>
    private const float MouthTransitionShare = 0.35f;
    /// <summary>Fraction of the ordinary channel width the course is tapered to at the mouth.</summary>
    private const float MouthChannelScale = 0.52f;
    /// <summary>
    /// Arc length past the shoreline over which inland water hands off to the ocean. The plan
    /// still routes across the shelf for the bed, but rendered inland-water geometry fades out
    /// within this distance instead of tiling the whole submerged continuation.
    /// </summary>
    private const float OceanHandoffLength = 2.5f;
    private static readonly int[] DX = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] DZ = { 0, 1, 1, 1, 0, -1, -1, -1 };

    public static RiverPlan Build(TerrainSampleCache terrain, TerrainGenerationSettings settings, int seed, out string report)
    {
        var ground = new Ground(terrain);
        float sea = settings.waterHeight;
        float minimumLength = settings.riverCorridors.minimumRiverLength;
        float maximumCut = Mathf.Max(2.5f, settings.riverCorridors.valleyDepth * 2f);
        int n = terrain.GridSize;
        var candidates = new List<Vector2Int>();
        for (int z = 3; z < n - 3; z += 2) for (int x = 3; x < n - 3; x += 2)
        {
            var p = new Vector2(x, z);
            if (ground.Shore(p) >= Mathf.Max(6f, minimumLength * 0.55f)
                && ground.Height(p) >= sea + 0.95f)
                candidates.Add(new Vector2Int(x, z));
        }
        candidates.Sort((a, b) =>
        {
            int h = ground.Shore(b).CompareTo(ground.Shore(a));
            return h != 0 ? h : (a.y * n + a.x).CompareTo(b.y * n + b.x);
        });
        var tried = new List<Vector2>();
        RiverLandform.Buffers scratch = null;
        string rejection = "No inland source with sufficient freeboard and room for banks.";
        foreach (Vector2Int source in candidates)
        {
            bool nearby = false;
            foreach (Vector2 p in tried) if ((p - (Vector2)source).sqrMagnitude < 9f) { nearby = true; break; }
            if (nearby) continue;
            tried.Add(source);
            List<List<Vector2>> routes = FindDrainageRoutes(ground, source, minimumLength, sea, seed);
            foreach (List<Vector2> route in routes)
            {
                List<Vector2> path = SmoothAndResample(route);
                if (!ContinueIntoOcean(path, ground, sea, settings.coastalProfile.surfWidth, out rejection)) continue;
                CourseProposal proposal = SolveCourse(path, ground, settings, seed, out rejection);
                if (proposal == null) continue;
                if (!ValidateSections(proposal.Sections, ground, proposal.MouthSection, out rejection)) continue;
                if (scratch == null) scratch = new RiverLandform.Buffers(terrain.Resolution * terrain.Resolution);
                RiverPlan plan = new RiverPlan(terrain, proposal.Sections, proposal.Waterfalls,
                    proposal.MouthSection, maximumCut, MaximumFill, seed, scratch);
                if (!ValidateLandform(plan, ground, sea, maximumCut, out rejection)) continue;
                report = $"accepted: {plan.Length:F1} units, {plan.Sections.Count} sections, " +
                    $"{plan.Waterfalls.Count} geological drops; cut <= {maximumCut:F2}, fill <= {MaximumFill:F2}; " +
                    $"mouth ({plan.Mouth.Centre.x:F1}, {plan.Mouth.Centre.y:F1}) at water {sea:F2}";
                return plan;
            }
            if (tried.Count >= MaximumSources) break;
        }
        report = $"No viable connected river after {tried.Count} source attempts: {rejection}";
        return null;
    }

    private static List<List<Vector2>> FindDrainageRoutes(Ground ground, Vector2Int source, float minimumLength, float sea, int seed)
    {
        int n = ground.Size, count = n * n;
        var costs = new float[count]; var parent = new int[count];
        for (int i = 0; i < count; i++) { costs[i] = float.PositiveInfinity; parent[i] = -1; }
        int start = source.y * n + source.x;
        var open = new MinHeap(); costs[start] = 0f; open.Push(start, 0f);
        var routes = new List<List<Vector2>>(); var mouths = new List<Vector2>();
        while (open.Count > 0 && routes.Count < 5)
        {
            int current = open.Pop(out float cost);
            if (cost > costs[current]) continue;
            Vector2 p = new Vector2(current % n, current / n);
            float h = ground.Height(p), shore = ground.Shore(p);
            if (shore < 1.5f && shore >= 0f && h < sea + 0.8f && Vector2.Distance(p, source) >= minimumLength)
            {
                bool separate = true;
                foreach (Vector2 mouth in mouths) if (Vector2.Distance(mouth, p) < 5f) { separate = false; break; }
                if (separate)
                {
                    var path = new List<Vector2>();
                    for (int at = current; at >= 0; at = parent[at]) path.Add(new Vector2(at % n, at / n));
                    path.Reverse(); routes.Add(path); mouths.Add(p);
                }
            }
            for (int d = 0; d < 8; d++)
            {
                int x = (int)p.x + DX[d], z = (int)p.y + DZ[d];
                if (x < 2 || z < 2 || x >= n - 2 || z >= n - 2) continue;
                Vector2 q = new Vector2(x, z);
                float qh = ground.Height(q), qs = ground.Shore(q);
                if (qs < -0.05f || qh < sea + 0.015f) continue;
                float length = (d & 1) == 0 ? 1f : 1.414214f;
                // Uphill excavation is expensive. Flatland micro-relief and broad spatial cost
                // variation break ties into drainage curves without adding a sine wave to a line.
                float uphill = Mathf.Max(0f, qh - h);
                float coastalPenalty = Mathf.Max(0f, 4f - qs) * 2f;
                float variation = Mathf.PerlinNoise(q.x * 0.075f + (seed & 255), q.y * 0.075f) * 0.4f;
                float step = length * (1f + uphill * 14f + coastalPenalty + variation + Mathf.Max(0f, qh - sea) * 0.025f);
                int next = z * n + x; float nextCost = cost + step;
                if (nextCost >= costs[next]) continue;
                costs[next] = nextCost; parent[next] = current; open.Push(next, nextCost);
            }
        }
        return routes;
    }

    private static List<Vector2> SmoothAndResample(List<Vector2> input)
    {
        var path = input;
        for (int pass = 0; pass < 3; pass++)
        {
            var smooth = new List<Vector2> { path[0] };
            for (int i = 0; i < path.Count - 1; i++)
            {
                smooth.Add(Vector2.Lerp(path[i], path[i + 1], 0.25f));
                smooth.Add(Vector2.Lerp(path[i], path[i + 1], 0.75f));
            }
            smooth.Add(path[path.Count - 1]); path = smooth;
        }
        path = Resample(path);
        // Round grid-search corners over a physical neighborhood before assigning widths.
        // Candidate validation still sees the real ground under the relaxed route.
        for (int pass = 0; pass < 24; pass++)
        {
            var relaxed = new List<Vector2>(path);
            for (int i = 1; i < path.Count - 1; i++)
                relaxed[i] = path[i] * 0.5f + (path[i - 1] + path[i + 1]) * 0.25f;
            path = relaxed;
        }
        return Resample(path);
    }

    private static List<Vector2> Resample(List<Vector2> path)
    {
        var result = new List<Vector2> { path[0] };
        float carry = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            Vector2 a = path[i - 1], b = path[i]; float segment = Vector2.Distance(a, b);
            if (segment < 0.00001f) continue;
            float at = StationSpacing - carry;
            while (at < segment) { result.Add(Vector2.Lerp(a, b, at / segment)); at += StationSpacing; }
            carry = segment - (at - StationSpacing);
        }
        Vector2 end = path[path.Count - 1];
        if (Vector2.Distance(result[result.Count - 1], end) < StationSpacing * 0.5f) result[result.Count - 1] = end;
        else result.Add(end);
        return result;
    }

    private static bool ContinueIntoOcean(List<Vector2> path, Ground ground, float sea, float surfWidth, out string reason)
    {
        Vector2 p = path[path.Count - 1];
        Vector2 outward = new Vector2(ground.Shore(p - Vector2.right) - ground.Shore(p + Vector2.right),
            ground.Shore(p - Vector2.up) - ground.Shore(p + Vector2.up)).normalized;
        if (outward.sqrMagnitude < 0.5f) { reason = "Mouth has no resolvable downstream shore normal."; return false; }
        bool connected = false;
        // Continue into the real surf/shelf, not an arbitrary +2 endpoint in the beach.
        for (float d = StationSpacing; d <= surfWidth + 6f; d += StationSpacing)
        {
            Vector2 q = p + outward * d;
            if (q.x < 1f || q.y < 1f || q.x > ground.Size - 1f || q.y > ground.Size - 1f) break;
            path.Add(q);
            if (ground.Shore(q) < -surfWidth - 0.5f && ground.Height(q) < sea - 0.4f) { connected = true; break; }
        }
        reason = connected ? null : "Mouth extension did not reach the submerged shelf inside this chunk.";
        return connected;
    }

    private static CourseProposal SolveCourse(List<Vector2> path, Ground ground,
        TerrainGenerationSettings settings, int seed, out string reason)
    {
        int n = path.Count;
        var distance = new float[n]; var height = new float[n]; var water = new float[n];
        var kinds = new RiverReachKind[n]; var poolWeight = new float[n]; var impactFoam = new float[n];
        float sea = settings.waterHeight;
        int mouth = -1, estuary = -1;
        for (int i = 0; i < n; i++)
        {
            if (i > 0) distance[i] = distance[i - 1] + Vector2.Distance(path[i], path[i - 1]);
            height[i] = ground.Height(path[i]);
            float shore = ground.Shore(path[i]);
            if (mouth < 0 && shore <= 0f) mouth = i;
            if (estuary < 0 && i > n / 3 && shore < settings.coastalProfile.beachWidth + settings.coastalProfile.backshoreWidth + 1f)
                estuary = i;
        }
        if (mouth < 0 || mouth < 20) { reason = "No continuous inland route to a shoreline crossing."; return null; }
        if (estuary < 0) estuary = mouth - 8;
        estuary = Mathf.Min(estuary, mouth - 4);

        var falls = new List<RiverWaterfall>();
        float lastPoolEnd = -10f;
        // Geological drops belong to the inland course. Everything downstream of this arc
        // length is reserved for the mouth transition and may only carry a tapered estuary.
        float transition = MouthTransition(distance[mouth]);
        float fallLimit = distance[mouth] - transition;
        water[0] = height[0] - 0.4f;
        for (int i = 0; i < estuary; i++)
        {
            float previous = i == 0 ? water[0] : water[i - 1];
            float desired = Mathf.Max(sea + 0.5f, height[i] - 0.45f);
            water[i] = Mathf.Min(previous, desired);
            kinds[i] = distance[i] < MinimumHeadwaterLength ? RiverReachKind.Headwater
                : height[i] - water[i] > 0.85f || height[i] > settings.surfaceFlatlandHeight + 0.6f
                ? RiverReachKind.Gorge : RiverReachKind.Lowland;

            // Consolidate a natural mountain descent into a supported crest, steep geological
            // face and receiving basin. Pool and downstream outlet share the same water level.
            int look = Mathf.Min(i + 9, estuary - 5);
            if (distance[i] < MinimumHeadwaterLength || look <= i + 4 || distance[i] - lastPoolEnd < 4f) continue;
            if (distance[i] > fallLimit) break;
            float lower = Mathf.Max(sea + 0.65f, height[look] - 0.45f);
            float drop = water[i] - lower;
            if (drop < MinimumFall || height[i] - height[look] < MinimumFall || height[i] - height[i + 2] < 0.3f) continue;
            int toe = i + 2;
            int poolEnd = toe;
            while (poolEnd < estuary - 3 && distance[poolEnd] - distance[toe] < PlungePoolLength) poolEnd++;
            if (distance[poolEnd] - distance[toe] < PlungePoolLength * 0.75f) continue;
            // The basin doubles the channel width where it is stamped. Reject the whole drop
            // rather than truncate it into the reserved mouth interval.
            if (distance[poolEnd] > fallLimit) break;
            lower = Mathf.Min(lower, water[i] - MinimumFall);
            for (int j = i; j <= toe; j++)
            {
                water[j] = Mathf.Lerp(water[i], lower, (distance[j] - distance[i]) / (distance[toe] - distance[i]));
                kinds[j] = RiverReachKind.Waterfall;
            }
            for (int j = toe; j <= poolEnd; j++)
            {
                water[j] = lower; kinds[j] = RiverReachKind.PlungePool;
                float u = Mathf.Clamp01((distance[j] - distance[toe]) /
                    Mathf.Max(0.01f, distance[poolEnd] - distance[toe]));
                // A plunge basin opens quickly below the impact, then narrows gradually into
                // its outlet.  Separate width and impact curves avoid the old symmetric bulb.
                poolWeight[j] = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.72f)), 0.8f);
                impactFoam[j] = Mathf.Exp(-u * 4.2f);
            }
            falls.Add(new RiverWaterfall(i, toe, poolEnd));
            lastPoolEnd = distance[poolEnd]; i = poolEnd;
        }
        // Waterfalls are optional geology; lowland spring-fed courses are valid on flat mainland.
        // This backstop is measured against the shoreline crossing, not the estuary station. A
        // pool ending four units before the estuary can still sit on the beach when the estuary
        // interval is itself short, which is how a basin got stamped out over the ocean.
        if (falls.Count > 0 && distance[mouth] - lastPoolEnd < transition)
        { reason = "Plunge pool intrudes into the reserved mouth transition."; return null; }

        // Mouth is a level ocean connection. The bed, not the water surface, descends into
        // the shelf. Below-sea river bed is confined to this explicit estuary interval.
        float entryWater = water[Mathf.Max(0, estuary - 1)];
        for (int i = estuary; i < n; i++)
        {
            float u = Mathf.Clamp01((distance[i] - distance[estuary - 1]) / (distance[mouth] - distance[estuary - 1]));
            water[i] = Mathf.Lerp(entryWater, sea, u);
            kinds[i] = RiverReachKind.Estuary;
        }

        var sections = new List<RiverSection>();
        for (int i = 0; i < n; i++)
        {
            Vector2 before = (path[i] - path[Mathf.Max(0, i - 1)]).normalized;
            Vector2 after = (path[Mathf.Min(n - 1, i + 1)] - path[i]).normalized;
            Vector2 forward = (before + after).normalized;
            if (forward.sqrMagnitude < 0.5f) forward = i == 0 ? after : before;
            Vector2 lateral = new Vector2(-forward.y, forward.x);
            float variation = Mathf.PerlinNoise(distance[i] * 0.065f + (seed & 255), 31.7f);
            float downstream = Mathf.Clamp01(distance[i] / distance[mouth]);
            // Taper into the coast. The channel reaches full width in the lower river and then
            // narrows across the reserved interval, so the outlet leaves the shoreline as one
            // continuous mouth rather than a widened field joined by two visible shoulders.
            float mouthApproach = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((distance[mouth] - distance[i]) / transition));
            float mouthTaper = Mathf.Lerp(MouthChannelScale, 1f, mouthApproach);
            float openRadius = Mathf.Clamp(settings.riverCorridors.channelRadius, 0.6f, 2f)
                * Mathf.Lerp(0.48f, 1f, downstream) * Mathf.Lerp(0.85f, 1.12f, variation);
            // Narrowing must not starve the outlet below the supported-channel floor that
            // ValidateSections enforces, or a small channelRadius would reject every route.
            float radius = Mathf.Max(openRadius * mouthTaper, Mathf.Min(openRadius, 0.42f));
            float sideNoise = Mathf.PerlinNoise(path[i].x * 0.11f + 43.1f,
                path[i].y * 0.11f + (seed & 255) * 0.17f);
            float ordinaryBias = (sideNoise - 0.5f) * 0.16f;
            float poolBias = Mathf.Lerp(-0.22f, 0.22f, sideNoise) * poolWeight[i];
            float leftWidth = radius * (1f + ordinaryBias + poolWeight[i] * 0.42f + poolBias);
            float rightWidth = radius * (1f - ordinaryBias + poolWeight[i] * 0.42f - poolBias);
            if (i > 0 && i < n - 1)
            {
                float turn = Vector2.Angle(before, after) * Mathf.Deg2Rad;
                float localRadius = (distance[i + 1] - distance[i - 1]) * 0.5f / Mathf.Max(0.001f, turn);
                float cap = localRadius * 0.7f;
                leftWidth = Mathf.Min(leftWidth, cap);
                rightWidth = Mathf.Min(rightWidth, cap);
            }
            // An elevated spring emerges from a closed rock pocket, not a ribbon's open cut end.
            float sourceOpening = Mathf.SmoothStep(0.015f, 1f, Mathf.Clamp01(distance[i] / 1.8f));
            leftWidth *= sourceOpening;
            rightWidth *= sourceOpening;
            float depth = kinds[i] == RiverReachKind.PlungePool
                ? 0.55f + poolWeight[i] * 0.72f + impactFoam[i] * 0.22f : 0.35f;
            float bed = water[i] - depth;
            if (i < estuary) bed = Mathf.Max(sea + 0.06f, bed);
            if (i >= mouth) bed = Mathf.Min(bed, height[i]);

            bool gorge = kinds[i] == RiverReachKind.Gorge || kinds[i] == RiverReachKind.Waterfall || kinds[i] == RiverReachKind.PlungePool;
            // Left lies along negative lateral in RiverLandform. Probe the bank actually
            // being constructed and sample each side independently, rather than complementing
            // one noise value into two effectively mirrored profiles.
            Vector2 leftProbe = path[i] - lateral * (leftWidth + 2f);
            Vector2 rightProbe = path[i] + lateral * (rightWidth + 2f);
            float leftVariation = Mathf.PerlinNoise(leftProbe.x * 0.13f + 19f, leftProbe.y * 0.13f + (seed & 127));
            float rightVariation = Mathf.PerlinNoise(rightProbe.x * 0.1f + 73f, rightProbe.y * 0.1f + (seed & 127));
            RiverBankProfile left = Bank(ground, path[i], -lateral, leftWidth, water[i], gorge, leftVariation);
            RiverBankProfile right = Bank(ground, path[i], lateral, rightWidth, water[i], gorge, rightVariation);
            // Offshore there is no relief to resolve, so an untapered bank would run its full
            // lowland extent across the shelf and carve a valley far wider than the outlet.
            if (mouthTaper < 1f) { left = Narrow(left, mouthTaper); right = Narrow(right, mouthTaper); }
            float foam = kinds[i] == RiverReachKind.PlungePool
                ? Mathf.Lerp(0.06f, 0.82f, impactFoam[i])
                : kinds[i] == RiverReachKind.Waterfall ? 0.5f : 0.025f;
            // Hand off to the ocean shortly after the crossing. The course still routes to the
            // shelf so the bed is continuous, but rendered inland water must not tile the whole
            // submerged continuation - the ocean already covers it.
            float oceanBlend = Mathf.Clamp01((distance[i] - distance[mouth]) / OceanHandoffLength);
            sections.Add(new RiverSection(path[i], lateral, distance[i], water[i], bed,
                leftWidth, rightWidth, left, right, kinds[i], foam, oceanBlend));
        }
        reason = null;
        return new CourseProposal(sections, falls, mouth);
    }

    private static RiverBankProfile Bank(Ground ground, Vector2 centre, Vector2 outward, float waterWidth,
        float water, bool gorge, float variation)
    {
        float shallow = gorge ? Mathf.Lerp(0.12f, 0.45f, variation) : Mathf.Lerp(0.35f, 0.95f, variation);
        float bench = gorge ? Mathf.Lerp(0.25f, 1.1f, variation) : Mathf.Lerp(0.65f, 2.2f, variation);
        float rise = Mathf.Max(0f, ground.Height(centre + outward * (waterWidth + shallow + bench + 2f)) - water - 0.16f);
        // Give the valley enough lateral run for its actual relief. Only resistant high
        // ground receives the steeper end of the range; lowland shoulders stay broad.
        float gradient = gorge ? Mathf.Lerp(0.95f, 0.42f, variation) : Mathf.Lerp(0.38f, 0.2f, variation);
        float wall = Mathf.Max(gorge ? 1.2f : 2.2f, rise / gradient);
        float rim = Mathf.Max(water + (gorge ? 0.5f : 0.23f),
            ground.Height(centre + outward * (waterWidth + shallow + bench + wall)));
        float rock = gorge ? 0.95f : Mathf.Lerp(0.08f, 0.4f, variation);
        return new RiverBankProfile(shallow, bench, wall, gorge ? 1.25f : 1.6f, rim, rock);
    }

    /// <summary>Arc length reserved for the mouth transition on a course of this length to the sea.</summary>
    private static float MouthTransition(float mouthDistance)
        => Mathf.Min(MouthTransitionLength, mouthDistance * MouthTransitionShare);

    /// <summary>Scales a bank's lateral run without touching its rim elevation or rock share.</summary>
    private static RiverBankProfile Narrow(RiverBankProfile bank, float scale)
        => new RiverBankProfile(bank.ShallowWidth * scale, bank.BenchWidth * scale,
            bank.WallWidth * scale, bank.BlendWidth * scale, bank.RimHeight, bank.RockWeight);

    private static bool ValidateSections(IReadOnlyList<RiverSection> sections, Ground ground,
        int mouthSection, out string reason)
    {
        if (!ValidateMouthSilhouette(sections, mouthSection, out reason)) return false;
        // Reject structural failures before allocating or stamping the full-resolution landform.
        for (int i = 0; i < sections.Count; i++)
        {
            RiverSection s = sections[i];
            if (i > 0 && s.WaterHeight > sections[i - 1].WaterHeight + 0.001f)
            { reason = "Water profile rises downstream."; return false; }
            if (s.BedHeight >= s.WaterHeight - 0.06f) { reason = "Insufficient water depth."; return false; }
            if (i > 3 && Mathf.Min(s.LeftWaterWidth, s.RightWaterWidth) < 0.3f)
            { reason = "Drainage turn is too tight for a supported channel."; return false; }
            for (float across = -s.LeftWaterWidth - s.LeftBank.Extent;
                across <= s.RightWaterWidth + s.RightBank.Extent; across += 0.3f)
            {
                Vector2 p = s.Centre + s.Lateral * across;
                if (p.x < 0.5f || p.y < 0.5f || p.x > ground.Size - 0.5f || p.y > ground.Size - 0.5f)
                { reason = "River banks exceed the terrain chunk."; return false; }
            }
            // A returning course may not overlap itself. Adjacent sections are expected to
            // share banks; distant sections must have separate wet footprints in this milestone.
            for (int j = 0; j < i; j++)
            {
                if (s.Distance - sections[j].Distance < 7f) continue;
                float min = s.MaximumWaterWidth + sections[j].MaximumWaterWidth + 0.25f;
                if ((s.Centre - sections[j].Centre).sqrMagnitude < min * min)
                { reason = "Route doubles back into its own channel."; return false; }
            }
        }
        reason = null; return true;
    }

    /// <summary>
    /// The semantic mouth rule the numeric checks never expressed: a course may be hydraulically
    /// admissible - correct depth, budget, bounds, no self-overlap - and still meet the sea as an
    /// artificial basin. The coastal outlet must be a tapered estuary, so the reserved interval
    /// carries no geological drop and never widens on its way to the shoreline.
    /// </summary>
    private static bool ValidateMouthSilhouette(IReadOnlyList<RiverSection> sections, int mouthSection,
        out string reason)
    {
        if (mouthSection < 0 || mouthSection >= sections.Count)
        { reason = "Course has no resolved shoreline crossing."; return false; }
        float intervalStart = sections[mouthSection].Distance - MouthTransition(sections[mouthSection].Distance);
        int entry = -1;
        for (int i = 0; i < sections.Count && sections[i].Distance < intervalStart; i++) entry = i;
        if (entry < 0) { reason = "River is shorter than the reserved mouth transition."; return false; }
        float entryWidth = sections[entry].LeftWaterWidth + sections[entry].RightWaterWidth;
        for (int i = entry + 1; i < sections.Count; i++)
        {
            RiverSection s = sections[i];
            if (s.Kind == RiverReachKind.Waterfall || s.Kind == RiverReachKind.PlungePool)
            { reason = $"{s.Kind} lies inside the reserved mouth transition."; return false; }
            if (s.LeftWaterWidth + s.RightWaterWidth > entryWidth + 0.1f)
            { reason = "Channel widens into the mouth instead of tapering."; return false; }
        }
        reason = null; return true;
    }

    private static bool ValidateLandform(RiverPlan plan, Ground ground, float sea, float maxCut, out string reason)
    {
        // Exact resolved-field checks retain the previous acceptance semantics, but run only
        // after section-only failures have been eliminated. Rejected candidates reuse buffers.
        foreach (RiverSection s in plan.Sections)
        for (float across = -s.LeftWaterWidth - s.LeftBank.Extent;
            across <= s.RightWaterWidth + s.RightBank.Extent; across += 0.3f)
        {
            Vector2 p = s.Centre + s.Lateral * across;
            if (!plan.TrySample(p, out RiverSurfaceSample sample)) continue;
            float original = ground.Height(p), result = sample.Apply(original), delta = result - original;
            if (delta > MaximumFill + 0.01f || -delta > maxCut + 0.01f)
            { reason = $"Landform budget exceeded (cut {-delta:F2}, fill {delta:F2})."; return false; }
            // The wet contour has zero depth at its bank. Channel depth is checked at sections;
            // reject penetration here, not the legitimate shallow edge of that contour.
            if (sample.IsWater && result > sample.WaterHeight + 0.001f)
            { reason = "Terrain obstructs the water footprint."; return false; }
            if (sample.Kind != RiverReachKind.Estuary && sample.IsWater && result < sea)
            { reason = "Inland course would intersect the ocean plane."; return false; }
        }
        reason = null; return true;
    }

    private sealed class CourseProposal
    {
        public readonly List<RiverSection> Sections;
        public readonly List<RiverWaterfall> Waterfalls;
        public readonly int MouthSection;
        public CourseProposal(List<RiverSection> sections, List<RiverWaterfall> waterfalls, int mouthSection)
        { Sections = sections; Waterfalls = waterfalls; MouthSection = mouthSection; }
    }

    private sealed class Ground
    {
        private readonly TerrainSampleCache cache;
        public int Size => cache.GridSize;
        public Ground(TerrainSampleCache cache) { this.cache = cache; }
        public float Height(Vector2 p) => Bilinear(cache.Heights, p);
        public float Shore(Vector2 p) => Bilinear(cache.ShoreDistances, p);
        private float Bilinear(float[] field, Vector2 p)
        {
            float x = Mathf.Clamp(p.x / cache.Step, 0f, cache.Resolution - 1.001f);
            float z = Mathf.Clamp(p.y / cache.Step, 0f, cache.Resolution - 1.001f);
            int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z), r = cache.Resolution;
            return Mathf.Lerp(Mathf.Lerp(field[iz * r + ix], field[iz * r + ix + 1], x - ix),
                Mathf.Lerp(field[(iz + 1) * r + ix], field[(iz + 1) * r + ix + 1], x - ix), z - iz);
        }
    }

    private sealed class MinHeap
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
