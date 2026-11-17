// tldr: Turns a river's centreline stations into a two-dimensional FIELD - bed, banks, water surface, rock and
// flow - that terrain can be sampled against and a water mesh built from.

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The resolved two-dimensional river landscape. A routed course is only a hydraulic guide:
/// this module grows an irregular valley into the existing terrain, cuts a thalweg through it,
/// forms plunge basins in area rather than by widening one station, and extracts the water mesh
/// from that same field. Terrain, material, classification and water therefore share one shape.
/// </summary>
internal sealed class RiverLandform
{
    internal sealed class Buffers
    {
        public readonly float[] Influence, WeightedTarget, WaterHeight, Distance, WetDistance;
        public readonly float[] Rock, Gravel, DirectionX, DirectionY, Foam, Edge, AcrossCoordinate, OceanBlend;
        public readonly float[] Ownership, SegmentWeightSum;
        public readonly RiverReachKind[] Kind;
        public readonly short[] CourseId;

        public Buffers(int count)
        {
            Influence = new float[count]; WeightedTarget = new float[count]; WaterHeight = new float[count];
            Distance = new float[count]; WetDistance = new float[count]; Rock = new float[count];
            Gravel = new float[count]; DirectionX = new float[count]; DirectionY = new float[count];
            Foam = new float[count]; Edge = new float[count]; AcrossCoordinate = new float[count];
            OceanBlend = new float[count]; Ownership = new float[count]; SegmentWeightSum = new float[count];
            Kind = new RiverReachKind[count];
            CourseId = new short[count];
        }

        public void Reset()
        {
            Array.Clear(Influence, 0, Influence.Length); Array.Clear(WeightedTarget, 0, WeightedTarget.Length);
            Array.Clear(WaterHeight, 0, WaterHeight.Length); Array.Clear(Distance, 0, Distance.Length);
            Array.Clear(Rock, 0, Rock.Length); Array.Clear(Gravel, 0, Gravel.Length);
            Array.Clear(DirectionX, 0, DirectionX.Length); Array.Clear(DirectionY, 0, DirectionY.Length);
            Array.Clear(Foam, 0, Foam.Length); Array.Clear(Edge, 0, Edge.Length);
            Array.Clear(AcrossCoordinate, 0, AcrossCoordinate.Length); Array.Clear(OceanBlend, 0, OceanBlend.Length);
            Array.Clear(SegmentWeightSum, 0, SegmentWeightSum.Length);
            Array.Clear(Kind, 0, Kind.Length);
            Array.Fill(WetDistance, -1000f);
            Array.Fill(Ownership, float.PositiveInfinity);
            Array.Fill(CourseId, (short)-1);
        }
    }

    /// <summary>The landform's view of one course: hydraulic stations, drops and optional source lake.</summary>
    internal readonly struct Course
    {
        public readonly IReadOnlyList<RiverSection> Sections;
        public readonly IReadOnlyList<RiverWaterfall> Waterfalls;
        public readonly bool HasLake;
        public readonly RiverSourceLake Lake;

        public Course(IReadOnlyList<RiverSection> sections, IReadOnlyList<RiverWaterfall> waterfalls,
            bool hasLake, RiverSourceLake lake)
        {
            Sections = sections;
            Waterfalls = waterfalls;
            HasLake = hasLake;
            Lake = lake;
        }
    }

    private readonly int resolution;
    private readonly float step;
    private readonly IReadOnlyList<Course> courses;
    private readonly short[] courseId;

    private readonly float[] influence;
    private readonly float[] weightedTarget;
    private readonly float[] waterHeight;
    private readonly float[] distance;
    private readonly float[] wetDistance;
    private readonly float[] rock;
    private readonly float[] gravel;
    private readonly float[] directionX;
    private readonly float[] directionY;
    private readonly float[] foam;
    private readonly float[] edge;
    private readonly float[] acrossCoordinate;
    private readonly float[] oceanBlend;
    private readonly RiverReachKind[] kind;

