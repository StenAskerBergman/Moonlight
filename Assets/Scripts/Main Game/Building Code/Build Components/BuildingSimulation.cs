using System;
using UnityEngine;

/// <summary>
/// Authoritative, headless simulation component for buildings.
/// Owns simulation state, efficiency, shutdown reasons, health, and construction progress.
/// Drives the reactive presentation layer exclusively through C# events (zero-polling observer model).
/// </summary>
public class BuildingSimulation : MonoBehaviour
{
    [Header("Simulation State")]
    [SerializeField] private BuildingEnums.BuildingState currentState = BuildingEnums.BuildingState.UnderConstruction;
    [SerializeField, Range(0f, 1f)] private float currentEfficiency = 0f;
    [SerializeField] private BuildingEnums.BuildingShutdownReason currentShutdownReason = BuildingEnums.BuildingShutdownReason.None;

    [Header("Health & Durability")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth = 100;

    [Header("Construction Progress")]
    [SerializeField, Range(0f, 1f)] private float constructionProgress = 0f;

    // Authoritative State Properties
    public BuildingEnums.BuildingState CurrentState => currentState;
    public float CurrentEfficiency => currentEfficiency;
    public BuildingEnums.BuildingShutdownReason CurrentShutdownReason => currentShutdownReason;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float ConstructionProgress => constructionProgress;
    public bool IsOperational => currentState == BuildingEnums.BuildingState.Active && currentEfficiency > 0f;

    // Reactive C# Events (Observed by Presentation, UI, and Logistics)
    public event Action<BuildingEnums.BuildingState> OnStateChanged;
    public event Action<float> OnEfficiencyChanged;
    public event Action<BuildingEnums.BuildingShutdownReason> OnShutdownReasonChanged;
    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action<float> OnConstructionProgressChanged;
    public event Action OnDamaged;
    public event Action OnDestroyed;

    private Building _legacyBuilding;

    private void Awake()
    {
        currentHealth = maxHealth;
        _legacyBuilding = GetComponent<Building>();
    }

    private void Start()
    {
        // Initial state broadcast
        if (_legacyBuilding != null && _legacyBuilding.CurrentState != currentState)
        {
            _legacyBuilding.SetState(currentState);
        }
    }

    /// <summary>
    /// Sets the high-level operational state of the building.
    /// </summary>
    public void SetState(BuildingEnums.BuildingState newState)
    {
        if (currentState == newState) return;

        currentState = newState;
        
        // Keep legacy Building component in sync
        if (_legacyBuilding != null && _legacyBuilding.CurrentState != newState)
        {
            _legacyBuilding.SetState(newState);
        }

        // Adjust shutdown reason defaults
        if (newState == BuildingEnums.BuildingState.Paused)
        {
            SetShutdownReason(BuildingEnums.BuildingShutdownReason.PausedByPlayer);
            SetEfficiency(0f);
        }
        else if (newState == BuildingEnums.BuildingState.UnderConstruction)
        {
            SetShutdownReason(BuildingEnums.BuildingShutdownReason.UnderConstruction);
            SetEfficiency(0f);
        }
        else if (newState == BuildingEnums.BuildingState.Active && currentShutdownReason == BuildingEnums.BuildingShutdownReason.PausedByPlayer)
        {
            SetShutdownReason(BuildingEnums.BuildingShutdownReason.None);
        }

        OnStateChanged?.Invoke(currentState);
    }

    /// <summary>
    /// Sets the runtime operating efficiency (0.0 to 1.0).
    /// </summary>
    public void SetEfficiency(float efficiency)
    {
        efficiency = Mathf.Clamp01(efficiency);
        if (Mathf.Approximately(currentEfficiency, efficiency)) return;

        currentEfficiency = efficiency;
        OnEfficiencyChanged?.Invoke(currentEfficiency);
    }

    /// <summary>
    /// Sets the discrete shutdown/stalled reason (observed by UI badges and audio alarms).
    /// </summary>
    public void SetShutdownReason(BuildingEnums.BuildingShutdownReason reason)
    {
        if (currentShutdownReason == reason) return;

        currentShutdownReason = reason;
        OnShutdownReasonChanged?.Invoke(currentShutdownReason);

        // If shutdown reason indicates a stoppage, zero out efficiency
        if (reason != BuildingEnums.BuildingShutdownReason.None)
        {
            SetEfficiency(0f);
        }
    }

    /// <summary>
    /// Updates construction progress (0.0 to 1.0). When reaching 1.0, transitions automatically to Active.
    /// </summary>
    public void SetConstructionProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (Mathf.Approximately(constructionProgress, progress)) return;

        constructionProgress = progress;
        OnConstructionProgressChanged?.Invoke(constructionProgress);

        if (constructionProgress >= 1f && currentState == BuildingEnums.BuildingState.UnderConstruction)
        {
            SetState(BuildingEnums.BuildingState.Active);
            SetShutdownReason(BuildingEnums.BuildingShutdownReason.None);
            SetEfficiency(1f);
        }
    }

    /// <summary>
    /// Applies damage to the building.
    /// </summary>
    public void TakeDamage(int damageAmount)
    {
        if (damageAmount <= 0 || currentState == BuildingEnums.BuildingState.Destroyed) return;

        currentHealth = Mathf.Max(0, currentHealth - damageAmount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamaged?.Invoke();

        if (currentHealth <= 0)
        {
            SetState(BuildingEnums.BuildingState.Destroyed);
            OnDestroyed?.Invoke();
        }
        else if (currentHealth < maxHealth * 0.3f)
        {
            SetShutdownReason(BuildingEnums.BuildingShutdownReason.Damaged);
        }
    }

    /// <summary>
    /// Repairs the building by the specified amount.
    /// </summary>
    public void Repair(int repairAmount)
    {
        if (repairAmount <= 0 || currentState == BuildingEnums.BuildingState.Destroyed) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + repairAmount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth >= maxHealth * 0.3f && currentShutdownReason == BuildingEnums.BuildingShutdownReason.Damaged)
        {
            SetShutdownReason(BuildingEnums.BuildingShutdownReason.None);
        }
    }

    public void SetMaxHealth(int newMax)
    {
        maxHealth = Mathf.Max(1, newMax);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
