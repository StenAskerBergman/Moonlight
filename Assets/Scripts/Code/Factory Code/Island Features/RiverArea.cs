using System;
using UnityEngine;

/// <summary>
/// Projects the generated landscape's meaning onto gameplay cells. It never traces a second
/// river, stamps a centreline, changes terrain height or invents waterfall/source features.
/// </summary>
public class RiverArea : IFeature
{
    public void GenerateFeature() { /* A RiverPlan is required; no independent fallback river. */ }

    public void GenerateRiver(Cell[,] grid, System.Random rng) => GenerateRiver(grid, null, rng);

    public void GenerateRiver(Cell[,] grid, FeatureReservationMap reservations, System.Random rng, Vector2 offset = default)
    {
        if (grid == null || reservations == null || reservations.RiverNetwork == null) return;
        RiverNetwork network = reservations.RiverNetwork;

        // One pass over the merged field. Each wet cell belongs to the course that owns it, so a
        // confluence is classified once instead of once per overlapping course.
        for (int z = 0; z < grid.GetLength(1); z++) for (int x = 0; x < grid.GetLength(0); x++)
        {
            Cell cell = grid[x, z];
            Vector2 centre = new Vector2(x + 0.5f, z + 0.5f) - offset;
            if (!network.TrySample(centre, out RiverSurfaceSample sample) || !sample.IsWater) continue;
            cell.ChangeTerrainType(sample.Classify(cell.currentTerrainType));
            cell.SetRiverData(Status(network, sample), Direction(sample.Direction));
        }

        foreach (RiverPlan plan in network.Courses)
        {
            ResourceNodeType sourceType = ResourceNodeType.RiverSource;
            int sources = PlaceBankSlot(grid, plan, plan.Source, plan.StartDistance,
                plan.StartDistance + (plan.HasSourceLake ? plan.SourceLake.Radius + 3f : 3f), sourceType, offset) ? 1 : 0;
            int waterfalls = 0;
            foreach (RiverWaterfall fall in plan.Waterfalls)
            {
                RiverSection crest = plan.Sections[fall.CrestSection];
                RiverSection pool = plan.Sections[fall.ToeSection];
                if (PlaceBankSlot(grid, plan, pool.Centre, crest.Distance,
                    plan.Sections[fall.PoolEndSection].Distance, ResourceNodeType.Waterfall, offset)) waterfalls++;
            }

            // Place ordinary riverbank access sparsely, on actual dry, buildable bank ground.
            int banks = 0;
            for (float at = plan.StartDistance + 6f; at < plan.TerminalDistance - 3f; at += 7f)
            {
                foreach (RiverSection section in plan.Sections)
                {
                    if (section.Distance < at) continue;
                    if (section.Kind == RiverReachKind.Lowland &&
                        PlaceBankSlot(grid, plan, section.Centre, at - 1f, at + 2f, ResourceNodeType.RiverBank, offset)) banks++;
                    break;
                }
            }
            // Only the trunk crosses the shoreline; tributaries share its outlet.
            int mouths = !plan.IsTributary && PlaceMouthSlot(grid, plan, offset) ? 1 : 0;
            Debug.Log($"[RiverPlan slots] course {plan.Index} ({(plan.IsTributary ? $"tributary of {plan.Parent}" : "trunk")}): " +
                $"source={sources}, waterfall={waterfalls}/{plan.Waterfalls.Count}, banks={banks}, mouth={mouths}. " +
                "Missing slots mean no valid placement cell.");
        }
    }

    private static Cell.RiverStatus Status(RiverNetwork network, RiverSurfaceSample sample)
    {
        if (sample.Course < 0 || sample.Course >= network.Courses.Count) return Cell.RiverStatus.River;
        RiverPlan course = network.Courses[sample.Course];
        if (sample.Kind == RiverReachKind.Lake) return Cell.RiverStatus.LakeSource;
        if (sample.Distance < course.StartDistance + 1.4f) return Cell.RiverStatus.RiverSource;
        if (!course.IsTributary && Mathf.Abs(sample.Distance - course.Mouth.Distance) < 0.9f)
            return Cell.RiverStatus.RiverMouth;
        return Cell.RiverStatus.River;
    }

    private static bool PlaceBankSlot(Cell[,] grid, RiverPlan plan, Vector2 anchor, float from, float to, ResourceNodeType type, Vector2 offset)
    {
        Cell best = null; float bestDistance = float.PositiveInfinity;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(anchor.x + offset.x - 5f)), x1 = Mathf.Min(grid.GetLength(0) - 1, Mathf.CeilToInt(anchor.x + offset.x + 5f));
        int z0 = Mathf.Max(0, Mathf.FloorToInt(anchor.y + offset.y - 5f)), z1 = Mathf.Min(grid.GetLength(1) - 1, Mathf.CeilToInt(anchor.y + offset.y + 5f));
        for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
        {
            Cell cell = grid[x, z];
            if (cell.isDeposit || !cell.IsBuildableSurface || cell.occupyingBuilding != null) continue;
            Vector2 p = new Vector2(x + 0.5f, z + 0.5f) - offset;
            if (!plan.TrySample(p, out RiverSurfaceSample sample) || sample.IsWater || sample.Influence < 0.5f
                || sample.Course != plan.Index || sample.Distance < from || sample.Distance > to) continue;
            bool nextToChannel = false;
            for (int d = 0; d < 4; d++)
            {
                Vector2 neighborOffset = d == 0 ? Vector2.right : d == 1 ? Vector2.left : d == 2 ? Vector2.up : Vector2.down;
                if (plan.TrySample(p + neighborOffset, out RiverSurfaceSample neighbor) && neighbor.IsWater)
                { nextToChannel = true; break; }
            }
            if (!nextToChannel) continue;
            float distance = (p - anchor).sqrMagnitude;
            if (distance >= bestDistance) continue;
            best = cell; bestDistance = distance;
        }
        if (best == null) return false;
        best.SetDeposit(type); return true;
    }

    private static bool PlaceMouthSlot(Cell[,] grid, RiverPlan plan, Vector2 offset)
    {
        Cell best = null; float closest = float.PositiveInfinity;
        for (int z = 0; z < grid.GetLength(1); z++) for (int x = 0; x < grid.GetLength(0); x++)
        {
            Cell cell = grid[x, z];
            if (cell.isDeposit) continue;
            Vector2 p = new Vector2(x + 0.5f, z + 0.5f) - offset;
            if (!plan.TrySample(p, out RiverSurfaceSample sample) || !sample.IsWater
                || sample.Kind != RiverReachKind.Estuary || Mathf.Abs(sample.Distance - plan.Mouth.Distance) > 1.1f) continue;
            float distance = (p - plan.Mouth.Centre).sqrMagnitude;
            if (distance >= closest) continue;
            best = cell; closest = distance;
        }
        if (best == null) return false;
        best.SetDeposit(ResourceNodeType.RiverMouth); return true;
    }

    private static Cell.RiverDirection Direction(Vector2 direction)
    {
        int x = Mathf.RoundToInt(direction.x), z = Mathf.RoundToInt(direction.y);
        if (x > 0) return z > 0 ? Cell.RiverDirection.NorthEast : z < 0 ? Cell.RiverDirection.SouthEast : Cell.RiverDirection.East;
        if (x < 0) return z > 0 ? Cell.RiverDirection.NorthWest : z < 0 ? Cell.RiverDirection.SouthWest : Cell.RiverDirection.West;
        return z >= 0 ? Cell.RiverDirection.North : Cell.RiverDirection.South;
    }
}
