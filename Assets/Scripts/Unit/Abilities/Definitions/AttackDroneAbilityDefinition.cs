using UnityEngine;

/// <summary>
/// Attack Drone active ability.
/// Deploys a temporary combat drone that can engage Air, Surface, and Submarine targets.
/// </summary>
[CreateAssetMenu(fileName = "Attack Drone Ability", menuName = "Data/Unit/Abilities/Attack Drone Ability")]
public class AttackDroneAbilityDefinition : AbilityDefinition
{
    [Header("Drone Settings")]
    public GameObject dronePrefab;
    public float droneLifetime = 45f;

    private void Reset()
    {
        displayName = "Attack Drone";
        description = "Launches an autonomous combat drone that engages surface, submarine, and airborne threats.";
        abilityType = AbilityType.Active;
        cooldown = 90f;
        duration = 45f;
        range = 25f;
        targetRestrictions = CombatTargetCapabilities.All;
    }

    public override bool Execute(Unit owner, UnitAbilities abilities, Vector3? targetPosition = null, Unit targetUnit = null)
    {
        if (owner == null) return false;

        Vector3 spawnPos = owner.transform.position + Vector3.up * 3f;
        if (dronePrefab != null)
        {
            GameObject droneObj = Instantiate(dronePrefab, spawnPos, owner.transform.rotation);
            Destroy(droneObj, droneLifetime > 0 ? droneLifetime : 45f);
        }

        Debug.Log($"<color=cyan>Attack Drone deployed by {owner.displayName}!</color>");
        return true;
    }
}
