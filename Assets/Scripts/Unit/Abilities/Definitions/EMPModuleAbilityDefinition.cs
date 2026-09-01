using UnityEngine;

/// <summary>
/// EMP Module active ability.
/// Disables movement, attacks, and abilities of vehicles within range.
/// Configurable: Range (10), Duration (60s), Cooldown (300s).
/// </summary>
[CreateAssetMenu(fileName = "EMP Module Ability", menuName = "Data/Unit/Abilities/EMP Module Ability")]
public class EMPModuleAbilityDefinition : AbilityDefinition
{
    private void Reset()
    {
        displayName = "EMP Module";
        description = "Discharges an electromagnetic pulse disabling enemy vehicles within range.";
        abilityType = AbilityType.Active;
        range = 10f;
        duration = 60f;
        cooldown = 300f;
        targetRestrictions = CombatTargetCapabilities.Surface | CombatTargetCapabilities.Submarine | CombatTargetCapabilities.Air;
    }

    public override bool Execute(Unit owner, UnitAbilities abilities, Vector3? targetPosition = null, Unit targetUnit = null)
    {
        if (owner == null) return false;
        Vector3 origin = owner.transform.position;

        Collider[] hits = Physics.OverlapSphere(origin, range);
        foreach (var hit in hits)
        {
            Unit target = hit.GetComponentInParent<Unit>();
            if (target != null && target != owner)
            {
                NavalUnit naval = target.GetComponent<NavalUnit>();
                if (naval != null)
                {
                    naval.ApplyEmpDisable(duration);
                }
            }
        }

        Debug.Log($"<color=cyan>EMP Module fired from {owner.displayName}! Range: {range}, Duration: {duration}s</color>");
        return true;
    }
}
