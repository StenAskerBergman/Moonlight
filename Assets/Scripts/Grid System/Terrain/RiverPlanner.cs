// tldr: PLANS the island's river network: one drainage field -> ranked sources (mountains first) -> each
// source traced down the shared receiver tree until it either reaches the sea (a trunk with its own outlet)
// or meets an accepted course (a tributary sharing that course's outlet) -> one validated landform.

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plans the complete river network against the finished, pre-river terrain cache.
/// Drainage field -> ranked sources -> per-source course (trunk or tributary) with the shared
/// hydraulic solver -> confluence widening -> one landform for every course, validated together.
/// No Unity scene access and no mutation of the input terrain while candidates are evaluated.
/// </summary>
public static class RiverPlanner
{
    private const float StationSpacing = 0.4f;
    private const float MinimumFall = 0.65f;
    private const float MinimumHeadwaterLength = 5.2f;
    private const float PlungePoolLength = 4.4f;
    private const float MaximumFill = 1.25f;
    /// <summary>Sources that reach full course solving before the planner stops looking.</summary>
    private const int MaximumCandidateTrials = 60;
    /// <summary>
    /// Arc length upstream of the shoreline crossing reserved for the mouth transition. No
    /// waterfall, plunge basin, channel widening or confluence is admissible inside it: the
    /// coastal outlet is a tapered estuary, not a place to park an inland basin.
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
    /// <summary>A tributary carries less water than the course it feeds.</summary>
    private const float TributaryWidthScale = 0.72f;
    /// <summary>Arc length over which a channel widens after a tributary joins it.</summary>
    private const float ConfluenceWideningRamp = 3f;
    /// <summary>Upper bound on the downstream widening a channel can accumulate from tributaries.</summary>
    private const float MaximumConfluenceWidening = 1.45f;
    /// <summary>Minimum parent arc length between two confluences.</summary>
    private const float ConfluenceSpacing = 5f;

    // ------------------------------------------------------------------------------------------
    // Network assembly
    // ------------------------------------------------------------------------------------------

    public static RiverNetwork BuildNetwork(TerrainSampleCache terrain, TerrainGenerationSettings settings, int seed,
        out string report)
    {
        RiverCorridorSettings rivers = settings.riverCorridors;
        var ground = new RiverGround(terrain);
        float sea = settings.waterHeight;
        float flatland = settings.surfaceFlatlandHeight;
        float maximumCut = Mathf.Max(2.5f, rivers.valleyDepth * 2f);
        var random = new System.Random(unchecked(seed * 73856093 ^ 0x5F3759DF));
        int minimum = Mathf.Min(rivers.minRivers, rivers.maxRivers);
        int target = random.Next(minimum, rivers.maxRivers + 1);
        if (target <= 0)
        {
            report = $"River count rolled 0 (range {minimum}-{rivers.maxRivers}).";
            return null;
        }

        var drainage = new RiverDrainageField(ground, sea, flatland, seed);
        var context = new PlanContext(ground, drainage, settings, seed, maximumCut, random);
        List<int> sources = RankSources(context);

        var drafts = new List<CourseDraft>();
        var rejections = new Dictionary<string, int>();
        var tried = new List<Vector2>();
        int trials = 0;
        foreach (int source in sources)
        {
            if (drafts.Count >= target || trials >= MaximumCandidateTrials) break;
            Vector2 at = drainage.Point(source);
            if (WithinAny(tried, at, 3f) || NearNetwork(drafts, at, rivers.clearanceRadius)) continue;
            tried.Add(at);
            trials++;
            CourseDraft draft = TryCourse(context, source, drafts, out string rejection);
            if (draft == null) { Tally(rejections, rejection); continue; }
            drafts.Add(draft);
            if (draft.Parent != null) draft.Parent.Tributaries.Add(draft);
        }

        // Every course is stamped into one field and validated against the merged result. A
        // failing course is removed together with every course that drains through it.
        RiverLandform landform = null;
        RiverLandform.Buffers scratch = null;
        while (drafts.Count > 0)
        {
            ApplyConfluenceWidening(context, drafts);
            if (scratch == null) scratch = new RiverLandform.Buffers(terrain.Resolution * terrain.Resolution);
            landform = BuildLandform(terrain, drafts, maximumCut, seed, scratch);
            int failing = ValidateLandform(landform, drafts, ground, sea, maximumCut, out string rejection);
            if (failing < 0) break;
            Tally(rejections, rejection);
            RemoveWithDescendants(drafts, drafts[failing]);
            landform = null;
        }

        if (drafts.Count == 0 || landform == null)
        {
            report = $"No viable river network after {trials} source attempts (target {target}): {Summarise(rejections)}";
            return null;
        }

        RiverNetwork network = Assemble(drafts, landform, maximumCut);
        report = Describe(network, target, minimum, trials, rejections, maximumCut, sea);
        return network;
    }

    private sealed class PlanContext
    {
        public readonly RiverGround Ground;
        public readonly RiverDrainageField Drainage;
        public readonly TerrainGenerationSettings Settings;
        public readonly RiverCorridorSettings Rivers;
        public readonly int Seed;
        public readonly float Sea, Flatland, MaximumCut, MinimumTributaryLength;
        public readonly System.Random Random;

        public PlanContext(RiverGround ground, RiverDrainageField drainage, TerrainGenerationSettings settings,
            int seed, float maximumCut, System.Random random)
        {
            Ground = ground; Drainage = drainage; Settings = settings; Rivers = settings.riverCorridors;
            Seed = seed; Sea = settings.waterHeight; Flatland = settings.surfaceFlatlandHeight;
            MaximumCut = maximumCut; Random = random;
            MinimumTributaryLength = Mathf.Max(MinimumHeadwaterLength + 4f, Rivers.minimumRiverLength * 0.45f);
        }
    }

