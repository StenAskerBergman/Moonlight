using UnityEngine;

public class BuildingCondition : MonoBehaviour
{
    private Building building;
    private BuildingSimulation simulation;
    private BuildingSupply buildingSupply;
    private BuildingProperties buildingProperties;

    [SerializeField] private float checkInterval = 2f;
    private float timeSinceLastCheck;
    private bool pausedByCondition;

    private void Awake()
    {
        CacheComponents();
    }

    private void OnEnable()
    {
        RoadPlacer.OnRoadPlaced += HandleRoadNetworkChanged;
        RoadPlacer.OnRoadRemoved += HandleRoadNetworkChanged;
    }

    private void OnDisable()
    {
        RoadPlacer.OnRoadPlaced -= HandleRoadNetworkChanged;
        RoadPlacer.OnRoadRemoved -= HandleRoadNetworkChanged;
    }

    private void HandleRoadNetworkChanged(Cell changedCell)
    {
        CheckConditions();
    }

    private void Update()
    {
        timeSinceLastCheck += Time.deltaTime;
        if (timeSinceLastCheck < checkInterval) return;

        timeSinceLastCheck = 0f;
        CheckConditions();
    }

    public bool EvaluateOperationalConditions(out BuildingEnums.BuildingShutdownReason failureReason)
    {
        CacheComponents();
        failureReason = BuildingEnums.BuildingShutdownReason.None;

        BuildingData data = ResolveBuildingData();
        if (data == null || data.BuildingRequirements == null) return true;

        GridSystem grid = null;
        Vector3Int origin = Vector3Int.zero;
        Vector2Int footprint = Vector2Int.one;
        bool hasPlacement = buildingProperties != null &&
                            buildingProperties.TryGetPlacementCells(out grid, out origin, out footprint);
        Island island = buildingProperties != null ? buildingProperties.currentIsland : GetComponentInParent<Island>();
        bool quayFoundation = data.requiresQuayFoundation;

        foreach (BuildingRequirement requirement in data.BuildingRequirements)
        {
            if (requirement == null) continue;

            if (requirement is GridRequirement gridRequirement)
            {
                if (hasPlacement)
                {
                    if (!quayFoundation && !IsFootprintTerrainSatisfied(gridRequirement, grid, origin, footprint))
                    {
                        failureReason = BuildingEnums.BuildingShutdownReason.UnsupportedAnchor;
                        return false;
                    }

                    if (gridRequirement.requiresRoadAccess &&
                        !ConditionEvaluator.EvaluateFootprint(ConditionEnums.ConditionType.Has_Road, grid, origin, footprint, island))
                    {
                        failureReason = BuildingEnums.BuildingShutdownReason.NoRoadAccess;
                        return false;
                    }
                }
                else
                {
                    Cell ownCell = GetOwnCell();
                    if (!quayFoundation && !gridRequirement.IsTerrainSatisfied(ownCell))
                    {
                        failureReason = BuildingEnums.BuildingShutdownReason.UnsupportedAnchor;
                        return false;
                    }

                    if (gridRequirement.requiresRoadAccess &&
                        !ConditionEvaluator.Evaluate(ConditionEnums.ConditionType.Has_Road, ownCell))
                    {
                        failureReason = BuildingEnums.BuildingShutdownReason.NoRoadAccess;
                        return false;
                    }
                }
            }
            else if (!requirement.IsSatisfied())
            {
                failureReason = BuildingEnums.BuildingShutdownReason.UnsupportedAnchor;
                return false;
            }
        }

        return true;
    }

    public void CheckConditions()
    {
        CacheComponents();
        if (ResolveBuildingData() == null) return;

        BuildingEnums.BuildingState state = simulation != null
            ? simulation.CurrentState
            : (building != null ? building.CurrentState : BuildingEnums.BuildingState.UnderConstruction);

        if (state == BuildingEnums.BuildingState.UnderConstruction
            || state == BuildingEnums.BuildingState.Destroyed)
        {
            return;
        }

        if (!EvaluateOperationalConditions(out BuildingEnums.BuildingShutdownReason failureReason))
        {
            pausedByCondition = true;
            if (simulation != null)
            {
                simulation.SetShutdownReason(failureReason);
                simulation.SetState(BuildingEnums.BuildingState.Paused);
                simulation.SetEfficiency(0f);
            }
            else if (building != null)
            {
                building.SetState(BuildingEnums.BuildingState.Paused);
            }
            return;
        }

        bool pausedDueToSiteCondition = pausedByCondition || (simulation != null
            && simulation.CurrentState == BuildingEnums.BuildingState.Paused
            && (simulation.CurrentShutdownReason == BuildingEnums.BuildingShutdownReason.NoRoadAccess
                || simulation.CurrentShutdownReason == BuildingEnums.BuildingShutdownReason.UnsupportedAnchor));

        if (pausedDueToSiteCondition)
        {
            pausedByCondition = false;
            if (simulation != null)
            {
                simulation.SetShutdownReason(BuildingEnums.BuildingShutdownReason.None);
                simulation.SetState(BuildingEnums.BuildingState.Active);
                simulation.SetEfficiency(1f);
            }
            else if (building != null)
            {
                building.SetState(BuildingEnums.BuildingState.Active);
            }
        }

        if (simulation == null || simulation.CurrentShutdownReason != BuildingEnums.BuildingShutdownReason.PausedByPlayer)
        {
            buildingSupply?.CheckSupplyState();
        }
    }

    private void CacheComponents()
    {
        if (building == null) building = GetComponent<Building>();
        if (simulation == null) simulation = GetComponent<BuildingSimulation>();
        if (buildingSupply == null) buildingSupply = GetComponent<BuildingSupply>();
        if (buildingProperties == null) buildingProperties = GetComponent<BuildingProperties>();
    }

    private BuildingData ResolveBuildingData()
    {
        if (building != null && building.buildingData != null) return building.buildingData;
        return buildingProperties != null ? buildingProperties.buildingData : null;
    }

    private static bool IsFootprintTerrainSatisfied(
        GridRequirement gridRequirement,
        GridSystem grid,
        Vector3Int origin,
        Vector2Int footprint)
    {
        if (grid == null) return false;
        for (int x = 0; x < footprint.x; x++)
        {
            for (int z = 0; z < footprint.y; z++)
            {
                Cell cell = grid.GetCell(origin.x + x, origin.z + z);
                if (!gridRequirement.IsTerrainSatisfied(cell))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private Cell GetOwnCell()
    {
        if (buildingProperties == null || buildingProperties.gridSystem == null) return null;
        return buildingProperties.gridSystem.GetCellAtWorldPosition(transform.position);
    }
}
