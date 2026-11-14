// tldr: One accepted river course: its ordered hydraulic stations, waterfalls and mouth, plus sampling access
// to the resolved landform field.

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A reach is a connected part of the course, not a second path or footprint.</summary>
public enum RiverReachKind : byte { Headwater, Gorge, Waterfall, PlungePool, Lowland, Estuary }

public readonly struct RiverBankProfile
{
    public readonly float ShallowWidth, BenchWidth, WallWidth, BlendWidth, RimHeight, RockWeight;
    public float Extent => ShallowWidth + BenchWidth + WallWidth + BlendWidth;
    public RiverBankProfile(float shallow, float bench, float wall, float blend, float rim, float rock)
    {
        ShallowWidth = shallow;
        BenchWidth = bench;
        WallWidth = wall;
        BlendWidth = blend;
        RimHeight = rim;
        RockWeight = rock;
    }
}

/// <summary>
/// A hydraulic station along the course. Stations describe longitudinal constraints; they are
/// not lofted into terrain. RiverLandform turns the complete set into one two-dimensional field.
/// </summary>
public readonly struct RiverSection
{
    public readonly Vector2 Centre, Lateral;
    public readonly float Distance, WaterHeight, BedHeight, LeftWaterWidth, RightWaterWidth, Foam, OceanBlend;
    public float HalfWidth => (LeftWaterWidth + RightWaterWidth) * 0.5f;
    public float MaximumWaterWidth => Mathf.Max(LeftWaterWidth, RightWaterWidth);
    public readonly RiverBankProfile LeftBank, RightBank;
    public readonly RiverReachKind Kind;

    public RiverSection(Vector2 centre, Vector2 lateral, float distance, float water, float bed,
        float leftWaterWidth, float rightWaterWidth, RiverBankProfile left, RiverBankProfile right,
        RiverReachKind kind, float foam = 0f, float oceanBlend = 0f)
    {
        Centre = centre;
        Lateral = lateral;
        Distance = distance;
        WaterHeight = water;
        BedHeight = bed;
        LeftWaterWidth = leftWaterWidth;
        RightWaterWidth = rightWaterWidth;
        LeftBank = left;
        RightBank = right;
        Kind = kind;
        Foam = foam;
        OceanBlend = oceanBlend;
    }
}

public readonly struct RiverReach
{
    public readonly int FirstSection, LastSection;
    public readonly RiverReachKind Kind;
    public RiverReach(int first, int last, RiverReachKind kind)
    {
        FirstSection = first;
        LastSection = last;
        Kind = kind;
    }
}

public readonly struct RiverWaterfall
{
    public readonly int CrestSection, ToeSection, PoolEndSection;
    public RiverWaterfall(int crest, int toe, int poolEnd)
    {
        CrestSection = crest;
        ToeSection = toe;
        PoolEndSection = poolEnd;
    }
}

/// <summary>The coastal handoff. The plan's estuary footprint continues past this section.</summary>
public readonly struct RiverMouth
{
    public readonly Vector2 Centre, Downstream;
    public readonly float Distance, WaterHeight, BedHeight, ChannelWidth, LeftBankExtent, RightBankExtent;
    public RiverMouth(RiverSection section, Vector2 downstream)
    {
        Centre = section.Centre;
        Downstream = downstream;
        Distance = section.Distance;
        WaterHeight = section.WaterHeight;
        BedHeight = section.BedHeight;
        ChannelWidth = section.LeftWaterWidth + section.RightWaterWidth;
        LeftBankExtent = section.LeftBank.Extent;
        RightBankExtent = section.RightBank.Extent;
    }
}