    /// <summary>A course while the network is still being negotiated.</summary>
    private sealed class CourseDraft
    {
        public List<Vector2> Path;
        public CourseOptions Options;
        /// <summary>Solved without confluence widening; widening is always re-derived from these.</summary>
        public List<RiverSection> BaseSections;
        public List<RiverSection> Sections;
        public List<RiverWaterfall> Falls;
        public int MouthSection = -1;
        public float DistanceOffset;
        public Vector2 OutletPoint;
        public CourseDraft Parent;
        public int ParentSection = -1;
        public float JunctionDistance;
        public readonly List<CourseDraft> Tributaries = new List<CourseDraft>();
        public bool HasLake;
        public RiverSourceLake Lake;
        /// <summary>Flow-coordinate distance below which tributaries may still join this course.</summary>
        public float JoinLimit;
        public bool IsTrunk => Parent == null;
        public float Terminal => IsTrunk ? Sections[MouthSection].Distance : JunctionDistance;
    }

    private sealed class CourseOptions
    {
        public bool Tributary;
        public float JunctionWater;
        /// <summary>Arc length before the confluence in which banks yield to the parent channel.</summary>
        public float JunctionClearance;
        public float StartWater = float.NaN;
        public float LakeRadius;
        /// <summary>(course-local distance, width scale reached after it), ordered downstream.</summary>
        public List<Vector2> Widening = new List<Vector2>();

        public CourseOptions WithWidening(List<Vector2> widening)
            => new CourseOptions
            {
                Tributary = Tributary, JunctionWater = JunctionWater, JunctionClearance = JunctionClearance,
                StartWater = StartWater, LakeRadius = LakeRadius, Widening = widening
            };
    }

    // ------------------------------------------------------------------------------------------
    // Sources
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Ranks drainable land as river sources. Relief and mountain ownership dominate, so courses
    /// start in the ranges and cross the plain, instead of the old rule that only accepted points
    /// far from the shore - which excluded every coastal massif outright. Flow length keeps the
    /// seaward flank of a coastal ridge (a few units to the sea) from outranking its landward side.
    /// </summary>
    private static List<int> RankSources(PlanContext c)
    {
        RiverDrainageField drainage = c.Drainage;
        int n = drainage.Size;
        float minimumLength = c.Rivers.minimumRiverLength;
        float sx = (c.Seed & 511) * 0.13f, sz = ((c.Seed >> 9) & 511) * 0.11f;
        var scored = new List<(int index, float score)>();
        for (int z = 3; z < n - 3; z++)
        for (int x = 3; x < n - 3; x++)
        {
            int i = drainage.Index(x, z);
            if (!drainage.Drains(i) || drainage.FlowLength[i] < c.MinimumTributaryLength) continue;
            var p = new Vector2(x, z);
            float h = drainage.Height[i];
            if (h < c.Sea + 0.95f || c.Ground.Shore(p) < 3f) continue;
            float relief = Mathf.Max(0f, h - c.Flatland);
            float mountain = Mathf.Min(c.Ground.MountainBoost(p), 2.5f);
            float reach = Mathf.Min(drainage.FlowLength[i] / minimumLength, 3f);
            // Prefer divides to channels: a source collects little water of its own.
            float divide = Mathf.Log(1f + drainage.Accumulation[i]) * 0.08f;
            // A mouth on a rocky headland is possible but a beach or bay reads far better.
            int outlet = drainage.Outlet[i];
            float headland = outlet >= 0 ? Mathf.Clamp01(c.Ground.Headland(drainage.Point(outlet))) * 0.6f : 0f;
            float variety = Mathf.PerlinNoise(x * 0.21f + sx, z * 0.21f + sz) * 0.35f;
            scored.Add((i, relief * 1.4f + mountain * 0.6f + reach * 0.45f + variety - divide - headland));
        }
        scored.Sort((a, b) =>
        {
            int s = b.score.CompareTo(a.score);
            return s != 0 ? s : a.index.CompareTo(b.index);
        });
        var result = new List<int>(scored.Count);
        foreach (var entry in scored) result.Add(entry.index);
        return result;
    }

    private static CourseDraft TryCourse(PlanContext c, int source, List<CourseDraft> network, out string reason)
    {
        List<Vector2> raw = c.Drainage.Trace(source);
        if (raw.Count < 4) { reason = "Source has no drainage path to the sea."; return null; }

        Junction join = FindJunction(raw, network, c.Rivers.clearanceRadius, out reason);
        if (reason != null) return null;
        return join.Course != null
            ? TryTributary(c, raw, join, network, out reason)
            : TryTrunk(c, raw, source, network, out reason);
    }

    private readonly struct Junction
    {
        public readonly CourseDraft Course;
        public readonly int Station, RawIndex;
        public readonly float Along;
        public Junction(CourseDraft course, int station, int rawIndex, float along)
        { Course = course; Station = station; RawIndex = rawIndex; Along = along; }
    }

