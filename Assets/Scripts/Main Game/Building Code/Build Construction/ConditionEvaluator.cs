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
}
