using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative space reservation and feature mask layer.
/// Coordinates spatial claims between river corridors and coastal/interior mountain
/// ridges before heights are synthesized.
/// </summary>
public sealed class FeatureReservationMap
{
    public readonly struct RiverWaypoint
    {
        public Vector2 Position { get; }
        public float ChannelRadius { get; }
        public float ClearanceRadius { get; }

        public RiverWaypoint(Vector2 position, float channelRadius, float clearanceRadius)
        {
            Position = position;
            ChannelRadius = Mathf.Max(0.2f, channelRadius);
            ClearanceRadius = Mathf.Max(ChannelRadius + 0.8f, clearanceRadius);
        }
    }

    public sealed class RiverCorridor
    {
        public List<RiverWaypoint> Waypoints { get; } = new List<RiverWaypoint>();
        public Vector2 Source => Waypoints.Count > 0 ? Waypoints[0].Position : Vector2.zero;
        public Vector2 Mouth => Waypoints.Count > 0 ? Waypoints[Waypoints.Count - 1].Position : Vector2.zero;
        public float ValleyDepth { get; }
        public float ChannelRadius { get; set; } = 1.0f;
        public float ClearanceRadius { get; set; } = 4.5f;
        public float MaxClearanceRadius { get; private set; } = 8f;

        private float boundsMinX = float.MaxValue;
        private float boundsMinZ = float.MaxValue;
        private float boundsMaxX = float.MinValue;
        private float boundsMaxZ = float.MinValue;
        private bool boundsComputed = false;

        public RiverCorridor(float valleyDepth, float channelRadius = 1.0f, float clearanceRadius = 4.5f)
        {
            ValleyDepth = Mathf.Max(0.5f, valleyDepth);
            ChannelRadius = Mathf.Max(0.2f, channelRadius);
            ClearanceRadius = Mathf.Max(ChannelRadius + 0.8f, clearanceRadius);
        }

        public void ComputeBounds()
        {
            if (Waypoints.Count == 0) return;
            boundsMinX = float.MaxValue;
            boundsMinZ = float.MaxValue;
            boundsMaxX = float.MinValue;
            boundsMaxZ = float.MinValue;
            MaxClearanceRadius = ClearanceRadius;

            for (int i = 0; i < Waypoints.Count; i++)
            {
                Vector2 p = Waypoints[i].Position;
                float cr = ClearanceRadius;

                if (p.x - cr < boundsMinX) boundsMinX = p.x - cr;
                if (p.x + cr > boundsMaxX) boundsMaxX = p.x + cr;
                if (p.y - cr < boundsMinZ) boundsMinZ = p.y - cr;
                if (p.y + cr > boundsMaxZ) boundsMaxZ = p.y + cr;
            }
            boundsComputed = true;
        }

        public float DistanceToCenterline(Vector2 point, out Vector2 closestPointOnCenterline)
        {
            closestPointOnCenterline = Vector2.zero;
            if (Waypoints.Count == 0) return float.MaxValue;
            if (!boundsComputed) ComputeBounds();

            // Spatial AABB early-out: if point is outside river corridor boundary, exit immediately
            if (point.x < boundsMinX || point.x > boundsMaxX || point.y < boundsMinZ || point.y > boundsMaxZ)
            {
                return float.MaxValue;
            }

            if (Waypoints.Count == 1)
            {
                closestPointOnCenterline = Waypoints[0].Position;
                return Vector2.Distance(point, closestPointOnCenterline);
            }

            float minDistanceSq = float.MaxValue;

            for (int i = 0; i < Waypoints.Count - 1; i++)
            {
                Vector2 a = Waypoints[i].Position;
                Vector2 b = Waypoints[i + 1].Position;
                Vector2 ab = b - a;
                float abLenSq = ab.sqrMagnitude;
                float t = abLenSq > 0.0001f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / abLenSq) : 0f;
                Vector2 projection = a + ab * t;
                float distSq = (point - projection).sqrMagnitude;
                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                    closestPointOnCenterline = projection;
                }
            }

