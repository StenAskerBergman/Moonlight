using UnityEngine;

/// <summary>
/// Simulation controller for buildings in the UnderConstruction phase.
/// Drives progress from 0.0 to 1.0, coordinates the ground-up construction reveal shader,
/// and commissions finished buildings (verifying road/anchor readiness) into Active or Paused.
/// </summary>
public class ConstructionSite : MonoBehaviour
{
    [Header("Construction Parameters")]
    [SerializeField] private float buildDurationSeconds = 4f;
    [SerializeField] private bool autoBuildOnStart = true;

    [Header("Visual Sub-Objects (Optional Deliverables)")]
    [SerializeField] private GameObject foundationObject;
    [SerializeField] private GameObject scaffoldingObject;

    private BuildingSimulation _simulation;
    private BuildingConstructionRevealDriver _revealDriver;
    private float _elapsedTime;
    private bool _isBuilding;

    public float Progress => _simulation != null ? _simulation.ConstructionProgress : 0f;
    public bool IsComplete => Progress >= 1f;

    private void Awake()
    {
        _simulation = GetComponent<BuildingSimulation>();
        if (_simulation == null)
        {
            _simulation = gameObject.AddComponent<BuildingSimulation>();
        }

        _revealDriver = GetComponent<BuildingConstructionRevealDriver>();
        if (_revealDriver == null)
        {
            _revealDriver = gameObject.AddComponent<BuildingConstructionRevealDriver>();
        }
    }

    private void Start()
    {
        if (autoBuildOnStart && _simulation.CurrentState == BuildingEnums.BuildingState.UnderConstruction)
        {
            StartConstruction();
        }
    }

    public void StartConstruction()
    {
        _isBuilding = true;
        _elapsedTime = 0f;
        _simulation.SetState(BuildingEnums.BuildingState.UnderConstruction);
        _simulation.SetConstructionProgress(0f, autoActivate: false);

        SetScaffoldingActive(true);
        _revealDriver?.BeginReveal();
    }

    private void Update()
    {
        if (!_isBuilding) return;

        _elapsedTime += Time.deltaTime;
        float progress = buildDurationSeconds > 0f ? Mathf.Clamp01(_elapsedTime / buildDurationSeconds) : 1f;
        _simulation.SetConstructionProgress(progress, autoActivate: false);
        _revealDriver?.SetProgress(progress);

        if (progress >= 1f)
        {
            FinishConstruction();
        }
    }

    public void FinishConstruction()
    {
        _isBuilding = false;
        _simulation.SetConstructionProgress(1f, autoActivate: false);
        _revealDriver?.EndReveal();
        SetScaffoldingActive(false);

        GetComponent<PlayerOwnershipVisual>()?.Refresh();
        CommissionBuilding();
        GetComponent<Building>()?.RefreshOutlineRenderers();
    }

    private void CommissionBuilding()
    {
        if (GetComponent<BuildingStatusBadge>() == null)
        {
            gameObject.AddComponent<BuildingStatusBadge>();
        }

        BuildingCondition condition = GetComponent<BuildingCondition>();
        if (condition == null)
        {
            condition = gameObject.AddComponent<BuildingCondition>();
        }

        if (condition.EvaluateOperationalConditions(out BuildingEnums.BuildingShutdownReason failureReason))
        {
            _simulation.SetShutdownReason(BuildingEnums.BuildingShutdownReason.None);
            _simulation.SetState(BuildingEnums.BuildingState.Active);
            _simulation.SetEfficiency(1f);
        }
        else
        {
            _simulation.SetShutdownReason(failureReason);
            _simulation.SetState(BuildingEnums.BuildingState.Paused);
            _simulation.SetEfficiency(0f);
        }
    }

    private void SetScaffoldingActive(bool active)
    {
        if (foundationObject != null) foundationObject.SetActive(active);
        if (scaffoldingObject != null) scaffoldingObject.SetActive(active);
    }
}