    /// <summary>
    /// Walks the candidate's drainage path downstream and stops at the first point that comes
    /// within clearance of an accepted course. That is where the two waters meet. Exact merges in
    /// the receiver tree have distance zero; near-parallel neighbours are pulled together instead
    /// of being allowed to cut two mouths a few units apart.
    /// </summary>
    private static Junction FindJunction(List<Vector2> raw, List<CourseDraft> network, float clearance, out string reason)
    {
        reason = null;
        float along = 0f;
        for (int k = 0; k < raw.Count; k++)
        {
            if (k > 0) along += Vector2.Distance(raw[k], raw[k - 1]);
            Vector2 p = raw[k];
            CourseDraft best = null;
            int bestStation = -1;
            float bestDistance = float.PositiveInfinity;
            bool blocked = false;
            foreach (CourseDraft course in network)
            {
                for (int j = 0; j < course.Sections.Count; j++)
                {
                    RiverSection s = course.Sections[j];
                    float limit = clearance + s.MaximumWaterWidth;
                    float sq = (s.Centre - p).sqrMagnitude;
                    if (sq > limit * limit) continue;
                    if (!Joinable(course, j)) { blocked = true; continue; }
                    if (sq >= bestDistance) continue;
                    bestDistance = sq; best = course; bestStation = j;
                }
            }
            if (best != null) return new Junction(best, bestStation, k, along);
            if (blocked)
            {
                reason = "Meets an existing river where it cannot join (headwater, drop, lake or estuary).";
                return default;
            }
        }
        return default;
    }

    private static bool Joinable(CourseDraft course, int station)
    {
        RiverSection s = course.Sections[station];
        if (s.Kind == RiverReachKind.Waterfall || s.Kind == RiverReachKind.PlungePool
            || s.Kind == RiverReachKind.Estuary) return false;
        float local = s.Distance - course.Sections[0].Distance;
        if (local < MinimumHeadwaterLength + 2f) return false;
        if (course.HasLake && local < course.Lake.Radius + 2.5f) return false;
        if (s.Distance > course.JoinLimit) return false;
        foreach (CourseDraft t in course.Tributaries)
            if (Mathf.Abs(t.JunctionDistance - s.Distance) < ConfluenceSpacing) return false;
        return true;
    }

    // ------------------------------------------------------------------------------------------
    // Trunks: courses that own an outlet
    // ------------------------------------------------------------------------------------------

    private static CourseDraft TryTrunk(PlanContext c, List<Vector2> raw, int source, List<CourseDraft> network,
        out string reason)
    {
        if (c.Drainage.FlowLength[source] < c.Rivers.minimumRiverLength)
        { reason = "Shorter than the minimum river length and joins no existing river."; return null; }
        Vector2 outletPoint = raw[raw.Count - 1];
        foreach (CourseDraft other in network)
        {
            if (!other.IsTrunk) continue;
            if ((other.OutletPoint - outletPoint).sqrMagnitude < Sq(c.Rivers.clearanceRadius * 2f))
            { reason = "Would cut a second mouth beside an existing outlet."; return null; }
        }

        List<Vector2> path = SmoothAndResample(raw);
        if (!ContinueIntoOcean(path, c.Ground, c.Sea, c.Settings.coastalProfile.surfWidth, out reason)) return null;
        var draft = new CourseDraft { Path = path, OutletPoint = outletPoint };
        var options = new CourseOptions();
        DecideSourceLake(c, draft, options, source, network);

        CourseProposal proposal = SolveCourse(path, c.Ground, c.Settings, c.Seed, options, out reason);
        if (proposal == null) return null;
        if (!ValidateSections(proposal.Sections, c.Ground, proposal.MouthSection, out reason)) return null;
        draft.Options = options;
        draft.BaseSections = draft.Sections = proposal.Sections;
        draft.Falls = proposal.Waterfalls;
        draft.MouthSection = proposal.MouthSection;
        if (!SeparateFromNetwork(draft, draft.Sections, network, out reason)) return null;
        float mouthDistance = draft.Sections[draft.MouthSection].Distance;
        draft.JoinLimit = mouthDistance - MouthTransition(mouthDistance) - ConfluenceWideningRamp;
        return draft;
    }

    // ------------------------------------------------------------------------------------------
    // Tributaries: courses that hand their water to a parent and share its outlet
    // ------------------------------------------------------------------------------------------

    private static CourseDraft TryTributary(PlanContext c, List<Vector2> raw, Junction join,
        List<CourseDraft> network, out string reason)
    {
        if (join.Along < c.MinimumTributaryLength)
        { reason = "Too short above its confluence to form a tributary."; return null; }
        CourseDraft parent = join.Course;
        RiverSection station = parent.Sections[join.Station];

        var pathRaw = raw.GetRange(0, join.RawIndex + 1);
        if (Vector2.Distance(pathRaw[pathRaw.Count - 1], station.Centre) < 0.6f) pathRaw.RemoveAt(pathRaw.Count - 1);
        if (pathRaw.Count < 2) { reason = "Confluence leaves no tributary channel."; return null; }
        pathRaw.Add(station.Centre);
        List<Vector2> path = SmoothAndResample(pathRaw);

        // Real tributaries meet the trunk going with its flow, not driving back up against it.
        Vector2 approach = (path[path.Count - 1] - path[Mathf.Max(0, path.Count - 8)]).normalized;
        Vector2 parentFlow = (parent.Sections[Mathf.Min(parent.Sections.Count - 1, join.Station + 1)].Centre
            - parent.Sections[Mathf.Max(0, join.Station - 1)].Centre).normalized;
        if (Vector2.Dot(approach, parentFlow) < -0.35f)
        { reason = "Would join against the parent's flow."; return null; }

        var draft = new CourseDraft
        {
            Path = path, Parent = parent, ParentSection = join.Station, JunctionDistance = station.Distance
        };
        var options = new CourseOptions
        {
            Tributary = true,
            JunctionWater = station.WaterHeight,
            JunctionClearance = station.MaximumWaterWidth + 1.6f
        };
        DecideSourceLake(c, draft, options, -1, network);

        CourseProposal proposal = SolveCourse(path, c.Ground, c.Settings, c.Seed, options, out reason);
        if (proposal == null) return null;
        if (!ValidateSections(proposal.Sections, c.Ground, -1, out reason)) return null;
        draft.Options = options;
        draft.DistanceOffset = station.Distance - proposal.Sections[proposal.Sections.Count - 1].Distance;
        draft.BaseSections = draft.Sections = Offset(proposal.Sections, draft.DistanceOffset);
        draft.Falls = proposal.Waterfalls;
        if (!SeparateFromNetwork(draft, draft.Sections, network, out reason)) return null;
        draft.JoinLimit = draft.JunctionDistance - (options.JunctionClearance + 2f) - ConfluenceWideningRamp;
        return draft;
    }

