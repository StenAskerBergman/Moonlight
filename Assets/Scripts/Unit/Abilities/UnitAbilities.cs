using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime manager for a unit's active and passive abilities.
/// Tracks cooldowns, execution states, and communicates with UI without hardcoding unit types.
/// </summary>
public class UnitAbilities : MonoBehaviour
{
    [System.Serializable]
    public class RuntimeAbility
    {
        public AbilityDefinition definition;
        public float cooldownRemaining;
        public bool isToggled;

        public bool IsOnCooldown => cooldownRemaining > 0f;
        public float CooldownNormalized => (definition != null && definition.cooldown > 0f)
            ? Mathf.Clamp01(cooldownRemaining / definition.cooldown)
            : 0f;

        public RuntimeAbility(AbilityDefinition def)
        {
            definition = def;
            cooldownRemaining = 0f;
            isToggled = false;
        }
    }

    [SerializeField] private List<AbilityDefinition> initialAbilities = new List<AbilityDefinition>();
    private readonly List<RuntimeAbility> runtimeAbilities = new List<RuntimeAbility>();

    private Unit ownerUnit;
    private float passivePulseTimer = 0f;

    public IReadOnlyList<RuntimeAbility> Abilities => runtimeAbilities;

    public event Action OnAbilitiesChanged;
    public event Action<int, float> OnAbilityCooldownUpdated;

    private void Awake()
    {
        ownerUnit = GetComponent<Unit>();
        InitializeFromDefinitions(initialAbilities);
    }

    public void SetAbilities(IEnumerable<AbilityDefinition> definitions)
    {
        runtimeAbilities.Clear();
        if (definitions != null)
        {
            foreach (var def in definitions)
            {
                if (def != null)
                {
                    runtimeAbilities.Add(new RuntimeAbility(def));
                }
            }
        }
        OnAbilitiesChanged?.Invoke();
    }

    public void InitializeFromDefinitions(IEnumerable<AbilityDefinition> definitions)
    {
        if (definitions == null) return;
        foreach (var def in definitions)
        {
            if (def != null && !runtimeAbilities.Exists(a => a.definition == def))
            {
                runtimeAbilities.Add(new RuntimeAbility(def));
            }
        }
        OnAbilitiesChanged?.Invoke();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // Tick cooldowns
        for (int i = 0; i < runtimeAbilities.Count; i++)
        {
            var ability = runtimeAbilities[i];
            if (ability.cooldownRemaining > 0f)
            {
                ability.cooldownRemaining = Mathf.Max(0f, ability.cooldownRemaining - dt);
                OnAbilityCooldownUpdated?.Invoke(i, ability.cooldownRemaining);
            }
        }

        // Tick passive abilities (such as Fleet Repair pulse)
        passivePulseTimer += dt;
        if (passivePulseTimer >= 2f)
        {
            passivePulseTimer = 0f;
            ExecutePassives();
        }
    }

    private void ExecutePassives()
    {
        foreach (var ability in runtimeAbilities)
        {
            if (ability.definition is FleetRepairAbilityDefinition fleetRepair)
            {
                fleetRepair.PulseRepair(ownerUnit);
            }
        }
    }

    public bool CanActivate(int index)
    {
        if (index < 0 || index >= runtimeAbilities.Count) return false;
        var ability = runtimeAbilities[index];
        if (ability.definition == null) return false;
        if (ability.IsOnCooldown) return false;

        return ability.definition.CanActivate(ownerUnit, this);
    }

    public bool Activate(int index, Vector3? targetPosition = null, Unit targetUnit = null)
    {
        if (!CanActivate(index)) return false;

        var ability = runtimeAbilities[index];
        bool success = ability.definition.Execute(ownerUnit, this, targetPosition, targetUnit);
        if (success)
        {
            if (ability.definition.cooldown > 0f)
            {
                ability.cooldownRemaining = ability.definition.cooldown;
            }
            ability.isToggled = !ability.isToggled;
            OnAbilitiesChanged?.Invoke();
        }
        return success;
    }

    public RuntimeAbility GetAbility(int index)
    {
        if (index >= 0 && index < runtimeAbilities.Count) return runtimeAbilities[index];
        return null;
    }
}
