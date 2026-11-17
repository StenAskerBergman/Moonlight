// tldr: One accepted river course - its ordered hydraulic stations, waterfalls, how it ends (a shared ocean
// outlet or a confluence with a parent course) and an optional source lake. Sampling goes through the network.

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A reach is a connected part of the course, not a second path or footprint. Lake is never
/// assigned to a station: it is the kind the landform gives cells inside a source-lake basin.
/// </summary>
public enum RiverReachKind : byte { Headwater, Gorge, Waterfall, PlungePool, Lowland, Estuary, Lake }

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
/// Distance is measured in the network's flow coordinate: a tributary's distances are offset so
/// that its last station carries the same value as the parent station it joins.
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

    /// <summary>The same station moved along the flow coordinate.</summary>
    public RiverSection WithDistance(float distance)
        => new RiverSection(Centre, Lateral, distance, WaterHeight, BedHeight, LeftWaterWidth,
            RightWaterWidth, LeftBank, RightBank, Kind, Foam, OceanBlend);
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

/// <summary>Where a tributary hands its water to a parent course.</summary>
public readonly struct RiverConfluence
{
    /// <summary>Index of the parent course in RiverNetwork.Courses.</summary>
    public readonly int ParentCourse;
    /// <summary>Parent station the tributary ends on.</summary>
    public readonly int ParentSection;
    public readonly Vector2 Centre;
    public readonly float Distance, WaterHeight;

    public RiverConfluence(int parentCourse, int parentSection, RiverSection station)
    {
        ParentCourse = parentCourse;
        ParentSection = parentSection;
        Centre = station.Centre;
        Distance = station.Distance;
        WaterHeight = station.WaterHeight;
    }
}

/// <summary>A closed basin the course drains out of instead of starting at a rock spring.</summary>
public readonly struct RiverSourceLake
{
    public readonly Vector2 Centre;
    public readonly float Radius, WaterHeight, Depth;
    public RiverSourceLake(Vector2 centre, float radius, float waterHeight, float depth)
    {
        Centre = centre;
        Radius = radius;
        WaterHeight = waterHeight;
        Depth = depth;
    }
}

/// <summary>
/// The shared "pour out of the island". Exactly one trunk course crosses the shoreline here and
/// owns the estuary geometry; every tributary that joins that trunk, directly or through another
/// tributary, leaves the island through the same outlet instead of cutting a mouth of its own.
/// </summary>
public sealed class RiverOutlet
{
    public int Index { get; }
    public RiverMouth Mouth { get; }
    /// <summary>Course index of the trunk that carries the estuary.</summary>
    public int TrunkCourse { get; }
    /// <summary>Every course draining through this outlet, trunk first.</summary>
    public IReadOnlyList<int> Courses { get; }

    internal RiverOutlet(int index, RiverMouth mouth, int trunkCourse, IList<int> courses)
    {
        Index = index;
        Mouth = mouth;
        TrunkCourse = trunkCourse;
        Courses = Array.AsReadOnly(new List<int>(courses).ToArray());
    }
}

/// <summary>Meaning and elevation sampled from the authoritative two-dimensional landform.</summary>
public readonly struct RiverSurfaceSample
{
    public readonly bool IsDefined, IsWater;
    public readonly float TargetHeight, Influence, WaterHeight, Distance, RockWeight, GravelWeight;
    public readonly Vector2 Direction;
    public readonly RiverReachKind Kind;
    /// <summary>Course that owns this point (nearest centreline), or -1.</summary>
    public readonly int Course;

    public RiverSurfaceSample(bool wet, float weightedTarget, float influence, float water, float distance,
        float rock, float gravel, Vector2 direction, RiverReachKind kind, int course = -1)
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
        Course = course;
    }

    public float Apply(float originalHeight)
    {
        float target = originalHeight * (1f - Influence) + TargetHeight;
        return Kind == RiverReachKind.Estuary ? Mathf.Min(originalHeight, target) : target;
    }

    public Cell.TerrainType Classify(Cell.TerrainType underlying)
    {
        if (IsWater)
            return Kind == RiverReachKind.Waterfall ? Cell.TerrainType.WaterFall
                : Kind == RiverReachKind.Lake ? Cell.TerrainType.Lake : Cell.TerrainType.River;
        if (Influence < 0.25f) return underlying;
        if (Kind == RiverReachKind.Estuary && underlying != Cell.TerrainType.Beach
            && underlying != Cell.TerrainType.Land && underlying != Cell.TerrainType.Cliff)
            return underlying;
        return RockWeight > 0.55f ? Cell.TerrainType.Cliff : Cell.TerrainType.Land;
    }
}

