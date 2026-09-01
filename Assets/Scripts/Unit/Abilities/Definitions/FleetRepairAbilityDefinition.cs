using UnityEngine;

/// <summary>
/// Fleet Repair support ability for repair/carrier vessels like Atlas.
/// Repairs friendly vehicles within influence radius.
/// </summary>
[CreateAssetMenu(fileName = "Fleet Repair Ability", menuName = "Data/Unit/Abilities/Fleet Repair Ability")]
public class FleetRepairAbilityDefinition : AbilityDefinition
{
    [Header("Repair Stats")]
    public int repairAmountPerPulse = 10;
    public float pulseInterval = 2f;

    private void Reset()
    {
        displayName = "Fleet Repair";
        description = "Provides continuous maintenance and field repairs to nearby friendly vessels.";
        abilityType = AbilityType.Passive;
        range = 18f;
        cooldown = 0f;
    }

    public void PulseRepair(Unit owner)
    {
        if (owner == null) return;
        Collider[] hits = Physics.OverlapSphere(owner.transform.position, range);
        foreach (var hit in hits)
        {
            Unit target = hit.GetComponentInParent<Unit>();
            if (target != null && target != owner)
            {
                Damageable dmg = target.GetComponent<Damageable>();
                if (dmg != null && dmg.currentHealth < dmg.totalHealth)
                {
                    dmg.currentHealth = Mathf.Min(dmg.totalHealth, dmg.currentHealth + repairAmountPerPulse);
                }
            }
        }
    }
}