    /// <summary>
    /// Stamps every course of a network into ONE field. Valley segments of all courses accumulate
    /// into the same weighted sums before normalisation, which is what turns a confluence into a
    /// single blended channel instead of two overlapping stamps where one arbitrarily wins.
    /// The buffers are owned by this landform from here on; never pass them to another one.
    /// </summary>
    public RiverLandform(TerrainSampleCache terrain, IReadOnlyList<Course> courses, float maximumCut,
        float maximumFill, int seed, Buffers buffers = null)
    {
        resolution = terrain.Resolution;
        step = terrain.Step;
        this.courses = courses;
        int count = resolution * resolution;
        buffers = buffers != null && buffers.Influence.Length == count ? buffers : new Buffers(count);
        buffers.Reset();
        influence = buffers.Influence; weightedTarget = buffers.WeightedTarget; waterHeight = buffers.WaterHeight;
        distance = buffers.Distance; wetDistance = buffers.WetDistance; rock = buffers.Rock; gravel = buffers.Gravel;
        directionX = buffers.DirectionX; directionY = buffers.DirectionY; foam = buffers.Foam; edge = buffers.Edge;
        acrossCoordinate = buffers.AcrossCoordinate; oceanBlend = buffers.OceanBlend; kind = buffers.Kind;
        courseId = buffers.CourseId;
        float[] ownership = buffers.Ownership;
        float[] segmentWeightSum = buffers.SegmentWeightSum;

        // Valley first. The station list supplies grade and reach constraints, while the field
        // boundary is resolved in world space against slope, rock and coherent erosion noise.
        for (int c = 0; c < courses.Count; c++)
        {
            IReadOnlyList<RiverSection> sections = courses[c].Sections;
            for (int i = 0; i < sections.Count - 1; i++)
                StampValleySegment(terrain, ownership, segmentWeightSum, sections, (short)c, i,
                    maximumCut, maximumFill, seed);
        }
        NormalizeValleySegments(segmentWeightSum);

        // Pools are terrain basins with an impact end and a constrained outlet. They replace the
        // centreline stamp inside their footprint rather than scaling the river strip sideways.
        for (int c = 0; c < courses.Count; c++)
        foreach (RiverWaterfall fall in courses[c].Waterfalls)
            StampPlungeBasin(terrain, ownership, courses[c].Sections, (short)c, fall, maximumCut, maximumFill, seed);

        // Source lakes last: a lake is a closed basin the headwater drains out of, so it must own
        // its own footprint, but it yields to the outlet channel already cut through its rim.
        for (int c = 0; c < courses.Count; c++)
            if (courses[c].HasLake)
                StampSourceLake(terrain, courses[c], (short)c, maximumCut, maximumFill, seed);
    }

    public bool TrySample(Vector2 point, out RiverSurfaceSample result)
    {
        result = default;
        float gx = point.x / step;
        float gz = point.y / step;
        if (gx < 0f || gz < 0f || gx > resolution - 1f || gz > resolution - 1f) return false;
        int x0 = Mathf.Clamp(Mathf.FloorToInt(gx), 0, resolution - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt(gz), 0, resolution - 1);
        int x1 = Mathf.Min(x0 + 1, resolution - 1);
        int z1 = Mathf.Min(z0 + 1, resolution - 1);
        float tx = gx - x0;
        float tz = gz - z0;
        float inf = Bilinear(influence, x0, z0, x1, z1, tx, tz);
        if (inf < 0.0001f) return false;

        int nearestX = tx < 0.5f ? x0 : x1;
        int nearestZ = tz < 0.5f ? z0 : z1;
        int nearest = nearestZ * resolution + nearestX;
        float wet = Bilinear(wetDistance, x0, z0, x1, z1, tx, tz);
        Vector2 flow = new Vector2(
            Bilinear(directionX, x0, z0, x1, z1, tx, tz),
            Bilinear(directionY, x0, z0, x1, z1, tx, tz)).normalized;
        result = new RiverSurfaceSample(
            wet >= 0f,
            Bilinear(weightedTarget, x0, z0, x1, z1, tx, tz),
            inf,
            Bilinear(waterHeight, x0, z0, x1, z1, tx, tz),
            Bilinear(distance, x0, z0, x1, z1, tx, tz),
            Bilinear(rock, x0, z0, x1, z1, tx, tz),
            Bilinear(gravel, x0, z0, x1, z1, tx, tz),
            flow,
            kind[nearest],
            courseId[nearest]);
        return true;
    }