/// <summary>
/// One course of the river network: a trunk that ends at a shared RiverOutlet, or a tributary
/// that ends on a parent course. The resolved landscape is owned by the network, because
/// confluences only exist in a field that every course was stamped into together.
/// </summary>
public sealed class RiverPlan
{
    public int Index { get; }
    public IReadOnlyList<RiverSection> Sections { get; }
    public IReadOnlyList<RiverReach> Reaches { get; }
    public IReadOnlyList<RiverWaterfall> Waterfalls { get; }
    /// <summary>Parent course index, or -1 for a trunk.</summary>
    public int Parent { get; }
    public bool IsTributary => Parent >= 0;
    /// <summary>Valid only for tributaries.</summary>
    public RiverConfluence Confluence { get; }
    /// <summary>The outlet this course ultimately drains through. Set by the network.</summary>
    public RiverOutlet Outlet { get; internal set; }
    /// <summary>The shoreline crossing of this course's outlet (the trunk's mouth for tributaries).</summary>
    public RiverMouth Mouth => Outlet != null ? Outlet.Mouth : default;
    public bool HasSourceLake { get; }
    public RiverSourceLake SourceLake { get; }
    public Vector2 Source => Sections[0].Centre;
    public float StartDistance => Sections[0].Distance;
    /// <summary>Flow-coordinate distance at which this course stops being its own channel.</summary>
    public float TerminalDistance { get; }
    public float Length => Sections[Sections.Count - 1].Distance - Sections[0].Distance;
    public float MaximumExcavation { get; }
    public float MaximumLandformFill { get; }
    public RiverNetwork Network { get; internal set; }

    internal RiverPlan(int index, IList<RiverSection> sections, IList<RiverWaterfall> falls, int parent,
        RiverConfluence confluence, float terminalDistance, bool hasLake, RiverSourceLake lake,
        float maximumExcavation, float maximumFill)
    {
        if (sections == null || sections.Count < 3)
            throw new ArgumentException("A river needs a connected course.", nameof(sections));

        Index = index;
        Sections = Array.AsReadOnly(new List<RiverSection>(sections).ToArray());
        Waterfalls = Array.AsReadOnly(new List<RiverWaterfall>(falls).ToArray());
        Parent = parent;
        Confluence = confluence;
        TerminalDistance = terminalDistance;
        HasSourceLake = hasLake;
        SourceLake = lake;
        MaximumExcavation = maximumExcavation;
        MaximumLandformFill = maximumFill;

        var reaches = new List<RiverReach>();
        int first = 0;
        for (int i = 1; i < sections.Count; i++)
        {
            if (i < sections.Count - 1 && sections[i].Kind == sections[first].Kind) continue;
            reaches.Add(new RiverReach(first, i, sections[first].Kind));
            first = i;
        }
        Reaches = reaches.AsReadOnly();
    }

    /// <summary>Samples the network field. The sample's Course tells which course owns the point.</summary>
    public bool TrySample(Vector2 position, out RiverSurfaceSample result)
    {
        if (Network != null) return Network.TrySample(position, out result);
        result = default;
        return false;
    }
}

/// <summary>
/// Immutable connected river landscape for a whole island: every course, the outlets they
/// share, and the single resolved landform they were stamped into.
/// </summary>
public sealed class RiverNetwork
{
    public IReadOnlyList<RiverPlan> Courses { get; }
    public IReadOnlyList<RiverOutlet> Outlets { get; }
    public float MaximumExcavation { get; }
    public float MaximumLandformFill { get; }

    private readonly RiverLandform landform;

    internal RiverNetwork(IList<RiverPlan> courses, IList<RiverOutlet> outlets, RiverLandform landform,
        float maximumExcavation, float maximumFill)
    {
        Courses = Array.AsReadOnly(new List<RiverPlan>(courses).ToArray());
        Outlets = Array.AsReadOnly(new List<RiverOutlet>(outlets).ToArray());
        this.landform = landform;
        MaximumExcavation = maximumExcavation;
        MaximumLandformFill = maximumFill;
        foreach (RiverPlan course in Courses) course.Network = this;
        foreach (RiverOutlet outlet in Outlets)
        foreach (int course in outlet.Courses)
            Courses[course].Outlet = outlet;
    }

    public bool TrySample(Vector2 position, out RiverSurfaceSample result)
        => landform.TrySample(position, out result);

    public void AppendWaterMesh(List<Vector3> positions, List<Vector2> uv, List<Color> colors, List<int> indices)
        => landform.AppendWaterMesh(positions, uv, colors, indices);
}
