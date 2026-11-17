// tldr: The chunk's booking system for space: which ground is already claimed by a ridge, river or anchor, so
// features are placed without silently overwriting each other.

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
    private static readonly RiverPlan[] NoRivers = new RiverPlan[0];

    /// <summary>The island's whole river network (all courses, shared outlets, one landform), or null.</summary>
    public RiverNetwork RiverNetwork { get; private set; }

    /// <summary>Every course of the network: trunks and tributaries.</summary>
    public IReadOnlyList<RiverPlan> RiverPlans => RiverNetwork != null ? RiverNetwork.Courses : NoRivers;

    public void SetRiverNetwork(RiverNetwork network) => RiverNetwork = network;

    /// <summary>
    /// Samples the merged network field. Courses were stamped into one landform, so there is a
    /// single answer at a confluence rather than whichever course happened to be listed first.
    /// </summary>
    public bool TrySampleRiver(Vector2 point, out RiverSurfaceSample sample)
    {
        if (RiverNetwork != null) return RiverNetwork.TrySample(point, out sample);
        sample = default;
        return false;
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
        // Sized to stay inside ValidateMountainHeightfield's existing max-slope envelope rather
        // than widening that envelope: at 0.34 a minority of seeds tipped just over the bound
        // (measured 2.32 vs 2.31 allowed) and aborted generation outright. The guard is load
        // bearing - it is what catches genuine spike artifacts - so the detail yields to it.
        private const float CragDepth = 0.26f;   // modulation spans [1-CragDepth .. 1]
        private const float CragScale = 0.28f;   // ~3.5 world units per feature

        // Rounding applied to the ridge fold. A hard Mathf.Abs in the ridged transform puts a
        // derivative discontinuity along every contour where the fractal crosses 0.5 - an
        // infinitely sharp crease line in the heightfield, scaled by PeakHeight. Inside the massif
        // those read as crisp crests, but where they ran out through the ridge taper they sliced
        // the mountain's base contour into repeated triangular teeth against the beach.
        private const float CragCreaseSoftness = 0.035f;
        private static readonly float CragCreaseRoot = Mathf.Sqrt(CragCreaseSoftness);
        private static readonly float CragSoftAbsNorm = Mathf.Sqrt(1f + CragCreaseSoftness) - CragCreaseRoot;

        // NOTE: a fade that killed crag detail through the ridge fringe was tried here and
        // reverted. It was added on the theory that crag was serrating the mountain's base
        // contour against the beach; that turned out to be false - the base contour traces with
        // zero direction reversals and the serration was a texture discontinuity at h=0. The fade
        // measurably removed detail from ridge flanks while moving fringe creases only 0.7% ->
        // 0.6%, so it cost terrain character for no demonstrated defect.

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

            // Bounded well under the ridge's transverse reach (Width * 1.35): at 0.38 the warp
            // could pinch a narrow ridge into separate blobs, tripping the fragmented-component
            // validation and aborting generation for some seeds.
            warpAmplitude = Width * 0.24f;

            center = origin + Direction * (Length * 0.5f);
            float maxAlongHalf = (Length + Width) * 0.5f;
            float maxPerpHalf = Width * 1.35f;
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
            //
            // sqrt(d^2 + e) instead of abs(d): same V shape away from the fold, but the vertex is
            // rounded over a small radius instead of being a true corner, so the crest has finite
            // curvature and does not stamp a hard crease line into the surface.
            float d = fractal * 2f - 1f;
            float softAbs = (Mathf.Sqrt(d * d + CragCreaseSoftness) - CragCreaseRoot) / CragSoftAbsNorm;
            float ridged = 1f - Mathf.Clamp01(softAbs);

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
            float maxPerp = Width * 1.35f;
            if (perp >= maxPerp) return 0f;

            // Longitudinal smooth sine envelope
            float tAlong = alongShifted / totalLen;
            float alongWeight = Mathf.Sin(tAlong * Mathf.PI);

            // Transverse smooth profile:
            // Core spine: [0..Width] smooth cosine curve (1.0 -> 0.28)
            // Foothill apron: [Width..1.35*Width] smooth cubic Hermite falloff (0.28 -> 0.0).
            // Keeping this narrow is what makes these coastal massifs instead of
            // mountain blankets which consume the island's city-building interior.
            float crossWeight;
            if (perp <= Width)
            {
                float u = perp / Width;
                float cos = Mathf.Cos(u * (Mathf.PI * 0.5f));
                crossWeight = Mathf.Lerp(0.28f, 1.0f, cos * cos);
            }
            else
            {
                float u = (perp - Width) / (maxPerp - Width);
                float smooth = (1f - u) * (1f - u) * (1f + 2f * u);
                crossWeight = 0.28f * smooth;
            }

            return PeakHeight * alongWeight * crossWeight * EvaluateCragModulation(point);
        }
    }

    public struct ReservationEvaluation
    {
        public float MountainAllowance;
        public float RawRidgeElevation;
        public short DominantRidgeId;
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

    private readonly List<CoastalRidge> ridges = new List<CoastalRidge>();
    private readonly List<MineAnchor> mineAnchors = new List<MineAnchor>();

    public PerimeterSectorMap Sectors { get; set; }
    public IReadOnlyList<CoastalRidge> Ridges => ridges;
    public IReadOnlyList<MineAnchor> MineAnchors => mineAnchors;

    public void AddRidge(CoastalRidge ridge)
    {
        if (ridge != null) ridges.Add(ridge);
    }

    public void AddMineAnchor(MineAnchor anchor)
    {
        mineAnchors.Add(anchor);
    }

    /// <summary>
    /// Evaluates pre-river geology. The connected river pass consumes its complete result.
    /// </summary>
    public ReservationEvaluation EvaluateAll(float localX, float localZ, float currentBaseHeight, float waterLevel)
    {
        ReservationEvaluation eval = new ReservationEvaluation
        {
            MountainAllowance = 1f,
            RawRidgeElevation = 0f,
            DominantRidgeId = -1
        };

        Vector2 point = new Vector2(localX, localZ);

        // Rivers are solved AFTER the full mountain/coastal terrain exists. This pass
        // must never remove mountain support or carve against the flat base alone.

        // 2. Ridges (only evaluated if mountain allowance is non-zero)
        if (eval.MountainAllowance > 0.0001f)
        {
            for (int i = 0; i < ridges.Count; i++)
            {
                float elevation = ridges[i].EvaluateRawElevation(point);
                if (elevation > eval.RawRidgeElevation)
                {
                    eval.RawRidgeElevation = elevation;
                    eval.DominantRidgeId = (short)i;
                }
            }
        }

        return eval;
    }

    public float GetMountainAllowance(float localX, float localZ) => 1f;

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
        bool found = TrySampleRiver(new Vector2(localX, localZ), out RiverSurfaceSample sample);
        isInRiverChannel = found && sample.IsWater;
        return found ? Mathf.Max(0f, currentHeight - sample.Apply(currentHeight)) : 0f;
    }

}