    private static List<RiverSection> Offset(List<RiverSection> sections, float offset)
    {
        if (Mathf.Abs(offset) < 0.00001f) return sections;
        var result = new List<RiverSection>(sections.Count);
        foreach (RiverSection s in sections) result.Add(s.WithDistance(s.Distance + offset));
        return result;
    }

    // ------------------------------------------------------------------------------------------
    // Source lakes
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Mountain sources begin at an alpine lake with lakeSourceChance, and natural hollows the
    /// drainage flood had to fill almost always hold one. The lake level is set below the lowest
    /// point of its rim so the basin genuinely contains it; the radius shrinks until the bowl fits
    /// the excavation budget, and the lake is dropped rather than built unsupported.
    /// </summary>
    private static void DecideSourceLake(PlanContext c, CourseDraft draft, CourseOptions options, int sourceCell,
        List<CourseDraft> network)
    {
        Vector2 centre = draft.Path[0];
        int cell = sourceCell >= 0 ? sourceCell
            : c.Drainage.Index(Mathf.RoundToInt(centre.x), Mathf.RoundToInt(centre.y));
        float pond = c.Drainage.PondDepth(cell);
        bool alpine = c.Ground.Height(centre) > c.Flatland + 0.6f || c.Ground.MountainBoost(centre) > 0.25f;
        float chance = pond > 0.12f ? Mathf.Max(c.Rivers.lakeSourceChance, 0.85f) : c.Rivers.lakeSourceChance;
        // Always consume the roll, so whether one course gets a lake never reshuffles the next.
        double roll = c.Random.NextDouble();
        float radius = Mathf.Lerp(c.Rivers.minLakeRadius, c.Rivers.maxLakeRadius, (float)c.Random.NextDouble());
        if ((!alpine && pond <= 0.12f) || roll >= chance) return;

        float depth = c.Rivers.lakeDepth;
        for (; radius >= c.Rivers.minLakeRadius * 0.999f; radius *= 0.82f)
        {
            float rim = float.PositiveInfinity;
            for (int a = 0; a < 20; a++)
            {
                float angle = a / 20f * Mathf.PI * 2f;
                Vector2 q = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius * 1.15f);
                if (q.x < 1f || q.y < 1f || q.x > c.Ground.Size - 1f || q.y > c.Ground.Size - 1f) { rim = float.NegativeInfinity; break; }
                rim = Mathf.Min(rim, c.Ground.Height(q));
            }
            // Same level SolveCourse gives the first station, so the outlet leaves the lake flush.
            float water = Mathf.Min(c.Ground.Height(centre) - 0.4f, rim - 0.12f);
            if (water < c.Sea + 0.6f) continue;
            if (!LakeFitsBudget(c.Ground, centre, radius, water, depth, c.MaximumCut * 0.9f)) continue;
            if (!LakeClearOfNetwork(centre, radius, network)) return;
            draft.HasLake = true;
            draft.Lake = new RiverSourceLake(centre, radius, water, depth);
            options.StartWater = water;
            options.LakeRadius = radius;
            return;
        }
    }

    private static bool LakeFitsBudget(RiverGround ground, Vector2 centre, float radius, float water, float depth, float budget)
    {
        for (float r = 0f; r <= radius; r += Mathf.Max(0.3f, radius / 6f))
        for (int a = 0; a < 16; a++)
        {
            float angle = a / 16f * Mathf.PI * 2f;
            Vector2 q = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            float u = r / radius;
            float bed = water - 0.06f - depth * (1f - u * u);
            if (ground.Height(q) - bed > budget) return false;
        }
        return true;
    }

    private static bool LakeClearOfNetwork(Vector2 centre, float radius, List<CourseDraft> network)
    {
        foreach (CourseDraft course in network)
        {
            foreach (RiverSection s in course.Sections)
                if ((s.Centre - centre).sqrMagnitude < Sq(radius * 1.3f + s.MaximumWaterWidth + 2f)) return false;
            if (course.HasLake && (course.Lake.Centre - centre).sqrMagnitude
                < Sq((course.Lake.Radius + radius) * 1.3f + 2f)) return false;
        }
        return true;
    }

    // ------------------------------------------------------------------------------------------
    // Path geometry
    // ------------------------------------------------------------------------------------------

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
        // Endpoints stay fixed: the source, and for a tributary the parent centreline it joins.
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

    // ------------------------------------------------------------------------------------------
    // Outlet: the shared pour-out of the island. Only trunks run this; tributaries inherit it.
    // ------------------------------------------------------------------------------------------

    private static bool ContinueIntoOcean(List<Vector2> path, RiverGround ground, float sea, float surfWidth, out string reason)
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

    /// <summary>Arc length reserved for the mouth transition on a course of this length to the sea.</summary>
    private static float MouthTransition(float mouthDistance)
        => Mathf.Min(MouthTransitionLength, mouthDistance * MouthTransitionShare);

    // ------------------------------------------------------------------------------------------
    // Hydraulic solve (shared by trunks and tributaries)
    // ------------------------------------------------------------------------------------------

    private static CourseProposal SolveCourse(List<Vector2> path, RiverGround ground,
        TerrainGenerationSettings settings, int seed, CourseOptions options, out string reason)
    {
        int n = path.Count;
        var distance = new float[n]; var height = new float[n]; var water = new float[n];
        var kinds = new RiverReachKind[n]; var poolWeight = new float[n]; var impactFoam = new float[n];
        float sea = settings.waterHeight;
        bool tributary = options.Tributary;
        bool lake = !float.IsNaN(options.StartWater);
        int mouth = -1, estuary = -1;
        for (int i = 0; i < n; i++)
        {
            if (i > 0) distance[i] = distance[i - 1] + Vector2.Distance(path[i], path[i - 1]);
            height[i] = ground.Height(path[i]);
            if (tributary) continue;
            float shore = ground.Shore(path[i]);
            if (mouth < 0 && shore <= 0f) mouth = i;
            if (estuary < 0 && i > n / 3 && shore < settings.coastalProfile.beachWidth + settings.coastalProfile.backshoreWidth + 1f)
                estuary = i;
        }
        if (tributary)
        {
            // A tributary ends on its parent's centreline: there is no estuary and no shoreline.
            if (n < 12) { reason = "Tributary course is too short to resolve."; return null; }
            mouth = n - 1;
            estuary = n;
        }
        else
        {
            if (mouth < 0 || mouth < 20) { reason = "No continuous inland route to a shoreline crossing."; return null; }
            if (estuary < 0) estuary = mouth - 8;
            estuary = Mathf.Min(estuary, mouth - 4);
        }

        var falls = new List<RiverWaterfall>();
        float lastPoolEnd = -10f;
        // Geological drops belong to the inland course. Everything downstream of this arc
        // length is reserved for the mouth transition, or for a tributary the confluence.
        float transition = tributary ? options.JunctionClearance + 2f : MouthTransition(distance[mouth]);
        float fallLimit = distance[mouth] - transition;
        float floor = tributary ? options.JunctionWater : sea + 0.5f;
        water[0] = lake ? Mathf.Min(height[0] - 0.4f, options.StartWater) : height[0] - 0.4f;
        for (int i = 0; i < estuary; i++)
        {
            float previous = i == 0 ? water[0] : water[i - 1];
            float desired = Mathf.Max(floor, height[i] - 0.45f);
            // Inside a source lake the surface is the lake's level, not the ground under the basin.
            water[i] = lake && distance[i] <= options.LakeRadius ? water[0] : Mathf.Min(previous, desired);
            kinds[i] = distance[i] < MinimumHeadwaterLength ? RiverReachKind.Headwater
                : height[i] - water[i] > 0.85f || height[i] > settings.surfaceFlatlandHeight + 0.6f
                ? RiverReachKind.Gorge : RiverReachKind.Lowland;

            // Consolidate a natural mountain descent into a supported crest, steep geological
            // face and receiving basin. Pool and downstream outlet share the same water level.
            int look = Mathf.Min(i + 9, estuary - 5);
            if (distance[i] < MinimumHeadwaterLength || look <= i + 4 || distance[i] - lastPoolEnd < 4f) continue;
            if (lake && distance[i] < options.LakeRadius + 1.5f) continue;
            if (distance[i] > fallLimit) break;
            float lower = Mathf.Max(floor + 0.15f, height[look] - 0.45f);
            float drop = water[i] - lower;
            if (drop < MinimumFall || height[i] - height[look] < MinimumFall || height[i] - height[i + 2] < 0.3f) continue;
            int toe = i + 2;
            int poolEnd = toe;
            while (poolEnd < estuary - 3 && distance[poolEnd] - distance[toe] < PlungePoolLength) poolEnd++;
            if (distance[poolEnd] - distance[toe] < PlungePoolLength * 0.75f) continue;
            // The basin doubles the channel width where it is stamped. Reject the whole drop
            // rather than truncate it into the reserved interval.
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
        // This backstop is measured against the terminal, not the estuary station.
        if (falls.Count > 0 && distance[mouth] - lastPoolEnd < transition)
        { reason = "Plunge pool intrudes into the reserved mouth or confluence transition."; return null; }

        if (tributary)
        {
            // Settle onto the parent's surface across the confluence approach. The floor above
            // already keeps every station at or above the junction level, so this stays monotone.
            float approach = options.JunctionClearance + 1.5f;
            int start = n - 1;
            while (start > 1 && distance[n - 1] - distance[start - 1] <= approach) start--;
            float from = water[start - 1];
            float span = Mathf.Max(0.01f, distance[n - 1] - distance[start - 1]);
            for (int i = start; i < n; i++)
            {
                water[i] = Mathf.Lerp(from, options.JunctionWater, (distance[i] - distance[start - 1]) / span);
                if (kinds[i] == RiverReachKind.Waterfall || kinds[i] == RiverReachKind.PlungePool) kinds[i] = RiverReachKind.Lowland;
            }
        }
        else
        {
            // Mouth is a level ocean connection. The bed, not the water surface, descends into
            // the shelf. Below-sea river bed is confined to this explicit estuary interval.
            float entryWater = water[Mathf.Max(0, estuary - 1)];
            for (int i = estuary; i < n; i++)
            {
                float u = Mathf.Clamp01((distance[i] - distance[estuary - 1]) / (distance[mouth] - distance[estuary - 1]));
                water[i] = Mathf.Lerp(entryWater, sea, u);
                kinds[i] = RiverReachKind.Estuary;
            }
        }

        float junctionReach = options.JunctionClearance + 2f;
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
            float mouthTaper = 1f;
            if (!tributary)
            {
                float mouthApproach = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((distance[mouth] - distance[i]) / transition));
                mouthTaper = Mathf.Lerp(MouthChannelScale, 1f, mouthApproach);
            }
            float openRadius = Mathf.Clamp(settings.riverCorridors.channelRadius, 0.6f, 2f)
                * Mathf.Lerp(0.48f, 1f, downstream) * Mathf.Lerp(0.85f, 1.12f, variation)
                * (tributary ? TributaryWidthScale : 1f) * WideningAt(options.Widening, distance[i]);
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
            // A lake-fed course leaves its basin at full width instead.
            float sourceOpening = lake ? 1f : Mathf.SmoothStep(0.015f, 1f, Mathf.Clamp01(distance[i] / 1.8f));
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
            // Approaching a confluence the parent's own valley is the bank. A full tributary
            // bank there would stamp bench and wall across the parent's channel.
            if (tributary)
            {
                float toJunction = distance[n - 1] - distance[i];
                if (toJunction < junctionReach)
                {
                    float yield = Mathf.Lerp(0.2f, 1f, Mathf.SmoothStep(0f, 1f, toJunction / junctionReach));
                    left = Narrow(left, yield); right = Narrow(right, yield);
                }
            }
            float foam = kinds[i] == RiverReachKind.PlungePool
                ? Mathf.Lerp(0.06f, 0.82f, impactFoam[i])
                : kinds[i] == RiverReachKind.Waterfall ? 0.5f : 0.025f;
            // Hand off to the ocean shortly after the crossing. The course still routes to the
            // shelf so the bed is continuous, but rendered inland water must not tile the whole
            // submerged continuation - the ocean already covers it.
            float oceanBlend = tributary ? 0f : Mathf.Clamp01((distance[i] - distance[mouth]) / OceanHandoffLength);
            sections.Add(new RiverSection(path[i], lateral, distance[i], water[i], bed,
                leftWidth, rightWidth, left, right, kinds[i], foam, oceanBlend));
        }
        reason = null;
        return new CourseProposal(sections, falls, tributary ? -1 : mouth);
    }

    /// <summary>Channel width scale after the confluences recorded upstream of this distance.</summary>
    private static float WideningAt(List<Vector2> widening, float distance)
    {
        float scale = 1f;
        foreach (Vector2 entry in widening)
            scale = Mathf.Lerp(scale, entry.y, Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((distance - entry.x) / ConfluenceWideningRamp)));
        return scale;
    }

    private static RiverBankProfile Bank(RiverGround ground, Vector2 centre, Vector2 outward, float waterWidth,
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

    /// <summary>Scales a bank's lateral run without touching its rim elevation or rock share.</summary>
    private static RiverBankProfile Narrow(RiverBankProfile bank, float scale)
        => new RiverBankProfile(bank.ShallowWidth * scale, bank.BenchWidth * scale,
            bank.WallWidth * scale, bank.BlendWidth * scale, bank.RimHeight, bank.RockWeight);

    // ------------------------------------------------------------------------------------------
    // Confluence widening
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A channel carries the water of everything that joined it. Discharge is counted in courses
    /// (each course one unit plus all of its own tributaries); width grows with roughly the cube
    /// root of discharge below each confluence. Water levels do not depend on width, so the
    /// re-solve keeps every confluence's junction level intact. A course whose widened solve no
    /// longer validates keeps its unwidened sections.
    /// </summary>
    private static void ApplyConfluenceWidening(PlanContext c, List<CourseDraft> drafts)
    {
        var discharge = new Dictionary<CourseDraft, float>();
        for (int k = drafts.Count - 1; k >= 0; k--) Discharge(drafts[k]);
        foreach (CourseDraft draft in drafts) draft.Sections = draft.BaseSections;

        foreach (CourseDraft draft in drafts)
        {
            if (draft.Tributaries.Count == 0) continue;
            var joins = new List<CourseDraft>(draft.Tributaries);
            joins.Sort((a, b) => a.JunctionDistance.CompareTo(b.JunctionDistance));
            var widening = new List<Vector2>();
            float carried = 1f;
            foreach (CourseDraft t in joins)
            {
                carried += discharge[t];
                widening.Add(new Vector2(t.JunctionDistance - draft.DistanceOffset,
                    Mathf.Min(MaximumConfluenceWidening, Mathf.Pow(carried, 0.33f))));
            }
            CourseOptions options = draft.Options.WithWidening(widening);
            CourseProposal proposal = SolveCourse(draft.Path, c.Ground, c.Settings, c.Seed, options, out _);
            if (proposal == null || proposal.Sections.Count != draft.BaseSections.Count) continue;
            if (!ValidateSections(proposal.Sections, c.Ground, proposal.MouthSection, out _)) continue;
            List<RiverSection> widened = Offset(proposal.Sections, draft.DistanceOffset);
            var others = new List<CourseDraft>(drafts);
            others.Remove(draft);
            if (!SeparateFromNetwork(draft, widened, others, out _)) continue;
            draft.Sections = widened;
        }

        float Discharge(CourseDraft d)
        {
            if (discharge.TryGetValue(d, out float known)) return known;
            float total = 1f;
            foreach (CourseDraft t in d.Tributaries) total += Discharge(t);
            discharge[d] = total;
            return total;
        }
    }

    // ------------------------------------------------------------------------------------------
    // Validation
    // ------------------------------------------------------------------------------------------

    private static bool ValidateSections(IReadOnlyList<RiverSection> sections, RiverGround ground,
        int mouthSection, out string reason)
    {
        // Tributaries have no shoreline crossing and therefore no mouth silhouette to keep.
        if (mouthSection >= 0 && !ValidateMouthSilhouette(sections, mouthSection, out reason)) return false;
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
            // share banks; distant sections must have separate wet footprints.
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
        float origin = sections[0].Distance;
        float intervalStart = sections[mouthSection].Distance
            - MouthTransition(sections[mouthSection].Distance - origin);
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

    /// <summary>
    /// Courses may only touch where one hands its water to the other. Anywhere else two wet
    /// footprints must stay apart, exactly like the self-overlap rule within one course.
    /// </summary>
    private static bool SeparateFromNetwork(CourseDraft self, List<RiverSection> sections, List<CourseDraft> network,
        out string reason)
    {
        float selfEnd = sections[sections.Count - 1].Distance;
        float selfClearance = self.Options != null ? self.Options.JunctionClearance : 0f;
        foreach (CourseDraft other in network)
        {
            if (other == self) continue;
            bool otherIsParent = other == self.Parent;
            bool otherIsChild = other.Parent == self;
            float otherEnd = other.Sections[other.Sections.Count - 1].Distance;
            float otherClearance = other.Options != null ? other.Options.JunctionClearance : 0f;
            foreach (RiverSection s in sections)
            {
                if (otherIsParent && selfEnd - s.Distance < selfClearance + 2.5f) continue;
                if (otherIsChild && Mathf.Abs(s.Distance - other.JunctionDistance) < otherClearance + 3f) continue;
                foreach (RiverSection t in other.Sections)
                {
                    if (otherIsParent && Mathf.Abs(t.Distance - self.JunctionDistance) < selfClearance + 3f) continue;
                    if (otherIsChild && otherEnd - t.Distance < otherClearance + 2.5f) continue;
                    float min = s.MaximumWaterWidth + t.MaximumWaterWidth + 1f;
                    if ((s.Centre - t.Centre).sqrMagnitude < min * min)
                    { reason = "Crosses another river's channel away from a confluence."; return false; }
                }
                if (other.HasLake && (s.Centre - other.Lake.Centre).sqrMagnitude
                    < Sq(other.Lake.Radius * 1.2f + s.MaximumWaterWidth + 1f))
                { reason = "Runs through another river's source lake."; return false; }
            }
        }
        reason = null; return true;
    }

    /// <summary>
    /// Exact resolved-field checks over the merged network landform. Returns the index of the
    /// course to drop, or -1. A failure is blamed on the course that owns the failing cell; at a
    /// confluence the tributary yields rather than taking its parent (and the outlet) with it.
    /// </summary>
    private static int ValidateLandform(RiverLandform landform, List<CourseDraft> drafts, RiverGround ground,
        float sea, float maxCut, out string reason)
    {
        for (int c = 0; c < drafts.Count; c++)
        {
            CourseDraft draft = drafts[c];
            foreach (RiverSection s in draft.Sections)
            for (float across = -s.LeftWaterWidth - s.LeftBank.Extent;
                across <= s.RightWaterWidth + s.RightBank.Extent; across += 0.3f)
            {
                Vector2 p = s.Centre + s.Lateral * across;
                if (!CheckPoint(p, out string why, out int owner)) continue;
                reason = why;
                return Blame(drafts, c, owner, s.Distance);
            }
            if (!draft.HasLake) continue;
            RiverSourceLake lake = draft.Lake;
            for (float r = 0f; r <= lake.Radius * 1.15f; r += 0.3f)
            for (int a = 0; a < 24; a++)
            {
                float angle = a / 24f * Mathf.PI * 2f;
                Vector2 p = lake.Centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                if (!CheckPoint(p, out string why, out int owner)) continue;
                reason = "Source lake: " + why;
                return owner >= 0 && owner < drafts.Count ? owner : c;
            }
        }
        reason = null;
        return -1;

        bool CheckPoint(Vector2 p, out string why, out int owner)
        {
            why = null; owner = -1;
            if (!landform.TrySample(p, out RiverSurfaceSample sample)) return false;
            owner = sample.Course;
            float original = ground.Height(p), result = sample.Apply(original), delta = result - original;
            if (delta > MaximumFill + 0.01f || -delta > maxCut + 0.01f)
            { why = $"Landform budget exceeded (cut {-delta:F2}, fill {delta:F2})."; return true; }
            // The wet contour has zero depth at its bank. Channel depth is checked at sections;
            // reject penetration here, not the legitimate shallow edge of that contour.
            if (sample.IsWater && result > sample.WaterHeight + 0.001f)
            { why = "Terrain obstructs the water footprint."; return true; }
            if (sample.Kind != RiverReachKind.Estuary && sample.IsWater && result < sea)
            { why = "Inland course would intersect the ocean plane."; return true; }
            return false;
        }
    }

    private static int Blame(List<CourseDraft> drafts, int scanned, int owner, float distance)
    {
        int blamed = owner >= 0 && owner < drafts.Count ? owner : scanned;
        CourseDraft course = drafts[blamed];
        // Near one of its confluences the parent is innocent: the junction is the tributary's.
        CourseDraft nearest = null;
        float best = float.PositiveInfinity;
        foreach (CourseDraft t in course.Tributaries)
        {
            float gap = Mathf.Abs(t.JunctionDistance - distance);
            float reach = (t.Options != null ? t.Options.JunctionClearance : 0f) + 4f;
            if (gap < reach && gap < best) { best = gap; nearest = t; }
        }
        return nearest != null ? drafts.IndexOf(nearest) : blamed;
    }

    // ------------------------------------------------------------------------------------------
    // Assembly
    // ------------------------------------------------------------------------------------------

    private static RiverLandform BuildLandform(TerrainSampleCache terrain, List<CourseDraft> drafts, float maxCut,
        int seed, RiverLandform.Buffers buffers)
    {
        var courses = new List<RiverLandform.Course>(drafts.Count);
        foreach (CourseDraft d in drafts)
            courses.Add(new RiverLandform.Course(d.Sections.AsReadOnly(), d.Falls.AsReadOnly(), d.HasLake, d.Lake));
        return new RiverLandform(terrain, courses, maxCut, MaximumFill, seed, buffers);
    }

    private static void RemoveWithDescendants(List<CourseDraft> drafts, CourseDraft root)
    {
        var doomed = new HashSet<CourseDraft> { root };
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (CourseDraft d in drafts)
                if (d.Parent != null && doomed.Contains(d.Parent) && doomed.Add(d)) grew = true;
        }
        drafts.RemoveAll(doomed.Contains);
        root.Parent?.Tributaries.Remove(root);
    }

    private static RiverNetwork Assemble(List<CourseDraft> drafts, RiverLandform landform, float maxCut)
    {
        var plans = new List<RiverPlan>(drafts.Count);
        for (int i = 0; i < drafts.Count; i++)
        {
            CourseDraft d = drafts[i];
            int parent = d.Parent != null ? drafts.IndexOf(d.Parent) : -1;
            RiverConfluence confluence = parent >= 0
                ? new RiverConfluence(parent, d.ParentSection, d.Parent.Sections[d.ParentSection])
                : default;
            plans.Add(new RiverPlan(i, d.Sections, d.Falls, parent, confluence, d.Terminal,
                d.HasLake, d.Lake, maxCut, MaximumFill));
        }

        var outlets = new List<RiverOutlet>();
        for (int i = 0; i < drafts.Count; i++)
        {
            CourseDraft trunk = drafts[i];
            if (!trunk.IsTrunk) continue;
            var members = new List<int> { i };
            for (int j = 0; j < drafts.Count; j++)
            {
                if (j == i) continue;
                CourseDraft root = drafts[j];
                while (root.Parent != null) root = root.Parent;
                if (root == trunk) members.Add(j);
            }
            int m = Mathf.Clamp(trunk.MouthSection, 1, trunk.Sections.Count - 2);
            var mouth = new RiverMouth(trunk.Sections[m],
                (trunk.Sections[m + 1].Centre - trunk.Sections[m - 1].Centre).normalized);
            outlets.Add(new RiverOutlet(outlets.Count, mouth, i, members));
        }
        return new RiverNetwork(plans, outlets, landform, maxCut, MaximumFill);
    }

    private static string Describe(RiverNetwork network, int target, int minimum, int trials,
        Dictionary<string, int> rejections, float maxCut, float sea)
    {
        int tributaries = 0, lakes = 0, drops = 0;
        foreach (RiverPlan course in network.Courses)
        {
            if (course.IsTributary) tributaries++;
            if (course.HasSourceLake) lakes++;
            drops += course.Waterfalls.Count;
        }
        var text = new System.Text.StringBuilder();
        text.Append($"accepted {network.Courses.Count}/{target} courses after {trials} source attempts: ");
        text.Append($"{network.Outlets.Count} outlet(s), {tributaries} tributar{(tributaries == 1 ? "y" : "ies")}, ");
        text.Append($"{lakes} source lake(s), {drops} geological drop(s); cut <= {maxCut:F2}, fill <= {MaximumFill:F2}, water {sea:F2}.");
        foreach (RiverOutlet outlet in network.Outlets)
            text.Append($" Outlet {outlet.Index} at ({outlet.Mouth.Centre.x:F1}, {outlet.Mouth.Centre.y:F1}) drains {outlet.Courses.Count} course(s).");
        foreach (RiverPlan course in network.Courses)
            text.Append($" [{course.Index}] {(course.IsTributary ? $"tributary of {course.Parent}" : "trunk")}, " +
                $"{course.Length:F1} units from ({course.Source.x:F1}, {course.Source.y:F1})" +
                $"{(course.HasSourceLake ? $", lake r={course.SourceLake.Radius:F1}" : "")}.");
        if (network.Courses.Count < minimum) text.Append($" Below minRivers ({minimum}).");
        if (rejections.Count > 0) text.Append(" Rejected: ").Append(Summarise(rejections));
        return text.ToString();
    }

    // ------------------------------------------------------------------------------------------
    // Small helpers
    // ------------------------------------------------------------------------------------------

    private static float Sq(float v) => v * v;

    private static bool WithinAny(List<Vector2> points, Vector2 p, float radius)
    {
        foreach (Vector2 q in points) if ((q - p).sqrMagnitude < radius * radius) return true;
        return false;
    }

    private static bool NearNetwork(List<CourseDraft> network, Vector2 p, float radius)
    {
        foreach (CourseDraft course in network)
        {
            foreach (RiverSection s in course.Sections)
                if ((s.Centre - p).sqrMagnitude < Sq(radius + s.MaximumWaterWidth)) return true;
            if (course.HasLake && (course.Lake.Centre - p).sqrMagnitude < Sq(radius + course.Lake.Radius)) return true;
        }
        return false;
    }

    private static void Tally(Dictionary<string, int> rejections, string reason)
    {
        if (string.IsNullOrEmpty(reason)) reason = "Unspecified rejection.";
        rejections.TryGetValue(reason, out int count);
        rejections[reason] = count + 1;
    }

    private static string Summarise(Dictionary<string, int> rejections)
    {
        if (rejections.Count == 0) return "no inland source with sufficient freeboard and room for banks.";
        var entries = new List<KeyValuePair<string, int>>(rejections);
        entries.Sort((a, b) => b.Value.CompareTo(a.Value));
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < entries.Count && i < 4; i++)
        {
            if (i > 0) text.Append(' ');
            text.Append($"{entries[i].Value}x {entries[i].Key}");
        }
        return text.ToString();
    }

    private sealed class CourseProposal
    {
        public readonly List<RiverSection> Sections;
        public readonly List<RiverWaterfall> Waterfalls;
        public readonly int MouthSection;
        public CourseProposal(List<RiverSection> sections, List<RiverWaterfall> waterfalls, int mouthSection)
        { Sections = sections; Waterfalls = waterfalls; MouthSection = mouthSection; }
    }
}
