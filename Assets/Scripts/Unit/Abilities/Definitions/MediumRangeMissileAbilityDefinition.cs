using UnityEngine;

/// <summary>
/// Medium-Range Missile active ability for strategic missile submarines (Orca, Erebos).
/// Configures strategic long-range bombardment parameters without hardcoding vessel names.
/// </summary>
[CreateAssetMenu(fileName = "Medium Range Missile Ability", menuName = "Data/Unit/Abilities/Medium Range Missile Ability")]
public class MediumRangeMissileAbilityDefinition : AbilityDefinition
{
    [Header("Missile Properties")]
    public int missileDamage = 250;
    public float areaOfEffect = 12f;
    public float castTime = 2.5f;
    public GameObject projectilePrefab;
    public int resourceCost = 0;

    private void Reset()
    {
        displayName = "Medium-Range Missile";
        description = "Launches a long-range tactical cruise missile against surface structures or vessels.";
        abilityType = AbilityType.Targeted;
        range = 50f;
        cooldown = 120f;
        targetRestrictions = CombatTargetCapabilities.Surface;
    }

    public override bool Execute(Unit owner, UnitAbilities abilities, Vector3? targetPosition = null, Unit targetUnit = null)
    {
        if (owner == null) return false;
        Vector3 blastCenter = targetPosition ?? (targetUnit != null ? targetUnit.transform.position : owner.transform.position + owner.transform.forward * range * 0.5f);

        Collider[] hits = Physics.OverlapSphere(blastCenter, areaOfEffect);
        foreach (var hit in hits)
        {
            Unit target = hit.GetComponentInParent<Unit>();
            if (target != null && target != owner)
            {
                Damageable dmg = target.GetComponent<Damageable>();
                if (dmg != null)
                {
                    dmg.Hit(missileDamage);
                }
            }
        }

        Debug.Log($"<color=cyan>Medium-Range Missile impacted near {blastCenter} for {missileDamage} damage!</color>");
        return true;
    }
}
