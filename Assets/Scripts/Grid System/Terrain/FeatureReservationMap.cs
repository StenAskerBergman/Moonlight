using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative space reservation and feature mask layer.
/// Coordinates spatial claims between Coastline shape, Harbor pads, River corridors,
/// and Coastal/Interior Mountain ridges before heights are synthesized.
/// </summary>
public sealed class FeatureReservationMap
{
    public readonly struct RiverWaypoint
    {
        public Vector2 Position { get; }
        public float Width { get; }

        public RiverWaypoint(Vector2 position, float width)
        {
            Position = position;
            Width = width;
        }
    }

    public sealed class RiverCorridor
    {
        public List<RiverWaypoint> Waypoints { get; } = new List<RiverWaypoint>();
        public Vector2 Source => Waypoints.Count > 0 ? Waypoints[0].Position : Vector2.zero;
        public Vector2 Mouth => Waypoints.Count > 0 ? Waypoints[Waypoints.Count - 1].Position : Vector2.zero;
        public float ChannelRadius { get; }
        public float ClearanceRadius { get; }
        public float ValleyDepth { get; }

        public RiverCorridor(float channelRadius, float clearanceRadius, float valleyDepth)
        {
            ChannelRadius = Mathf.Max(0.5f, channelRadius);
            ClearanceRadius = Mathf.Max(ChannelRadius + 1f, clearanceRadius);
            ValleyDepth = Mathf.Max(0.5f, valleyDepth);
        }

        public float DistanceToCenterline(Vector2 point, out Vector2 closestPointOnCenterline, out float segmentT)
        {
            closestPointOnCenterline = Vector2.zero;
            segmentT = 0f;
            if (Waypoints.Count == 0) return float.MaxValue;
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
                    segmentT = (i + t) / (Waypoints.Count - 1);
                }
            }

