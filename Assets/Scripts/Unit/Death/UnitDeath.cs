using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// What dying looks like. Boats list over and sink below the waterline; everything else
/// falls and fades. Either way the unit stops being a participant the instant it dies -
/// deselected, unable to move, and no longer blocking anything - and the corpse is
/// purely cosmetic until it is destroyed.
///
/// The sink is procedural because no unit prefab has an Animator. If one is added with a
/// matching trigger, that is played instead and the procedural fallback is skipped.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Unit))]
public sealed class UnitDeath : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds from death to the wreck disappearing.")]
    [SerializeField, Min(0.1f)] private float sinkDuration = 4f;

    [Tooltip("Extra seconds the wreck lingers below the surface before being destroyed.")]
    [SerializeField, Min(0f)] private float lingerAfterSink = 0.5f;

    [Header("Sinking (boats)")]
    [Tooltip("How far the hull drops, in world units. Must clear the camera's view of the surface.")]
    [SerializeField] private float sinkDepth = 6f;

    [Tooltip("Degrees the hull rolls onto its side as it goes down.")]
    [SerializeField] private float sinkRoll = 55f;

    [Tooltip("Degrees the bow pitches up as the stern goes under.")]
    [SerializeField] private float sinkPitch = 18f;

    [Header("Land units")]
    [Tooltip("Degrees the unit topples as it dies.")]
    [SerializeField] private float collapseRoll = 90f;

    [Header("Animation")]
    [Tooltip("Optional Animator trigger. When the unit has an Animator with this trigger, it plays instead of the procedural sequence.")]
    [SerializeField] private string deathTrigger = "Die";

    [Header("Cargo")]
    [Tooltip("ON: cargo goes down with the ship. OFF: cargo is silently voided (kept for testing).")]
    [SerializeField] private bool cargoSinksWithUnit = true;

    private bool dying;

    public delegate void DiedHandler(UnitDeath unit);
    public event DiedHandler OnDeathSequenceStarted;

    private void Awake()
    {
        UnitHealth health = GetComponent<UnitHealth>();
        if (health != null) health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        UnitHealth health = GetComponent<UnitHealth>();
        if (health != null) health.OnDied -= HandleDied;
    }

    private void HandleDied(UnitHealth health) => BeginDeath();

    /// <summary>
    /// Starts the death sequence directly, for a unit with no <see cref="UnitHealth"/>.
    /// </summary>
    public void BeginDeath()
    {
        if (dying) return;
        dying = true;

        Detach();
        OnDeathSequenceStarted?.Invoke(this);

        if (TryPlayDeathAnimation())
        {
            StartCoroutine(DestroyAfter(sinkDuration + lingerAfterSink));
            return;
        }

        StartCoroutine(InfluenceManager.IsBoatUnit(GetComponent<Unit>()) ? SinkRoutine() : CollapseRoutine());
    }

    /// <summary>
    /// Take the unit out of every system that would otherwise keep driving it or keep
    /// showing it to the player. Done before any animation so a dying unit can't be
    /// re-selected, ordered around, or counted as alive mid-sink.
    /// </summary>
    private void Detach()
    {
        Unit unit = GetComponent<Unit>();

        if (UnitSelections.Instance != null)
        {
            if (UnitSelections.Instance.unitsSelected != null && UnitSelections.Instance.unitsSelected.Contains(unit))
            {
                unit.OnDeselect();
                UnitSelections.Instance.unitsSelected.Remove(unit);
                UnitSelections.Instance.NotifySelectionChanged();
            }

            // Off the roster too, so nothing counts a wreck as a live vessel - the
            // "island already settled" and founding-boat searches both read this.
            UnitSelections.Instance.unitList?.Remove(unit);
        }

        if (unit != null) unit.Selectable = false;

        // Movement first: a live NavMeshAgent overwrites transform changes every frame,
        // so the hull would never actually descend.
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
        }

        UnitMovement movement = GetComponent<UnitMovement>();
        if (movement != null) movement.enabled = false;

        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        if (cargoSinksWithUnit) return;

        UnitInventory cargo = GetComponent<UnitInventory>();
        if (cargo != null)
        {
            foreach (var entry in new System.Collections.Generic.Dictionary<ItemData, int>(cargo.GetAllItems()))
            {
                if (entry.Key != null && entry.Value > 0) cargo.RemoveItem(entry.Key, entry.Value);
            }
        }
    }

    private bool TryPlayDeathAnimation()
    {
        if (string.IsNullOrEmpty(deathTrigger)) return false;

        Animator animator = GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null) return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Trigger) continue;
            if (parameter.name != deathTrigger) continue;

            animator.SetTrigger(deathTrigger);
            return true;
        }

        return false;
    }

    private IEnumerator SinkRoutine()
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;

        Vector3 endPosition = startPosition + Vector3.down * sinkDepth;
        Quaternion endRotation = startRotation * Quaternion.Euler(sinkPitch, 0f, sinkRoll);

        float elapsed = 0f;
        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / sinkDuration);

            // Ease in: the hull hangs a moment, then goes under quickly, rather than
            // descending at a constant and obviously linear rate.
            float descent = t * t;

            transform.position = Vector3.Lerp(startPosition, endPosition, descent);
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, Mathf.Sqrt(t));

            yield return null;
        }

        yield return new WaitForSeconds(lingerAfterSink);
        Destroy(gameObject);
    }

    private IEnumerator CollapseRoutine()
    {
        Quaternion startRotation = transform.rotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(collapseRoll, 0f, 0f);
        Vector3 startPosition = transform.position;

        float elapsed = 0f;
        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / sinkDuration);

            transform.rotation = Quaternion.Slerp(startRotation, endRotation, Mathf.Min(1f, t * 3f));

            // Sink into the ground only at the end, so the topple reads first.
            if (t > 0.6f)
            {
                float submerge = Mathf.InverseLerp(0.6f, 1f, t);
                transform.position = Vector3.Lerp(startPosition, startPosition + Vector3.down * 2f, submerge);
            }

            yield return null;
        }

        yield return new WaitForSeconds(lingerAfterSink);
        Destroy(gameObject);
    }

    private IEnumerator DestroyAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Destroy(gameObject);
    }
}
