using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Core MonoBehaviour for naval vessels in Moonlight.
/// Associates the runtime unit with its NavalUnitDefinition and synchronizes stats across
/// Damageable, NavMeshAgent, UnitMovement, UnitInventory, UnitEquipment, and UnitAbilities.
/// </summary>
[RequireComponent(typeof(Unit))]
public class NavalUnit : MonoBehaviour
{
    [SerializeField] public NavalUnitDefinition definition;

    protected Unit unit;
    protected Damageable damageable;
    protected NavMeshAgent agent;
    protected UnitMovement movement;
    protected UnitInventory unitInventory;
    protected UnitEquipment equipment;
    protected UnitAbilities unitAbilities;
    protected DiveInteraction diveInteraction;

    private float empDisabledUntil = 0f;

    public NavalUnitDefinition Definition => definition;
    public Unit Unit => unit != null ? unit : (unit = GetComponent<Unit>());
    public UnitEquipment Equipment => equipment != null ? equipment : (equipment = GetComponent<UnitEquipment>());
    public UnitAbilities Abilities => unitAbilities != null ? unitAbilities : (unitAbilities = GetComponent<UnitAbilities>());
    public DiveInteraction Dive => diveInteraction != null ? diveInteraction : (diveInteraction = GetComponent<DiveInteraction>());

    public bool IsEmpDisabled => Time.time < empDisabledUntil;

    public virtual NavalMovementState CurrentState
    {
        get
        {
            if (IsEmpDisabled) return NavalMovementState.Disabled;

            if (Dive != null && Dive.IsSubmerged)
            {
                return NavalMovementState.Submerged;
            }

            if (agent != null && agent.enabled && agent.hasPath && agent.velocity.sqrMagnitude > 0.05f)
            {
                return NavalMovementState.Moving;
            }

            return NavalMovementState.Idle;
        }
    }

    protected virtual void Awake()
    {
        unit = GetComponent<Unit>();
        damageable = GetComponent<Damageable>();
        agent = GetComponent<NavMeshAgent>();
        movement = GetComponent<UnitMovement>();
        unitInventory = GetComponent<UnitInventory>();
        equipment = GetComponent<UnitEquipment>();
        unitAbilities = GetComponent<UnitAbilities>();
        diveInteraction = GetComponent<DiveInteraction>();

        if (definition != null)
        {
            ApplyDefinition(definition);
        }
    }

    public virtual void ApplyDefinition(NavalUnitDefinition def)
    {
        definition = def;
        if (def == null) return;

        // Display Name
        if (unit != null && !string.IsNullOrEmpty(def.displayName))
        {
            unit.SetDisplayName(def.displayName);
        }

        // Hull / Health
        if (damageable != null && def.maxHealth > 0)
        {
            var prop = typeof(Damageable).GetField("totalHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (prop != null) prop.SetValue(damageable, def.maxHealth);
            damageable.currentHealth = def.maxHealth;
        }

        // NavMesh Agent Navigation Stats
        if (agent != null)
        {
            agent.speed = def.movementSpeed;
            agent.acceleration = def.acceleration;
            agent.angularSpeed = def.turnSpeed;
        }

        // Cargo Inventory Configuration
        if (unitInventory != null)
        {
            unitInventory.ConfigureSlots(def.cargoSlotCount, def.cargoCapacityPerSlot);
        }

        // Equipment Slots Configuration
        if (Equipment != null)
        {
            Equipment.ConfigureSlots(def.equipmentSlotCount);
        }

        // Abilities Binding
        if (Abilities != null && def.abilities != null)
        {
            Abilities.SetAbilities(def.abilities);
        }
    }

    public void ApplyEmpDisable(float duration)
    {
        empDisabledUntil = Mathf.Max(empDisabledUntil, Time.time + duration);
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
    }

    /// <summary>
    /// Checks whether this vessel can target another unit based on data-driven capability flags.
    /// Purely categorical — never checks vessel names.
    /// </summary>
    public bool CanTarget(Unit target)
    {
        if (target == null || target == unit || definition == null) return false;

        CombatTargetCapabilities reqFlag = CombatTargetCapabilities.Surface;

        DiveInteraction targetDive = target.GetComponent<DiveInteraction>();
        bool isSubmerged = false;
        if (targetDive != null)
        {
            isSubmerged = targetDive.IsSubmerged || target.transform.position.y <= -10f;
        }
        else if (target.moveType == MoveType.Submersible)
        {
            isSubmerged = target.transform.position.y <= -10f;
        }

        if (isSubmerged)
        {
            reqFlag = CombatTargetCapabilities.Submarine;
        }
        else if (target.moveType == MoveType.Aircraft || target.unitType == UnitType.Drone)
        {
            reqFlag = CombatTargetCapabilities.Air;
        }

        return (definition.attackCapabilities & reqFlag) != 0;
    }
}