            return Mathf.Sqrt(minDistanceSq);
        }
    }

    public sealed class CoastalRidge
    {
        public Vector2 Origin { get; }
        public Vector2 Direction { get; }
        public float Length { get; }
        public float Width { get; }
        public float PeakHeight { get; }
        public float CliffSharpness { get; }
        private readonly Vector2 center;
        private readonly float boundingRadiusSq;
        private readonly float cragOffsetX;
        private readonly float cragOffsetZ;

        // Surface-detail modulation depth. The ridge envelope alone is an analytic capsule
        // (sine along the axis, cosine across it), which renders as a smooth extruded pill -
        // visibly artificial. This breaks the silhouette up into spurs and gullies.
        // Kept MULTIPLICATIVE against the envelope rather than added on top: the envelope
        // still drives the value to exactly 0 at the ridge boundary, so detail can never
        // reintroduce a hard edge where the ridge meets surrounding terrain (the failure mode
        // that produced saw-tooth spikes and notches elsewhere in this system).
        private const float CragDepth = 0.34f;   // modulation spans [1-CragDepth .. 1]
        private const float CragScale = 0.32f;   // ~3 world units per feature

        // Domain warp applied to the ridge's own coordinate frame. Without it the capsule's
        // parallel sides survive every amount of surface detail: crag modulation changes the
        // height *inside* the footprint but the footprint edge stays a straight analytic line,
        // which reads as hard polygonal rock/grass borders once textured. Warping the sample
        // point before measuring along/perp makes the boundary itself meander.
        private const float WarpScale = 0.19f;
        private readonly float warpAmplitude;

        public CoastalRidge(Vector2 origin, Vector2 direction, float length, float width, float peakHeight, float cliffSharpness = 1.5f)
        {
            Origin = origin;
            Direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
            Length = Mathf.Max(4f, length);
            Width = Mathf.Max(3f, width);
            PeakHeight = Mathf.Max(0.5f, peakHeight);
            CliffSharpness = Mathf.Max(1f, cliffSharpness);

            warpAmplitude = Width * 0.38f;

            center = origin + Direction * (Length * 0.5f);
            float maxAlongHalf = (Length + Width) * 0.5f;
            float maxPerpHalf = Width * 1.8f;
            // Bounding radius must cover the warped footprint, or the early-out clips the
            // very boundary meander the warp exists to create (re-introducing a straight edge).
            float radius = Mathf.Sqrt(maxAlongHalf * maxAlongHalf + maxPerpHalf * maxPerpHalf) + 1.0f + warpAmplitude;
            boundingRadiusSq = radius * radius;

            // Deterministic per-ridge crag offsets derived from the (already seeded) origin, so
            // regeneration with the same seed reproduces identical mountains.
            cragOffsetX = Mathf.Repeat(origin.x * 12.9898f + origin.y * 78.233f, 1000f);
            cragOffsetZ = Mathf.Repeat(origin.x * 39.3468f + origin.y * 11.135f, 1000f);
        }

        /// <summary>
        /// Fractal surface detail in [1-CragDepth .. 1]. Multiplied into the ridge envelope.
        /// </summary>
        private float EvaluateCragModulation(Vector2 point)
        {
            float n1 = Mathf.PerlinNoise(point.x * CragScale + cragOffsetX, point.y * CragScale + cragOffsetZ);
            float n2 = Mathf.PerlinNoise(point.x * CragScale * 2.7f + cragOffsetX + 41.7f, point.y * CragScale * 2.7f + cragOffsetZ + 93.1f);
            float fractal = n1 * 0.68f + n2 * 0.32f;

            // Ridged transform: fold the noise about its midpoint so the high values form
            // narrow crests and spurs rather than smooth rolling bumps.
            float ridged = 1f - Mathf.Abs(fractal * 2f - 1f);

            return 1f - CragDepth * (1f - ridged);
        }

        /// <summary>Irregularises the ridge footprint so its silhouette isn't an analytic capsule.</summary>
        private Vector2 EvaluateFootprintWarp(Vector2 point)
        {
            float wx = Mathf.PerlinNoise(point.x * WarpScale + cragOffsetX + 7.3f, point.y * WarpScale + cragOffsetZ + 19.1f) * 2f - 1f;
            float wz = Mathf.PerlinNoise(point.x * WarpScale + cragOffsetX + 63.7f, point.y * WarpScale + cragOffsetZ + 51.9f) * 2f - 1f;
            return new Vector2(wx, wz) * warpAmplitude;
        }

        public float EvaluateRawElevation(Vector2 point)
        {
            if ((point - center).sqrMagnitude > boundingRadiusSq) return 0f;

            // Measure the capsule in warped space; the envelope still reaches exactly 0 at its
            // (now meandering) boundary, so this adds no discontinuity.
            Vector2 delta = (point + EvaluateFootprintWarp(point)) - Origin;
            float along = Vector2.Dot(delta, Direction);
            float endcap = Width * 0.5f;
            float totalLen = Length + endcap * 2f;
            float alongShifted = along + endcap;
            if (alongShifted <= 0f || alongShifted >= totalLen) return 0f;

            Vector2 normal = new Vector2(-Direction.y, Direction.x);
            float perp = Mathf.Abs(Vector2.Dot(delta, normal));
            float maxPerp = Width * 1.8f;
            if (perp >= maxPerp) return 0f;

            // Longitudinal smooth sine envelope
            float tAlong = alongShifted / totalLen;
            float alongWeight = Mathf.Sin(tAlong * Mathf.PI);

            // Transverse smooth profile:
            // Core spine: [0..Width] smooth cosine curve (1.0 -> 0.35)
            // Foothill apron: [Width..1.8*Width] smooth cubic Hermite falloff (0.35 -> 0.0)
            float crossWeight;
            if (perp <= Width)
            {
                float u = perp / Width;
                float cos = Mathf.Cos(u * (Mathf.PI * 0.5f));
                crossWeight = Mathf.Lerp(0.35f, 1.0f, cos * cos);
            }
            else
            {
                float u = (perp - Width) / (maxPerp - Width);
                float smooth = (1f - u) * (1f - u) * (1f + 2f * u);
                crossWeight = 0.35f * smooth;
            }

            return PeakHeight * alongWeight * crossWeight * EvaluateCragModulation(point);
        }
    }

    public struct ReservationEvaluation
    {
        public float MountainAllowance;
        public float RawRidgeElevation;
        public float RiverCarveDepth;
        public bool IsInRiverChannel;
    }

    public readonly struct MineAnchor
    {
        public Vector2 Position { get; }
        public Vector2 Normal { get; }
        public float Slope { get; }
        public ResourceNodeType ResourceType { get; }

        public MineAnchor(Vector2 position, Vector2 normal, float slope, ResourceNodeType resourceType)
        {
            Position = position;
            Normal = normal;
            Slope = slope;
            ResourceType = resourceType;
        }
    }

    private readonly List<RiverCorridor> rivers = new List<RiverCorridor>();
    private readonly List<CoastalRidge> ridges = new List<CoastalRidge>();
    private readonly List<MineAnchor> mineAnchors = new List<MineAnchor>();

    public PerimeterSectorMap Sectors { get; set; }
    public IReadOnlyList<RiverCorridor> Rivers => rivers;
    public IReadOnlyList<CoastalRidge> Ridges => ridges;
    public IReadOnlyList<MineAnchor> MineAnchors => mineAnchors;

    public void AddRiver(RiverCorridor river)
    {
        if (river != null) rivers.Add(river);
    }

    public void AddRidge(CoastalRidge ridge)
    {
        if (ridge != null) ridges.Add(ridge);
    }

    public void AddMineAnchor(MineAnchor anchor)
    {
        mineAnchors.Add(anchor);
    }

    /// <summary>
    /// Evaluates all reservations in a single unified pass with cached river distances.
    /// </summary>
    public ReservationEvaluation EvaluateAll(float localX, float localZ, float currentBaseHeight, float waterLevel)
    {
        ReservationEvaluation eval = new ReservationEvaluation
        {
            MountainAllowance = 1f,
            RawRidgeElevation = 0f,
            RiverCarveDepth = 0f,
            IsInRiverChannel = false
        };

        Vector2 point = new Vector2(localX, localZ);

        // 1. Geological Rivers & Valleys
        for (int i = 0; i < rivers.Count; i++)
        {
            RiverCorridor river = rivers[i];
            float dist = river.DistanceToCenterline(point, out _);
            float channelRadius = river.ChannelRadius;
            float clearanceRadius = river.ClearanceRadius;

            float distFromSource = Vector2.Distance(point, river.Source);
            float sourceFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distFromSource / 4.0f));
            float valleySuppression = 0f;

            if (dist <= channelRadius)
            {
                valleySuppression = 1f;

                // Parabolic thalweg bed profile: deepest in the center, curving gently up to the banks
                float u = dist / Mathf.Max(0.01f, channelRadius); // 0 at center, 1 at channel edge
                float bedProfile = 1f - (u * u); // Parabolic cross-section

                eval.IsInRiverChannel = distFromSource > 3.0f; // Only mark as open water channel once it deepens towards the coast

                // Riverbed elevation: smoothly descends from highland spring down to MSL waterline (0.0m)
                float inlandSurface = Mathf.Max(0.15f, currentBaseHeight);
                float distToMouth = Vector2.Distance(point, river.Mouth);
                float totalRiverLen = Mathf.Max(1f, Vector2.Distance(river.Source, river.Mouth));
                float streamBed = Mathf.Lerp(inlandSurface - 0.18f, -0.20f, Mathf.Clamp01(distFromSource / totalRiverLen));
                float targetWaterbedHeight = Mathf.Lerp(inlandSurface, streamBed, bedProfile);

                // Estuary transition: near the mouth, smoothly blend into natural coastal seabed
                if (distToMouth < 4.0f)
                {
                    float mouthBlend = 1f - (distToMouth / 4.0f);
                    targetWaterbedHeight = Mathf.Lerp(targetWaterbedHeight, Mathf.Min(targetWaterbedHeight, currentBaseHeight), mouthBlend);
                }

                float requiredCarve = Mathf.Max(0f, currentBaseHeight - targetWaterbedHeight) * sourceFade;
                eval.RiverCarveDepth = Mathf.Max(eval.RiverCarveDepth, requiredCarve);
            }
            else if (dist < clearanceRadius)
            {
                // Smooth polynomial valley bank gently sloping from the surrounding plain down towards the riverbank
                float v = (dist - channelRadius) / Mathf.Max(0.01f, clearanceRadius - channelRadius);
                float bankSmooth = v * v * v * (v * (v * 6f - 15f) + 10f); // Quintic S-curve (0 at channel edge, 1 at outer plain)
                valleySuppression = 1f - bankSmooth;

                // Gentle valley slope: dips by at most 0.20m so riverbanks stay safely dry (+0.65m) above waterLevel.
                //
                // Deliberately NOT the same shape as valleySuppression above (1 - bankSmooth, which
                // peaks at v=0). The channel branch's own carve is provably 0 at its rim -
                // targetWaterbedHeight collapses to inlandSurface = Max(0.15, currentBaseHeight),
                // which is always >= currentBaseHeight, so requiredCarve = Max(0, currentBaseHeight
                // - targetWaterbedHeight) is always 0 there. A valley-carve factor that peaks at
                // v=0 disagreed with that guaranteed-zero edge value: a hard discontinuity right on
                // the channel/bank seam, visible as a sharp notch in the heightfield (and, via
                // TextureBuilder's slope-driven rock blend, as a jagged texture seam too). This hump
                // shape is zero at both ends - the channel's rim (v=0) and the undisturbed outer
                // plain (v=1) - and only rises in between.
                float valleyCarveFactor = Mathf.Sin(Mathf.Clamp01(v) * Mathf.PI);
                float maxValleyDip = Mathf.Min(0.20f, Mathf.Max(0f, currentBaseHeight - (waterLevel + 0.50f)));
                float valleyCarve = maxValleyDip * valleyCarveFactor * sourceFade;
                eval.RiverCarveDepth = Mathf.Max(eval.RiverCarveDepth, valleyCarve);
            }

            float riverAllowance = 1f - (valleySuppression * sourceFade);
            eval.MountainAllowance = Mathf.Min(eval.MountainAllowance, riverAllowance);
        }

        // 2. Ridges (only evaluated if mountain allowance is non-zero)
        if (eval.MountainAllowance > 0.0001f)
        {
            for (int i = 0; i < ridges.Count; i++)
            {
                float elevation = ridges[i].EvaluateRawElevation(point);
                if (elevation > eval.RawRidgeElevation)
                {
                    eval.RawRidgeElevation = elevation;
                }
            }
        }

        return eval;
    }

    public float GetMountainAllowance(float localX, float localZ)
    {
        Vector2 point = new Vector2(localX, localZ);

        float minAllowance = 1f;
        for (int i = 0; i < rivers.Count; i++)
        {
            RiverCorridor river = rivers[i];
            float dist = river.DistanceToCenterline(point, out _);
            float channelRadius = river.ChannelRadius;
            float clearanceRadius = river.ClearanceRadius;
            float distFromSource = Vector2.Distance(point, river.Source);
            float sourceFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distFromSource / 4.0f));
            float valleySuppression = 0f;

            if (dist <= channelRadius)
            {
                valleySuppression = 1f;
            }
            else if (dist < clearanceRadius)
            {
                float v = (dist - channelRadius) / Mathf.Max(0.01f, clearanceRadius - channelRadius);
                float bankSmooth = v * v * v * (v * (v * 6f - 15f) + 10f);
                valleySuppression = 1f - bankSmooth;
            }

            float riverAllowance = 1f - (valleySuppression * sourceFade);
            minAllowance = Mathf.Min(minAllowance, riverAllowance);
        }

        return minAllowance;
    }

    public float GetSynthesizedMountainHeight(float localX, float localZ)
    {
        float allowance = GetMountainAllowance(localX, localZ);
        if (allowance <= 0.0001f) return 0f;

        Vector2 point = new Vector2(localX, localZ);
        float totalRidgeElevation = 0f;

        for (int i = 0; i < ridges.Count; i++)
        {
            float elevation = ridges[i].EvaluateRawElevation(point);
            totalRidgeElevation = Mathf.Max(totalRidgeElevation, elevation);
        }

        return totalRidgeElevation * allowance;
    }

    public float GetRiverCarveDepth(float localX, float localZ, float currentHeight, float waterLevel, out bool isInRiverChannel)
    {
        var eval = EvaluateAll(localX, localZ, currentHeight, waterLevel);
        isInRiverChannel = eval.IsInRiverChannel;
        return eval.RiverCarveDepth;
    }

}