    private void StampValleySegment(TerrainSampleCache terrain, float[] ownership, float[] segmentWeightSum,
        IReadOnlyList<RiverSection> sections, short course, int sectionIndex,
        float maximumCut, float maximumFill, int seed)
    {
        RiverSection a = sections[sectionIndex];
        RiverSection b = sections[sectionIndex + 1];
        Vector2 segment = b.Centre - a.Centre;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared < 0.00001f) return;
        float maximumExtent = Mathf.Max(
            Mathf.Max(a.LeftWaterWidth + a.LeftBank.Extent, a.RightWaterWidth + a.RightBank.Extent),
            Mathf.Max(b.LeftWaterWidth + b.LeftBank.Extent, b.RightWaterWidth + b.RightBank.Extent));
        maximumExtent = maximumExtent * 1.28f + step * 2f;
        int minX = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.Centre.x, b.Centre.x) - maximumExtent) / step));
        int maxX = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(a.Centre.x, b.Centre.x) + maximumExtent) / step));
        int minZ = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.Centre.y, b.Centre.y) - maximumExtent) / step));
        int maxZ = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(a.Centre.y, b.Centre.y) + maximumExtent) / step));
        Vector2 flow = segment.normalized;

        for (int z = minZ; z <= maxZ; z++)
        for (int x = minX; x <= maxX; x++)
        {
            Vector2 p = new Vector2(x * step, z * step);
            float projected = Vector2.Dot(p - a.Centre, segment) / lengthSquared;
            float along = Mathf.Clamp01(projected);
            Vector2 centre = Vector2.Lerp(a.Centre, b.Centre, along);
            Vector2 lateral = Vector2.Lerp(a.Lateral, b.Lateral, along).normalized;
            if (lateral.sqrMagnitude < 0.5f) lateral = new Vector2(-flow.y, flow.x);
            float signed = Vector2.Dot(p - centre, lateral);
            bool leftSide = signed < 0f;
            // Include the longitudinal residual at endpoints. Lateral distance alone turns
            // every short station into an infinite strip, allowing upstream grades to own
            // unrelated downstream terrain.
            float across = Vector2.Distance(p, centre);
            RiverBankProfile bank = LerpBank(leftSide ? a.LeftBank : a.RightBank,
                leftSide ? b.LeftBank : b.RightBank, along);
            float authoredWaterWidth = Mathf.Lerp(
                leftSide ? a.LeftWaterWidth : a.RightWaterWidth,
                leftSide ? b.LeftWaterWidth : b.RightWaterWidth, along);

            int index = z * resolution + x;
            float macro = ErosionNoise(p, seed);
            float rockConstraint = Mathf.Clamp01(terrain.Slopes[index] * 0.85f
                + terrain.MountainBoosts[index] * 0.18f);
            float channelVariation = 1f + macro * 0.1f;
            if (rockConstraint > 0.35f)
                channelVariation *= Mathf.Lerp(1f, 0.72f, rockConstraint * Mathf.Max(0f, macro));
            float wetWidth = Mathf.Max(0.025f, authoredWaterWidth * channelVariation);
            float valleyVariation = Mathf.Max(0.25f, 1f + macro * 0.16f);
            float outer = wetWidth + bank.Extent * valleyVariation;
            if (across >= outer) continue;

            float feather = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((outer - across) / Mathf.Max(0.15f, outer * 0.25f)));
            if (feather <= 0.0001f) continue;
            float denom = 0.08f + across;
            float segW = feather / (denom * denom * denom * denom);

            // Track closest finite hydraulic segment for discrete reach kind while blending fields continuously.
            RiverReachKind reachKind = along < 0.5f ? a.Kind : b.Kind;
            float score = (p - centre).sqrMagnitude;
            if (score < ownership[index])
            {
                ownership[index] = score;
                kind[index] = reachKind;
                courseId[index] = course;
            }

            float longitudinalDistance = Mathf.Lerp(a.Distance, b.Distance, along);
            float water = Mathf.Lerp(a.WaterHeight, b.WaterHeight, along);
            float bed = Mathf.Lerp(a.BedHeight, b.BedHeight, along);
            float original = terrain.Heights[index];
            float signedWet = wetWidth - across;
            float target;
            float weight;
            float rockWeight;
            float gravelWeight;

            if (signedWet >= 0f)
            {
                float cross = across / Mathf.Max(0.025f, wetWidth);
                // The thalweg is offset by the asymmetric widths and is materially deeper than
                // the margins. It is a bed in the 2D field, not a flat strip under a water mesh.
                target = Mathf.Lerp(bed, water - 0.06f, Mathf.Pow(cross, 1.55f));
                target -= Mathf.Max(0f, macro) * 0.035f * (1f - cross);
                weight = 1f;
                rockWeight = Mathf.Max(0.08f, rockConstraint * 0.58f);
                gravelWeight = Mathf.Lerp(0.38f, 0.9f, cross);
            }
            else
            {
                float bankDistance = (across - wetWidth) / valleyVariation;
                ResolveNaturalBank(original, water, bank, bankDistance, macro, rockConstraint,
                    out target, out weight, out rockWeight, out gravelWeight);
            }

            target = Mathf.Clamp(target, original - maximumCut, original + maximumFill);
            if (reachKind == RiverReachKind.Estuary) target = Mathf.Min(original, target);
            // Depth is the contour authority inland, including the shallow margin, so a nominal
            // radius does not leave submerged gravel outside the rendered water footprint.
            // WetSignal adds the bounds depth alone cannot express below the shoreline.
            float finished = Mathf.Lerp(original, target, weight);
            float ocean = Mathf.Lerp(a.OceanBlend, b.OceanBlend, along);
            Accumulate(index, segW, segmentWeightSum, target, weight, water, longitudinalDistance,
                WetSignal(water - finished, across, wetWidth, bank, reachKind, ocean), rockWeight,
                gravelWeight, flow, Mathf.Lerp(a.Foam, b.Foam, along), ocean, wetWidth,
                Mathf.Clamp01(0.5f + signed / (2f * Mathf.Max(0.025f, wetWidth))));
        }
    }

    private void Accumulate(int index, float segW, float[] segmentWeightSum, float target, float weight, float water,
        float atDistance, float signedWet, float rockWeight, float gravelWeight, Vector2 flow, float foamWeight,
        float ocean, float wetWidth, float across)
    {
        if (segmentWeightSum[index] <= 0f) wetDistance[index] = 0f;
        segmentWeightSum[index] += segW;
        influence[index] += segW * weight;
        weightedTarget[index] += segW * (target * weight);
        waterHeight[index] += segW * water;
        distance[index] += segW * atDistance;
        wetDistance[index] += segW * signedWet;
        rock[index] += segW * rockWeight;
        gravel[index] += segW * gravelWeight;
        directionX[index] += segW * flow.x;
        directionY[index] += segW * flow.y;
        foam[index] += segW * foamWeight;
        edge[index] += segW * (signedWet >= 0f ? 1f - Mathf.Clamp01(signedWet / Mathf.Max(0.025f, wetWidth)) : 0f);
        acrossCoordinate[index] += segW * across;
        oceanBlend[index] += segW * ocean;
    }

    private void NormalizeValleySegments(float[] segmentWeightSum)
    {
        for (int i = 0; i < segmentWeightSum.Length; i++)
        {
            float w = segmentWeightSum[i];
            if (w <= 0f) continue;
            float inv = 1f / w;
            influence[i] *= inv;
            weightedTarget[i] *= inv;
            waterHeight[i] *= inv;
            distance[i] *= inv;
            wetDistance[i] *= inv;
            rock[i] *= inv;
            gravel[i] *= inv;
            directionX[i] *= inv;
            directionY[i] *= inv;
            foam[i] *= inv;
            edge[i] *= inv;
            acrossCoordinate[i] *= inv;
            oceanBlend[i] *= inv;
        }
    }

    private void StampPlungeBasin(TerrainSampleCache terrain, float[] ownership, IReadOnlyList<RiverSection> sections,
        short course, RiverWaterfall fall, float maximumCut, float maximumFill, int seed)
    {
        RiverSection toe = sections[fall.ToeSection];
        RiverSection outlet = sections[fall.PoolEndSection];
        Vector2 axisVector = outlet.Centre - toe.Centre;
        float length = axisVector.magnitude;
        if (length < 0.5f) return;
        Vector2 axis = axisVector / length;
        Vector2 lateral = new Vector2(-axis.y, axis.x);
        float maximumWidth = Mathf.Max(toe.MaximumWaterWidth, outlet.MaximumWaterWidth) * 2.05f;
        float bankExtent = Mathf.Max(toe.LeftBank.Extent, Mathf.Max(toe.RightBank.Extent,
            Mathf.Max(outlet.LeftBank.Extent, outlet.RightBank.Extent)));
        float extent = maximumWidth + bankExtent * 1.2f;
        int minX = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(toe.Centre.x, outlet.Centre.x) - extent) / step));
        int maxX = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(toe.Centre.x, outlet.Centre.x) + extent) / step));
        int minZ = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(toe.Centre.y, outlet.Centre.y) - extent) / step));
        int maxZ = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(toe.Centre.y, outlet.Centre.y) + extent) / step));

        for (int z = minZ; z <= maxZ; z++)
        for (int x = minX; x <= maxX; x++)
        {
            Vector2 p = new Vector2(x * step, z * step);
            float alongWorld = Vector2.Dot(p - toe.Centre, axis);
            if (alongWorld < 0f || alongWorld > length) continue;
            float u = Mathf.Clamp01(alongWorld / length);
            float signed = Vector2.Dot(p - (toe.Centre + axis * alongWorld), lateral);
            bool leftSide = signed < 0f;
            float across = Mathf.Abs(signed);
            float baseWidth = Mathf.Lerp(
                leftSide ? toe.LeftWaterWidth : toe.RightWaterWidth,
                leftSide ? outlet.LeftWaterWidth : outlet.RightWaterWidth, u);
            float opening = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.18f));
            float narrowing = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.5f) / 0.5f));
            float basin = opening * narrowing;
            float sideBias = ErosionNoise(p + lateral * (leftSide ? -2.7f : 3.9f), seed + 317) * 0.32f;
            float macro = ErosionNoise(p, seed + 911);
            float wetWidth = baseWidth * (1f + basin * (0.58f + sideBias) + macro * 0.08f);
            RiverBankProfile bank = LerpBank(leftSide ? toe.LeftBank : toe.RightBank,
                leftSide ? outlet.LeftBank : outlet.RightBank, u);
            int index = z * resolution + x;
            float rockConstraint = Mathf.Clamp01(terrain.Slopes[index] * 0.85f
                + terrain.MountainBoosts[index] * 0.18f);
            // Resistant terrain constrains the basin before the water contour is extracted.
            wetWidth *= Mathf.Lerp(1f, 0.78f, rockConstraint * basin);
            float basinBankVariation = Mathf.Max(0.25f, 1f + macro * 0.13f);
            float outer = wetWidth + bank.Extent * basinBankVariation;
            if (across > outer) continue;

            float score = -1f + across / Mathf.Max(0.05f, outer) * 0.5f;
            if (score >= ownership[index]) continue;
            ownership[index] = score;
            float original = terrain.Heights[index];
            float signedWet = wetWidth - across;
            float water = toe.WaterHeight;
            float target;
            float weight;
            float rockWeight;
            float gravelWeight;
            float impact = Mathf.Exp(-u * 5.2f);

            if (signedWet >= 0f)
            {
                float cross = across / Mathf.Max(0.025f, wetWidth);
                float availableDepth = Mathf.Max(0.06f, water - Mathf.Lerp(toe.BedHeight, outlet.BedHeight, u));
                float bowl = (1f - cross * cross) * Mathf.Max(0f, availableDepth - 0.06f);
                target = water - 0.06f - bowl;
                weight = 1f;
                rockWeight = Mathf.Max(0.16f, rockConstraint * 0.72f);
                gravelWeight = Mathf.Lerp(0.3f, 0.88f, cross);
            }
            else
            {
                ResolveNaturalBank(original, water, bank, (across - wetWidth) / basinBankVariation, macro, rockConstraint,
                    out target, out weight, out rockWeight, out gravelWeight);
            }

            target = Mathf.Clamp(target, original - maximumCut, original + maximumFill);
            // Fade the basin into the inlet/outlet field with a zero-slope transition.
            // The old unconditional override made transverse steps at both ends.
            float join = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(alongWorld / 0.8f))
                * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((length - alongWorld) / 1.2f));
            float oldWeight = influence[index];
            float oldTarget = original * (1f - oldWeight) + weightedTarget[index];
            float basinBlend = weight * join;
            float finished = Mathf.Lerp(oldTarget, target, basinBlend);
            weight = Mathf.Lerp(oldWeight, 1f, basinBlend);
            float blendedWater = Mathf.Lerp(waterHeight[index], water, basinBlend);
            float attrBlend = oldWeight > 0.0001f ? basinBlend : join;
            target = weight > 0.0001f ? (finished - original * (1f - weight)) / weight : original;
            Write(index, target, weight, blendedWater, Mathf.Lerp(distance[index], Mathf.Lerp(toe.Distance, outlet.Distance, u), attrBlend),
                WetSignal(blendedWater - finished, across, wetWidth, bank, RiverReachKind.PlungePool, 0f),
                Mathf.Lerp(rock[index], rockWeight, attrBlend),
                Mathf.Lerp(gravel[index], gravelWeight, attrBlend), axis, RiverReachKind.PlungePool,
                Mathf.Lerp(0.04f, 0.88f, impact), 0f, wetWidth,
                Mathf.Clamp01(0.5f + signed / (2f * Mathf.Max(0.025f, wetWidth))), course);
        }
    }

    /// <summary>
    /// A closed basin around the source. The shoreline is an irregular contour around the planned
    /// radius; inside it the bed is a bowl below the lake level, outside it the ordinary natural
    /// bank profile rises back to the original ground. Where the outlet channel already cuts the
    /// rim, the bank yields to it, otherwise the lake would dam its own river.
    /// </summary>
    private void StampSourceLake(TerrainSampleCache terrain, Course course, short courseIndex,
        float maximumCut, float maximumFill, int seed)
    {
        RiverSourceLake lake = course.Lake;
        float water = lake.WaterHeight;
        // Lake banks are lowland-like: a shallow shelf, a short bench and a broad wall back to grade.
        var bank = new RiverBankProfile(0.55f, 0.7f, 1.6f, 1.3f, water + 0.3f, 0.3f);
        float extent = lake.Radius * 1.25f + bank.Extent + step * 2f;
        int minX = Mathf.Max(0, Mathf.FloorToInt((lake.Centre.x - extent) / step));
        int maxX = Mathf.Min(resolution - 1, Mathf.CeilToInt((lake.Centre.x + extent) / step));
        int minZ = Mathf.Max(0, Mathf.FloorToInt((lake.Centre.y - extent) / step));
        int maxZ = Mathf.Min(resolution - 1, Mathf.CeilToInt((lake.Centre.y + extent) / step));
        RiverSection first = course.Sections[0];
        Vector2 outflow = course.Sections.Count > 1
            ? (course.Sections[1].Centre - first.Centre).normalized : Vector2.right;

        for (int z = minZ; z <= maxZ; z++)
        for (int x = minX; x <= maxX; x++)
        {
            Vector2 p = new Vector2(x * step, z * step);
            Vector2 offset = p - lake.Centre;
            float d = offset.magnitude;
            Vector2 dir = d > 0.0001f ? offset / d : Vector2.right;
            // Irregular shoreline: coherent noise sampled on the unit circle keeps the contour
            // closed and smooth while breaking the perfect disc.
            float shoreNoise = ErosionNoise(lake.Centre + dir * 3.1f, seed + 2203);
            float shore = lake.Radius * (1f + shoreNoise * 0.2f);
            float outer = shore + bank.Extent;
            if (d >= outer) continue;

            int index = z * resolution + x;
            float original = terrain.Heights[index];
            float macro = ErosionNoise(p, seed + 1409);
            float rockConstraint = Mathf.Clamp01(terrain.Slopes[index] * 0.85f
                + terrain.MountainBoosts[index] * 0.18f);
            float signedWet = shore - d;
            float target, weight, rockWeight, gravelWeight;
            if (signedWet >= 0f)
            {
                float cross = d / Mathf.Max(0.05f, shore);
                target = water - 0.06f - lake.Depth * (1f - cross * cross);
                weight = 1f;
                rockWeight = Mathf.Max(0.12f, rockConstraint * 0.5f);
                gravelWeight = Mathf.Lerp(0.3f, 0.85f, cross);
            }
            else
            {
                ResolveNaturalBank(original, water, bank, -signedWet, macro, rockConstraint,
                    out target, out weight, out rockWeight, out gravelWeight);
            }
            target = Mathf.Clamp(target, original - maximumCut, original + maximumFill);

            float oldWeight = influence[index];
            float existingWet = oldWeight > 0f ? wetDistance[index] : -1000f;
            // The bank must not refill the outlet channel the valley stamp already cut through it.
            float yield = signedWet >= 0f ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((existingWet + 0.35f) / 0.5f));
            float blend = weight * (1f - yield);
            if (blend <= 0.0001f) continue;
            float oldTarget = original * (1f - oldWeight) + weightedTarget[index];
            float finished = Mathf.Lerp(oldTarget, target, blend);
            float newWeight = Mathf.Lerp(oldWeight, 1f, blend);
            float blendedWater = oldWeight > 0.0001f ? Mathf.Lerp(waterHeight[index], water, blend) : water;
            float resolvedTarget = newWeight > 0.0001f ? (finished - original * (1f - newWeight)) / newWeight : original;
            float lakeWet = Mathf.Min(blendedWater - finished, signedWet + bank.ShallowWidth * 0.6f);
            float wet = Mathf.Max(existingWet, lakeWet);
            bool lakeOwns = signedWet >= 0f || existingWet < 0f;
            Write(index, resolvedTarget, newWeight, blendedWater,
                lakeOwns ? first.Distance : distance[index], wet,
                Mathf.Lerp(rock[index], rockWeight, blend), Mathf.Lerp(gravel[index], gravelWeight, blend),
                lakeOwns ? outflow : new Vector2(directionX[index], directionY[index]),
                lakeOwns ? RiverReachKind.Lake : kind[index], lakeOwns ? 0.02f : foam[index], 0f,
                Mathf.Max(0.05f, shore), Mathf.Clamp01(0.5f + Vector2.Dot(offset, new Vector2(-outflow.y, outflow.x))
                    / (2f * Mathf.Max(0.05f, shore))),
                lakeOwns ? courseIndex : courseId[index]);
        }
    }

    private static void ResolveNaturalBank(float original, float water, RiverBankProfile bank,
        float d, float macro, float rockConstraint, out float target, out float weight,
        out float rockWeight, out float gravelWeight)
    {
        float shallow = bank.ShallowWidth;
        float benchEnd = shallow + bank.BenchWidth;
        float wallEnd = benchEnd + bank.WallWidth;
        if (d <= shallow)
        {
            float u = d / Mathf.Max(0.01f, shallow);
            target = Mathf.Lerp(water - 0.06f, water + 0.045f, Mathf.SmoothStep(0f, 1f, u));
            weight = 1f;
            rockWeight = Mathf.Max(0.06f, rockConstraint * 0.45f);
            gravelWeight = 0.94f;
        }
        else if (d <= benchEnd)
        {
            float u = (d - shallow) / Mathf.Max(0.01f, bank.BenchWidth);
            target = water + Mathf.Lerp(0.045f, 0.16f, u) + macro * 0.025f;
            weight = 1f;
            rockWeight = Mathf.Max(0.1f, rockConstraint * 0.62f);
            gravelWeight = Mathf.Lerp(0.92f, 0.64f, u);
        }
        else if (d <= wallEnd)
        {
            float u = (d - benchEnd) / Mathf.Max(0.01f, bank.WallWidth);
            float erodedWall = Mathf.SmoothStep(0f, 1f, u);
            target = Mathf.Lerp(water + 0.16f, Mathf.Max(water + 0.16f, original), erodedWall);
            weight = 1f;
            rockWeight = Mathf.Max(bank.RockWeight, rockConstraint);
            gravelWeight = Mathf.Lerp(0.52f, 0.12f, u);
        }
        else
        {
            float u = (d - wallEnd) / Mathf.Max(0.01f, bank.BlendWidth);
            target = Mathf.Max(water + 0.16f, original);
            weight = 1f - Mathf.SmoothStep(0f, 1f, u);
            rockWeight = Mathf.Max(bank.RockWeight * (1f - u), rockConstraint * 0.55f);
            gravelWeight = 0.12f * (1f - u);
        }
    }

    /// <summary>
    /// The wet contour, which is not the same question as "is there water above this cell".
    /// Inland the two agree, because a bank rises through the water surface and the depth term
    /// crosses zero on its own. Across the shoreline they diverge: the estuary carve is cut-only,
    /// so the submerged shelf keeps its original height and every cell in the stamped footprint -
    /// banks, bench, blend skirt - reads as metres of water. That exported the whole valley as
    /// inland-water geometry, and because the depth term never reached zero inside the footprint,
    /// the mesh clipper had no crossing to interpolate and fell back to the grid, which is the
    /// stair-stepped edge. Bounding the contour by the channel and its shallow margin keeps the
    /// estuary a river and restores a real zero crossing; the ocean term retires the surface once
    /// the handoff is complete, so nothing is drawn over open sea.
    /// </summary>
    private static float WetSignal(float depth, float across, float wetWidth, RiverBankProfile bank,
        RiverReachKind reachKind, float ocean)
    {
        float signal = Mathf.Min(depth, wetWidth + bank.ShallowWidth * 0.6f - across);
        return reachKind == RiverReachKind.Estuary ? Mathf.Min(signal, (1f - ocean) * 8f) : signal;
    }

    private void Write(int index, float target, float weight, float water, float atDistance,
        float signedWet, float rockWeight, float gravelWeight, Vector2 flow,
        RiverReachKind reachKind, float foamWeight, float ocean, float wetWidth, float across, short course)
    {
        courseId[index] = course;
        influence[index] = weight;
        weightedTarget[index] = target * weight;
        waterHeight[index] = water;
        distance[index] = atDistance;
        wetDistance[index] = signedWet;
        rock[index] = rockWeight;
        gravel[index] = gravelWeight;
        directionX[index] = flow.x;
        directionY[index] = flow.y;
        foam[index] = foamWeight;
        edge[index] = signedWet >= 0f
            ? 1f - Mathf.Clamp01(signedWet / Mathf.Max(0.025f, wetWidth)) : 0f;
        acrossCoordinate[index] = across;
        oceanBlend[index] = ocean;
        kind[index] = reachKind;
    }

    private static RiverBankProfile LerpBank(RiverBankProfile a, RiverBankProfile b, float t)
        => new RiverBankProfile(
            Mathf.Lerp(a.ShallowWidth, b.ShallowWidth, t),
            Mathf.Lerp(a.BenchWidth, b.BenchWidth, t),
            Mathf.Lerp(a.WallWidth, b.WallWidth, t),
            Mathf.Lerp(a.BlendWidth, b.BlendWidth, t),
            Mathf.Lerp(a.RimHeight, b.RimHeight, t),
            Mathf.Lerp(a.RockWeight, b.RockWeight, t));

    private static float ErosionNoise(Vector2 p, int seed)
    {
        float sx = (seed & 1023) * 0.017f;
        float sz = ((seed >> 10) & 1023) * 0.019f;
        float broad = Mathf.PerlinNoise(p.x * 0.055f + sx, p.y * 0.055f + sz) * 2f - 1f;
        float detail = Mathf.PerlinNoise(p.x * 0.17f + sz + 41.3f, p.y * 0.17f + sx - 23.7f) * 2f - 1f;
        return broad * 0.72f + detail * 0.28f;
    }

    private float Bilinear(float[] values, int x0, int z0, int x1, int z1, float tx, float tz)
    {
        // Match TerrainMeshBuilder and AppendWaterMesh's BL-to-TR diagonal. Bilinear
        // interpolation would describe a different bed between the same four vertices.
        float bl = values[z0 * resolution + x0];
        float br = values[z0 * resolution + x1];
        float tl = values[z1 * resolution + x0];
        float tr = values[z1 * resolution + x1];
        return tz >= tx ? bl * (1f - tz) + tl * (tz - tx) + tr * tx
            : bl * (1f - tx) + br * (tx - tz) + tr * tz;
    }

    public void AppendWaterMesh(List<Vector3> positions, List<Vector2> uv, List<Color> colors,
        List<int> indices)
    {
        var sharedGridVertices = new Dictionary<int, int>();
        for (int z = 0; z < resolution - 1; z++)
        for (int x = 0; x < resolution - 1; x++)
        {
            int bl = z * resolution + x;
            int br = bl + 1;
            int tl = bl + resolution;
            int tr = tl + 1;
            AppendClippedTriangle(tl, tr, bl, sharedGridVertices, positions, uv, colors, indices);
            AppendClippedTriangle(tr, br, bl, sharedGridVertices, positions, uv, colors, indices);
        }
        foreach (Course course in courses)
        foreach (RiverWaterfall fall in course.Waterfalls)
            AppendWaterfallCurtain(course.Sections, fall, positions, uv, colors, indices);
    }

    private void AppendClippedTriangle(int ia, int ib, int ic, Dictionary<int, int> sharedGridVertices,
        List<Vector3> positions, List<Vector2> uv, List<Color> colors, List<int> indices)
    {
        FieldVertex a = Vertex(ia);
        FieldVertex b = Vertex(ib);
        FieldVertex c = Vertex(ic);
        if (a.Mask < 0f && b.Mask < 0f && c.Mask < 0f) return;
        if (a.Mask >= 0f && b.Mask >= 0f && c.Mask >= 0f)
        {
            indices.Add(GetOrAppendGridVertex(ia, a, sharedGridVertices, positions, uv, colors));
            indices.Add(GetOrAppendGridVertex(ib, b, sharedGridVertices, positions, uv, colors));
            indices.Add(GetOrAppendGridVertex(ic, c, sharedGridVertices, positions, uv, colors));
            return;
        }

        var input = new List<FieldVertex>(4) { a, b, c };
        var output = new List<FieldVertex>(5);
        FieldVertex previous = input[input.Count - 1];
        bool previousInside = previous.Mask >= 0f;
        foreach (FieldVertex current in input)
        {
            bool currentInside = current.Mask >= 0f;
            if (currentInside != previousInside)
            {
                float t = previous.Mask / (previous.Mask - current.Mask);
                output.Add(FieldVertex.Lerp(previous, current, t));
            }
            if (currentInside) output.Add(current);
            previous = current;
            previousInside = currentInside;
        }
        if (output.Count < 3) return;
        for (int i = 1; i < output.Count - 1; i++)
            AppendTriangle(output[0], output[i], output[i + 1], positions, uv, colors, indices);
    }

    private static int GetOrAppendGridVertex(int gridIndex, FieldVertex vertex,
        Dictionary<int, int> sharedGridVertices, List<Vector3> positions, List<Vector2> uv,
        List<Color> colors)
    {
        if (sharedGridVertices.TryGetValue(gridIndex, out int meshIndex)) return meshIndex;
        meshIndex = positions.Count;
        AppendVertex(vertex, positions, uv, colors);
        sharedGridVertices.Add(gridIndex, meshIndex);
        return meshIndex;
    }

    private FieldVertex Vertex(int index)
    {
        int x = index % resolution;
        int z = index / resolution;
        float mask = influence[index] > 0f && kind[index] != RiverReachKind.Waterfall
            ? wetDistance[index] : -1000f;
        return new FieldVertex(new Vector2(x * step, z * step), mask, waterHeight[index],
            distance[index], foam[index], edge[index], acrossCoordinate[index], oceanBlend[index]);
    }

    private static void AppendTriangle(FieldVertex a, FieldVertex b, FieldVertex c,
        List<Vector3> positions, List<Vector2> uv, List<Color> colors, List<int> indices)
    {
        int start = positions.Count;
        AppendVertex(a, positions, uv, colors);
        AppendVertex(b, positions, uv, colors);
        AppendVertex(c, positions, uv, colors);
        indices.Add(start);
        indices.Add(start + 1);
        indices.Add(start + 2);
    }

    private static void AppendVertex(FieldVertex v, List<Vector3> positions, List<Vector2> uv,
        List<Color> colors)
    {
        positions.Add(new Vector3(v.Position.x, v.Water, v.Position.y));
        uv.Add(new Vector2(v.Across, v.Distance));
        colors.Add(new Color(v.Foam, v.Edge, 0f, 1f - v.Ocean));
    }

    private static void AppendWaterfallCurtain(IReadOnlyList<RiverSection> sections, RiverWaterfall fall,
        List<Vector3> positions, List<Vector2> uv, List<Color> colors, List<int> indices)
    {
        RiverSection crest = sections[fall.CrestSection];
        RiverSection toe = sections[fall.ToeSection];
        Vector2 forward = (toe.Centre - crest.Centre).normalized;
        if (forward.sqrMagnitude < 0.5f) return;
        Vector2 topCentre = crest.Centre + forward * 0.08f;
        Vector2 bottomCentre = topCentre + forward * 0.16f;
        int rows = Mathf.Max(6, Mathf.CeilToInt((crest.WaterHeight - toe.WaterHeight) / 0.22f));
        int start = positions.Count;
        for (int row = 0; row <= rows; row++)
        {
            float u = row / (float)rows;
            Vector2 centre = Vector2.Lerp(topCentre, bottomCentre, u)
                + forward * (Mathf.Sin(u * Mathf.PI) * 0.035f);
            float y = Mathf.Lerp(crest.WaterHeight, toe.WaterHeight + 0.025f, u);
            float left = Mathf.Lerp(crest.LeftWaterWidth, toe.LeftWaterWidth, u);
            float right = Mathf.Lerp(crest.RightWaterWidth, toe.RightWaterWidth, u);
            for (int column = 0; column < 3; column++)
            {
                float across = column == 0 ? -left : column == 2 ? right : 0f;
                Vector2 p = centre + crest.Lateral * across;
                positions.Add(new Vector3(p.x, y, p.y));
                uv.Add(new Vector2(column * 0.5f,
                    crest.Distance + u * (crest.WaterHeight - toe.WaterHeight)));
                colors.Add(new Color(Mathf.Lerp(0.42f, 0.92f, u), column == 1 ? 0f : 1f, 1f, 1f));
            }
        }
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < 2; column++)
        {
            int a = start + row * 3 + column;
            int b = a + 3;
            indices.Add(a);
            indices.Add(b);
            indices.Add(a + 1);
            indices.Add(a + 1);
            indices.Add(b);
            indices.Add(b + 1);
        }
    }

    private readonly struct FieldVertex
    {
        public readonly Vector2 Position;
        public readonly float Mask, Water, Distance, Foam, Edge, Across, Ocean;
        public FieldVertex(Vector2 position, float mask, float water, float distance,
            float foam, float edge, float across, float ocean)
        {
            Position = position;
            Mask = mask;
            Water = water;
            Distance = distance;
            Foam = foam;
            Edge = edge;
            Across = across;
            Ocean = ocean;
        }
        public static FieldVertex Lerp(FieldVertex a, FieldVertex b, float t)
            => new FieldVertex(
                Vector2.Lerp(a.Position, b.Position, t),
                Mathf.Lerp(a.Mask, b.Mask, t),
                Mathf.Lerp(a.Water, b.Water, t),
                Mathf.Lerp(a.Distance, b.Distance, t),
                Mathf.Lerp(a.Foam, b.Foam, t),
                Mathf.Lerp(a.Edge, b.Edge, t),
                Mathf.Lerp(a.Across, b.Across, t),
                Mathf.Lerp(a.Ocean, b.Ocean, t));
    }
}
