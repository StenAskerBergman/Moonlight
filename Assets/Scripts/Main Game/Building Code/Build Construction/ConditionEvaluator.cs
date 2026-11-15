// Evaluates ConditionEnums.ConditionType values that need live world state
// (road access, etc). Conditions with no evaluation logic yet default to true.
public static class ConditionEvaluator
{
    public static bool Evaluate(ConditionEnums.ConditionType condition, Cell buildingCell)
    {
        return condition switch
        {
            ConditionEnums.ConditionType.Has_Road => RoadNetwork.Instance != null && RoadNetwork.Instance.HasRoadAccess(buildingCell),
            _ => true
        };
    }

    /// <summary>
    /// Footprint-aware runtime condition check used at construction commissioning and on road updates.
    /// </summary>
    public static bool EvaluateFootprint(
        ConditionEnums.ConditionType condition,
        GridSystem grid,
        UnityEngine.Vector3Int origin,
        UnityEngine.Vector2Int footprint,
        Island island = null)
    {
        switch (condition)
        {
            case ConditionEnums.ConditionType.Has_Road:
                return RoadNetwork.Instance != null
                    && grid != null
                    && RoadNetwork.Instance.HasRoadAccess(grid, origin, footprint);
            case ConditionEnums.ConditionType.Has_CityCenter:
                if (island == null) return true;
                InfluenceManager influence = island.GetComponent<InfluenceManager>();
                return influence != null && influence.HasWarehouse;
            default:
                return true;
        }
    }
}
