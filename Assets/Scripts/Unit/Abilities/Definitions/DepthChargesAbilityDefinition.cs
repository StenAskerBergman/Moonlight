using UnityEngine;

/// <summary>
/// Depth Charges active anti-submarine ability for heavy warships like Colossus.
/// Targets submerged units with an area attack.
/// </summary>
[CreateAssetMenu(fileName = "Depth Charges Ability", menuName = "Data/Unit/Abilities/Depth Charges Ability")]
public class DepthChargesAbilityDefinition : AbilityDefinition
{
    [Header("Depth Charge Stats")]
    public int damage = 120;
    public float areaOfEffect = 8f;

    private void Reset()
    {
        displayName = "Depth Charges";
        description = "Launches explosive depth charges targeting submerged submarines in the area.";
        abilityType = AbilityType.Targeted;
        cooldown = 30f;
        range = 20f;
        targetRestrictions = CombatTargetCapabilities.Submarine;
    }

    public override bool Execute(Unit owner, UnitAbilities abilities, Vector3? targetPosition = null, Unit targetUnit = null)
    {
        if (owner == null) return false;
        Vector3 blastCenter = targetPosition ?? (targetUnit != null ? targetUnit.transform.position : owner.transform.position + owner.transform.forward * 10f);

        Collider[] hits = Physics.OverlapSphere(blastCenter, areaOfEffect);
        foreach (var hit in hits)
        {
            Unit target = hit.GetComponentInParent<Unit>();
            if (target != null && target != owner)
            {
                DiveInteraction dive = target.GetComponent<DiveInteraction>();
                if (dive != null && dive.IsSubmerged)
                {
                    Damageable dmg = target.GetComponent<Damageable>();
                    if (dmg != null)
                    {
                        dmg.Hit(damage);
                    }
                }
            }
        }

        Debug.Log($"<color=cyan>Depth Charges dropped by {owner.displayName} at {blastCenter}!</color>");
        return true;
    }
}