            return Mathf.Sqrt(minDistanceSq);
        }
    }

    public sealed class HarborPad
    {
        public Vector2 Center { get; }
        public float Radius { get; }
        public float TargetHeight { get; }

        public HarborPad(Vector2 center, float radius, float targetHeight)
        {
            Center = center;
            Radius = Mathf.Max(2f, radius);
            TargetHeight = targetHeight;
        }

        public float CalculateInfluence(Vector2 point)
        {
            float dist = Vector2.Distance(point, Center);
            if (dist >= Radius) return 0f;
            float t = dist / Radius;
            return 1f - (t * t * (3f - 2f * t)); // Smoothstep falloff
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

        public CoastalRidge(Vector2 origin, Vector2 direction, float length, float width, float peakHeight, float cliffSharpness = 1.5f)
        {
            Origin = origin;
            Direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
            Length = Mathf.Max(3f, length);
            Width = Mathf.Max(2f, width);
            PeakHeight = peakHeight;
            CliffSharpness = Mathf.Max(1f, cliffSharpness);
        }

        public float EvaluateRawElevation(Vector2 point)
        {
            Vector2 delta = point - Origin;
            float along = Vector2.Dot(delta, Direction);
            if (along < -Width * 0.5f || along > Length + Width * 0.5f) return 0f;

            Vector2 normal = new Vector2(-Direction.y, Direction.x);
            float perp = Mathf.Abs(Vector2.Dot(delta, normal));
            if (perp > Width) return 0f;

            // Longitudinal taper along the spine
            float tAlong = Mathf.Clamp01(along / Length);
            float alongWeight = Mathf.Sin(tAlong * Mathf.PI);

            // Transverse bell-curve cross section
            float tPerp = Mathf.Clamp01(perp / Width);
            float crossWeight = Mathf.Pow(1f - tPerp * tPerp, CliffSharpness);

            return PeakHeight * alongWeight * crossWeight;
        }
    }

    private readonly List<RiverCorridor> rivers = new List<RiverCorridor>();
    private readonly List<HarborPad> harbors = new List<HarborPad>();
    private readonly List<CoastalRidge> ridges = new List<CoastalRidge>();

    public IReadOnlyList<RiverCorridor> Rivers => rivers;
    public IReadOnlyList<HarborPad> Harbors => harbors;
    public IReadOnlyList<CoastalRidge> Ridges => ridges;

    public void AddRiver(RiverCorridor river)
    {
        if (river != null) rivers.Add(river);
    }

    public void AddHarbor(HarborPad harbor)
    {
        if (harbor != null) harbors.Add(harbor);
    }

    public void AddRidge(CoastalRidge ridge)
    {
        if (ridge != null) ridges.Add(ridge);
    }

    /// <summary>
    /// Returns 0..1 factor of mountain allowance at local (x, z).
    /// strictly 0 within river channels and harbor pads, smooth transition in corridor margins.
    /// </summary>
    public float GetMountainAllowance(float localX, float localZ)
    {
        Vector2 point = new Vector2(localX, localZ);

        // Harbors suppress mountains completely
        for (int i = 0; i < harbors.Count; i++)
        {
            float harborInf = harbors[i].CalculateInfluence(point);
            if (harborInf >= 0.99f) return 0f;
            if (harborInf > 0f) return Mathf.Clamp01(1f - harborInf * 1.2f);
        }

        // Rivers suppress mountains within their clearance corridor
        float minAllowance = 1f;
        for (int i = 0; i < rivers.Count; i++)
        {
            RiverCorridor river = rivers[i];
            float dist = river.DistanceToCenterline(point, out _, out _);
            if (dist <= river.ChannelRadius) return 0f;
            if (dist < river.ClearanceRadius)
            {
                float t = (dist - river.ChannelRadius) / (river.ClearanceRadius - river.ChannelRadius);
                float corridorAllowance = t * t * (3f - 2f * t);
                minAllowance = Mathf.Min(minAllowance, corridorAllowance);
            }
        }

        return minAllowance;
    }

    /// <summary>
    /// Evaluates total mountain elevation boost at (x, z), masked by the space reservation layer.
    /// </summary>
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

    /// <summary>
    /// Evaluates river valley carve depth and whether point is inside river channel.
    /// </summary>
    public float GetRiverCarveDepth(float localX, float localZ, float currentHeight, float waterLevel, out bool isInRiverChannel)
    {
        isInRiverChannel = false;
        if (rivers.Count == 0) return 0f;

        Vector2 point = new Vector2(localX, localZ);
        float maxCarve = 0f;

        for (int i = 0; i < rivers.Count; i++)
        {
            RiverCorridor river = rivers[i];
            float dist = river.DistanceToCenterline(point, out _, out float segmentT);

            if (dist <= river.ChannelRadius)
            {
                isInRiverChannel = true;
                // Carve down to water level or slightly submerged
                float targetWaterDepth = Mathf.Lerp(waterLevel - 0.2f, waterLevel - 0.5f, segmentT);
                float requiredCarve = Mathf.Max(0f, currentHeight - targetWaterDepth);
                maxCarve = Mathf.Max(maxCarve, requiredCarve);
            }
            else if (dist < river.ClearanceRadius)
            {
                float t = (dist - river.ChannelRadius) / (river.ClearanceRadius - river.ChannelRadius);
                float valleyProfile = 1f - (t * t * (3f - 2f * t)); // 1 at channel edge, 0 at clearance boundary
                float carveAmount = Mathf.Min(river.ValleyDepth * valleyProfile, Mathf.Max(0f, currentHeight - (waterLevel + 0.1f)));
                maxCarve = Mathf.Max(maxCarve, carveAmount);
            }
        }

        return maxCarve;
    }

    /// <summary>
    /// Calculates harbor pad leveling influence.
    /// </summary>
    public float GetHarborFlattenInfluence(float localX, float localZ, out float targetHeight)
    {
        targetHeight = 0f;
        if (harbors.Count == 0) return 0f;

        Vector2 point = new Vector2(localX, localZ);
        float maxInf = 0f;

        for (int i = 0; i < harbors.Count; i++)
        {
            float inf = harbors[i].CalculateInfluence(point);
            if (inf > maxInf)
            {
                maxInf = inf;
                targetHeight = harbors[i].TargetHeight;
            }
        }

        return maxInf;
    }
}
