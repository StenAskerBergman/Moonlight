using System;
using UnityEngine;

/// <summary>
/// Simulation controller for buildings in the UnderConstruction phase.
/// Drives progress from 0.0 to 1.0 and toggles scaffolding / foundation visibility.
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
        _simulation.SetConstructionProgress(0f);

        SetScaffoldingActive(true);
    }

    private void Update()
    {
        if (!_isBuilding) return;

        _elapsedTime += Time.deltaTime;
        float progress = buildDurationSeconds > 0f ? Mathf.Clamp01(_elapsedTime / buildDurationSeconds) : 1f;
        _simulation.SetConstructionProgress(progress);

        if (progress >= 1f)
        {
            FinishConstruction();
        }
    }

    public void FinishConstruction()
    {
        _isBuilding = false;
        _simulation.SetConstructionProgress(1f);
        SetScaffoldingActive(false);
    }

    private void SetScaffoldingActive(bool active)
    {
        if (foundationObject != null) foundationObject.SetActive(active);
        else if (active) AssetFallback.LogMissingDeliverable("GameObject", "foundationObject", this);

        if (scaffoldingObject != null) scaffoldingObject.SetActive(active);
        else if (active) AssetFallback.LogMissingDeliverable("GameObject", "scaffoldingObject", this);
    }
}
