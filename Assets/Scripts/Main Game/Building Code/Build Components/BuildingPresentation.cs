using UnityEngine;

/// <summary>
/// Reactive presentation observer for buildings.
/// Observes BuildingSimulation events without polling Update loops.
/// Drives machinery animators (with procedural phase offsets and jitter), smoke/steam VFX,
/// 3D positional audio loops, and damage/fire states.
/// </summary>
[RequireComponent(typeof(BuildingSimulation))]
public class BuildingPresentation : MonoBehaviour
{
    [Header("Simulation Reference")]
    [SerializeField] private BuildingSimulation simulation;

    [Header("Articulated Machinery (Optional)")]
    [SerializeField] private Animator machineryAnimator;
    [SerializeField, Range(0f, 0.15f)] private float speedJitter = 0.05f;

    [Header("Operational VFX (Optional)")]
    [SerializeField] private ParticleSystem smokeVfx;
    [SerializeField] private float baseSmokeRate = 10f;
    [SerializeField] private ParticleSystem fireVfx;

    [Header("Positional 3D Audio (Optional)")]
    [SerializeField] private AudioSource ambientAudioSource;
    [SerializeField] private AudioClip productionLoopClip;

    private float _randomSpeedMultiplier = 1f;

    private void Awake()
    {
        if (simulation == null)
        {
            simulation = GetComponent<BuildingSimulation>();
        }

        // Apply instance-specific speed jitter so adjacent buildings don't animate in lockstep
        _randomSpeedMultiplier = 1f + Random.Range(-speedJitter, speedJitter);

        if (machineryAnimator != null)
        {
            // Randomize starting animation frame/phase [0.0, 1.0]
            machineryAnimator.Play(0, -1, Random.value);
        }
        else
        {
            AssetFallback.LogMissingDeliverable("Animator", "machineryAnimator", this);
        }

        if (smokeVfx == null)
        {
            AssetFallback.LogMissingDeliverable("ParticleSystem", "smokeVfx", this);
        }

        if (ambientAudioSource == null)
        {
            AssetFallback.LogMissingDeliverable("AudioSource", "ambientAudioSource", this);
        }
        else if (productionLoopClip != null && ambientAudioSource.clip == null)
        {
            ambientAudioSource.clip = productionLoopClip;
        }
    }

    private void OnEnable()
    {
        if (simulation == null) return;

        // 1. Subscribe to simulation events
        simulation.OnStateChanged += HandleStateChanged;
        simulation.OnEfficiencyChanged += HandleEfficiencyChanged;
        simulation.OnShutdownReasonChanged += HandleShutdownReasonChanged;
        simulation.OnHealthChanged += HandleHealthChanged;

        // 2. Initial state catch-up (Save/Load and Pool safe)
        HandleStateChanged(simulation.CurrentState);
        HandleEfficiencyChanged(simulation.CurrentEfficiency);
        HandleShutdownReasonChanged(simulation.CurrentShutdownReason);
        HandleHealthChanged(simulation.CurrentHealth, simulation.MaxHealth);
    }

    private void OnDisable()
    {
        if (simulation == null) return;

        // Prevent dangling event references
        simulation.OnStateChanged -= HandleStateChanged;
        simulation.OnEfficiencyChanged -= HandleEfficiencyChanged;
        simulation.OnShutdownReasonChanged -= HandleShutdownReasonChanged;
        simulation.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleStateChanged(BuildingEnums.BuildingState state)
    {
        bool isActive = (state == BuildingEnums.BuildingState.Active);

        // Machinery Animator
        if (machineryAnimator != null)
        {
            machineryAnimator.enabled = isActive;
        }

        // Smoke / Steam VFX
        if (smokeVfx != null)
        {
            var emission = smokeVfx.emission;
            emission.enabled = isActive && simulation.CurrentEfficiency > 0f;
        }

        // Ambient Audio
        if (ambientAudioSource != null && ambientAudioSource.clip != null)
        {
            if (isActive && !ambientAudioSource.isPlaying)
            {
                ambientAudioSource.Play();
            }
            else if (!isActive && ambientAudioSource.isPlaying)
            {
                ambientAudioSource.Stop();
            }
        }
    }

    private void HandleEfficiencyChanged(float efficiency)
    {
        // Modulate Machinery Animation Speed
        if (machineryAnimator != null)
        {
            machineryAnimator.speed = efficiency * _randomSpeedMultiplier;
        }

        // Modulate Smoke / Steam Emission Rate
        if (smokeVfx != null)
        {
            var emission = smokeVfx.emission;
            emission.rateOverTimeMultiplier = efficiency * baseSmokeRate;
            emission.enabled = efficiency > 0f && simulation.CurrentState == BuildingEnums.BuildingState.Active;
        }

        // Modulate Audio Pitch and Volume
        if (ambientAudioSource != null)
        {
            ambientAudioSource.volume = Mathf.Clamp01(efficiency);
            ambientAudioSource.pitch = Mathf.Lerp(0.85f, 1.15f, efficiency);
        }
    }

    private void HandleShutdownReasonChanged(BuildingEnums.BuildingShutdownReason reason)
    {
        // If a building is shut down, freeze moving parts
        if (reason != BuildingEnums.BuildingShutdownReason.None)
        {
            if (machineryAnimator != null) machineryAnimator.speed = 0f;
            if (smokeVfx != null)
            {
                var emission = smokeVfx.emission;
                emission.enabled = false;
            }
        }
    }

    private void HandleHealthChanged(int currentHp, int maxHp)
    {
        float healthFraction = maxHp > 0 ? (float)currentHp / maxHp : 1f;

        // Toggle fire / damaged VFX if health drops below 35%
        if (fireVfx != null)
        {
            bool isBurning = healthFraction < 0.35f && currentHp > 0;
            var emission = fireVfx.emission;
            emission.enabled = isBurning;
        }
    }
}