/// <summary>Meaning and elevation sampled from the authoritative two-dimensional landform.</summary>
public readonly struct RiverSurfaceSample
{
    public readonly bool IsDefined, IsWater;
    public readonly float TargetHeight, Influence, WaterHeight, Distance, RockWeight, GravelWeight;
    public readonly Vector2 Direction;
    public readonly RiverReachKind Kind;

    public RiverSurfaceSample(bool wet, float weightedTarget, float influence, float water, float distance,
        float rock, float gravel, Vector2 direction, RiverReachKind kind)
    {
        IsDefined = true;
        IsWater = wet;
        TargetHeight = weightedTarget;
        Influence = influence;
        WaterHeight = water;
        Distance = distance;
        RockWeight = rock;
        GravelWeight = gravel;
        Direction = direction;
        Kind = kind;
    }

    public float Apply(float originalHeight)
    {
        float target = originalHeight * (1f - Influence) + TargetHeight;
        return Kind == RiverReachKind.Estuary ? Mathf.Min(originalHeight, target) : target;
    }

    public Cell.TerrainType Classify(Cell.TerrainType underlying)
    {
        if (IsWater)
            return Kind == RiverReachKind.Waterfall ? Cell.TerrainType.WaterFall : Cell.TerrainType.River;
        if (Influence < 0.25f) return underlying;
        if (Kind == RiverReachKind.Estuary && underlying != Cell.TerrainType.Beach
            && underlying != Cell.TerrainType.Land && underlying != Cell.TerrainType.Cliff)
            return underlying;
        return RockWeight > 0.55f ? Cell.TerrainType.Cliff : Cell.TerrainType.Land;
    }
}

/// <summary>
/// Immutable connected river landscape. The public interface deliberately exposes only the
/// resolved feature. Internally RiverLandform owns the irregular 2D valley, channel, pool and
/// estuary field; ordered stations are retained as hydraulic/geological metadata.
/// </summary>
public sealed class RiverPlan
{
    public IReadOnlyList<RiverSection> Sections { get; }
    public IReadOnlyList<RiverReach> Reaches { get; }
    public IReadOnlyList<RiverWaterfall> Waterfalls { get; }
    public RiverMouth Mouth { get; }
    public Vector2 Source => Sections[0].Centre;
    public float Length => Sections[Sections.Count - 1].Distance;
    public float MaximumExcavation { get; }
    public float MaximumLandformFill { get; }

    private readonly RiverLandform landform;

    public RiverPlan(TerrainSampleCache terrain, IList<RiverSection> sections, IList<RiverWaterfall> falls,
        int mouthSection, float maximumExcavation, float maximumFill, int seed)
        : this(terrain, sections, falls, mouthSection, maximumExcavation, maximumFill, seed, null) { }

    internal RiverPlan(TerrainSampleCache terrain, IList<RiverSection> sections, IList<RiverWaterfall> falls,
        int mouthSection, float maximumExcavation, float maximumFill, int seed, RiverLandform.Buffers buffers)
    {
        if (terrain == null) throw new ArgumentNullException(nameof(terrain));
        if (sections == null || sections.Count < 3)
            throw new ArgumentException("A river needs a connected course.", nameof(sections));

        Sections = Array.AsReadOnly(new List<RiverSection>(sections).ToArray());
        Waterfalls = Array.AsReadOnly(new List<RiverWaterfall>(falls).ToArray());
        MaximumExcavation = maximumExcavation;
        MaximumLandformFill = maximumFill;
        int mouth = Mathf.Clamp(mouthSection, 1, sections.Count - 2);
        Mouth = new RiverMouth(sections[mouth],
            (sections[mouth + 1].Centre - sections[mouth - 1].Centre).normalized);

        var reaches = new List<RiverReach>();
        int first = 0;
        for (int i = 1; i < sections.Count; i++)
        {
            if (i < sections.Count - 1 && sections[i].Kind == sections[first].Kind) continue;
            reaches.Add(new RiverReach(first, i, sections[first].Kind));
            first = i;
        }
        Reaches = reaches.AsReadOnly();
        landform = new RiverLandform(terrain, Sections, Waterfalls, maximumExcavation, maximumFill, seed, buffers);
    }

    public bool TrySample(Vector2 position, out RiverSurfaceSample result)
        => landform.TrySample(position, out result);

    public void AppendWaterMesh(List<Vector3> positions, List<Vector2> uv, List<Color> colors, List<int> indices)
        => landform.AppendWaterMesh(positions, uv, colors, indices);
}
